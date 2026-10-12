/*
 *
 * On-screen notifications for Direct Input by ImDanOush (ATG-Simulator.com), Good for debugs and logs
 *
 */
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DirectInputManager
{
    /// <summary>
    /// DirectInput notifications: every report goes to the Console once, and (unless disabled) to a card in the
    /// top-right corner of the Game view. IMGUI, so it needs no assets, packages, EventSystem or input backend.
    /// </summary>
    public class DirectInputLogger : MonoBehaviour
    {
        /// <summary>
        /// Show on-screen notifications (default true). Console logging is not affected.
        /// Define DIRECTINPUT_DISABLE_TOASTS to compile the drawing out entirely.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        public static DirectInputLogger Instance { get; private set; }

        /// <summary>
        /// Reports a DirectInput problem or event. Callable from any thread, also before a scene loads:
        /// it logs to the Console at once and queues the card for the main thread.
        /// </summary>
        public static void Report(DirectInputNotificationSeverity severity, string title, string details = null)
        {
            string text = string.IsNullOrEmpty(details) ? "[DirectInput] " + title : "[DirectInput] " + title + "\n" + details;
            if (severity == DirectInputNotificationSeverity.Error) { Debug.LogError(text); }
            else if (severity == DirectInputNotificationSeverity.Warning) { Debug.LogWarning(text); }
            else { Debug.Log(text); }
#if !DIRECTINPUT_DISABLE_TOASTS
            if (!Enabled || s_quitting) { return; }
            lock (s_pending)
            {
                if (s_pending.Count >= MaxStored) { s_pending.RemoveAt(0); }
                s_pending.Add(new Pending { severity = severity, title = title, details = details });
            }
            if (Thread.CurrentThread.ManagedThreadId == s_mainThread) { Drain(); }
            else if (s_mainContext != null && Interlocked.Exchange(ref s_drainPosted, 1) == 0) { s_mainContext.Post(s_drainCallback, null); }
#endif
        }

        /// <summary>Kept for existing callers: reports a Warning.</summary>
        public static void AddLog(string message) => Report(DirectInputNotificationSeverity.Warning, message);

        /// <summary>Creates the logger object if it does not exist. Main thread, Play Mode only.</summary>
        public static void EnsureLoggerExists()
        {
            if (Instance != null || s_quitting || !Application.isPlaying) { return; }
            GameObject loggerObject = new("DirectInputLogger");
            DontDestroyOnLoad(loggerObject);
            Instance = loggerObject.AddComponent<DirectInputLogger>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            useGUILayout = false; // cards use GUI rects only, so skip the Layout pass
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
        }

        private static bool s_quitting, s_domainUsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
#if !DIRECTINPUT_DISABLE_TOASTS
            s_mainThread = Thread.CurrentThread.ManagedThreadId;
            s_mainContext = SynchronizationContext.Current;
#endif
            s_quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
            Instance = null;
            // With domain reload disabled the last session's cards and "already reported" state survive; a fresh domain keeps
            // what was reported while it loaded (an Initialize failure during the Play Mode domain reload).
            if (s_domainUsed)
            {
#if !DIRECTINPUT_DISABLE_TOASTS
                lock (s_pending) { s_pending.Clear(); }
                s_cards.Clear();
#endif
                DIManager.ResetReports();
            }
            s_domainUsed = true;
        }

        private static void OnQuitting() => s_quitting = true; // also raised when the Editor leaves Play Mode

#if !DIRECTINPUT_DISABLE_TOASTS
        private const int MaxStored = 20, MaxVisible = 5;
        private const float FadeSeconds = 1f, Margin = 16f, CardWidth = 440f, Pad = 10f, Stripe = 6f, Gap = 8f, ButtonHeight = 22f;

        private struct Pending { public DirectInputNotificationSeverity severity; public string title, details; }

        private sealed class Card
        {
            public DirectInputNotificationSeverity severity;
            public string title, details, countLabel;
            public int count;
            public float expiresAt;
            public bool expanded, hovered;
        }

        private static readonly List<Pending> s_pending = new();   // any thread, guarded by itself
        private static readonly List<Card> s_cards = new();        // main thread only, oldest first
        private static int s_mainThread = -1, s_drainPosted;
        private static SynchronizationContext s_mainContext;      // Unity's, to bring worker-thread reports to the main thread
        private static readonly SendOrPostCallback s_drainCallback = _ => { Interlocked.Exchange(ref s_drainPosted, 0); Drain(); };
        private static string s_moreLabel;
        private static int s_moreCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            s_mainContext ??= SynchronizationContext.Current; // in case it was not installed yet at SubsystemRegistration
            EnsureLoggerExists();
            if (Instance != null) { Instance.enabled = false; }
            Drain();
        }

        // Main thread: turns queued reports into cards (merging duplicates) and wakes the component.
        private static void Drain()
        {
            if (!Application.isPlaying) { return; } // edit-mode reports wait for Play Mode, or are dropped by a domain reload
            float now = Time.unscaledTime;
            lock (s_pending)
            {
                foreach (Pending p in s_pending) { AddCard(p, now); }
                s_pending.Clear();
            }
            if (s_cards.Count > 0 && Instance != null) { Instance.enabled = true; }
        }

        private static void AddCard(Pending p, float now)
        {
            Card card = null;
            for (int i = 0; i < s_cards.Count; i++)
            {
                if (s_cards[i].severity == p.severity && s_cards[i].title == p.title) { card = s_cards[i]; s_cards.RemoveAt(i); break; }
            }
            card ??= new Card { severity = p.severity, title = p.title };
            card.details = p.details;
            card.count++;
            card.countLabel = card.count > 1 ? "x" + card.count : null;
            card.expiresAt = now + (p.severity == DirectInputNotificationSeverity.Error ? 20f : p.severity == DirectInputNotificationSeverity.Warning ? 12f : 6f);
            s_cards.Add(card); // newest last, drawn on top
            if (s_cards.Count > MaxStored) { s_cards.RemoveAt(0); }
        }

        private void Update()
        {
            if (!Enabled) { s_cards.Clear(); } // switched off while cards were showing
            float now = Time.unscaledTime;
            for (int i = s_cards.Count - 1; i >= 0; i--)
            {
                Card c = s_cards[i];
                if (c.expanded || c.hovered) { c.expiresAt = Mathf.Max(c.expiresAt + Time.unscaledDeltaTime, now + FadeSeconds); } // reading pauses expiry
                else if (now >= c.expiresAt) { s_cards.RemoveAt(i); }
            }
            if (s_cards.Count == 0) { enabled = false; } // idle: no Update or OnGUI calls at all
        }

        private GUIStyle titleStyle, detailsStyle, labelStyle, buttonStyle, linkStyle;
        private static readonly GUIContent s_content = new();

        private static Color SeverityColor(DirectInputNotificationSeverity s) =>
            s == DirectInputNotificationSeverity.Error ? new Color(0.95f, 0.3f, 0.3f) :
            s == DirectInputNotificationSeverity.Warning ? new Color(1f, 0.72f, 0.2f) : new Color(0.35f, 0.65f, 1f);

        private static string SeverityName(DirectInputNotificationSeverity s) =>
            s == DirectInputNotificationSeverity.Error ? "ERROR" : s == DirectInputNotificationSeverity.Warning ? "WARNING" : "INFO";

        private void InitializeStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true, richText = false };
            titleStyle.normal.textColor = Color.white;
            detailsStyle = new GUIStyle(titleStyle) { fontSize = 13, fontStyle = FontStyle.Normal };
            detailsStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
            labelStyle = new GUIStyle(titleStyle) { fontSize = 12, wordWrap = false };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, richText = false };
            linkStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Normal };
            linkStyle.normal.textColor = linkStyle.hover.textColor = new Color(0.55f, 0.8f, 1f);
        }

        private void OnGUI()
        {
            if (s_cards.Count == 0) { return; }
            if (titleStyle == null) { InitializeStyles(); }
            GUI.depth = -10000; // lower depth draws on top of other OnGUI scripts
            float scale = Mathf.Clamp(Screen.height / 1080f, 1f, 4f);
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Mathf.Min(CardWidth, Screen.width / scale - 2 * Margin);
            float x = Screen.width / scale - width - Margin, y = Margin;
            float textWidth = width - Stripe - 2 * Pad;
            Vector2 mouse = Event.current.mousePosition;
            float now = Time.unscaledTime;
            int dismiss = -1;
            bool dismissAll = false;

            if (s_cards.Count > 1)
            {
                GUI.color = Color.white;
                if (GUI.Button(new Rect(x + width - 100, y, 100, ButtonHeight), "Dismiss all", buttonStyle)) { dismissAll = true; }
                y += ButtonHeight + 4;
            }

            int first = Mathf.Max(0, s_cards.Count - MaxVisible);
            for (int i = 0; i < first; i++) { s_cards[i].hovered = false; }
            for (int i = s_cards.Count - 1; i >= first; i--)
            {
                Card c = s_cards[i];
                bool hasDetails = !string.IsNullOrEmpty(c.details);
                s_content.text = c.title;
                float titleHeight = titleStyle.CalcHeight(s_content, textWidth);
                float detailsHeight = 0f;
                if (hasDetails && c.expanded) { s_content.text = c.details; detailsHeight = detailsStyle.CalcHeight(s_content, textWidth) + 4; }
                float height = Pad + ButtonHeight + 4 + titleHeight + (hasDetails ? 4 + 20 + detailsHeight : 0) + Pad;
                Rect card = new Rect(x, y, width, height);
                c.hovered = card.Contains(mouse);

                float alpha = Mathf.Clamp01((c.expiresAt - now) / FadeSeconds);
                Color severity = SeverityColor(c.severity);
                GUI.color = new Color(0.1f, 0.1f, 0.11f, alpha);
                GUI.DrawTexture(card, Texture2D.whiteTexture);
                GUI.color = new Color(severity.r, severity.g, severity.b, alpha);
                GUI.DrawTexture(new Rect(x, y, Stripe, height), Texture2D.whiteTexture);

                float left = x + Stripe + Pad, top = y + Pad;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                labelStyle.normal.textColor = severity; // the word as well as the colour, for colour-blind readers
                GUI.Label(new Rect(left, top, 90, ButtonHeight), SeverityName(c.severity), labelStyle);
                if (c.countLabel != null)
                {
                    labelStyle.normal.textColor = Color.white;
                    GUI.Label(new Rect(left + 90, top, 60, ButtonHeight), c.countLabel, labelStyle);
                }
                if (GUI.Button(new Rect(x + width - Pad - 28, top, 28, ButtonHeight), "x", buttonStyle)) { dismiss = i; }
                if (GUI.Button(new Rect(x + width - Pad - 28 - 4 - 56, top, 56, ButtonHeight), "Copy", buttonStyle))
                {
                    GUIUtility.systemCopyBuffer = hasDetails ? c.title + "\n" + c.details : c.title;
                }
                top += ButtonHeight + 4;
                GUI.Label(new Rect(left, top, textWidth, titleHeight), c.title, titleStyle);
                top += titleHeight + 4;
                if (hasDetails)
                {
                    if (GUI.Button(new Rect(left, top, 110, 20), c.expanded ? "Hide details" : "Show details", linkStyle)) { c.expanded = !c.expanded; }
                    top += 20 + 4;
                    if (c.expanded) { GUI.Label(new Rect(left, top, textWidth, detailsHeight), c.details, detailsStyle); }
                }
                y += height + Gap;
            }

            if (first > 0)
            {
                if (s_moreCount != first) { s_moreCount = first; s_moreLabel = $"+{first} more"; }
                GUI.color = Color.white;
                labelStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x, y, width, ButtonHeight), s_moreLabel, labelStyle);
            }

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
            if (dismissAll) { s_cards.Clear(); }
            else if (dismiss >= 0) { s_cards.RemoveAt(dismiss); }
        }
#endif
    }
}
