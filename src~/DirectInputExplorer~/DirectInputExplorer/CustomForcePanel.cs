using System;
using System.Drawing;
using System.Windows.Forms;

namespace DirectInputExplorer
{
    /// <summary>
    /// Editor for a DirectInput custom force (GUID_CustomForce, DICUSTOMFORCE): one channel, N force samples
    /// of -10000..10000 played in a loop every sample period. The DLL creates the effect with ten zero
    /// samples; "Apply" downloads the samples shown here. Nothing is sent to the device until the user
    /// enables the effect and either presses Apply or turns on "send on edit".
    /// </summary>
    public sealed class CustomForcePanel : UserControl
    {
        public event EventHandler<bool> EnableToggled;
        public event EventHandler ApplyRequested;

        private readonly CheckBox _enable;
        private readonly Label _status;
        private readonly NumericUpDown _sampleCount;
        private readonly NumericUpDown _samplePeriod;
        private readonly CheckBox _sendOnEdit;
        private readonly Button _apply;
        private readonly DataGridView _grid;
        private readonly Label _devicePeriod;
        private bool _silent;
        private bool _rebuilding;

        public CustomForcePanel()
        {
            Dock = DockStyle.Fill;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _enable = new CheckBox { Text = "Enabled (creates the effect with zero force)", AutoSize = true, Anchor = AnchorStyles.Left, AccessibleName = "Custom force enabled" };
            _enable.CheckedChanged += (s, e) => { if (!_silent) EnableToggled?.Invoke(this, _enable.Checked); SetEditingEnabled(_enable.Checked); };
            _status = new Label { Text = "Not created", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
            table.Controls.Add(_enable, 0, 0);
            table.Controls.Add(_status, 1, 0);

            var settings = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Anchor = AnchorStyles.Left };
            _sampleCount = new NumericUpDown { Minimum = 1, Maximum = 256, Value = 10, Width = 70, Enabled = false, AccessibleName = "Custom force sample count" };
            _sampleCount.ValueChanged += (s, e) => RebuildRows();
            _samplePeriod = new NumericUpDown { Minimum = 0, Maximum = 1000000, Value = 0, Increment = 1000, Width = 100, Enabled = false, ThousandsSeparator = true, AccessibleName = "Custom force sample period in microseconds" };
            _devicePeriod = new Label { Text = "", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 8, 3, 3) };
            settings.Controls.Add(new Label { Text = "Samples:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            settings.Controls.Add(_sampleCount);
            settings.Controls.Add(new Label { Text = "Sample period (microseconds, 0 = keep the period the device reported):", AutoSize = true, Margin = new Padding(12, 8, 3, 3) });
            settings.Controls.Add(_samplePeriod);
            settings.Controls.Add(_devicePeriod);
            table.Controls.Add(settings, 0, 1);
            table.SetColumnSpan(settings, 2);

            var hint = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                ForeColor = SystemColors.GrayText,
                Text = "One channel. Each row is one sample of -10000..10000 (DI_FFNOMINALMAX); the device plays the rows in order, one per sample period, and repeats. " +
                       "Forces reach the device only when you press Apply (or on every edit with the box below). Not every device supports custom forces; the enable box is greyed when the device does not enumerate GUID_CustomForce.",
            };
            table.Controls.Add(hint, 0, 2);
            table.SetColumnSpan(hint, 2);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersWidth = 60,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Enabled = false,
                AccessibleName = "Custom force samples",
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Force (-10000..10000)", Name = "force", ValueType = typeof(int) });
            _grid.CellValidating += GridCellValidating;
            _grid.CellValueChanged += (s, e) => { if (!_rebuilding && _sendOnEdit.Checked && _enable.Checked) ApplyRequested?.Invoke(this, EventArgs.Empty); };
            table.Controls.Add(_grid, 0, 3);
            table.SetColumnSpan(_grid, 2);

            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left };
            _apply = new Button { Text = "Apply samples to device", AutoSize = true, Enabled = false };
            _apply.Click += (s, e) => ApplyRequested?.Invoke(this, EventArgs.Empty);
            _sendOnEdit = new CheckBox { Text = "Send on every edit", AutoSize = true, Enabled = false, Margin = new Padding(12, 6, 3, 3) };
            var fill = new Button { Text = "Fill: sine wave", AutoSize = true, Enabled = false };
            fill.Click += (s, e) => FillSine();
            var zero = new Button { Text = "Fill: zeros", AutoSize = true, Enabled = false };
            zero.Click += (s, e) => FillConstant(0);
            _enable.CheckedChanged += (s, e) => { fill.Enabled = zero.Enabled = _enable.Checked; };
            buttons.Controls.Add(_apply);
            buttons.Controls.Add(_sendOnEdit);
            buttons.Controls.Add(fill);
            buttons.Controls.Add(zero);
            table.Controls.Add(buttons, 0, 4);
            table.SetColumnSpan(buttons, 2);

            Controls.Add(table);
            RebuildRows();
        }

        public bool IsEffectEnabled => _enable.Checked;

        /// <summary>Sample period in microseconds; 0 keeps the DIDEVCAPS period the DLL used when it created the effect.</summary>
        public uint SamplePeriod => (uint)_samplePeriod.Value;

        /// <summary>The force samples as currently edited, clamped to the DirectInput range.</summary>
        public int[] Samples
        {
            get
            {
                var result = new int[_grid.Rows.Count];
                for (int i = 0; i < result.Length; i++)
                {
                    object v = _grid.Rows[i].Cells[0].Value;
                    int value = v is int iv ? iv : int.TryParse(Convert.ToString(v), out int parsed) ? parsed : 0;
                    result[i] = Math.Max(-10000, Math.Min(10000, value));
                }
                return result;
            }
        }

        public void SetStatus(string text, bool isError = false)
        {
            _status.Text = text;
            _status.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
        }

        public void ShowDevicePeriod(uint ffSamplePeriodMicroseconds)
        {
            _devicePeriod.Text = ffSamplePeriodMicroseconds == 0 ? "device reports no FF sample period" : $"device FF sample period: {ffSamplePeriodMicroseconds:N0} us";
        }

        public void ResetToDisabled(string status)
        {
            _silent = true;
            try { _enable.Checked = false; }
            finally { _silent = false; }
            SetEditingEnabled(false);
            _status.Text = status;
            _status.ForeColor = SystemColors.GrayText;
        }

        public void EnableFailed(string reason)
        {
            ResetToDisabled(reason);
            _status.ForeColor = Color.Firebrick;
        }

        public void SetAvailable(bool available, string reason)
        {
            if (!available && _enable.Checked) ResetToDisabled(reason);
            _enable.Enabled = available;
            if (!available)
            {
                _status.Text = reason;
                _status.ForeColor = SystemColors.GrayText;
            }
        }

        private void SetEditingEnabled(bool enabled)
        {
            _sampleCount.Enabled = _samplePeriod.Enabled = _grid.Enabled = _apply.Enabled = _sendOnEdit.Enabled = enabled;
        }

        private void RebuildRows()
        {
            _rebuilding = true;
            try
            {
                int wanted = (int)_sampleCount.Value;
                while (_grid.Rows.Count > wanted) _grid.Rows.RemoveAt(_grid.Rows.Count - 1);
                while (_grid.Rows.Count < wanted)
                {
                    int index = _grid.Rows.Add(0);
                    _grid.Rows[index].HeaderCell.Value = index.ToString();
                }
            }
            finally { _rebuilding = false; }
        }

        private void FillSine()
        {
            int n = _grid.Rows.Count;
            _rebuilding = true;
            try
            {
                for (int i = 0; i < n; i++) _grid.Rows[i].Cells[0].Value = (int)Math.Round(10000 * Math.Sin(2 * Math.PI * i / n));
            }
            finally { _rebuilding = false; }
            if (_sendOnEdit.Checked && _enable.Checked) ApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FillConstant(int value)
        {
            _rebuilding = true;
            try
            {
                for (int i = 0; i < _grid.Rows.Count; i++) _grid.Rows[i].Cells[0].Value = value;
            }
            finally { _rebuilding = false; }
            if (_sendOnEdit.Checked && _enable.Checked) ApplyRequested?.Invoke(this, EventArgs.Empty);
        }

        private void GridCellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (!int.TryParse(Convert.ToString(e.FormattedValue), out int value) || value < -10000 || value > 10000)
            {
                _grid.Rows[e.RowIndex].ErrorText = "Enter a whole number from -10000 to 10000";
                e.Cancel = true;
            }
            else
            {
                _grid.Rows[e.RowIndex].ErrorText = "";
            }
        }
    }
}
