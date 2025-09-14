/*
 *
 * Logger for Direct Input by ImDanOush (ATG-Simulator.com), Good for debugs and logs
 *
 */
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace DirectInputManager
{
    public class DirectInputLogger : MonoBehaviour
    {
    public static DirectInputLogger Instance { get; private set; }
    private static List<LogEntry> visualLogs = new();
    private static Vector2 logScrollPosition;
    private GUIStyle logHeaderStyle;
    private GUIStyle logEntryStyle;
    private GUIStyle logWindowStyle;
    private bool stylesInitialized = false;
    private float windowFadeout = 0f; // Used to fade out the window

    private class LogEntry
    {
        public string message;
        public float time;
        public float displayDuration = 7f;

        public LogEntry(string message)
        {
            this.message = message;
            this.time = Time.time;
        }

        public bool IsExpired => (Time.time - time) > displayDuration;
        public float Opacity => Mathf.Clamp01(1f - ((Time.time - time) / displayDuration));
        public float RemainingTime => Mathf.Max(0, (time + displayDuration) - Time.time);
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void AddLog(string message)
    {
        // Make sure the logger exists
        EnsureLoggerExists();

        // Add to visual logs
        visualLogs.Add(new LogEntry(message));

        // Send to console too
        Debug.Log($"<color=#9416f9>[DirectInputManager]</color> <color=#ff0000>{message}</color>");

        // Cap the log size to prevent memory issues
        if (visualLogs.Count > 20)
            visualLogs.RemoveAt(0);

        // Reset window fade
        if (Instance != null)
            Instance.windowFadeout = 1f;
    }

    public static void EnsureLoggerExists()
    {
        if (Instance == null)
        {
            GameObject loggerObject = new("DirectInputLogger");
            Instance = loggerObject.AddComponent<DirectInputLogger>();
            DontDestroyOnLoad(loggerObject);
        }
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        logWindowStyle = new GUIStyle();
        logWindowStyle.normal.background = CreateColorTexture(new Color(0.15f, 0.15f, 0.15f, 0.9f));
        logWindowStyle.border = new RectOffset(8, 8, 8, 8);
        logWindowStyle.padding = new RectOffset(10, 10, 10, 10);

        logHeaderStyle = new GUIStyle();
        logHeaderStyle.fontSize = 12;
        logHeaderStyle.fontStyle = FontStyle.Bold;
        logHeaderStyle.normal.textColor = Color.white;
        logHeaderStyle.margin = new RectOffset(0, 0, 0, 10);

        logEntryStyle = new GUIStyle();
        logEntryStyle.fontSize = 11;
        logEntryStyle.wordWrap = true;
        logEntryStyle.normal.background = CreateColorTexture(new Color(0.7f, 0.1f, 0.1f, 1f));
        logEntryStyle.normal.textColor = Color.white;
        logEntryStyle.padding = new RectOffset(8, 8, 6, 6);
        logEntryStyle.margin = new RectOffset(0, 0, 2, 2);

        stylesInitialized = true;
    }

    private Texture2D CreateColorTexture(Color color)
    {
        Texture2D tex = new(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void Update()
    {
        // Clean up expired logs
        bool logsRemoved = false;
        int beforeCount = visualLogs.Count;
        visualLogs.RemoveAll(log => log.IsExpired);
        logsRemoved = beforeCount > visualLogs.Count;

        // If we have logs, keep window visible
        if (visualLogs.Count > 0)
        {
            windowFadeout = 1f;

            // Find the log with the shortest remaining time
            float minRemainingTime = visualLogs.Min(log => log.RemainingTime);

            // Start fading out the window when the last log is almost expired
            if (visualLogs.Count == 1 && minRemainingTime < 1.0f)
            {
                windowFadeout = minRemainingTime;
            }
        }
        // If logs were just removed and now we have none, start fading out
        else if (logsRemoved)
        {
            windowFadeout = Mathf.Max(0, windowFadeout - Time.deltaTime);
        }
        // If no logs at all, ensure window is hidden
        else
        {
            windowFadeout = 0f;
        }
    }

    private void OnGUI()
    {
        // No need to show anything if no logs and window fully faded out
        if (visualLogs.Count == 0 && windowFadeout <= 0.01f)
            return;

        // First ensure our styles are initialized
        if (!stylesInitialized)
        {
            InitializeStyles();
        }

        float windowWidth = 350;
        float windowHeight = Mathf.Min(300, 20 + visualLogs.Count * 50);
        if (windowHeight < 80) windowHeight = 80; // Minimum height

        // Draw background box
        Rect windowRect = new(
            Screen.width - windowWidth - 20,
            20,
            windowWidth,
            windowHeight
        );

        // Apply fadeout to entire window
        Color originalColor = GUI.color;
        GUI.color = new Color(originalColor.r, originalColor.g, originalColor.b, windowFadeout);

        GUI.Box(windowRect, "", logWindowStyle);

        GUILayout.BeginArea(windowRect);

        // Header
        GUILayout.Space(5);
        GUILayout.Label("[DirectInputManager] Log Messages", logHeaderStyle);

        // Scrollable logs area
        logScrollPosition = GUILayout.BeginScrollView(
            logScrollPosition,
            GUIStyle.none,
            GUIStyle.none,
            GUILayout.Height(windowHeight - 40)
        );

        foreach (var log in visualLogs.OrderByDescending(l => l.time))
        {
            // Apply log's own opacity on top of window fadeout
            GUI.color = new Color(1, 1, 1, log.Opacity * windowFadeout);

            GUILayout.BeginVertical(logEntryStyle);
            GUILayout.Label(log.message);
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // Restore original color
        GUI.color = originalColor;
    }
}
}

