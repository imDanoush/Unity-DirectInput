using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DirectInputManager;

namespace DirectInputExplorer
{
    /// <summary>One numeric parameter of an effect, in the unit DirectInput uses for it.</summary>
    public sealed class EffectParameter
    {
        public string Key { get; }
        public string Label { get; }
        public string Unit { get; }
        public int Min { get; }
        public int Max { get; }
        public int Default { get; }

        public EffectParameter(string key, string label, string unit, int min, int max, int @default)
        {
            Key = key; Label = label; Unit = unit; Min = min; Max = max; Default = @default;
        }
    }

    /// <summary>
    /// The parameters each DirectInput effect type exposes through the DLL. Units follow dinput.h:
    /// magnitudes and coefficients are -10000..10000 (DI_FFNOMINALMAX), saturation and dead band 0..10000,
    /// periods are microseconds.
    /// </summary>
    public sealed class EffectDescriptor
    {
        public FFBEffects Type { get; }
        public string Name { get; }
        public EffectParameter[] Parameters { get; }

        private EffectDescriptor(FFBEffects type, string name, params EffectParameter[] parameters)
        {
            Type = type; Name = name; Parameters = parameters;
        }

        private static readonly EffectParameter SignedMagnitude = new EffectParameter("magnitude", "Magnitude", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter UnsignedMagnitude = new EffectParameter("magnitude", "Magnitude", "0..10000", 0, 10000, 0);
        private static readonly EffectParameter Offset = new EffectParameter("offset", "Offset", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter Period = new EffectParameter("period", "Period", "microseconds", 1000, 1000000, 30000);
        private static readonly EffectParameter RampStart = new EffectParameter("start", "Start", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter RampEnd = new EffectParameter("end", "End", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter RampDuration = new EffectParameter("duration", "Duration", "microseconds", 1000, 10000000, 1000000);
        private static readonly EffectParameter PosCoef = new EffectParameter("posCoef", "Coefficient +", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter NegCoef = new EffectParameter("negCoef", "Coefficient -", "-10000..10000", -10000, 10000, 0);
        private static readonly EffectParameter PosSat = new EffectParameter("posSat", "Saturation +", "0..10000", 0, 10000, 10000);
        private static readonly EffectParameter NegSat = new EffectParameter("negSat", "Saturation -", "0..10000", 0, 10000, 10000);
        private static readonly EffectParameter DeadBand = new EffectParameter("deadband", "Dead band", "0..10000", 0, 10000, 0);

        private static EffectDescriptor Periodic(FFBEffects type, string name) => new EffectDescriptor(type, name, UnsignedMagnitude, Offset, Period);
        private static EffectDescriptor Condition(FFBEffects type, string name) => new EffectDescriptor(type, name, Offset, PosCoef, NegCoef, PosSat, NegSat, DeadBand);

        /// <summary>Every standard effect, in the order the DLL numbers them. CustomForce has its own panel.</summary>
        public static readonly EffectDescriptor[] Standard =
        {
            new EffectDescriptor(FFBEffects.ConstantForce, "Constant force", SignedMagnitude),
            new EffectDescriptor(FFBEffects.RampForce, "Ramp force", RampStart, RampEnd, RampDuration),
            Periodic(FFBEffects.Square, "Square"),
            Periodic(FFBEffects.Sine, "Sine"),
            Periodic(FFBEffects.Triangle, "Triangle"),
            Periodic(FFBEffects.SawtoothUp, "Sawtooth up"),
            Periodic(FFBEffects.SawtoothDown, "Sawtooth down"),
            Condition(FFBEffects.Spring, "Spring"),
            Condition(FFBEffects.Damper, "Damper"),
            Condition(FFBEffects.Inertia, "Inertia"),
            Condition(FFBEffects.Friction, "Friction"),
        };

        public bool IsPeriodic => Type == FFBEffects.Square || Type == FFBEffects.Sine || Type == FFBEffects.Triangle || Type == FFBEffects.SawtoothUp || Type == FFBEffects.SawtoothDown;
        public bool IsCondition => Type == FFBEffects.Spring || Type == FFBEffects.Damper || Type == FFBEffects.Inertia || Type == FFBEffects.Friction;
    }

    /// <summary>
    /// One effect: an enable box, a status line that only ever shows what the API reported, and one
    /// slider plus numeric box per parameter. The panel never talks to the device itself; the form does.
    /// </summary>
    public sealed class EffectPanel : UserControl
    {
        public EffectDescriptor Descriptor { get; }

        /// <summary>Raised when the user toggles the enable box. The form creates or destroys the effect.</summary>
        public event EventHandler<bool> EnableToggled;

        /// <summary>Raised when any parameter changes while the effect is enabled.</summary>
        public event EventHandler ParametersChanged;

        private readonly CheckBox _enable;
        private readonly Label _status;
        private readonly Dictionary<string, TrackBar> _sliders = new Dictionary<string, TrackBar>();
        private readonly Dictionary<string, NumericUpDown> _boxes = new Dictionary<string, NumericUpDown>();
        private bool _syncing;
        private bool _silent;

        public EffectPanel(EffectDescriptor descriptor)
        {
            Descriptor = descriptor;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(0, 0, 0, 8);

            // Fixed slider width keeps the panel's preferred size independent of the window, so a
            // vertical FlowLayoutPanel can stack and scroll these without a layout feedback loop.
            var box = new GroupBox { Text = descriptor.Name, Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 4, 8, 8) };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 4 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _enable = new CheckBox { Text = "Enabled", AutoSize = true, Anchor = AnchorStyles.Left, AccessibleName = descriptor.Name + " enabled" };
            _enable.CheckedChanged += (s, e) => { if (!_silent) EnableToggled?.Invoke(this, _enable.Checked); SetParameterControlsEnabled(_enable.Checked); };
            _status = new Label { Text = "Not created", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };
            table.Controls.Add(_enable, 0, 0);
            table.Controls.Add(_status, 1, 0);
            table.SetColumnSpan(_status, 3);

            int row = 1;
            foreach (var p in descriptor.Parameters)
            {
                var label = new Label { Text = p.Label + ":", AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
                var slider = new TrackBar { Minimum = p.Min, Maximum = p.Max, Value = p.Default, TickStyle = TickStyle.None, AutoSize = false, Height = 28, Width = 430, Anchor = AnchorStyles.Left, Enabled = false, AccessibleName = descriptor.Name + " " + p.Label };
                slider.LargeChange = Math.Max(1, (p.Max - p.Min) / 20);
                slider.SmallChange = Math.Max(1, (p.Max - p.Min) / 200);
                var numeric = new NumericUpDown { Minimum = p.Min, Maximum = p.Max, Value = p.Default, Width = 90, Anchor = AnchorStyles.Left, Enabled = false, AccessibleName = descriptor.Name + " " + p.Label + " value" };
                numeric.Increment = Math.Max(1, (p.Max - p.Min) / 200);
                var unit = new Label { Text = p.Unit, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText, TextAlign = ContentAlignment.MiddleLeft };

                string key = p.Key;
                slider.Scroll += (s, e) => Sync(key, slider.Value, fromSlider: true);
                numeric.ValueChanged += (s, e) => Sync(key, (int)numeric.Value, fromSlider: false);
                // Double-clicking the label resets the parameter to its default.
                int def = p.Default;
                label.DoubleClick += (s, e) => { if (numeric.Enabled) numeric.Value = def; };

                _sliders[key] = slider;
                _boxes[key] = numeric;
                table.Controls.Add(label, 0, row);
                table.Controls.Add(slider, 1, row);
                table.Controls.Add(numeric, 2, row);
                table.Controls.Add(unit, 3, row);
                row++;
            }

            box.Controls.Add(table);
            Controls.Add(box);
        }

        /// <summary>Current value of a parameter, in DirectInput units.</summary>
        public int this[string key] => (int)_boxes[key].Value;

        /// <summary>Whether the user has the effect switched on (commanded state, not device state).</summary>
        public bool IsEffectEnabled => _enable.Checked;

        /// <summary>Show text that came from the API (status, error), never an assumption.</summary>
        public void SetStatus(string text, bool isError = false)
        {
            _status.Text = text;
            _status.ForeColor = isError ? Color.Firebrick : SystemColors.ControlText;
        }

        /// <summary>Switch the box off without raising EnableToggled (after STOP ALL, detach, device loss).</summary>
        public void ResetToDisabled(string status)
        {
            _silent = true;
            try { _enable.Checked = false; }
            finally { _silent = false; }
            SetParameterControlsEnabled(false);
            _status.Text = status;
            _status.ForeColor = SystemColors.GrayText;
        }

        /// <summary>Reflect a failed enable: the box goes back off and the reason is shown.</summary>
        public void EnableFailed(string reason)
        {
            ResetToDisabled(reason);
            _status.ForeColor = Color.Firebrick;
        }

        /// <summary>Grey the whole panel out with a reason (device not attached, effect not enumerated).</summary>
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

        private void SetParameterControlsEnabled(bool enabled)
        {
            foreach (var s in _sliders.Values) s.Enabled = enabled;
            foreach (var b in _boxes.Values) b.Enabled = enabled;
        }

        private void Sync(string key, int value, bool fromSlider)
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                if (fromSlider) _boxes[key].Value = value; else _sliders[key].Value = value;
            }
            finally { _syncing = false; }
            if (_enable.Checked) ParametersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
