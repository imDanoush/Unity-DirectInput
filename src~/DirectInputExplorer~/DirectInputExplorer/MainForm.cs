using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DirectInputManager;

namespace DirectInputExplorer
{
    /// <summary>
    /// DirectInput Explorer main window. Layout is built in code with layout panels (no designer file):
    /// a device bar, five tabs (Info, Input, Effects, Custom force, Diagnostics) and an always visible
    /// STOP ALL FORCES button (also Escape). Every status shown comes from the API's return values.
    /// </summary>
    public sealed class MainForm : Form
    {
        private sealed class DeviceItem
        {
            public DeviceInfo Info;
            public override string ToString() => $"{Info.productName}{(Info.FFBCapable ? "   [force feedback]" : "")}   {Info.guidInstance}";
        }

        private sealed class AxisRow
        {
            public string Name;
            public Func<FlatJoyState2, int> Read;
        }

        private static readonly AxisRow[] AxisRows =
        {
            new AxisRow { Name = "X", Read = s => s.lX }, new AxisRow { Name = "Y", Read = s => s.lY }, new AxisRow { Name = "Z", Read = s => s.lZ },
            new AxisRow { Name = "Rx", Read = s => s.lRx }, new AxisRow { Name = "Ry", Read = s => s.lRy }, new AxisRow { Name = "Rz", Read = s => s.lRz },
            new AxisRow { Name = "Slider 0 (U)", Read = s => s.lU }, new AxisRow { Name = "Slider 1 (V)", Read = s => s.lV },
            new AxisRow { Name = "Velocity X", Read = s => s.lVX }, new AxisRow { Name = "Velocity Y", Read = s => s.lVY }, new AxisRow { Name = "Velocity Z", Read = s => s.lVZ },
            new AxisRow { Name = "Velocity Rx", Read = s => s.lVRx }, new AxisRow { Name = "Velocity Ry", Read = s => s.lVRy }, new AxisRow { Name = "Velocity Rz", Read = s => s.lVRz },
            new AxisRow { Name = "Velocity slider 0", Read = s => s.lVU }, new AxisRow { Name = "Velocity slider 1", Read = s => s.lVV },
            new AxisRow { Name = "Acceleration X", Read = s => s.lAX }, new AxisRow { Name = "Acceleration Y", Read = s => s.lAY }, new AxisRow { Name = "Acceleration Z", Read = s => s.lAZ },
            new AxisRow { Name = "Acceleration Rx", Read = s => s.lARx }, new AxisRow { Name = "Acceleration Ry", Read = s => s.lARy }, new AxisRow { Name = "Acceleration Rz", Read = s => s.lARz },
            new AxisRow { Name = "Acceleration slider 0", Read = s => s.lAU }, new AxisRow { Name = "Acceleration slider 1", Read = s => s.lAV },
            new AxisRow { Name = "Force X", Read = s => s.lFX }, new AxisRow { Name = "Force Y", Read = s => s.lFY }, new AxisRow { Name = "Force Z", Read = s => s.lFZ },
            new AxisRow { Name = "Torque Rx", Read = s => s.lFRx }, new AxisRow { Name = "Torque Ry", Read = s => s.lFRy }, new AxisRow { Name = "Torque Rz", Read = s => s.lFRz },
            new AxisRow { Name = "Force slider 0", Read = s => s.lFU }, new AxisRow { Name = "Force slider 1", Read = s => s.lFV },
        };

        private readonly ComboBox _devices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Device" };
        private readonly Button _refresh = new Button { Text = "Refresh list", AutoSize = true, AccessibleName = "Refresh device list" };
        private readonly Button _attach = new Button { Text = "Attach", AutoSize = true, Width = 90 };
        private readonly CheckBox _autocenter = new CheckBox { Text = "Device autocenter spring", AutoSize = true, CheckState = CheckState.Indeterminate, Enabled = false, Margin = new Padding(12, 6, 3, 3) };
        private readonly Button _stopAll = new Button { Text = "STOP ALL FORCES  (Esc)", AutoSize = true, BackColor = Color.Firebrick, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold), Margin = new Padding(12, 0, 0, 0), AccessibleName = "Stop all forces" };
        private readonly Label _state = new Label { AutoSize = true, Text = "No device attached.", Margin = new Padding(3, 6, 3, 3) };
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private readonly TextBox _info = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 9f), WordWrap = false };
        private readonly ListView _inputList = new ListView { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, AccessibleName = "Live input values" };
        private readonly Dictionary<string, ListViewItem> _inputRows = new Dictionary<string, ListViewItem>();
        private readonly FlowLayoutPanel _effectsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
        private readonly List<EffectPanel> _effectPanels = new List<EffectPanel>();
        private readonly CustomForcePanel _custom = new CustomForcePanel();
        private readonly TextBox _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font(FontFamily.GenericMonospace, 9f), WordWrap = false };
        private readonly Timer _poll = new Timer { Interval = 20 };
        private readonly Timer _statusRefresh = new Timer { Interval = 250 };

        private DeviceInfo? _attached;
        private ActiveDeviceInfo _attachedAdi;
        private bool _enumerating;
        private bool _closing;
        private bool _autocenterUpdating;
        private string _inputError; // last input-read failure on the attached device; null while reads succeed
        private readonly HashSet<Control> _statusLatched = new HashSet<Control>(); // panels whose error or hint the status timer must not overwrite

        public MainForm()
        {
            Text = "DirectInput Explorer";
            MinimumSize = new Size(1000, 680);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var bar = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 5, RowCount = 2 };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.Controls.Add(_devices, 0, 0);
            bar.Controls.Add(_refresh, 1, 0);
            bar.Controls.Add(_attach, 2, 0);
            bar.Controls.Add(_autocenter, 3, 0);
            bar.Controls.Add(_stopAll, 4, 0);
            bar.Controls.Add(_state, 0, 1);
            bar.SetColumnSpan(_state, 5);
            root.Controls.Add(bar, 0, 0);

            _tabs.TabPages.Add(MakePage("Info", _info));
            _inputList.Columns.Add("Control", 220);
            _inputList.Columns.Add("Value", 140);
            _inputList.Columns.Add("Raw bits", 420);
            _tabs.TabPages.Add(MakePage("Input", _inputList));
            foreach (var d in EffectDescriptor.Standard)
            {
                var panel = new EffectPanel(d);
                panel.EnableToggled += EffectEnableToggled;
                panel.ParametersChanged += (s, e) => SendParameters((EffectPanel)s);
                _effectPanels.Add(panel);
                _effectsFlow.Controls.Add(panel);
            }
            _tabs.TabPages.Add(MakePage("Effects", _effectsFlow));
            _custom.EnableToggled += CustomEnableToggled;
            _custom.ApplyRequested += (s, e) => ApplyCustomForce();
            _tabs.TabPages.Add(MakePage("Custom force", _custom));
            _tabs.TabPages.Add(MakeDiagnosticsPage());
            root.Controls.Add(_tabs, 0, 1);
            Controls.Add(root);

            _refresh.Click += async (s, e) => await EnumerateAsync();
            _attach.Click += (s, e) => { if (_attached.HasValue) Detach(); else AttachSelected(); };
            _stopAll.Click += (s, e) => StopAllForces("STOP ALL pressed");
            _autocenter.CheckedChanged += (s, e) => SetAutocenter();
            _poll.Tick += (s, e) => PollAttached();
            _statusRefresh.Tick += (s, e) => RefreshEffectStatuses();
            BuildInputRows();
            ResetEffectPanels("Attach a device first");

            Load += async (s, e) => await OnLoadAsync();
            FormClosing += OnClosing;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Runs before any child control, so Escape stops forces even while a grid cell is being edited.
            if ((keyData & Keys.KeyCode) == Keys.Escape) StopAllForces("Escape pressed");
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static TabPage MakePage(string title, Control content)
        {
            var page = new TabPage(title) { Padding = new Padding(4), UseVisualStyleBackColor = true };
            page.Controls.Add(content);
            return page;
        }

        private TabPage MakeDiagnosticsPage()
        {
            var page = new TabPage("Diagnostics") { Padding = new Padding(4), UseVisualStyleBackColor = true };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
            var report = new Button { Text = "Device report (does not change forces)", AutoSize = true };
            report.Click += (s, e) => DeviceReport();
            var copy = new Button { Text = "Copy log", AutoSize = true };
            copy.Click += (s, e) =>
            {
                if (_log.Text.Length == 0) return;
                try { Clipboard.SetText(_log.Text); }
                catch (System.Runtime.InteropServices.ExternalException ex) { Log("Copy log failed (another program holds the clipboard): " + ex.Message); }
            };
            var clear = new Button { Text = "Clear log", AutoSize = true };
            clear.Click += (s, e) => _log.Clear();
            buttons.Controls.Add(report);
            buttons.Controls.Add(copy);
            buttons.Controls.Add(clear);
            table.Controls.Add(buttons, 0, 0);
            table.Controls.Add(_log, 0, 1);
            page.Controls.Add(table);
            return page;
        }

        //////////////////////////////////////////////////////////////
        // Lifecycle
        //////////////////////////////////////////////////////////////

        private async System.Threading.Tasks.Task OnLoadAsync()
        {
            if (!DIManager.Initialize())
            {
                _state.Text = "DirectInput failed to start: " + DIManager.LastError;
                Log(_state.Text);
                _refresh.Enabled = _attach.Enabled = false;
                return;
            }
            DIManager.OnDeviceAdded += OnDeviceAdded;
            DIManager.OnDeviceRemoved += OnDeviceRemoved;
            Log("DirectInput started.");
            _poll.Start();
            _statusRefresh.Start();
            await EnumerateAsync();
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            _closing = true;
            _poll.Stop();
            _statusRefresh.Stop();
            DIManager.OnDeviceAdded -= OnDeviceAdded;
            DIManager.OnDeviceRemoved -= OnDeviceRemoved;
            if (_attached.HasValue)
            {
                Detach();
            }
            if (DIManager.IsInitialized) DIManager.StopDirectInput(); // stops and releases everything the DLL holds; skipped when the DLL never loaded
        }

        //////////////////////////////////////////////////////////////
        // Devices
        //////////////////////////////////////////////////////////////

        private async System.Threading.Tasks.Task EnumerateAsync()
        {
            if (_enumerating) return;
            _enumerating = true;
            _refresh.Enabled = _attach.Enabled = false;
            _poll.Stop(); // enumeration runs on a worker thread; keep the DLL single-threaded meanwhile
            _statusRefresh.Stop(); // and its API lock is held for the whole enumeration, which would freeze the UI
            _state.Text = "Enumerating devices (a stuck driver can make this take up to a minute)...";
            try
            {
                await DIManager.EnumerateDevicesAsync();
            }
            finally
            {
                _enumerating = false;
                _refresh.Enabled = _attach.Enabled = true;
                _poll.Start();
                _statusRefresh.Start();
            }
            RebuildDeviceList();
            Log($"Enumerated {DIManager.Devices.Length} device(s).");
        }

        private void RebuildDeviceList()
        {
            string selected = SelectedDevice()?.guidInstance;
            _devices.Items.Clear();
            foreach (var d in DIManager.Devices) _devices.Items.Add(new DeviceItem { Info = d });
            int index = -1;
            if (selected != null) index = Array.FindIndex(DIManager.Devices, d => d.guidInstance == selected);
            if (index < 0 && _devices.Items.Count > 0) index = 0;
            _devices.SelectedIndex = index;

            UpdateStateLabel();
            if (_attached.HasValue && DIManager.Devices.All(d => d.guidInstance != _attached.Value.guidInstance))
            {
                DeviceLost(_attached.Value); // last, so its safety warning stays in the state bar
            }
        }

        private DeviceInfo? SelectedDevice() => (_devices.SelectedItem as DeviceItem)?.Info;

        private void OnDeviceAdded(DeviceInfo device)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<DeviceInfo>(OnDeviceAdded), device); return; }
            Log($"Device added: {device.productName} {device.guidInstance}");
            RebuildDeviceList();
        }

        private void OnDeviceRemoved(DeviceInfo device)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<DeviceInfo>(OnDeviceRemoved), device); return; }
            Log($"Device removed: {device.productName} {device.guidInstance}");
            RebuildDeviceList();
            if (_attached.HasValue && _attached.Value.guidInstance == device.guidInstance) DeviceLost(device);
        }

        private void AttachSelected()
        {
            var selected = SelectedDevice();
            if (!selected.HasValue) { _state.Text = "Select a device first."; return; }
            var device = selected.Value;
            if (!DIManager.Attach(device))
            {
                _state.Text = $"Attach failed: {DIManager.LastError}";
                Log(_state.Text);
                return;
            }
            _attached = device;
            _inputError = null;
            if (DIManager.GetADI(device, out _attachedAdi))
            {
                _attachedAdi.OnDeviceStateChange -= OnDeviceState; // Attach on an attached device returns true; never subscribe twice
                _attachedAdi.OnDeviceStateChange += OnDeviceState;
            }
            var caps = DIManager.GetDeviceCapabilities(device);
            string capsError = DIManager.LastHResult < 0 ? DIManager.LastError : null; // a failed query returns zeros, which read as "no force feedback"
            bool ffb = caps.dwFlags.HasFlag(DwFlags.DIDC_FORCEFEEDBACK);
            _attach.Text = "Detach";
            _autocenter.Enabled = ffb;
            ShowAutocenterUnknown(); // a new device object starts at the driver default, which is not read back
            if (capsError != null)
            {
                Log($"Attached {device.productName}, but {capsError}");
                ResetEffectPanels("Capabilities query failed: " + capsError);
            }
            else
            {
                _custom.ShowDevicePeriod(caps.dwFFSamplePeriod);
                Log($"Attached {device.productName} (force feedback: {(ffb ? "yes" : "no")}, axes {caps.dwAxes}, buttons {caps.dwButtons}, POVs {caps.dwPOVs}).");
                ApplyEffectAvailability(device, ffb);
            }
            FillInfo(device, caps, capsError);
            UpdateStateLabel();
        }

        private void Detach()
        {
            if (!_attached.HasValue) return;
            var device = _attached.Value;
            StopAllForces("detaching");
            if (_attachedAdi != null) _attachedAdi.OnDeviceStateChange -= OnDeviceState;
            _attachedAdi = null;
            if (!DIManager.Destroy(device)) Log($"Destroy reported: {DIManager.LastError}");
            _attached = null;
            _attach.Text = "Attach";
            _autocenter.Enabled = false;
            ResetEffectPanels("Attach a device first");
            ClearInputRows();
            _info.Clear();
            Log($"Detached {device.productName}.");
            UpdateStateLabel();
        }

        private void DeviceLost(DeviceInfo device)
        {
            // A manual Refresh drops the device without DIManager destroying it; stop and release its effects (no-op if already destroyed).
            if (!DIManager.Destroy(device)) Log($"Destroy reported: {DIManager.LastError}");
            if (_attachedAdi != null) _attachedAdi.OnDeviceStateChange -= OnDeviceState;
            _attachedAdi = null;
            _attached = null;
            _attach.Text = "Attach";
            _autocenter.Enabled = false;
            ResetEffectPanels("Device was disconnected");
            ClearInputRows();
            _state.Text = $"{device.productName} was disconnected. Software cannot stop a wheel that lost its driver; check the wheelbase itself.";
            Log(_state.Text);
        }

        private void UpdateStateLabel()
        {
            if (_attached.HasValue)
            {
                var d = _attached.Value;
                _state.Text = _inputError != null
                    ? $"Attached: {d.productName}, but input read failed: {_inputError}"
                    : $"Attached: {d.productName}   instance {d.guidInstance}   (exclusive, background cooperative level)";
            }
            else if (DIManager.Devices.Length == 0)
            {
                _state.Text = "No DirectInput game controllers found. Plug one in: the list refreshes on its own.";
            }
            else
            {
                _state.Text = "No device attached. Pick one and press Attach.";
            }
        }

        private void SetAutocenter()
        {
            if (!_attached.HasValue || _autocenterUpdating) return;
            bool ok = DIManager.SetAutocenter(_attached.Value.guidInstance, _autocenter.Checked);
            Log(ok ? $"Autocenter {(_autocenter.Checked ? "on" : "off")}." : $"Autocenter change failed: {DIManager.LastError}");
            if (!ok) ShowAutocenterUnknown(); // the DLL can fail after setting the property (reacquire), so neither state is known
        }

        private void ShowAutocenterUnknown()
        {
            _autocenterUpdating = true;
            _autocenter.CheckState = CheckState.Indeterminate;
            _autocenterUpdating = false;
        }

        //////////////////////////////////////////////////////////////
        // Effects
        //////////////////////////////////////////////////////////////

        private void ResetEffectPanels(string reason)
        {
            foreach (var p in _effectPanels) p.SetAvailable(false, reason);
            _custom.SetAvailable(false, reason);
        }

        private void ApplyEffectAvailability(DeviceInfo device, bool ffb)
        {
            if (!ffb)
            {
                ResetEffectPanels("Device reports no force feedback (DIDC_FORCEFEEDBACK not set)");
                return;
            }
            bool known = DIManager.TryGetSupportedFFBEffects(device.guidInstance, out uint mask);
            string notCreated = known ? "Not created" : "Not created (support not verified: " + DIManager.LastError + ")";
            if (!known) Log("Effect support unknown: " + DIManager.LastError);
            foreach (var p in _effectPanels)
            {
                bool supported = !known || (mask & (1u << (int)p.Descriptor.Type)) != 0;
                p.SetAvailable(supported, "Device does not enumerate this effect type");
                if (supported) p.ResetToDisabled(notCreated);
            }
            bool customSupported = !known || (mask & (1u << (int)FFBEffects.CustomForce)) != 0;
            _custom.SetAvailable(customSupported, "Device does not enumerate GUID_CustomForce");
            if (customSupported) _custom.ResetToDisabled("Not created");
        }

        private void EffectEnableToggled(object sender, bool enable)
        {
            var panel = (EffectPanel)sender;
            if (!_attached.HasValue) { panel.EnableFailed("No device attached"); return; }
            string guid = _attached.Value.guidInstance;
            if (enable)
            {
                if (!DIManager.EnableFFBEffect(guid, panel.Descriptor.Type))
                {
                    panel.EnableFailed(DIManager.LastError);
                    Log($"{panel.Descriptor.Name}: create failed: {DIManager.LastError}");
                    return;
                }
                Log($"{panel.Descriptor.Name}: created and started with zero force.");
                SendParameters(panel); // apply the values shown so the UI and the device agree
            }
            else
            {
                bool ok = DIManager.DestroyFFBEffect(guid, panel.Descriptor.Type);
                panel.SetStatus(ok ? "Destroyed" : "Destroy failed: " + DIManager.LastError, !ok);
                Log($"{panel.Descriptor.Name}: {(ok ? "destroyed" : "destroy failed: " + DIManager.LastError)}");
            }
        }

        private void SendParameters(EffectPanel panel)
        {
            if (!_attached.HasValue || !panel.IsEffectEnabled) return;
            string guid = _attached.Value.guidInstance;
            var d = panel.Descriptor;
            bool ok;
            if (d.Type == FFBEffects.ConstantForce)
                ok = DIManager.UpdateConstantForceNative(guid, panel["magnitude"]);
            else if (d.Type == FFBEffects.RampForce)
                ok = DIManager.UpdateRampForceNative(guid, panel["start"], panel["end"], (uint)panel["duration"]);
            else if (d.IsPeriodic)
                ok = DIManager.UpdatePeriodicForceNative(guid, d.Type, panel["magnitude"], (uint)panel["period"], panel["offset"]);
            else if (d.IsCondition)
                ok = DIManager.UpdateConditionForceNative(guid, d.Type, panel["offset"], panel["posCoef"], panel["negCoef"], (uint)panel["posSat"], (uint)panel["negSat"], panel["deadband"]);
            else
                return;
            if (ok) { _statusLatched.Remove(panel); return; }
            _statusLatched.Add(panel);
            panel.SetStatus("Update failed: " + DIManager.LastError, isError: true);
            Log($"{d.Name}: update failed: {DIManager.LastError}");
        }

        private void CustomEnableToggled(object sender, bool enable)
        {
            if (!_attached.HasValue) { _custom.EnableFailed("No device attached"); return; }
            string guid = _attached.Value.guidInstance;
            if (enable)
            {
                if (!DIManager.EnableFFBEffect(guid, FFBEffects.CustomForce))
                {
                    _custom.EnableFailed(DIManager.LastError);
                    Log("Custom force: create failed: " + DIManager.LastError);
                    return;
                }
                _custom.SetStatus("Created with 10 zero samples; press Apply to download yours");
                _statusLatched.Add(_custom);
                Log("Custom force: created and started with zero samples.");
            }
            else
            {
                bool ok = DIManager.DestroyFFBEffect(guid, FFBEffects.CustomForce);
                _custom.SetStatus(ok ? "Destroyed" : "Destroy failed: " + DIManager.LastError, !ok);
                Log($"Custom force: {(ok ? "destroyed" : "destroy failed: " + DIManager.LastError)}");
            }
        }

        private void ApplyCustomForce()
        {
            if (!_attached.HasValue || !_custom.IsEffectEnabled) return;
            int[] samples = _custom.Samples;
            bool ok = DIManager.UpdateCustomForceNative(_attached.Value.guidInstance, samples, _custom.SamplePeriod);
            if (ok) _statusLatched.Remove(_custom); else _statusLatched.Add(_custom);
            _custom.SetStatus(ok ? $"Sent {samples.Length} samples, period {(_custom.SamplePeriod == 0 ? "device default" : _custom.SamplePeriod + " us")}" : "Update failed: " + DIManager.LastError, !ok);
            Log(ok ? $"Custom force: sent {samples.Length} samples." : "Custom force: update failed: " + DIManager.LastError);
        }

        private void StopAllForces(string why)
        {
            if (_attached.HasValue)
            {
                bool ok = DIManager.StopAllFFBEffects(_attached.Value.guidInstance);
                Log(ok ? $"All effects stopped and destroyed ({why})." : $"StopAllFFBEffects failed ({why}): {DIManager.LastError}");
            }
            foreach (var p in _effectPanels) if (p.IsEffectEnabled) p.ResetToDisabled("Stopped");
            if (_custom.IsEffectEnabled) _custom.ResetToDisabled("Stopped");
        }

        private void RefreshEffectStatuses()
        {
            if (!_attached.HasValue) return;
            string guid = _attached.Value.guidInstance;
            foreach (var p in _effectPanels)
            {
                if (!p.IsEffectEnabled || _statusLatched.Contains(p)) continue;
                p.SetStatus(DescribeStatus(guid, p.Descriptor.Type, out bool failed), failed);
            }
            if (_custom.IsEffectEnabled && !_statusLatched.Contains(_custom)) _custom.SetStatus(DescribeStatus(guid, FFBEffects.CustomForce, out bool customFailed), customFailed);
        }

        private static string DescribeStatus(string guid, FFBEffects type, out bool failed)
        {
            failed = !DIManager.TryGetFFBEffectStatus(guid, type, out bool playing);
            // LastError names an old DLL only when the export is missing; a driver's own E_NOTIMPL is shown as such.
            if (failed) return "Enabled; status unavailable: " + DIManager.LastError;
            return playing ? "Playing (reported by DirectInput)" : "Created, not playing (reported by DirectInput)";
        }

        //////////////////////////////////////////////////////////////
        // Input and info
        //////////////////////////////////////////////////////////////

        private void PollAttached()
        {
            // Poll records no result for a device DIManager no longer holds, so LastHResult would be stale.
            if (!_attached.HasValue || !DIManager.IsDeviceActive(_attached.Value.guidInstance)) return;
            DIManager.Poll(_attached.Value);
            string error = DIManager.LastHResult < 0 ? DIManager.LastError : null;
            if ((error == null) == (_inputError == null)) return; // report transitions only, not every 20 ms tick
            _inputError = error;
            Log(error == null ? "Input reads recovered." : "Input read failed: " + error);
            UpdateStateLabel();
        }

        private void BuildInputRows()
        {
            _inputList.BeginUpdate();
            foreach (var row in AxisRows) AddInputRow(row.Name);
            for (int i = 0; i < 4; i++) AddInputRow("POV " + i);
            AddInputRow("Buttons 0-63");
            AddInputRow("Buttons 64-127");
            _inputList.EndUpdate();
        }

        private void AddInputRow(string name)
        {
            var item = new ListViewItem(new[] { name, "", "" });
            _inputRows[name] = item;
            _inputList.Items.Add(item);
        }

        private void ClearInputRows()
        {
            foreach (var item in _inputRows.Values) { item.SubItems[1].Text = ""; item.SubItems[2].Text = ""; }
        }

        private void OnDeviceState(DeviceInfo device, FlatJoyState2 state)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new deviceStateEvent(OnDeviceState), device, state); return; }
            _inputList.BeginUpdate();
            foreach (var row in AxisRows)
            {
                int v = row.Read(state);
                var item = _inputRows[row.Name];
                string text = v.ToString();
                if (item.SubItems[1].Text != text)
                {
                    item.SubItems[1].Text = text;
                    item.SubItems[2].Text = $"{(v - 32767.5) / 32767.5:+0.000;-0.000} centred";
                }
            }
            for (int i = 0; i < 4; i++)
            {
                int bits = (state.rgdwPOV >> (i * 4)) & 0xF;
                string text = bits == 0 ? "centred" : string.Join("+", new[] { (bits & 1) != 0 ? "up" : null, (bits & 2) != 0 ? "down" : null, (bits & 4) != 0 ? "left" : null, (bits & 8) != 0 ? "right" : null }.Where(x => x != null));
                _inputRows["POV " + i].SubItems[1].Text = text;
            }
            SetButtons("Buttons 0-63", state.buttonsA, 0);
            SetButtons("Buttons 64-127", state.buttonsB, 64);
            _inputList.EndUpdate();
        }

        private void SetButtons(string rowName, ulong bank, int first)
        {
            var item = _inputRows[rowName];
            string bits = Convert.ToString((long)bank, 2).PadLeft(64, '0');
            if (item.SubItems[2].Text == bits) return;
            var pressed = new List<int>();
            for (int i = 0; i < 64; i++) if ((bank & (1UL << i)) != 0) pressed.Add(first + i);
            item.SubItems[1].Text = pressed.Count == 0 ? "none" : string.Join(" ", pressed);
            item.SubItems[2].Text = bits;
        }

        private void FillInfo(DeviceInfo device, DIDEVCAPS caps, string capsError)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Device");
            sb.AppendLine($"  product name     {device.productName}");
            sb.AppendLine($"  instance name    {device.instanceName}");
            sb.AppendLine($"  instance GUID    {device.guidInstance}");
            sb.AppendLine($"  product GUID     {device.guidProduct}");
            sb.AppendLine($"  dwDevType        0x{device.deviceType:X8}  (type 0x{device.deviceType & 0xFF:X2}, subtype 0x{(device.deviceType >> 8) & 0xFF:X2}{(((device.deviceType >> 16) & 1) != 0 ? ", HID" : "")})");
            sb.AppendLine($"  enumerated as FFB {device.FFBCapable}");
            sb.AppendLine();
            sb.AppendLine("Capabilities (DIDEVCAPS)");
            if (capsError != null)
            {
                sb.AppendLine("  query failed: " + capsError); // the struct is zeroed, not reported by the device
            }
            else
            {
                sb.AppendLine($"  dwFlags          0x{(uint)caps.dwFlags:X8}  {caps.dwFlags}");
                sb.AppendLine($"  dwAxes           {caps.dwAxes}");
                sb.AppendLine($"  dwButtons        {caps.dwButtons}");
                sb.AppendLine($"  dwPOVs           {caps.dwPOVs}");
                sb.AppendLine($"  dwFFSamplePeriod {caps.dwFFSamplePeriod} us   (minimum time between force commands)");
                sb.AppendLine($"  dwFFMinTimeRes   {caps.dwFFMinTimeResolution} us");
                sb.AppendLine($"  firmware         {caps.dwFirmwareRevision}");
                sb.AppendLine($"  hardware         {caps.dwHardwareRevision}");
                sb.AppendLine($"  FF driver        {caps.dwFFDriverVersion}");
            }
            sb.AppendLine();
            sb.AppendLine("Force feedback effects enumerated by the driver");
            var names = DIManager.GetDeviceFFBCapabilities(device);
            if (names.Length == 0) sb.AppendLine("  (none)");
            foreach (var n in names) sb.AppendLine("  " + n);
            if (DIManager.TryGetSupportedFFBEffects(device.guidInstance, out uint mask))
            {
                sb.AppendLine();
                sb.AppendLine("Standard effect types recognised");
                foreach (FFBEffects t in Enum.GetValues(typeof(FFBEffects)))
                    sb.AppendLine($"  {t,-14} {((mask & (1u << (int)t)) != 0 ? "yes" : "no")}");
            }
            _info.Text = sb.ToString();
        }

        //////////////////////////////////////////////////////////////
        // Diagnostics
        //////////////////////////////////////////////////////////////

        private void DeviceReport()
        {
            if (!_attached.HasValue) { Log("Device report: attach a device first."); return; }
            try
            {
                int hr = Native.DEBUG1(_attached.Value.guidInstance, out string[] lines);
                Log($"Device report (HRESULT {WinErrors.Describe(hr)}):");
                if (lines != null) foreach (var line in lines) Log("    " + line);
            }
            catch (Exception ex)
            {
                Log("Device report failed: " + ex.Message);
            }
        }

        private void Log(string message)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            if (_log.TextLength > 200_000) _log.Clear();
            _log.AppendText($"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
    }
}
