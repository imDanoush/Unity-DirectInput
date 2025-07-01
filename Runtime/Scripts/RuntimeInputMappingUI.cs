// Script written by ImDanOush (ATG-Simulator.com)

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using static DIInputManager;

[System.Serializable]
public class InputMappingSaveData
{
    public string mappingName;
    public string deviceName;
    public string inputName;
    public bool inverted;
}

[DefaultExecutionOrder(-715)]
public class RuntimeInputMappingUI : MonoBehaviour
{
    private DIInputManager inputManager;
    private bool showWindow = false;
    private Vector2 scrollPosition;
    private Dictionary<string, bool> mappingFoldouts = new();
    private int listeningIndex = -1;
    private float listenStartTime;
    private const float LISTEN_TIMEOUT = 5.0f;
    private Dictionary<string, float> previousValues = new();
    private const string SAVE_KEY = "InputMappingConfig";

    private Rect windowRect = new(20, 20, 400, 600);
    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle boxStyle;
    public InputAction inputAction;

    private void OnEnable()
    {
        inputManager = DIInputManager.Instance;
        inputAction.Enable();
        inputAction.performed += InputAction_performed;
        LoadMappings();
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
    }

    private void OnGUI()
    {
        if (!showWindow) return;

        // Initialize styles in OnGUI
        InitializeStyles();

        windowRect = GUILayout.Window(0, windowRect, DrawWindow, "Input Mapping Configuration", GUI.skin.window);
    }

    private void DrawDeviceList()
    {
        try
        {
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Connected Devices:", headerStyle);

            if (inputManager != null && inputManager.connectedDevices != null)
            {
                foreach (var device in inputManager.connectedDevices)
                {
                    if (device != null)
                    {
                        GUILayout.Label($"• {device.name}", GUI.skin.label);
                    }
                }
            }

            GUILayout.EndVertical();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in DrawDeviceList: {e.Message}");
            GUILayout.EndVertical(); // Ensure we always end the vertical group
        }
    }

    private void DrawMappingConfigurations()
    {
        try
        {
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label("Input Mappings:", headerStyle);

            if (inputManager != null && inputManager.inputMappings != null)
            {
                for (int i = 0; i < inputManager.inputMappings.Length; i++)
                {
                    var mapping = inputManager.inputMappings[i];
                    if (mapping == null) continue;

                    if (!mappingFoldouts.ContainsKey(mapping.mappingName))
                        mappingFoldouts[mapping.mappingName] = false;

                    mappingFoldouts[mapping.mappingName] = GUILayout.Toggle(
                        mappingFoldouts[mapping.mappingName],
                        mapping.mappingName,
                        GUI.skin.button
                    );

                    if (mappingFoldouts[mapping.mappingName])
                    {
                        DrawMappingDetails(i, mapping);
                    }
                }
            }

            GUILayout.EndVertical();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in DrawMappingConfigurations: {e.Message}");
            GUILayout.EndVertical(); // Ensure we always end the vertical group
        }
    }

    private void DrawWindow(int windowID)
    {
        try
        {
            if (inputManager == null || inputManager.connectedDevices == null)
            {
                GUILayout.Label("No input manager or devices found", headerStyle);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            DrawDeviceList();
            DrawMappingConfigurations();

            if (GUILayout.Button("Save Mappings", buttonStyle))
            {
                SaveMappings();
            }

            GUILayout.EndScrollView();

            if (GUILayout.Button("Close", buttonStyle))
            {
                showWindow = false;
            }

            GUI.DragWindow();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in DrawWindow: {e.Message}");
            GUILayout.EndScrollView(); // Ensure we always end the scroll view
        }
    }


    [System.Serializable]
    public class InputMappingSaveDataWrapper
    {
        public InputMappingSaveData[] mappings;
    }

    private void LoadMappings()
    {
        if (inputManager == null || inputManager.inputMappings == null) return;

        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var wrapper = JsonUtility.FromJson<InputMappingSaveDataWrapper>(json);
            if (wrapper.mappings == null) return;

            foreach (var savedMapping in wrapper.mappings)
            {
                var mapping = inputManager.inputMappings.FirstOrDefault(m => m.mappingName == savedMapping.mappingName);
                if (mapping != null)
                {
                    mapping.deviceName = savedMapping.deviceName;
                    mapping.inputName = savedMapping.inputName;
                    mapping.inverted = savedMapping.inverted;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading input mappings: {e.Message}");
        }
    }

    private void SaveMappings()
    {
        if (inputManager == null || inputManager.inputMappings == null) return;

        var saveData = new InputMappingSaveDataWrapper
        {
            mappings = inputManager.inputMappings.Select(m => new InputMappingSaveData
            {
                mappingName = m.mappingName,
                deviceName = m.deviceName,
                inputName = m.inputName,
                inverted = m.inverted
            }).ToArray()
        };

        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (listeningIndex >= 0)
        {
            ListenForInput();
        }
    }


    private void DrawMappingDetails(int index, DeviceInputMapping mapping)
    {
        GUILayout.BeginVertical(GUI.skin.box);

        // Device selection
        GUILayout.BeginHorizontal();
        GUILayout.Label("Device:", GUILayout.Width(100));

        if (inputManager.connectedDevices != null && inputManager.connectedDevices.Length > 0)
        {
            string[] deviceNames = inputManager.connectedDevices
                .Select(d => d.name)
                .ToArray();

            int currentIndex = System.Array.IndexOf(deviceNames, mapping.deviceName);
            int newIndex = GUILayout.SelectionGrid(
                currentIndex,
                deviceNames,
                1,
                buttonStyle
            );

            if (newIndex != currentIndex && newIndex >= 0 && newIndex < deviceNames.Length)
            {
                mapping.deviceName = deviceNames[newIndex];
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Input:", GUILayout.Width(100));

        if (listeningIndex == index)
        {
            float remainingTime = LISTEN_TIMEOUT - (Time.realtimeSinceStartup - listenStartTime);
            GUILayout.Label($"Listening... ({remainingTime:F1}s)");

            if (GUILayout.Button("Cancel", GUILayout.Width(60)))
            {
                listeningIndex = -1;
            }
        }
        else
        {
            GUILayout.Label(string.IsNullOrEmpty(mapping.inputName) ? "Not Set" : mapping.inputName);

            if (GUILayout.Button("Assign", GUILayout.Width(60)))
            {
                StartListening(index);
            }
        }
        GUILayout.EndHorizontal();

        mapping.inverted = GUILayout.Toggle(mapping.inverted, "Invert Input");
        GUILayout.Label($"Current Value: {mapping.currentValue:F3}");

        GUILayout.EndVertical();
    }


    private void StartListening(int index)
    {
        listeningIndex = index;
        listenStartTime = Time.realtimeSinceStartup;
        previousValues.Clear();
    }

    private void ListenForInput()
    {
        if (listeningIndex >= inputManager.inputMappings.Length)
        {
            listeningIndex = -1;
            return;
        }

        var mapping = inputManager.inputMappings[listeningIndex];
        if (string.IsNullOrEmpty(mapping.deviceName)) return;

        var device = inputManager.connectedDevices.FirstOrDefault(d => d.name == mapping.deviceName);
        if (device == null || !device.added) return;

        if (Time.realtimeSinceStartup - listenStartTime > LISTEN_TIMEOUT)
        {
            listeningIndex = -1;
            return;
        }

        foreach (var control in device.allControls)
        {
            if (control is InputControl<float> floatControl)
            {
                float currentValue = floatControl.ReadValue();
                string controlKey = $"{mapping.deviceName}{control.name}";

                if (!previousValues.ContainsKey(controlKey))
                {
                    previousValues[controlKey] = currentValue;
                    continue;
                }

                float previousValue = previousValues[controlKey];
                float changeThreshold = 0.1f;

                if (Mathf.Abs(currentValue - previousValue) > changeThreshold)
                {
                    mapping.inputName = control.name;
                    listeningIndex = -1;
                    previousValues.Clear();
                    return;
                }

                previousValues[controlKey] = currentValue;
            }
        }
    }
}
