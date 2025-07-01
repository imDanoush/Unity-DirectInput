// Script written by ImDanOush (ATG-Simulator.com)

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

[System.Serializable]
public class FFBConfigSaveData
{
    public string ffbDeviceSearchTerm;
    public int fFBDeviceNumber;
    public bool constantForceEnabled;
    public int constantForceMagnitude;
    public bool damperForceEnabled;
    public int damperMagnitude;
    public bool frictionForceEnabled;
    public int frictionMagnitude;
    public bool inertiaForceEnabled;
    public int inertiaMagnitude;
    public bool springForceEnabled;
    public uint springDeadband;
    public int springOffset;
    public int springCoefficient;
    public uint springSaturation;
    public bool sineForceEnabled;
    public int sineMagnitude;
    public uint sinePeriod;
    public bool squareForceEnabled;
    public int squareMagnitude;
    public uint squarePeriod;
    public bool triangleForceEnabled;
    public int triangleMagnitude;
    public uint trianglePeriod;
    public bool sawtoothUpForceEnabled;
    public int sawtoothUpMagnitude;
    public uint sawtoothUpPeriod;
    public bool sawtoothDownForceEnabled;
    public int sawtoothDownMagnitude;
    public uint sawtoothDownPeriod;
    public bool rampForceEnabled;
    public int rampStart;
    public int rampEnd;
}
[DefaultExecutionOrder(-720)]
public class RuntimeFFBConfigUI : MonoBehaviour
{
    private DIInputManager inputManager;
    private bool showWindow = false;
    private bool showLiveValues = false;
    private Vector2 scrollPosition;
    private Dictionary<string, bool> effectFoldouts = new();
    private const string SAVE_KEY = "FFBConfig";

    private Rect windowRect = new(420, 20, 400, 600);
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle boxStyle;
    private GUIStyle sliderStyle;
    private GUIStyle sliderThumbStyle;

    private string ffbDeviceSearchTerm = "";
    private bool deviceSearchModified = false;
    public InputAction inputAction;

    private void OnEnable()
    {
        inputManager = DIInputManager.Instance;
        inputAction.Enable();
        inputAction.performed += InputAction_performed;
        LoadFFBConfig();

        // Initialize the device if we have a search term
        if (!string.IsNullOrEmpty(ffbDeviceSearchTerm))
        {
            inputManager.StartService(ffbDeviceSearchTerm);
        }
    }

    private void InputAction_performed(InputAction.CallbackContext obj)
    {
        showWindow = !showWindow;
    }

    private void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle();
            headerStyle.fontSize = 14;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.padding = new RectOffset(5, 5, 5, 5);
            headerStyle.normal.textColor = Color.white;
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.padding = new RectOffset(10, 10, 5, 5);
            buttonStyle.margin = new RectOffset(5, 5, 5, 5);
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.padding = new RectOffset(10, 10, 10, 10);
            boxStyle.margin = new RectOffset(0, 0, 5, 5);
        }

        if (sliderStyle == null)
        {
            sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            sliderStyle.margin = new RectOffset(5, 5, 5, 5);
        }

        if (sliderThumbStyle == null)
        {
            sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);
        }
    }

    private void SaveFFBConfig()
    {
        if (inputManager == null) return;

        var saveData = new FFBConfigSaveData
        {
            ffbDeviceSearchTerm = ffbDeviceSearchTerm,
            fFBDeviceNumber = inputManager.fFBDeviceNumber,
            constantForceEnabled = inputManager.ConstantForceEnabled,
            constantForceMagnitude = inputManager.ConstantForceMagnitude,
            damperForceEnabled = inputManager.DamperForceEnabled,
            damperMagnitude = inputManager.DamperMagnitude,
            frictionForceEnabled = inputManager.FrictionForceEnabled,
            frictionMagnitude = inputManager.FrictionMagnitude,
            inertiaForceEnabled = inputManager.InertiaForceEnabled,
            inertiaMagnitude = inputManager.InertiaMagnitude,
            springForceEnabled = inputManager.SpringForceEnabled,
            springDeadband = inputManager.SpringDeadband,
            springOffset = inputManager.SpringOffset,
            springCoefficient = inputManager.SpringCoefficient,
            springSaturation = inputManager.SpringSaturation,
            sineForceEnabled = inputManager.SineForceEnabled,
            sineMagnitude = inputManager.SineMagnitude,
            sinePeriod = inputManager.SinePeriod,
            squareForceEnabled = inputManager.SquareForceEnabled,
            squareMagnitude = inputManager.SquareMagnitude,
            squarePeriod = inputManager.SquarePeriod,
            triangleForceEnabled = inputManager.TriangleForceEnabled,
            triangleMagnitude = inputManager.TriangleMagnitude,
            trianglePeriod = inputManager.TrianglePeriod,
            sawtoothUpForceEnabled = inputManager.SawtoothUpForceEnabled,
            sawtoothUpMagnitude = inputManager.SawtoothUpMagnitude,
            sawtoothUpPeriod = inputManager.SawtoothUpPeriod,
            sawtoothDownForceEnabled = inputManager.SawtoothDownForceEnabled,
            sawtoothDownMagnitude = inputManager.SawtoothDownMagnitude,
            sawtoothDownPeriod = inputManager.SawtoothDownPeriod,
            rampForceEnabled = inputManager.RampForceEnabled,
            rampStart = inputManager.RampStart,
            rampEnd = inputManager.RampEnd
        };

        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadFFBConfig()
    {
        if (inputManager == null) return;

        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var saveData = JsonUtility.FromJson<FFBConfigSaveData>(json);

            ffbDeviceSearchTerm = saveData.ffbDeviceSearchTerm;
            inputManager.fFBDeviceNumber = saveData.fFBDeviceNumber;
            inputManager.ConstantForceEnabled = saveData.constantForceEnabled;
            inputManager.ConstantForceMagnitude = saveData.constantForceMagnitude;
            inputManager.DamperForceEnabled = saveData.damperForceEnabled;
            inputManager.DamperMagnitude = saveData.damperMagnitude;
            inputManager.FrictionForceEnabled = saveData.frictionForceEnabled;
            inputManager.FrictionMagnitude = saveData.frictionMagnitude;
            inputManager.InertiaForceEnabled = saveData.inertiaForceEnabled;
            inputManager.InertiaMagnitude = saveData.inertiaMagnitude;
            inputManager.SpringForceEnabled = saveData.springForceEnabled;
            inputManager.SpringDeadband = saveData.springDeadband;
            inputManager.SpringOffset = saveData.springOffset;
            inputManager.SpringCoefficient = saveData.springCoefficient;
            inputManager.SpringSaturation = saveData.springSaturation;
            inputManager.SineForceEnabled = saveData.sineForceEnabled;
            inputManager.SineMagnitude = saveData.sineMagnitude;
            inputManager.SinePeriod = saveData.sinePeriod;
            inputManager.SquareForceEnabled = saveData.squareForceEnabled;
            inputManager.SquareMagnitude = saveData.squareMagnitude;
            inputManager.SquarePeriod = saveData.squarePeriod;
            inputManager.TriangleForceEnabled = saveData.triangleForceEnabled;
            inputManager.TriangleMagnitude = saveData.triangleMagnitude;
            inputManager.TrianglePeriod = saveData.trianglePeriod;
            inputManager.SawtoothUpForceEnabled = saveData.sawtoothUpForceEnabled;
            inputManager.SawtoothUpMagnitude = saveData.sawtoothUpMagnitude;
            inputManager.SawtoothUpPeriod = saveData.sawtoothUpPeriod;
            inputManager.SawtoothDownForceEnabled = saveData.sawtoothDownForceEnabled;
            inputManager.SawtoothDownMagnitude = saveData.sawtoothDownMagnitude;
            inputManager.SawtoothDownPeriod = saveData.sawtoothDownPeriod;
            inputManager.RampForceEnabled = saveData.rampForceEnabled;
            inputManager.RampStart = saveData.rampStart;
            inputManager.RampEnd = saveData.rampEnd;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading FFB config: {e.Message}");
        }
    }

    private void OnGUI()
    {
        if (!showWindow)
        {

            if (inputManager.ffbDevice == null)
            {
                if (!string.IsNullOrEmpty(ffbDeviceSearchTerm))
                    inputManager.StartService(ffbDeviceSearchTerm);
                else
                    if (GUILayout.Button("Connect to the default FFB device (if any)"))
                {
                    inputManager.StartService("di");
                    deviceSearchModified = false;
                    SaveFFBConfig();
                }
            }

            return;
        }

        InitializeStyles();
        windowRect = GUILayout.Window(1, windowRect, DrawWindow, "Force Feedback Configuration", GUI.skin.window);
    }

    private void DrawWindow(int windowID)
    {
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Always show device search controls
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("FFB Device Configuration", headerStyle);

        // Search term input
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search Term:", GUILayout.Width(100));
        string newSearchTerm = GUILayout.TextField(ffbDeviceSearchTerm, GUILayout.ExpandWidth(true));
        if (newSearchTerm != ffbDeviceSearchTerm)
        {
            ffbDeviceSearchTerm = newSearchTerm;
            deviceSearchModified = true;
        }
        GUILayout.EndHorizontal();

        // Help text
        GUILayout.Label("Enter part of your device name to auto-select it\nExample: For 'Fanatec CSL DD', enter 'fanatec'",
            new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true });

        // Connection controls
        if (deviceSearchModified || inputManager.ffbDevice == null)
        {
            if (GUILayout.Button("Connect Device", buttonStyle))
            {
                inputManager.ffbDevice = null;
                inputManager.StartService(ffbDeviceSearchTerm);
                deviceSearchModified = false;
                SaveFFBConfig();
            }
        }
        GUILayout.EndVertical();

        // Show device status
        GUILayout.Label(inputManager.ffbDevice != null ?
            $"Connected to: {inputManager.FFBDeviceName}" :
            "No FFB device connected",
            new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });

        // Only show FFB controls if device is connected
        if (inputManager.ffbDevice != null)
        {
            DrawFFBDeviceInfo();
            DrawLiveValues();
            DrawBasicEffects();
            DrawPeriodicEffects();
            DrawTestCollisions();

            if (GUILayout.Button("Save Configuration", buttonStyle))
            {
                SaveFFBConfig();
            }
        }

        GUILayout.EndScrollView();

        if (GUILayout.Button("Close", buttonStyle))
        {
            showWindow = false;
        }

        GUI.DragWindow();
    }

    private void DrawFFBDeviceInfo()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("FFB Device:", headerStyle);

        // Current device info
        if (inputManager.ffbDevice != null)
        {
            GUILayout.Label(inputManager.FFBDeviceName, GUI.skin.label);
        }
        else
        {
            GUILayout.Label("No FFB device connected", GUI.skin.label);
        }

        // Device search field
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search Term:", GUILayout.Width(100));
        string newSearchTerm = GUILayout.TextField(ffbDeviceSearchTerm, GUILayout.ExpandWidth(true));
        if (newSearchTerm != ffbDeviceSearchTerm)
        {
            ffbDeviceSearchTerm = newSearchTerm;
            deviceSearchModified = true;
        }
        GUILayout.EndHorizontal();

        // Help text
        GUILayout.Label("Enter part of your device name to auto-select it\nExample: For 'Fanatec CSL DD', enter 'fanatec'",
            new GUIStyle(GUI.skin.label) { fontSize = 10, wordWrap = true });

        // Show warning and restart button if search term was modified
        if (deviceSearchModified)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Device search term changed! Click 'Reconnect Device' to apply.",
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.yellow } });

            if (GUILayout.Button("Reconnect Device", buttonStyle))
            {
                SaveFFBConfig();
                inputManager.StartService(ffbDeviceSearchTerm);
                deviceSearchModified = false;
            }
            GUILayout.EndVertical();
        }

        // Device number slider
        inputManager.fFBDeviceNumber = Mathf.RoundToInt(GUILayout.HorizontalSlider(
            inputManager.fFBDeviceNumber, 0, 126, sliderStyle, sliderThumbStyle));
        GUILayout.Label($"Device Number: {inputManager.fFBDeviceNumber}");

        GUILayout.EndVertical();
    }

    private void DrawLiveValues()
    {
        if (inputManager.ffbDevice == null) return;

        GUILayout.BeginVertical(boxStyle);

        // Toggle button
        if (GUILayout.Button(showLiveValues ? "Hide Live Values" : "Show Live Values", buttonStyle))
        {
            showLiveValues = !showLiveValues;
        }

        if (showLiveValues)
        {
            GUILayout.Label("Live Axis Values", headerStyle);

            foreach (var control in inputManager.ffbDevice.allControls)
            {
                if (control is AxisControl || control is InputControl<float>)
                {
                    float value = inputManager.GetFFBDeviceAxisValue(control.name);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{control.name}:", GUILayout.Width(150));
                    GUILayout.Label($"{value:F3}", new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold });
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawBasicEffects()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Basic Effects", headerStyle);

        // Constant Force
        DrawEffectControls("Constant Force",
            ref inputManager.ConstantForceEnabled,
            ref inputManager.ConstantForceMagnitude,
            -10000, 10000);

        // Damper
        DrawEffectControls("Damper",
            ref inputManager.DamperForceEnabled,
            ref inputManager.DamperMagnitude,
            -10000, 10000);

        // Friction
        DrawEffectControls("Friction",
            ref inputManager.FrictionForceEnabled,
            ref inputManager.FrictionMagnitude,
            -10000, 10000);

        // Inertia
        DrawEffectControls("Inertia",
            ref inputManager.InertiaForceEnabled,
            ref inputManager.InertiaMagnitude,
            -10000, 10000);

        // Spring
        if (!effectFoldouts.ContainsKey("Spring"))
            effectFoldouts["Spring"] = false;

        effectFoldouts["Spring"] = EditorDrawToggleHeader("Spring",
            effectFoldouts["Spring"],
            ref inputManager.SpringForceEnabled);

        if (effectFoldouts["Spring"])
        {
            GUILayout.BeginVertical(GUI.skin.box);

            inputManager.SpringDeadband = (uint)DrawSliderUInt("Deadband",
                inputManager.SpringDeadband, 0, 10000);

            inputManager.SpringOffset = DrawSliderInt("Offset",
                inputManager.SpringOffset, -10000, 10000);

            inputManager.SpringCoefficient = DrawSliderInt("Coefficient",
                inputManager.SpringCoefficient, 0, 10000);

            inputManager.SpringSaturation = (uint)DrawSliderUInt("Saturation",
                inputManager.SpringSaturation, 0, 10000);

            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
    }

    private void DrawPeriodicEffects()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Periodic Effects", headerStyle);

        // Sine
        DrawPeriodicEffectControls("Sine",
            ref inputManager.SineForceEnabled,
            ref inputManager.SineMagnitude,
            ref inputManager.SinePeriod);

        // Square
        DrawPeriodicEffectControls("Square",
            ref inputManager.SquareForceEnabled,
            ref inputManager.SquareMagnitude,
            ref inputManager.SquarePeriod);

        // Triangle
        DrawPeriodicEffectControls("Triangle",
            ref inputManager.TriangleForceEnabled,
            ref inputManager.TriangleMagnitude,
            ref inputManager.TrianglePeriod);

        // Sawtooth Up
        DrawPeriodicEffectControls("Sawtooth Up",
            ref inputManager.SawtoothUpForceEnabled,
            ref inputManager.SawtoothUpMagnitude,
            ref inputManager.SawtoothUpPeriod);

        // Sawtooth Down
        DrawPeriodicEffectControls("Sawtooth Down",
            ref inputManager.SawtoothDownForceEnabled,
            ref inputManager.SawtoothDownMagnitude,
            ref inputManager.SawtoothDownPeriod);

        // Ramp
        if (!effectFoldouts.ContainsKey("Ramp"))
            effectFoldouts["Ramp"] = false;

        effectFoldouts["Ramp"] = EditorDrawToggleHeader("Ramp",
            effectFoldouts["Ramp"],
            ref inputManager.RampForceEnabled);

        if (effectFoldouts["Ramp"])
        {
            GUILayout.BeginVertical(GUI.skin.box);

            inputManager.RampStart = DrawSliderInt("Start",
                inputManager.RampStart, -10000, 10000);

            inputManager.RampEnd = DrawSliderInt("End",
                inputManager.RampEnd, -10000, 10000);

            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
    }

    private void DrawTestCollisions()
    {
        GUILayout.BeginVertical(boxStyle);
        GUILayout.Label("Test Collision Effects", headerStyle);

        inputManager.TestCollisionMagnitude = DrawSliderInt("Collision Magnitude",
            inputManager.TestCollisionMagnitude, 0, 10000);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test Front", buttonStyle))
            inputManager.TestFrontalCollision = true;
        if (GUILayout.Button("Test Rear", buttonStyle))
            inputManager.TestRearCollision = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Test Left", buttonStyle))
            inputManager.TestLeftCollision = true;
        if (GUILayout.Button("Test Right", buttonStyle))
            inputManager.TestRightCollision = true;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawEffectControls(string label, ref bool enabled, ref int magnitude, int minValue, int maxValue)
    {
        if (!effectFoldouts.ContainsKey(label))
            effectFoldouts[label] = false;

        effectFoldouts[label] = EditorDrawToggleHeader(label,
            effectFoldouts[label],
            ref enabled);

        if (effectFoldouts[label])
        {
            GUILayout.BeginVertical(GUI.skin.box);
            magnitude = DrawSliderInt("Magnitude", magnitude, minValue, maxValue);
            GUILayout.EndVertical();
        }
    }

    private void DrawPeriodicEffectControls(string label, ref bool enabled, ref int magnitude, ref uint period)
    {
        if (!effectFoldouts.ContainsKey(label))
            effectFoldouts[label] = false;

        effectFoldouts[label] = EditorDrawToggleHeader(label,
            effectFoldouts[label],
            ref enabled);

        if (effectFoldouts[label])
        {
            GUILayout.BeginVertical(GUI.skin.box);
            magnitude = DrawSliderInt("Magnitude", magnitude, -10000, 10000);
            period = (uint)DrawSliderUInt("Period", period, 0, 100000);
            GUILayout.EndVertical();
        }
    }

    private bool EditorDrawToggleHeader(string label, bool foldout, ref bool enabled)
    {
        GUILayout.BeginHorizontal();
        enabled = GUILayout.Toggle(enabled, "", GUILayout.Width(20));
        bool newFoldout = GUILayout.Toggle(foldout, label, GUI.skin.button);
        GUILayout.EndHorizontal();
        return newFoldout;
    }

    private int DrawSliderInt(string label, int value, int minValue, int maxValue)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));

        // Reserve space for the slider
        Rect sliderRect = GUILayoutUtility.GetRect(100, 20);

        // Check for right click before drawing the slider
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && sliderRect.Contains(e.mousePosition))
        {
            value = 0;
            e.Use(); // Prevent event from propagating to window drag
            GUI.changed = true;
        }

        // Draw the slider
        value = Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, value, minValue, maxValue, sliderStyle, sliderThumbStyle));

        GUILayout.Label(value.ToString(), GUILayout.Width(50));
        GUILayout.EndHorizontal();
        return value;
    }

    private uint DrawSliderUInt(string label, uint value, uint minValue, uint maxValue)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));

        // Reserve space for the slider
        Rect sliderRect = GUILayoutUtility.GetRect(100, 20);

        // Check for right click before drawing the slider
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && sliderRect.Contains(e.mousePosition))
        {
            value = 0;
            e.Use(); // Prevent event from propagating to window drag
            GUI.changed = true;
        }

        // Draw the slider
        value = (uint)Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, value, minValue, maxValue, sliderStyle, sliderThumbStyle));

        GUILayout.Label(value.ToString(), GUILayout.Width(50));
        GUILayout.EndHorizontal();
        return value;
    }

}
