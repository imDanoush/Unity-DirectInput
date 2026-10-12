using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DirectInputManager
{
    /// <summary>Severity of a DirectInput notification (Console level and on-screen card colour).</summary>
    public enum DirectInputNotificationSeverity { Info, Warning, Error }

    class Native
    {
        // Windows x64 only. Unity ships the DLL from Runtime/Plugins/DLL; the Explorer csproj copies it next to the exe.
        internal const string DLLFile = @"DirectInputForceFeedback.dll";
        [DllImport(DLLFile, CharSet = CharSet.Ansi, EntryPoint = "UpdateConstantForce")]
        internal static extern int UpdateConstantForceSimple([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int magnitude);

        [DllImport(DLLFile)]
        internal static extern void InitializeForStandalone();

        [DllImport(DLLFile)]
        internal static extern int StartDirectInput();

        [DllImport(DLLFile)]
        internal static extern int StopDirectInput();

        [DllImport(DLLFile)]
        internal static extern IntPtr EnumerateDevices(out int deviceCount);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int CreateDevice([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DestroyDevice([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceState([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out FlatJoyState2 DeviceState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceStateRaw([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out DIJOYSTATE2 DeviceState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetDeviceCapabilities([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out DIDEVCAPS DeviceCapabilitiesOut);


        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int EnumerateFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out int count, out IntPtr strings);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int EnumerateFFBAxes([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out int count, out IntPtr strings);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int CreateFFBEffect([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DestroyFFBEffect([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateConstantForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int magnitude);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdatePeriodicForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType, int magnitude, uint period, int offset);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateRampForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int startMagnitude, int endMagnitude, uint durationMicroseconds);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateConditionForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType,
            int offset, int positiveCoefficient, int negativeCoefficient,
            uint positiveSaturation, uint negativeSaturation, int deadBand);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int UpdateCustomForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance,
            [In] int[] forceData, int sampleCount, uint samplePeriod);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int StopAllFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int SetAutocenter([MarshalAs(UnmanagedType.LPStr)] string guidInstance, [MarshalAs(UnmanagedType.I1)] bool autocenterState);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetFFBEffectStatus([MarshalAs(UnmanagedType.LPStr)] string guidInstance, FFBEffects effectType, out uint status);

        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int GetSupportedFFBEffects([MarshalAs(UnmanagedType.LPStr)] string guidInstance, out uint mask);


        [DllImport(DLLFile, CharSet = CharSet.Ansi)]
        internal static extern int DEBUG1([MarshalAs(UnmanagedType.LPStr)] string guidInstance,
            [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] DEBUGDATA);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning restore IDE0079 // Remove unnecessary suppression

        [DllImport(DLLFile)]
        internal static extern int GetActiveDevices(out int count, out IntPtr strings);

        [DllImport(DLLFile)]
        internal static extern int GetDILastError([Out] byte[] buffer, int bufferSize);

        [DllImport(DLLFile)]
        internal static extern void FreeStringArray(IntPtr strings, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        [DllImport(DLLFile)]
        internal static extern void SetDeviceChangeCallback([MarshalAs(UnmanagedType.FunctionPtr)] DeviceChangeCallback onDeviceChange);


    }
    public class DIManager
    {
        // Define all callback delegates at class level with proper attributes
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        // Static delegate instances to prevent garbage collection
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0052 // Remove unread private members
        private static Native.DeviceChangeCallback s_deviceChangeCallback;
#pragma warning restore IDE0052 // Remove unread private members
#pragma warning restore IDE0079 // Remove unnecessary suppression


        //////////////////////////////////////////////////////////////
        // Cross Platform "Macros" - Allows lib to work in Visual Studio & Unity
        //////////////////////////////////////////////////////////////
#if UNITY_5_3_OR_NEWER
        /// <summary>Kept for the samples: the same switch as DirectInputLogger.Enabled (on-screen notifications).</summary>
        public static bool showLogsRuntime { get => DirectInputLogger.Enabled; set => DirectInputLogger.Enabled = value; }

        private static uint ClampAgnostic(uint value, uint min, uint max) => (uint)Mathf.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Mathf.Clamp(value, min, max);

        // Where reports go (Console once, plus the on-screen card). Internal so tests can observe reports.
        internal static Action<DirectInputNotificationSeverity, string, string> Reporter = DirectInputLogger.Report;
#else
        private static uint ClampAgnostic(uint value, uint min, uint max) => Math.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Math.Clamp(value, min, max);
        internal static Action<DirectInputNotificationSeverity, string, string> Reporter =
            (severity, title, details) => System.Diagnostics.Debug.WriteLine($"[DirectInput] {severity}: {title}\n{details}");
#endif

#if UNITY_EDITOR
        // In edit mode the Editor self-check reports an Initialize failure once, with the plugin asset as context.
        private static bool ReportInitFailures => UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
#else
        private static bool ReportInitFailures => true;
#endif

        //////////////////////////////////////////////////////////////
        // Private Variables - For Internal use
        //////////////////////////////////////////////////////////////

        private static bool _isInitialized = false;               // is DIManager ready
        private static DeviceInfo[] _devices = Array.Empty<DeviceInfo>(); // Hold data for devices plugged in
        private static readonly Dictionary<string, ActiveDeviceInfo> _activeDevices = new(); // Hold data for devices actively attached

        //////////////////////////////////////////////////////////////
        // Public Variables
        //////////////////////////////////////////////////////////////
        public static bool IsInitialized { get => _isInitialized; }
        public static DeviceInfo[] Devices { get => _devices; }
        public static Dictionary<string, ActiveDeviceInfo> ActiveDevices { get => _activeDevices; }

        //////////////////////////////////////////////////////////////
        // Methods
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Initializes DirectInput<br/><br/>
        /// </summary>
        /// <returns>
        /// True if sucessful or DI already initialized<br/>
        /// False if failed
        /// </returns>
        public static bool Initialize()
        {
            if (_isInitialized) { return _isInitialized; }
            int hr;
            try
            {
#if !UNITY_5_3_OR_NEWER
                Native.InitializeForStandalone();
#endif
                hr = Native.StartDirectInput();
                if (hr >= 0)
                {
                    s_deviceChangeCallback = OnDeviceChange;
                    Native.SetDeviceChangeCallback(s_deviceChangeCallback);
                    // The DLL outlives a Unity domain reload; a pointer to a collected delegate would crash on the next hot-plug.
                    AppDomain.CurrentDomain.DomainUnload += (sender, args) => Native.SetDeviceChangeCallback(null);
                }
            }
            catch (Exception ex) // DllNotFoundException hides the cause (missing file, missing VC++ runtime, wrong CPU), so probe it
            {
                var (severity, title, details) = DIDiagnostics.ExplainLoadFailure(ex, PluginCandidatePaths());
                return FailInit(ex.HResult, severity, title, details);
            }
            if (hr < 0) { return FailInit(hr, DirectInputNotificationSeverity.Error, "DirectInput could not be started", Details("StartDirectInput", hr, null, null)); }
            Check("Initialize", hr); // reports recovery when an earlier attempt failed
            return _isInitialized = true;
        }

        private static bool FailInit(int hr, DirectInputNotificationSeverity severity, string title, string details)
        {
            LastHResult = hr;
            LastError = title + "\n" + details;
            if (ReportInitFailures && !Seen("Initialize", null, null, hr, out _))
            {
                Remember("Initialize", null, null, hr, LastError);
                Reporter(severity, title, details);
            }
            return _isInitialized = false;
        }

        // Where the plugin should be, for the load-failure probe.
        private static string[] PluginCandidatePaths()
        {
#if UNITY_EDITOR
            string asset = UnityEditor.AssetDatabase.GUIDToAssetPath("fa5394ba60c8227438c47b1d0d16805c"); // Runtime/Plugins/DLL/DirectInputForceFeedback.dll
            if (string.IsNullOrEmpty(asset)) { return Array.Empty<string>(); }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asset); // "Packages/..." is virtual: map it to the package folder
            return new[] { package != null ? Path.Combine(package.resolvedPath, asset.Substring(package.assetPath.Length + 1)) : Path.GetFullPath(asset) };
#elif UNITY_5_3_OR_NEWER
            // HYPOTHESIS: Windows players keep plugins in <Game>_Data/Plugins/x86_64 (older layouts: Plugins).
            return new[] { Path.Combine(Application.dataPath, "Plugins", "x86_64", Native.DLLFile), Path.Combine(Application.dataPath, "Plugins", Native.DLLFile) };
#else
            return new[] { Path.Combine(AppContext.BaseDirectory, Native.DLLFile) };
#endif
        }

        private static readonly object _enumerateLock = new object();
        private static int s_enumOutcome = -2;                               // -2 not yet, 0 no devices, 1 devices; guarded by _enumerateLock
        private static readonly HashSet<string> s_ffbNoted = new HashSet<string>(); // devices already given the no-FFB hint

        /// <summary>
        /// Fetch currently available devices and populate DIManager.devices<br/>
        /// </summary>
        public static void EnumerateDevices() => EnumerateDevices(true);

        // reportOutcome false skips the "no controller" and no-FFB notes: the Editor re-enumerates on every script compile.
        internal static void EnumerateDevices(bool reportOutcome)
        {
            // The native list stays valid only until the next EnumerateDevices call, so the read must not overlap another one.
            lock (_enumerateLock)
            {
                IntPtr ptrDevices = Native.EnumerateDevices(out int deviceCount); // Returns pointer to list of devices and how many are available

                if (deviceCount < 0)
                {
                    // A failed enumeration is not "no devices": keep the old list so a rescan does not detach every device.
                    LastHResult = E_FAIL;
                    LastError = "EnumerateDevices failed: DirectInput could not list the game controllers";
                    if (!Seen("EnumerateDevices", null, null, E_FAIL, out _))
                    {
                        Remember("EnumerateDevices", null, null, E_FAIL, LastError);
                        Reporter(DirectInputNotificationSeverity.Error, "Game controllers could not be listed",
                            "DirectInput's device enumeration failed, so the device list was left as it was." + PluginMessageLine() +
                            "\nWhat to do: reconnect the controllers. If this keeps happening, restart the game.");
                    }
                    return;
                }
                Forget("EnumerateDevices", null, null, out _);

                // Fill a local array and publish it once: another thread may read Devices while this runs.
                DeviceInfo[] devices = deviceCount > 0 ? new DeviceInfo[deviceCount] : Array.Empty<DeviceInfo>();
                int deviceSize = Marshal.SizeOf(typeof(DeviceInfo)); // Size of each Device entry
                for (int i = 0; i < deviceCount; i++)
                {
                    devices[i] = Marshal.PtrToStructure<DeviceInfo>(ptrDevices + i * deviceSize); // Transform the Ptr into a C# instance of DeviceInfo
                }
                _devices = devices;
                if (!reportOutcome) { return; }

                int outcome = deviceCount == 0 ? 0 : 1;
                if (outcome == 0 && s_enumOutcome != 0)
                {
                    Reporter(DirectInputNotificationSeverity.Info, "No DirectInput game controller found",
                        "What to do: connect and switch on the wheel or controller. If it is connected, check that its driver is installed and that it is in PC mode, not a console mode.");
                }
                s_enumOutcome = outcome;
                foreach (DeviceInfo device in devices)
                {
                    string note = DIDiagnostics.NoForceFeedbackNote(device);
                    if (note != null && s_ffbNoted.Add(device.guidInstance)) { Reporter(DirectInputNotificationSeverity.Warning, $"No force feedback on {device.productName}", note); }
                }
            }
        }

        public static async Task EnumerateDevicesAsync()
        {
            Task enumDevicesTask = Task.Run(EnumerateDevices);
            Task warningTimeout = Task.Delay(1000);

            if (warningTimeout == await Task.WhenAny(enumDevicesTask, warningTimeout))
            {
                Reporter(DirectInputNotificationSeverity.Warning, "Controller scan is taking longer than 1 second",
                    "DirectInput device enumeration is slow. A device or its driver may be busy; the scan continues.");
            }
            await enumDevicesTask; // also surfaces an exception from a scan that failed before the timeout
        }

        /// <summary>
        /// Attach to Device, ready to get state/ForceFeedback<br/><br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Device was attached
        /// </returns>
        public static bool Attach(string guidInstance)
        {
            if (_activeDevices.ContainsKey(guidInstance)) { return true; } // We're already attached to that device
            int index = Array.FindIndex(_devices, d => d.guidInstance == guidInstance);
            if (index < 0)
            {
                return Invalid("Attach", guidInstance, null, unchecked((int)0x80070490), // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)
                    $"Attach: device {guidInstance} is not in the enumerated device list; call EnumerateDevices first", DirectInputNotificationSeverity.Warning);
            }
            if (!Check("CreateDevice", Native.CreateDevice(guidInstance), guidInstance)) { return false; }
            _activeDevices.Add(guidInstance, new ActiveDeviceInfo() { deviceInfo = _devices[index] }); // Add device to our C# active device tracker (Dictionary allows us to easily check if GUID already exists)
            return true;
        }


        /// <summary>
        /// Remove a specified Device
        /// </summary>
        /// <returns>
        /// True upon sucessful destruction or device already didn't exist
        /// </returns>
        public static bool Destroy(string guidInstance)
        {
            if (!_activeDevices.ContainsKey(guidInstance)) { return true; } // We don't think we're attached to that device, consider it removed
            int hresult = Native.DestroyDevice(guidInstance);
            _activeDevices.Remove(guidInstance); // remove from our C# active device tracker
            ForgetDevice(guidInstance);          // a reattached device starts with a clean report state
            return Check("DestroyDevice", hresult, guidInstance);
        }

        /// <summary>
        /// Retrieve state of the Device, Flattened for easier comparison.<br/>
        /// </summary>
        /// <returns>
        /// FlatJoyState2
        /// </returns>
        public static FlatJoyState2 GetDeviceState(string guidInstance)
        {
            Check("GetDeviceState", Native.GetDeviceState(guidInstance, out FlatJoyState2 DeviceState), guidInstance);
            return DeviceState;
        }

        // public static async Task<FlatJoyState2> GetDeviceStateAsync(string guidInstance){
        //   return await Task.Run(()=>{return GetDeviceState(guidInstance);});
        // }

        /// <summary>
        /// Retrieve state of the Device<br/>
        /// *Warning* DIJOYSTATE2 contains arrays making it difficult to compare, concider using GetDeviceState
        /// </summary>
        /// <returns>
        /// DIJOYSTATE2
        /// </returns>
        public static DIJOYSTATE2 GetDeviceStateRaw(string guidInstance)
        {
            Check("GetDeviceStateRaw", Native.GetDeviceStateRaw(guidInstance, out DIJOYSTATE2 DeviceState), guidInstance);
            return DeviceState;
        }

        public static string[] GetActiveDevices()
        {
            int hr = Native.GetActiveDevices(out int count, out IntPtr stringsPtr);

            if (!Check("GetActiveDevices", hr)) { return Array.Empty<string>(); }
            if (count <= 0 || stringsPtr == IntPtr.Zero) { return Array.Empty<string>(); } // S_OK with nothing attached

            try
            {
                string[] result = ReadStringArray(stringsPtr, count);
                if (result.Length != _activeDevices.Count)
                {
                    Reporter(DirectInputNotificationSeverity.Warning, "Attached device lists disagree",
                        $"The DLL holds {result.Length} device(s), DIManager holds {_activeDevices.Count}.");
                }
                return result;
            }
            finally
            {
                Native.FreeStringArray(stringsPtr, count);
            }
        }

        public static string[] GetDeviceFFBCapabilities(string guidInstance)
        {
            int hr = Native.EnumerateFFBEffects(guidInstance, out int count, out IntPtr stringsPtr);

            if (!Check("EnumerateFFBEffects", hr, guidInstance)) { return Array.Empty<string>(); }
            if (count <= 0 || stringsPtr == IntPtr.Zero) { return Array.Empty<string>(); } // S_OK when the device lists no effects

            try
            {
                return ReadStringArray(stringsPtr, count);
            }
            finally
            {
                Native.FreeStringArray(stringsPtr, count);
            }
        }

        private static string[] ReadStringArray(IntPtr stringsPtr, int count)
        {
            string[] result = new string[count];
            for (int i = 0; i < count; ++i)
            {
                IntPtr currentPtr = Marshal.ReadIntPtr(stringsPtr, i * IntPtr.Size);
                result[i] = DirectInputManager.WinErrors.PtrToStringUTF8(currentPtr);
            }
            return result;
        }

        /// <summary>The DLL returns E_ABORT for "effect not created" and "effect already exists".</summary>
        private const int E_ABORT = unchecked((int)0x80004004);
        private const int E_FAIL = unchecked((int)0x80004005);
        private const int E_INVALIDARG = unchecked((int)0x80070057);

        /// <summary>
        /// HRESULT of the most recent native call made through a DIManager method that reports
        /// success or failure (0 on success). Lets a caller show the actual API error instead of a bare false.
        /// </summary>
        public static int LastHResult { get; private set; }

        /// <summary>
        /// Human readable description of the most recent failure reported by a DIManager method, or empty.
        /// A later success does not clear it: LastHResult &lt; 0 tells whether it belongs to the last call.
        /// </summary>
        public static string LastError { get; private set; } = "";

        // Last reported non-OK outcome per (operation, device, effect), so per-frame paths report each change once.
        private static readonly Dictionary<(string op, string guid, int effect), (int hr, string message)> s_reported = new();

        private static (string, string, int) Key(string operation, string guid, FFBEffects? effect) => (operation, guid ?? "", effect.HasValue ? (int)effect.Value : -1);

        private static bool Seen(string operation, string guid, FFBEffects? effect, int hr, out string message)
        {
            lock (s_reported)
            {
                bool seen = s_reported.TryGetValue(Key(operation, guid, effect), out var last) && last.hr == hr;
                message = seen ? last.message : null;
                return seen;
            }
        }

        private static void Remember(string operation, string guid, FFBEffects? effect, int hr, string message)
        {
            lock (s_reported) { s_reported[Key(operation, guid, effect)] = (hr, message); }
        }

        private static bool Forget(string operation, string guid, FFBEffects? effect, out int lastHr, bool keepWarnings = false)
        {
            lastHr = 0;
            if (s_reported.Count == 0) { return false; } // per-poll fast path: nothing is failing
            lock (s_reported)
            {
                var key = Key(operation, guid, effect);
                if (!s_reported.TryGetValue(key, out var last) || (keepWarnings && last.hr >= 0)) { return false; }
                s_reported.Remove(key);
                lastHr = last.hr;
                return true;
            }
        }

        private static bool IsFailing(string operation, string guid, FFBEffects effect, out string message)
        {
            lock (s_reported)
            {
                bool failing = s_reported.TryGetValue(Key(operation, guid, effect), out var last) && last.hr < 0;
                message = last.message;
                return failing;
            }
        }

        private static void ForgetDevice(string guid)
        {
            lock (s_reported) { foreach (var key in s_reported.Keys.Where(k => k.guid == guid).ToList()) { s_reported.Remove(key); } }
        }

        /// <summary>Forgets what was already reported, so the next play session reports ongoing problems again.</summary>
        internal static void ResetReports()
        {
            lock (s_reported) { s_reported.Clear(); }
            lock (_enumerateLock) { s_enumOutcome = -2; s_ffbNoted.Clear(); }
        }

        /// <summary>DI_DOWNLOADSKIPPED, DI_TRUNCATED, DI_TRUNCATEDANDRESTARTED: success codes for a force that is not what was asked for.</summary>
        internal static bool IsPartialSuccess(int hr) => hr == 3 || hr == 8 || hr == 0xC;

        // Records the outcome of a native call and reports each change of (operation, device, effect, HRESULT) once:
        // a new failure, a success code that did not fully apply, or a recovery. Repeats only refresh LastError and allocate nothing.
        // HRESULT success is "not negative" (DI_EFFECTRESTARTED is 4). log:false is for status queries whose failure is an answer.
        internal static bool Check(string operation, int hresult, string guidInstance = null, FFBEffects? effect = null, bool log = true)
        {
            LastHResult = hresult;
            bool failed = hresult < 0;
            if (!failed && !IsPartialSuccess(hresult))
            {
                // A clipped force can alternate with an exact one every frame, so a warning is kept rather than "recovered".
                if (log && Forget(operation, guidInstance, effect, out int previous, keepWarnings: true))
                {
                    Reporter(DirectInputNotificationSeverity.Info, Title(operation, effect, guidInstance, "works again"),
                        $"Previous result: {WinErrors.Describe(previous, operation)}");
                }
                return true;
            }
            if (log && Seen(operation, guidInstance, effect, hresult, out string message))
            {
                if (failed) { LastError = message; }
                return !failed;
            }
            string target = effect.HasValue ? $"{effect} {guidInstance}" : guidInstance;
            message = $"{operation} failed: {WinErrors.Describe(hresult, operation)} {target}".TrimEnd();
            if (failed) { LastError = message; }
            if (!log) { return !failed; }
            Remember(operation, guidInstance, effect, hresult, message);
            var severity = failed && operation != "GetDeviceState" ? DirectInputNotificationSeverity.Error : DirectInputNotificationSeverity.Warning; // a lost device is a state, not a fault
            Reporter(severity, Title(operation, effect, guidInstance, failed ? "failed" : "only partly applied"), Details(operation, hresult, guidInstance, effect));
            return !failed;
        }

        // An argument or state error found before calling the DLL; reported once like a native failure.
        private static bool Invalid(string operation, string guid, FFBEffects? effect, int hr, string message,
            DirectInputNotificationSeverity severity = DirectInputNotificationSeverity.Error)
        {
            LastHResult = hr;
            LastError = message;
            if (!Seen(operation, guid, effect, hr, out _))
            {
                Remember(operation, guid, effect, hr, message);
                Reporter(severity, Title(operation, effect, guid, "failed"), message);
            }
            return false;
        }

        private static string Title(string operation, FFBEffects? effect, string guid, string outcome) =>
            $"{operation}{(effect.HasValue ? " " + effect.Value : "")} {outcome}{(guid == null ? "" : " on " + DeviceName(guid))}";

        private static string DeviceName(string guid)
        {
            if (_activeDevices.TryGetValue(guid, out var adi)) { return adi.deviceInfo.productName; }
            foreach (DeviceInfo d in _devices) { if (d.guidInstance == guid) { return d.productName; } }
            return guid;
        }

        /// <summary>Multi-line report body: operation, device, HRESULT name and meaning, the DLL's message, what to do.</summary>
        internal static string Details(string operation, int hr, string guid, FFBEffects? effect)
        {
            var text = new StringBuilder("Operation: ").Append(operation);
            if (effect.HasValue) { text.Append(" (").Append(effect.Value).Append(')'); }
            if (guid != null)
            {
                text.Append("\nDevice: ").Append(DeviceName(guid)).Append(' ').Append(guid);
                foreach (DeviceInfo d in _devices)
                {
                    if (d.guidInstance == guid && DIDiagnostics.TryGetVidPid(d.guidProduct, out ushort vid, out ushort pid)) { text.Append($", VID {vid:X4} PID {pid:X4}"); }
                }
            }
            text.Append("\nResult: ").Append(WinErrors.Describe(hr, operation));
            if (hr < 0) { text.Append(PluginMessageLine()); }
            string hint = WinErrors.Hint(hr, operation);
            if (hint != null) { text.Append("\nWhat to do: ").Append(hint); }
            return text.ToString();
        }

        // The DLL keeps the text of its last failure (it is not cleared on success, hence "last").
        private static string PluginMessageLine()
        {
            try
            {
                var buffer = new byte[512];
                if (Native.GetDILastError(buffer, buffer.Length) < 0) { return ""; }
                int length = Array.IndexOf(buffer, (byte)0);
                string text = Encoding.UTF8.GetString(buffer, 0, length < 0 ? buffer.Length : length);
                return text.Length == 0 || text == "No error" ? "" : "\nDLL's last error message: " + text;
            }
            catch (Exception e) when (e is EntryPointNotFoundException || e is DllNotFoundException) { return ""; } // older DLL, or none
        }

        /// <summary>
        /// Retrieve the capabilities of the device. E.g. # Buttons, # Axes, Driver Version<br/>
        /// Device must be attached first<br/>
        /// </summary>
        /// <returns>
        /// DIDEVCAPS https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
        /// </returns>
        public static DIDEVCAPS GetDeviceCapabilities(string guidInstance)
        {
            Check("GetDeviceCapabilities", Native.GetDeviceCapabilities(guidInstance, out DIDEVCAPS DeviceCapabilities), guidInstance);
            return DeviceCapabilities; // zeroed when the query failed; LastHResult tells
        }

        /// <summary>
        /// Returns if the device has the ForceFeedback Flag<br/>
        /// </summary>
        /// <returns>
        /// True if device can provide ForceFeedback<br/>
        /// </returns>
        public static bool FFBCapable(string guidInstance)
        {
            return GetDeviceCapabilities(guidInstance).dwFlags.HasFlag(DwFlags.DIDC_FORCEFEEDBACK);
        }

        /// <summary>
        /// Returns the attached status of the device<br/>
        /// </summary>
        /// <returns>
        /// True if device is attached
        /// </returns>
        public static bool IsDeviceActive(string guidInstance)
        {
            return _activeDevices.ContainsKey(guidInstance);
        }

        /// <summary>
        /// Enables an FFB Effect on the device.<br/>
        /// E.g. FFBEffects.ConstantForce<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was added sucessfully
        /// </returns>
        public static bool EnableFFBEffect(string guidInstance, FFBEffects effectType)
        {
            return Check("CreateFFBEffect", Native.CreateFFBEffect(guidInstance, effectType), guidInstance, effectType);
        }

        /// <summary>
        /// Removes an FFB Effect from the device<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was removed sucessfully
        /// </returns>
        public static bool DestroyFFBEffect(string guidInstance, FFBEffects effectType)
        {
            return Check("DestroyFFBEffect", Native.DestroyFFBEffect(guidInstance, effectType), guidInstance, effectType);
        }

        /// <summary>
        /// Stops and removes all active effects on a device<br/>
        /// *Warning* Effects will have to be enabled again before use<br/>
        /// </summary>
        /// <returns>
        /// True if effects stopped successfully
        /// </returns>
        public static bool StopAllFFBEffects(string guidInstance)
        {
            return Check("StopAllFFBEffects", Native.StopAllFFBEffects(guidInstance), guidInstance);
        }

        /// <summary>
        /// Turns the device's own centering spring on or off (DIPROP_AUTOCENTER). Many wheels enable it by
        /// default, where it fights the effects an application plays. The DLL briefly unacquires the device
        /// to change the property and restarts active effects afterwards.
        /// </summary>
        public static bool SetAutocenter(string guidInstance, bool enabled)
        {
            return Check("SetAutocenter", Native.SetAutocenter(guidInstance, enabled), guidInstance);
        }

        /// <summary>
        /// Which standard effect types the attached device enumerates: bit n set means FFBEffects value n
        /// is available. Returns false, with LastError set, when the device is not attached or when the
        /// native DLL predates this export (then treat every effect as possibly supported).
        /// </summary>
        public static bool TryGetSupportedFFBEffects(string guidInstance, out uint mask)
        {
            mask = 0;
            try
            {
                return Check("GetSupportedFFBEffects", Native.GetSupportedFFBEffects(guidInstance, out mask), guidInstance);
            }
            catch (EntryPointNotFoundException)
            {
                return OldDll("GetSupportedFFBEffects");
            }
        }

        /// <summary>
        /// Asks DirectInput whether an effect is playing right now (IDirectInputEffect::GetEffectStatus).<br/>
        /// Returns false, with LastError set, when the effect was never created on this device or when the
        /// native DLL predates this export.
        /// </summary>
        public static bool TryGetFFBEffectStatus(string guidInstance, FFBEffects effectType, out bool playing)
        {
            playing = false;
            try
            {
                if (!Check("GetFFBEffectStatus", Native.GetFFBEffectStatus(guidInstance, effectType, out uint status), guidInstance, effectType, log: false)) { return false; }
                playing = (status & 1) != 0; // DIEGES_PLAYING
                return true;
            }
            catch (EntryPointNotFoundException)
            {
                return OldDll("GetFFBEffectStatus");
            }
        }

        private static bool OldDll(string export)
        {
            LastHResult = unchecked((int)0x80004001); // E_NOTIMPL
            LastError = $"{export}: the loaded DirectInputForceFeedback.dll predates this export; rebuild it from src~/DirectInputForceFeedback~";
            if (!Seen("OldDll", null, null, LastHResult, out _))
            {
                Remember("OldDll", null, null, LastHResult, LastError);
                Reporter(DirectInputNotificationSeverity.Warning, "DirectInputForceFeedback.dll is older than this package's scripts",
                    $"The loaded DLL has no {export} export, so features that need it are unavailable.\nWhat to do: replace Runtime/Plugins/DLL/DirectInputForceFeedback.dll with a build of src~/DirectInputForceFeedback~ from this package version, then restart.");
            }
            return false;
        }

        /// <summary>
        /// Stops DirectInput<br/>
        /// </summary>
        /// <returns>
        /// True if DirectInput was stopped successfully
        /// </returns>
        public static bool StopDirectInput()
        {
            int hr;
            lock (_enumerateLock) { hr = Native.StopDirectInput(); } // the DLL frees the device list an enumeration may be reading
            _activeDevices.Clear();
            _devices = Array.Empty<DeviceInfo>();
            _isInitialized = false;
            ResetReports();
            return Check("StopDirectInput", hr);
        }

        /// <summary>
        /// Returns byte[] of state<br/>
        /// </summary>
        /// <returns>
        /// byte[]
        /// </returns>
        public static byte[] FlatStateToBytes(FlatJoyState2 state)
        {
            int size = Marshal.SizeOf(state);
            byte[] StateRawBytes = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(state, ptr, true);
            Marshal.Copy(ptr, StateRawBytes, 0, size);
            Marshal.FreeHGlobal(ptr);
            return StateRawBytes;
        }

        /// <summary>
        /// Computes MD5 for FlatState<br/>
        /// </summary>
        /// <returns>
        /// byte[] MD5 Hash
        /// </returns>
        public static byte[] FlatStateMD5(FlatJoyState2 state)
        {
            using MD5 md5 = MD5.Create();
            var StateRawBytes = FlatStateToBytes(state);
            return md5.ComputeHash(StateRawBytes);
        }

        /// <summary>
        /// Fetches device state and triggers events if state changed<br/>
        /// </summary>
        public static void Poll(string guidInstance)
        {
            if (_activeDevices.TryGetValue(guidInstance, out ActiveDeviceInfo ADI))
            { // Check if device active
                // A failed read (device unplugged, input lost) leaves the struct zeroed; publishing it would report full-lock axes.
                if (!Check("GetDeviceState", Native.GetDeviceState(guidInstance, out FlatJoyState2 state), guidInstance)) { return; }
                if (!ADI.hasState || !state.Equals(ADI.lastState)) // Field-wise compare: no allocation, no hashing per poll
                {
                    ADI.lastState = state;
                    ADI.hasState = true;
                    ADI.stateHash = state.GetHashCode();
                    ADI.DeviceStateChange(ADI.deviceInfo, state); // Invoke all event listeners for this device
                }
            }
            else
            {
                // Device isn't attached
            }

        }

        /// <summary>
        /// Fetches device state for all devices and queues events<br/>
        /// </summary>
        public static void PollAll()
        {
            foreach (ActiveDeviceInfo ADI in _activeDevices.Values)
            {
                Poll(ADI.deviceInfo);
            }
        }

        /// <summary>
        /// Obtains ActiveDeviceInfo for specified GUID<br/>
        /// </summary>
        /// <returns>
        /// Bool if GUID was found <br/>
        /// OUT ADI of device if found
        /// </returns>
        public static bool GetADI(string guidInstance, out ActiveDeviceInfo ADI)
        {
            return _activeDevices.TryGetValue(guidInstance, out ADI);
        }

        /// <summary>
        /// *Internal use only*
        /// Used to test C++ code in the DLL during devlopment
        /// </summary>
        // public static string[] DEBUG1() {
        //   string[] DEBUGDATA = null;
        //   DEBUGDATA = new string[1] { "Test" };
        //   int hresult = Native.DEBUG1(out DEBUGDATA);
        //   if (hresult != 0) { DebugLog($"DEBUG1 Failed: 0x{hresult.ToString("x")} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }

        //   return DEBUGDATA;
        // }


        //////////////////////////////////////////////////////////////
        // Device Events
        //////////////////////////////////////////////////////////////

        // Events to add listners too                   E.g. DIManager.OnDeviceAdded += MyFunctionWhenDeviceAdded;
        public static event deviceInfoEvent OnDeviceAdded;
        public static event deviceInfoEvent OnDeviceRemoved;

        // Functions to invoke event listeners
        public static void DeviceAdded(DeviceInfo device) { OnDeviceAdded?.Invoke(device); }
        public static void DeviceRemoved(DeviceInfo device) { OnDeviceRemoved?.Invoke(device); }

        // static Action InvokeDebounce;

        private static readonly Debouncer ODCDebouncer = new Debouncer(150);        // 150ms (OnDeviceChangeDebouncer)

        /// <summary>
        /// *Internal use only*
        /// Called from the DLL when a windows WM_DEVICECHANGE event is captured
        /// This function invokes the necessary events
        /// </summary>
#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(Native.DeviceChangeCallback))]
#endif
        private static void OnDeviceChange(DBTEvents DBTEvent)
        {
            //DebugLog($"DeviceChange {DBTEvent.ToString()}");

            ODCDebouncer.Debounce(() => { ScanDevicesForChanges(); });
        }

        private static bool s_scanning, s_rescan; // one scan at a time; a change during a scan queues one more

        private static async void ScanDevicesForChanges()
        {
            if (s_scanning) { s_rescan = true; return; }
            s_scanning = true;
            try
            {
                do
                {
                    s_rescan = false;
                    DeviceInfo[] oldDevices = _devices;                          // Store currently known devices
                    try { await EnumerateDevicesAsync(); }                       // Fetch what devices are available now
                    catch (Exception ex)
                    {
                        Reporter(DirectInputNotificationSeverity.Error, "Game controllers could not be rescanned", $"{ex.GetType().Name}: {ex.Message}");
                        return;
                    }

                    foreach (DeviceInfo device in oldDevices.Except(_devices).ToList())
                    {                                                            // Process removed devices
                        Reporter(DirectInputNotificationSeverity.Warning, $"Device disconnected: {device.productName}",
                            $"Device: {device.productName} {device.guidInstance}\nInput and force feedback from it stop until it is connected again.");
                        try
                        {
                            if (_activeDevices.TryGetValue(device.guidInstance, out ActiveDeviceInfo ADI)) { ADI.DeviceRemoved(device); } // Invoke event listeners for this device
                            DeviceRemoved(device);                                   // Invoke all event listeners all devices
                        }
                        catch (Exception ex) { Reporter(DirectInputNotificationSeverity.Error, "An OnDeviceRemoved handler threw", ex.ToString()); }
                        finally { Destroy(device); }                             // If device was connected remove it gracefully
                    }

                    foreach (DeviceInfo device in _devices.Except(oldDevices).ToList())
                    {                                                            // Process newly added devices
                        Reporter(DirectInputNotificationSeverity.Info, $"Device connected: {device.productName}", $"Device: {device.productName} {device.guidInstance}");
                        try { DeviceAdded(device); }                             // Invoke event to broadcast a new device is available
                        catch (Exception ex) { Reporter(DirectInputNotificationSeverity.Error, "An OnDeviceAdded handler threw", ex.ToString()); }
                    }
                } while (s_rescan);
            }
            finally { s_scanning = false; }
        }

        //////////////////////////////////////////////////////////////
        // Effect Specific Methods
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Update existing effect with new DICONDITION array<br/><br/>
        ///
        /// DICondition[DeviceFFBEffectAxesCount]:<br/><br/>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/><br/>
        /// Only element 0 reaches the DLL, which applies it to every actuator axis. For non-condition effects the
        /// fields are reused: ConstantForce magnitude = positiveCoefficient; RampForce start/end = positive/negativeCoefficient;
        /// periodic magnitude = positiveCoefficient, period (microseconds, unclamped) = positiveSaturation, offset = offset;
        /// CustomForce samples = positiveCoefficient of every element, sample period (unclamped) = element 0 negativeCoefficient.
        /// Prefer the typed Update*Native functions.
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateEffect(string guidInstance, FFBEffects effectType, DICondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
            {
                return Invalid("UpdateEffect", guidInstance, effectType, E_INVALIDARG, "UpdateEffect: conditions must contain at least one element");
            }
            uint period = (uint)Math.Max(0, conditions[0].positiveSaturation);
            uint samplePeriod = (uint)Math.Max(0, conditions[0].negativeCoefficient);
            conditions = (DICondition[])conditions.Clone(); // clamp a copy, not the caller's array

            // Apply clamping to all values
            for (int i = 0; i < conditions.Length; i++)
            {
                conditions[i].deadband = ClampAgnostic(conditions[i].deadband, 0, 10000);
                conditions[i].offset = ClampAgnostic(conditions[i].offset, -10000, 10000);
                conditions[i].negativeCoefficient = ClampAgnostic(conditions[i].negativeCoefficient, -10000, 10000);
                conditions[i].positiveCoefficient = ClampAgnostic(conditions[i].positiveCoefficient, -10000, 10000);
                conditions[i].negativeSaturation = ClampAgnostic(conditions[i].negativeSaturation, 0, 10000);
                conditions[i].positiveSaturation = ClampAgnostic(conditions[i].positiveSaturation, 0, 10000);
            }

            // Use the appropriate native function based on effect type
            bool success = false;
            switch (effectType)
            {
                case FFBEffects.ConstantForce:
                    success = UpdateConstantForceNative(guidInstance, conditions[0].positiveCoefficient);
                    break;

                case FFBEffects.Spring:
                case FFBEffects.Damper:
                case FFBEffects.Friction:
                case FFBEffects.Inertia:
                    success = UpdateConditionForceNative(guidInstance, effectType,
                        conditions[0].offset,
                        conditions[0].positiveCoefficient,
                        conditions[0].negativeCoefficient,
                        (uint)conditions[0].positiveSaturation,
                        (uint)conditions[0].negativeSaturation,
                        conditions[0].deadband);
                    break;

                case FFBEffects.RampForce:
                    success = UpdateRampForceNative(guidInstance,
                        conditions[0].positiveCoefficient,
                        conditions[0].negativeCoefficient);
                    break;

                case FFBEffects.Sine:
                case FFBEffects.Square:
                case FFBEffects.Triangle:
                case FFBEffects.SawtoothUp:
                case FFBEffects.SawtoothDown:
                    success = UpdatePeriodicForceNative(guidInstance, effectType,
                        conditions[0].positiveCoefficient,
                        period,
                        conditions[0].offset);
                    break;

                case FFBEffects.CustomForce:
                    // For custom force, we extract the data from conditions array
                    int[] forceData = new int[conditions.Length];
                    for (int i = 0; i < conditions.Length; i++)
                    {
                        forceData[i] = conditions[i].positiveCoefficient;
                    }
                    success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
                    break;

                default:
                    return Invalid("UpdateEffect", guidInstance, effectType, E_INVALIDARG, $"UpdateEffect: unsupported effect type {effectType}");
            }

            return success; // a failure was reported by the update it came from
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateConstantForceSimple(string guidInstance, int magnitude)
        {
            return UpdateConstantForceNative(guidInstance, magnitude);
        }

        /// <summary>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateSpringSimple(string guidInstance, uint deadband, int offset, int negativeCoefficient, int positiveCoefficient, uint negativeSaturation, uint positiveSaturation)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Spring,
                offset, positiveCoefficient, negativeCoefficient,
                positiveSaturation, negativeSaturation, (int)deadband);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateDamperSimple(string guidInstance, int magnitude)
        {
            // Saturation is the maximum force the condition may output (0..10000); 0 would silence the effect
            // on devices that honour DIDC_SATURATION, so the simple helpers always allow full output.
            return UpdateConditionForceNative(guidInstance, FFBEffects.Damper,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateFrictionSimple(string guidInstance, int magnitude)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Friction,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateInertiaSimple(string guidInstance, int magnitude)
        {
            return UpdateConditionForceNative(guidInstance, FFBEffects.Inertia,
                0, magnitude, magnitude, 10000, 10000, 0);
        }

        public static bool UpdatePeriodicSimple(string guidInstance, FFBEffects effectType, int magnitude, uint period = 30000, int rampStart = 0, int rampEnd = 0)
        {
            // For RampForce effect, use UpdateRampForceNative
            if (effectType == FFBEffects.RampForce)
            {
                int hr = Native.UpdateRampForce(guidInstance, rampStart, rampEnd, 1000000);
                if (hr == E_ABORT) // effect not created yet
                {
                    if (!CreateForSimple(guidInstance, effectType)) { return false; }
                    hr = Native.UpdateRampForce(guidInstance, rampStart, rampEnd, 1000000);
                }
                return Check("UpdateRampForce", hr, guidInstance, effectType);
            }
            // For other periodic effects, use UpdatePeriodicForceNative
            else
            {
                int hr = PeriodicForce(guidInstance, effectType, magnitude, period, 0);
                if (hr == E_ABORT) // effect not created yet
                {
                    if (!CreateForSimple(guidInstance, effectType)) { return false; }
                    hr = PeriodicForce(guidInstance, effectType, magnitude, period, 0);
                }
                return Check("UpdatePeriodicForce", hr, guidInstance, effectType);
            }
        }

        // Creates the effect a Simple helper found missing. While creation keeps failing, its report stands
        // and it is not retried every frame (until an explicit EnableFFBEffect succeeds, or a reattach).
        // DestroyFFBEffect must not reset this: the samples call Enable, then Destroy on failure, every frame.
        private static bool CreateForSimple(string guidInstance, FFBEffects effectType)
        {
            if (IsFailing("CreateFFBEffect", guidInstance, effectType, out string createError)) { LastHResult = E_ABORT; LastError = createError; return false; }
            return EnableFFBEffect(guidInstance, effectType);
        }

        public static bool UpdateCustomForceSimple(string guidInstance, int[] forceData, uint samplePeriod, int offset = 0, uint deadband = 0)
        {
            if (forceData == null || forceData.Length == 0)
            {
                return Invalid("UpdateCustomForce", guidInstance, FFBEffects.CustomForce, E_INVALIDARG, "UpdateCustomForce: forceData must contain at least one sample");
            }

            int hr = Native.UpdateCustomForce(guidInstance, forceData, forceData.Length, samplePeriod);
            if (hr == E_ABORT) // effect not created yet
            {
                if (!CreateForSimple(guidInstance, FFBEffects.CustomForce)) { return false; }
                hr = Native.UpdateCustomForce(guidInstance, forceData, forceData.Length, samplePeriod);
            }
            return Check("UpdateCustomForce", hr, guidInstance, FFBEffects.CustomForce);
        }

        //////////////////////////////////////////////////////////////
        // Overloads                                                //
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Attach to Device, ready to get state/ForceFeedback<br/><br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Device was attached
        /// </returns>
        public static bool Attach(DeviceInfo device) => Attach(device.guidInstance);

        /// <summary>
        /// Remove a specified Device
        /// </summary>
        /// <returns>
        /// True upon sucessful destruction
        /// </returns>
        public static bool Destroy(DeviceInfo device) => Destroy(device.guidInstance);

        /// <summary>
        /// Retrieve state of the Device, Flattened for easier comparison.<br/>
        /// </summary>
        /// <returns>
        /// FlatJoyState2
        /// </returns>
        public static FlatJoyState2 GetDeviceState(DeviceInfo device) => GetDeviceState(device.guidInstance);

        /// <summary>
        /// Retrieve state of the Device<br/>
        /// *Warning* DIJOYSTATE2 contains arrays making it difficult to compare, concider using GetDeviceState
        /// </summary>
        /// <returns>
        /// DIJOYSTATE2
        /// </returns>
        public static DIJOYSTATE2 GetDeviceStateRaw(DeviceInfo device) => GetDeviceStateRaw(device.guidInstance);

        /// <summary>
        /// Retrieve the capabilities of the device. E.g. # Buttons, # Axes, Driver Version<br/>
        /// Device must be attached first<br/>
        /// </summary>
        /// <returns>
        /// DIDEVCAPS https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
        /// </returns>
        public static DIDEVCAPS GetDeviceCapabilities(DeviceInfo device) => GetDeviceCapabilities(device.guidInstance);

        /// <summary>
        /// Returns if the device has the ForceFeedback Flag<br/>
        /// </summary>
        /// <returns>
        /// True if device can provide ForceFeedback<br/>
        /// </returns>
        public static bool FFBCapable(DeviceInfo device) => FFBCapable(device.guidInstance);

        /// <summary>
        /// Returns the attached status of the device<br/>
        /// </summary>
        /// <returns>
        /// True if device is attached
        /// </returns>
        public static bool IsDeviceActive(DeviceInfo device) => IsDeviceActive(device.guidInstance);

        /// <summary>
        /// Enables an FFB Effect on the device.<br/>
        /// E.g. FFBEffects.ConstantForce<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was added sucessfully
        /// </returns>
        public static bool EnableFFBEffect(DeviceInfo device, FFBEffects effectType) => EnableFFBEffect(device.guidInstance, effectType);

        /// <summary>
        /// Removes an FFB Effect from the device<br/>
        /// Refer to FFBEffects enum for all effect types
        /// </summary>
        /// <returns>
        /// True if effect was removed sucessfully
        /// </returns>
        public static bool DestroyFFBEffect(DeviceInfo device, FFBEffects effectType) => DestroyFFBEffect(device.guidInstance, effectType);

        /// <summary>
        /// Fetches supported FFB Effects by specified Device<br/>
        /// </summary>
        /// <returns>
        /// string[] of effect names supported
        /// </returns>
        public static string[] GetDeviceFFBCapabilities(DeviceInfo device) => GetDeviceFFBCapabilities(device.guidInstance);

        /// <summary>
        /// Stops and removes all active effects on a device<br/>
        /// *Warning* Effects will have to be enabled again before use<br/>
        /// </summary>
        /// <returns>
        /// True if effects stopped successfully
        /// </returns>
        public static bool StopAllFFBEffects(DeviceInfo device) => StopAllFFBEffects(device.guidInstance);

        /// <summary>
        /// Fetches device state and triggers events if state changed<br/>
        /// </summary>
        public static void Poll(DeviceInfo device) => Poll(device.guidInstance);

        /// <summary>
        /// Obtains ActiveDeviceInfo for specified GUID<br/>
        /// </summary>
        /// <returns>
        /// Bool if GUID was found <br/>
        /// OUT ADI of device if found
        /// </returns>
        public static bool GetADI(DeviceInfo device, out ActiveDeviceInfo ADI) => GetADI(device.guidInstance, out ADI);

        //////////////////////////////////////////////////////////////
        // Effect Specific Methods Overloads
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Update existing effect with new DICONDITION array<br/><br/>
        ///
        /// DICondition[DeviceFFBEffectAxesCount]:<br/><br/>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateEffect(DeviceInfo device, FFBEffects effect, DICondition[] conditions) => UpdateEffect(device.guidInstance, effect, conditions);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateConstantForceSimple(DeviceInfo device, int Magnitude) => UpdateConstantForceSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// deadband: Inacive Zone [-10,000 - 10,000]<br/>
        /// offset: Move Effect Center[-10,000 - 10,000]<br/>
        /// negativeCoefficient: Negative of center coefficient [-10,000 - 10,000]<br/>
        /// positiveCoefficient: Positive of center Coefficient [-10,000 - 10,000]<br/>
        /// negativeSaturation: Negative of center saturation [0 - 10,000]<br/>
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateSpringSimple(DeviceInfo device, uint deadband, int offset, int negativeCoefficient, int positiveCoefficient, uint negativeSaturation, uint positiveSaturation) => UpdateSpringSimple(device.guidInstance, deadband, offset, negativeCoefficient, positiveCoefficient, negativeSaturation, positiveSaturation);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateDamperSimple(DeviceInfo device, int Magnitude) => UpdateDamperSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateFrictionSimple(DeviceInfo device, int Magnitude) => UpdateFrictionSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateInertiaSimple(DeviceInfo device, int Magnitude) => UpdateInertiaSimple(device.guidInstance, Magnitude);

        /// <summary>
        /// Updates a periodic force feedback effect with specified magnitude
        /// Magnitude: Strength of Force [-10,000 - 10,0000]
        /// </summary>
        /// <returns>
        /// A boolean representing if the Effect updated successfully
        /// </returns>
        public static bool UpdatePeriodicSimple(DeviceInfo device, FFBEffects effectType, int Magnitude, uint period = 30000, int rampStart = 0, int rampEnd = 0) => UpdatePeriodicSimple(device.guidInstance, effectType, Magnitude, period, rampStart, rampEnd);

        public static bool UpdateCustomForceEffect(DeviceInfo device, int[] forceData, uint samplePeriod) => UpdateCustomForceSimple(device.guidInstance, forceData, samplePeriod);

        /// <summary>
        /// Updates the constant force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="magnitude">Force [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateConstantForceNative(string guidInstance, int magnitude)
        {
            return Check("UpdateConstantForce", Native.UpdateConstantForce(guidInstance, magnitude), guidInstance, FFBEffects.ConstantForce);
        }

        /// <summary>
        /// Updates a periodic effect (Sine, Square, Triangle, etc.).
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="effectType">Type of periodic effect</param>
        /// <param name="magnitude">Effect amplitude [0, 10000] - note: unsigned, use offset for direction</param>
        /// <param name="period">Period in microseconds</param>
        /// <param name="offset">Effect offset [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdatePeriodicForceNative(string guidInstance, FFBEffects effectType, int magnitude, uint period = 30000, int offset = 0)
        {
            return Check("UpdatePeriodicForce", PeriodicForce(guidInstance, effectType, magnitude, period, offset), guidInstance, effectType);
        }

        private static int PeriodicForce(string guidInstance, FFBEffects effectType, int magnitude, uint period, int offset)
        {
            int positiveMagnitude = Math.Abs(magnitude);
            if (positiveMagnitude > 10000) positiveMagnitude = 10000;
            return Native.UpdatePeriodicForce(guidInstance, effectType, positiveMagnitude, period, offset);
        }

        /// <summary>
        /// Updates a ramp force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="startMagnitude">Starting force [-10000, 10000]</param>
        /// <param name="endMagnitude">Ending force [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        /// <summary>Plays a ramp from startMagnitude to endMagnitude once over durationMicroseconds (a ramp cannot run forever).</summary>
        public static bool UpdateRampForceNative(string guidInstance, int startMagnitude, int endMagnitude, uint durationMicroseconds = 1000000)
        {
            return Check("UpdateRampForce", Native.UpdateRampForce(guidInstance, startMagnitude, endMagnitude, durationMicroseconds), guidInstance, FFBEffects.RampForce);
        }

        /// <summary>
        /// Updates a condition effect (Spring, Damper, Friction, Inertia).
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="effectType">Type of condition effect</param>
        /// <param name="offset">Center offset [-10000, 10000]</param>
        /// <param name="positiveCoefficient">Positive side coefficient [-10000, 10000]</param>
        /// <param name="negativeCoefficient">Negative side coefficient [-10000, 10000]</param>
        /// <param name="positiveSaturation">Positive side saturation [0, 10000]</param>
        /// <param name="negativeSaturation">Negative side saturation [0, 10000]</param>
        /// <param name="deadBand">Dead zone [0, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateConditionForceNative(string guidInstance, FFBEffects effectType,
            int offset = 0, int positiveCoefficient = 10000, int negativeCoefficient = 10000,
            uint positiveSaturation = 10000, uint negativeSaturation = 10000, int deadBand = 0)
        {
            return Check("UpdateConditionForce", Native.UpdateConditionForce(guidInstance, effectType,
                offset, positiveCoefficient, negativeCoefficient,
                positiveSaturation, negativeSaturation, deadBand), guidInstance, effectType);
        }

        /// <summary>
        /// Updates a custom force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="forceData">Array of force values [-10000, 10000]</param>
        /// <param name="samplePeriod">Sample period in microseconds</param>
        /// <returns>True if the update was successful</returns>
        /// <summary>Downloads forceData (-10000..10000 each) as the custom force samples; samplePeriod is microseconds, 0 keeps the period the DLL took from DIDEVCAPS.</summary>
        public static bool UpdateCustomForceNative(string guidInstance, int[] forceData, uint samplePeriod)
        {
            if (forceData == null || forceData.Length == 0) { return Invalid("UpdateCustomForce", guidInstance, FFBEffects.CustomForce, E_INVALIDARG, "UpdateCustomForce: forceData must contain at least one sample"); }
            return Check("UpdateCustomForce", Native.UpdateCustomForce(guidInstance, forceData, forceData.Length, samplePeriod), guidInstance, FFBEffects.CustomForce);
        }

    } // End of DIManager



    //////////////////////////////////////////////////////////////
    // Utilities
    //////////////////////////////////////////////////////////////

    /// <summary>
    /// Helper class to print out user friendly system error codes.
    /// Taken from: https://stackoverflow.com/a/21174331/9053848
    /// </summary>
    public static class WinErrors
    {
        #region definitions
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr LocalFree(IntPtr hMem);

        // Unicode binds FormatMessageW: the ANSI export returns code-page bytes that do not decode as UTF-8 on non-English Windows.
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern int FormatMessage(FormatMessageFlags dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, ref IntPtr lpBuffer, uint nSize, IntPtr Arguments);

        [Flags]
        private enum FormatMessageFlags : uint
        {
            FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100,
            FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,
            FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
            FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000,
            FORMAT_MESSAGE_FROM_HMODULE = 0x00000800,
            FORMAT_MESSAGE_FROM_STRING = 0x00000400,
        }
        #endregion

        /// <summary>
        /// Gets a user friendly string message for a system error code
        /// </summary>
        /// <param name="errorCode">System error code</param>
        /// <returns>Error string</returns>
        public static string GetSystemMessage(int errorCode)
        {
            try
            {
                IntPtr lpMsgBuf = IntPtr.Zero;

                int dwChars = FormatMessage(
                    FormatMessageFlags.FORMAT_MESSAGE_ALLOCATE_BUFFER | FormatMessageFlags.FORMAT_MESSAGE_FROM_SYSTEM | FormatMessageFlags.FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero,
                    (uint)errorCode,
                    0, // Default language
                    ref lpMsgBuf,
                    0,
                    IntPtr.Zero);
                if (dwChars == 0)
                {
                    // Handle the error.
                    int le = Marshal.GetLastWin32Error();
                    return "Unable to get error code string from System - Error " + le.ToString();
                }

                string sRet = Marshal.PtrToStringUni(lpMsgBuf);

                // Free the buffer.
                lpMsgBuf = LocalFree(lpMsgBuf);
                return sRet;
            }
            catch (Exception e)
            {
                return "Unable to get error code string from System -> " + e.ToString();
            }
        }

        /// <summary>
        /// Describes an HRESULT: DirectInput codes by name (FormatMessage does not know them), then the system message.
        /// </summary>
        public static string Describe(int hr) => Describe(hr, null);

        /// <summary>
        /// Like Describe(int), naming E_ACCESSDENIED by the call that returned it: DirectInput uses the one value
        /// for DIERR_OTHERAPPHASPRIO, DIERR_READONLY and DIERR_HANDLEEXISTS.
        /// </summary>
        public static string Describe(int hr, string operation)
        {
            string name = hr switch
            {
                0 => "DI_OK",
                1 => "S_FALSE (DI_NOEFFECT / DI_NOTATTACHED / DI_PROPNOEFFECT)",
                2 => "DI_POLLEDDEVICE (the device is polled)",
                3 => "DI_DOWNLOADSKIPPED (parameters stored but not sent to the device)",
                4 => "DI_EFFECTRESTARTED",
                8 => "DI_TRUNCATED (some parameters were beyond the device's capabilities and were clipped)",
                0xC => "DI_TRUNCATEDANDRESTARTED (parameters clipped and the effect restarted)",
                unchecked((int)0x8000000A) => "E_PENDING (data is not yet available)",
                unchecked((int)0x80004001) => "DIERR_UNSUPPORTED (E_NOTIMPL)",
                unchecked((int)0x80004002) => "DIERR_NOINTERFACE (E_NOINTERFACE)",
                unchecked((int)0x80004003) => "E_POINTER",
                unchecked((int)0x80004004) => "E_ABORT (DLL: effect already exists or was never created)",
                unchecked((int)0x80004005) => "DIERR_GENERIC (E_FAIL; DLL: device not attached, device has no force-feedback actuators (FFB driver or vendor software missing?), or internal error)",
                unchecked((int)0x80040154) => "DIERR_DEVICENOTREG (device, instance or effect not registered with DirectInput)",
                unchecked((int)0x80070002) => "DIERR_NOTFOUND / DIERR_OBJECTNOTFOUND",
                unchecked((int)0x80070005) => operation == null || operation == "SetAutocenter"
                    ? "DIERR_OTHERAPPHASPRIO / DIERR_READONLY / DIERR_HANDLEEXISTS (E_ACCESSDENIED)"
                    : "DIERR_OTHERAPPHASPRIO (E_ACCESSDENIED; another application has priority)",
                unchecked((int)0x80070006) => "E_HANDLE",
                unchecked((int)0x8007000E) => "DIERR_OUTOFMEMORY (E_OUTOFMEMORY)",
                unchecked((int)0x80070077) => "DIERR_BADDRIVERVER (incompatible driver version or incomplete driver components)",
                unchecked((int)0x8007047E) => "DIERR_OLDDIRECTINPUTVERSION (a newer DirectInput is required)",
                unchecked((int)0x80070481) => "DIERR_BETADIRECTINPUTVERSION (a pre-release DirectInput is installed)",
                unchecked((int)0x800704DF) => "DIERR_ALREADYINITIALIZED",
                unchecked((int)0x8007000C) => "DIERR_NOTACQUIRED",
                unchecked((int)0x80070015) => "DIERR_NOTINITIALIZED",
                unchecked((int)0x8007001E) => "DIERR_INPUTLOST",
                unchecked((int)0x80070057) => "DIERR_INVALIDPARAM (E_INVALIDARG)",
                unchecked((int)0x800700AA) => "DIERR_ACQUIRED",
                unchecked((int)0x80070490) => "ERROR_NOT_FOUND",
                unchecked((int)0x80040200) => "DIERR_INSUFFICIENTPRIVS",
                unchecked((int)0x80040201) => "DIERR_DEVICEFULL",
                unchecked((int)0x80040202) => "DIERR_MOREDATA",
                unchecked((int)0x80040203) => "DIERR_NOTDOWNLOADED",
                unchecked((int)0x80040204) => "DIERR_HASEFFECTS",
                unchecked((int)0x80040205) => "DIERR_NOTEXCLUSIVEACQUIRED",
                unchecked((int)0x80040206) => "DIERR_INCOMPLETEEFFECT",
                unchecked((int)0x80040207) => "DIERR_NOTBUFFERED",
                unchecked((int)0x80040208) => "DIERR_EFFECTPLAYING",
                unchecked((int)0x80040209) => "DIERR_UNPLUGGED",
                unchecked((int)0x8004020A) => "DIERR_REPORTFULL",
                unchecked((int)0x8004F300) => "DIERR_NOWINDOWHANDLE (DLL could not create its hidden window)",
                unchecked((int)0x8004F301) => "DIERR_UNITYNOTINITIALIZED (plugin load callback has not run)",
                _ when (hr & 0xFFFFFF00) == 0x80040300 => $"DIERR_DRIVERFIRST+{hr & 0xFF} (defined by the device's driver)",
                _ when (hr & 0xFFFF0000) == 0xC0110000 => $"HID error 0x{hr & 0xFFFF:x4} (reported by the device's HID driver)",
                _ => null,
            };
            string text = name != null ? name : GetSystemMessage(hr).Trim();
            return $"0x{hr:x8} {text}";
        }

        /// <summary>Plain-language next step for an HRESULT returned by a DIManager operation, or null when there is none to give.</summary>
        internal static string Hint(int hr, string operation)
        {
            switch (unchecked((uint)hr))
            {
                case 0x80070005: return operation == "SetAutocenter" ? "The device refused the change; another program may be controlling it."
                        : "Another program is using this device exclusively, for example another game or the wheel vendor's tuning or telemetry app. Close it, then reconnect the device or restart this application.";
                case 0x8007001E: case 0x8007000C: case 0x80040209:
                    return "The device was unplugged, switched off or taken over by another program. Reconnect it; input resumes when it is back.";
                case 0x80004005:
                    if (operation == "CreateDevice") { return "The device may have been unplugged since it was listed. Reconnect it and try again."; }
                    if (operation == "CreateFFBEffect") { return "The device reported no force-feedback axes, or its driver failed. This usually means the wheel's vendor driver or software is not installed or running, or the wheel is in a console or compatibility mode."; }
                    return "The device may be disconnected, or DirectInput is not started.";
                case 0x80040201: return "The device has no room for another effect. Disable effects you do not need.";
                case 0x80004001: return "This device or its driver does not support this effect or operation.";
                case 0x80040206: return "The driver rejected the effect as incomplete (for example, no force-feedback axis).";
                case 0x80040205: return "Force feedback needs exclusive access to the device, which this application does not have now. Another program may be holding it.";
                case 0x80040208: return "This device cannot change an effect while it is playing.";
                case 0x80040203: return "The effect is no longer on the device (for example after a reset or reconnect). Disable and enable the effect again.";
                case 0x80070057: return "A parameter is outside the range this device or call accepts.";
                case 0x80004004: return operation == "CreateFFBEffect" ? "The effect already exists on this device." : "The effect has not been created on this device. Call EnableFFBEffect first.";
                case 3: return "The new values were stored but not sent to the device because it is not exclusively acquired, so the device did not receive them.";
                case 8: case 0xC: return "Some values were beyond what the device supports and were clipped, so the force differs from what was requested.";
                case 0x8007047E: case 0x80070481: return "DirectInput on this PC does not match what the plugin needs. Update Windows.";
                case 0x80070077: return "The device's driver is incompatible or incomplete. Reinstall the device's driver.";
                case 0x80040154: return "The device or effect is not registered with DirectInput. Reinstall the device's driver.";
                case 0x8007000E: return "Windows ran out of memory for DirectInput. Close other programs and restart.";
                case 0x80070015: return "DirectInput is not started. Call DIManager.Initialize first.";
                case 0x80070490: return "Call EnumerateDevices first, or reconnect the device.";
                case 0x8004F301: return "Unity did not run the plugin's load callback. This usually means DirectInputForceFeedback.dll is not imported as a native plugin for this platform: check its import settings (Editor and Standalone Windows x86_64), then restart the Editor.";
            }
            if ((hr & 0xFFFFFF00) == 0x80040300) { return "The meaning of this code is defined by the device's driver. Check the vendor's driver and firmware versions."; }
            if ((hr & 0xFFFF0000) == 0xC0110000) { return "The device's HID driver rejected the request. Check the vendor's driver and firmware versions."; }
            return null;
        }

        public static string PtrToStringUTF8(IntPtr ptr) //by Risto-Paasivirta
        {
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) ++len;
            if (len == 0) return "";
            byte[] array = new byte[len];
            Marshal.Copy(ptr, array, 0, len);
            return Encoding.UTF8.GetString(array);
        }
    }

    /// <summary>
    /// Plain-language diagnosis for what DirectInput itself cannot report: why the plugin did not load,
    /// and the likely reason a wheel shows no force feedback. The mappings are pure so they can be tested anywhere.
    /// </summary>
    internal static class DIDiagnostics
    {
        internal enum LoadFailure { NotWindows, Not64Bit, Arm64, OldDll, FileMissing, DependencyMissing, WrongFormat, NotLoadedByEngine, Other }

        // Imports of DirectInputForceFeedback.dll that are not always present (objdump of the shipped DLL).
        internal static readonly string[] Dependencies = { "VCRUNTIME140.dll", "VCRUNTIME140_1.dll", "MSVCP140.dll", "dinput8.dll" };

        internal static LoadFailure ClassifyLoadFailure(bool isWindows, bool is64Bit, bool isArm64, bool entryPointMissing, bool fileFound, int loadError)
        {
            if (!isWindows) { return LoadFailure.NotWindows; }
            if (!is64Bit) { return LoadFailure.Not64Bit; }
            if (isArm64) { return LoadFailure.Arm64; }
            if (entryPointMissing) { return LoadFailure.OldDll; }
            if (!fileFound) { return LoadFailure.FileMissing; }
            if (loadError == 0) { return LoadFailure.NotLoadedByEngine; }
            if (loadError == 126) { return LoadFailure.DependencyMissing; } // ERROR_MOD_NOT_FOUND: the DLL or one of its imports
            if (loadError == 193) { return LoadFailure.WrongFormat; }      // ERROR_BAD_EXE_FORMAT
            return LoadFailure.Other;
        }

        internal static (DirectInputNotificationSeverity severity, string title, string details) ExplainLoadFailure(
            LoadFailure kind, string[] searched, string path, int loadError, string missingDependency, string exceptionText)
        {
            const string Dll = "DirectInputForceFeedback.dll";
            var error = DirectInputNotificationSeverity.Error;
            switch (kind)
            {
                case LoadFailure.NotWindows:
                    return (DirectInputNotificationSeverity.Warning, "DirectInput works only on Windows",
                        $"{Dll} is a Windows x64 plugin, so wheel input and force feedback are off on this platform.");
                case LoadFailure.Not64Bit:
                    return (error, "The DirectInput plugin needs a 64-bit process",
                        $"{Dll} is x64 only and cannot load into this 32-bit process.\nWhat to do: build for Windows x86_64.");
                case LoadFailure.Arm64:
                    return (error, "The DirectInput plugin cannot load in an Arm64 process",
                        $"{Dll} is x64 only, and an Arm64 process can load only Arm64 DLLs.\nWhat to do: build or run the Windows x86_64 version.");
                case LoadFailure.OldDll:
                    return (error, $"{Dll} is older than this package's scripts",
                        $"The loaded DLL lacks an export the scripts call ({exceptionText}).\nWhat to do: replace Runtime/Plugins/DLL/{Dll} with a build of src~/DirectInputForceFeedback~ from this package version, then restart.");
                case LoadFailure.FileMissing:
                    return (error, $"{Dll} was not found",
                        $"Looked in: {(searched.Length == 0 ? "(no known location)" : string.Join(", ", searched))}.\nWhat to do: reinstall the package or rebuild the game, and check that antivirus software did not quarantine the DLL.");
                case LoadFailure.DependencyMissing:
                    if (missingDependency == null)
                    {
                        return (error, $"{Dll} could not load one of its dependencies",
                            $"Windows reported error 126 for {path}, but {string.Join(", ", Dependencies)} all loaded.\nWhat to do: check the DLL's imports for another missing DLL.");
                    }
                    return (error, $"{missingDependency}, needed by the DirectInput plugin, is missing",
                        missingDependency.Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase)
                            ? "dinput8.dll is DirectInput, part of Windows, and it could not be loaded.\nWhat to do: repair the Windows installation."
                            : $"{missingDependency} is part of the Microsoft Visual C++ runtime.\nWhat to do: install the Microsoft Visual C++ 2015-2022 Redistributable (x64), then restart.");
                case LoadFailure.WrongFormat:
                    return (error, $"{Dll} is not a valid x64 DLL",
                        $"Windows reported error 193 for {path}: the file is for another CPU architecture or is damaged.\nWhat to do: reinstall the package.");
                case LoadFailure.NotLoadedByEngine:
                    return (error, $"{Dll} exists but was not loaded",
                        $"{path} loads when tested directly ({exceptionText}).\nWhat to do: in the Editor, select the DLL and check that it is enabled for the Editor and Standalone Windows x86_64, then restart the Editor.");
                default:
                    return (error, $"{Dll} could not be loaded",
                        $"{(path == null ? "" : $"Windows error {loadError} for {path}: {WinErrors.GetSystemMessage(loadError).Trim()} ")}({exceptionText})");
            }
        }

        /// <summary>Diagnoses an exception from the first call into the plugin, probing the file and its dependencies on Windows.</summary>
        internal static (DirectInputNotificationSeverity severity, string title, string details) ExplainLoadFailure(Exception ex, string[] candidates)
        {
            // Any other exception came from inside a call into a loaded DLL, so a load diagnosis would mislead.
            if (!(ex is DllNotFoundException || ex is BadImageFormatException || ex is EntryPointNotFoundException))
            {
                return (DirectInputNotificationSeverity.Error, "DirectInput could not be started", $"{ex.GetType().Name}: {ex.Message}");
            }
            bool windows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string path = null, missingDependency = null;
            int loadError = 0;
            bool probe = windows && IntPtr.Size == 8 && !(ex is EntryPointNotFoundException);
            foreach (string candidate in probe ? candidates : Array.Empty<string>()) { if (File.Exists(candidate)) { path = candidate; break; } }
            if (path != null)
            {
                loadError = TryLoad(path, 0x8); // LOAD_WITH_ALTERED_SEARCH_PATH: resolve imports next to the DLL, as the engine does
                if (loadError == 126) { missingDependency = Array.Find(Dependencies, d => TryLoad(d, 0) != 0); }
            }
            LoadFailure kind = ClassifyLoadFailure(windows, IntPtr.Size == 8, RuntimeInformation.ProcessArchitecture == Architecture.Arm64,
                ex is EntryPointNotFoundException, path != null, loadError);
            return ExplainLoadFailure(kind, candidates, path, loadError, missingDependency, $"{ex.GetType().Name}: {ex.Message}");
        }

        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string fileName, IntPtr reserved, uint flags);

        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll")]
        static extern uint SetErrorMode(uint mode);

        // 0 when the module loads (it is released again), else the Win32 error.
        static int TryLoad(string module, uint flags)
        {
            uint previous = SetErrorMode(0x0001); // SEM_FAILCRITICALERRORS: no system dialog, as the engine's own loader does
            try
            {
                IntPtr handle = LoadLibraryEx(module, IntPtr.Zero, flags);
                if (handle == IntPtr.Zero) { return Marshal.GetLastWin32Error(); }
                FreeLibrary(handle);
                return 0;
            }
            finally { SetErrorMode(previous); }
        }

        /// <summary>USB VID and PID from DIDEVICEINSTANCE.guidProduct: Data1 is MAKELONG(VID, PID) when Data4 spells "PIDVID".</summary>
        internal static bool TryGetVidPid(string guidProduct, out ushort vid, out ushort pid)
        {
            vid = pid = 0;
            if (guidProduct == null || guidProduct.Length != 38 || !guidProduct.EndsWith("-504944564944}", StringComparison.OrdinalIgnoreCase)) { return false; }
            if (!uint.TryParse(guidProduct.Substring(1, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint data1)) { return false; }
            vid = (ushort)data1;
            pid = (ushort)(data1 >> 16);
            return true;
        }

        /// <summary>Wheel vendors whose devices commonly need vendor software for force feedback; null for others.</summary>
        internal static string VendorName(ushort vid) => vid switch
        {
            0x046D => "Logitech",
            0x044F => "Thrustmaster",
            0x0EB7 => "Fanatec",
            0x346E => "Moza",
            0x3416 => "Cammus",
            0x2433 => "Asetek",
            _ => null,
        };

        // Vendor-specific likely causes. Vendor software requirements are unverified, so they are worded as things to check.
        internal static string VendorHint(ushort vid) => vid switch
        {
            0x046D => "For Logitech wheels: check that G HUB or Logitech Gaming Software is installed and running.",
            0x044F => "For Thrustmaster wheels: check that Thrustmaster's force feedback driver is installed and that the base is set to its PC mode.",
            0x0EB7 => "For Fanatec wheels: check that the Fanatec driver is installed and that the base is in PC mode.",
            _ when VendorName(vid) != null => $"For {VendorName(vid)} bases: check that the vendor's driver or software is installed and that force feedback is not turned off in it.",
            _ => null,
        };

        /// <summary>
        /// A HEURISTIC hint for a device that looks like a wheel (or a joystick from a wheel vendor) but enumerates without
        /// force feedback; null when no hint applies. It names likely causes, never a confirmed one.
        /// </summary>
        internal static string NoForceFeedbackNote(DeviceInfo device)
        {
            if (device.FFBCapable) { return null; }
            uint type = device.deviceType & 0xFF; // DI8DEVTYPE_*
            bool hasVidPid = TryGetVidPid(device.guidProduct, out ushort vid, out ushort pid);
            bool wheelVendor = hasVidPid && VendorName(vid) != null;
            if (type != 0x16 && !((type == 0x14 || type == 0x17) && wheelVendor)) { return null; } // DRIVING; JOYSTICK or FLIGHT from a wheel vendor
            return $"Device: {device.productName} {device.guidInstance}{(hasVidPid ? $", VID {vid:X4} PID {pid:X4}" : "")}\n" +
                "Windows does not report force feedback for this device. If it has force feedback, this usually means the wheel's vendor driver or software " +
                "is not installed or not running, or the wheel is in a console or compatibility mode. " +
                (hasVidPid ? VendorHint(vid) + " " : "") + "These are likely causes, not a confirmed diagnosis.";
        }
    }

    /// <summary>
    /// Only execute an Action after it hasn't been called for a timeout period <br/>
    /// Setup: private static Debouncer DebouncerName = new Debouncer(300); // 300ms<br/>
    /// Invocation: DebouncerName.Debounce(() => { Console.WriteLine("Executed"); });<br/>
    /// Source: https://stackoverflow.com/a/47933557/3055031 (Modifed)
    /// </summary>
    public class Debouncer
    {
        private List<CancellationTokenSource> CancelTokens;
        private readonly int TimeoutMs;
        private readonly object _lockThis;                    // Use a locking object to prevent the debouncer to trigger again while the func is still running

        public Debouncer(int timeoutMs = 300)
        {
            this.TimeoutMs = timeoutMs;
            CancelTokens = new List<CancellationTokenSource>();
            _lockThis = new object();
        }

        public void Debounce(Action TargetAction)
        {
            CancelAllTokens();                                                 // Cancel existing Tokens Each invocation
            var tokenSource = new CancellationTokenSource();                   // Token for this invocation
            lock (_lockThis) { CancelTokens.Add(tokenSource); }                // Safely add this Token to the list
            // Run the action on the caller's synchronization context (Unity main thread, WinForms UI thread).
            // A thread without one (plain console host) must not throw inside a native callback, so fall back to the pool.
            TaskScheduler scheduler = SynchronizationContext.Current != null ? TaskScheduler.FromCurrentSynchronizationContext() : TaskScheduler.Default;
            Task.Delay(TimeoutMs, tokenSource.Token).ContinueWith(task =>
            {    // (Note: All Tasks continue)
                if (!tokenSource.IsCancellationRequested)
                {                      // if this is the task that hasn't been canceled
                    CancelAllTokens();                                             // Clear
                    CancelTokens = new List<CancellationTokenSource>();            // Empty List
                    lock (_lockThis) { TargetAction(); }                           // Excute Action
                }
            }, scheduler);
        }

        private void CancelAllTokens()
        {
            lock (_lockThis)
            {
                foreach (var token in CancelTokens)
                {
                    if (!token.IsCancellationRequested) { token.Cancel(); }
                }
            }
        }
    }

    /// <summary>
    /// Enum of possible FFB Effects<br/>
    /// </summary>
    public enum FFBEffects
    {
        ConstantForce = 0,
        RampForce = 1,
        Square = 2,
        Sine = 3,
        Triangle = 4,
        SawtoothUp = 5,
        SawtoothDown = 6,
        Spring = 7,
        Damper = 8,
        Inertia = 9,
        Friction = 10,
        CustomForce = 11
    }

    public struct CustomForceData
    {
        public int[] ForceData;      // Array of force values
        public uint SamplePeriod;    // Time in microseconds between samples
        public uint Channels;        // Number of channels (typically 1)
    }

    /// <summary>
    /// Types of OnDeviceChange DBTEvents<br/>
    /// More info: https://docs.microsoft.com/en-us/windows/win32/devio/wm-devicechange
    /// </summary>
    public enum DBTEvents
    {
        DBT_DEVNODES_CHANGED = 0x0007,
        DBT_QUERYCHANGECONFIG = 0x0017,
        DBT_CONFIGCHANGED = 0x0018,
        DBT_CONFIGCHANGECANCELED = 0x0019,
        DBT_DEVICEARRIVAL = 0x8000,
        DBT_DEVICEQUERYREMOVE = 0x8001,
        DBT_DEVICEQUERYREMOVEFAILED = 0x8002,
        DBT_DEVICEREMOVEPENDING = 0x8003,
        DBT_DEVICEREMOVECOMPLETE = 0x8004,
        DBT_DEVICETYPESPECIFIC = 0x8005,
        DBT_CUSTOMEVENT = 0x8006,
        DBT_USERDEFINED = 0xFFFF
    }

    /// <summary>
    /// Struct to hold device specific info<br/>
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfo
    {
        public uint deviceType;
        [MarshalAs(UnmanagedType.LPStr)] public string guidInstance;
        [MarshalAs(UnmanagedType.LPStr)] public string guidProduct;
        [MarshalAs(UnmanagedType.LPStr)] public string instanceName;
        [MarshalAs(UnmanagedType.LPStr)] public string productName;
        [MarshalAs(UnmanagedType.I1)] public bool FFBCapable; // native field is a one-byte C++ bool, not a four-byte BOOL
    }

    public delegate void deviceInfoEvent(DeviceInfo device); // delegate for handling events that pass DeviceInfo
    public delegate void deviceStateEvent(DeviceInfo device, FlatJoyState2 state); // delegate for handling events that pass DeviceInfo & FlatJoyState2
    /// <summary>
    /// Like DeviceInfo but allows for events per device<br/>
    /// </summary>
    public class ActiveDeviceInfo
    {
        public DeviceInfo deviceInfo;                     // Hold the info about the device
        public Int32 stateHash;                           // Hash of the last known state (see FlatJoyState2.GetHashCode)
        public FlatJoyState2 lastState;                   // Last state delivered through OnDeviceStateChange
        public bool hasState;                             // False until the first poll has delivered a state
        public event deviceInfoEvent OnDeviceRemoved;     // Event to add listners too
        public event deviceStateEvent OnDeviceStateChange; // Event to add listners too
        public void DeviceRemoved(DeviceInfo device) { OnDeviceRemoved?.Invoke(device); }         // Function to invoke event listeners
        public void DeviceStateChange(DeviceInfo device, FlatJoyState2 state) { OnDeviceStateChange?.Invoke(device, state); } // Function to invoke event listeners
    }

    /// <summary>
    /// DirectInput DIConditon Struct <br/>
    /// Offset: -10,000 - 10,000 <br/>
    /// positiveCoefficient: -10,000 - 10,000 <br/>
    /// negativeCoefficient: -10,000 - 10,000 <br/>
    /// positiveSaturation: 0 - 10,000 <br/>
    /// negativeSaturation: 0 - 10,000 <br/>
    /// deadband: 0 - 10,000 <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416601(v=vs.85)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DICondition
    {
        /// <summary>
        /// Offset for the condition, in the range from - 10,000 through 10,000.
        /// </summary>
        public int offset;
        /// <summary>
        /// Coefficient constant on the positive side of the offset, in the range
        /// from - 10,000 through 10,000.
        /// </summary>
        public int positiveCoefficient;
        /// <summary>
        /// Coefficient constant on the negative side of the offset, in the range
        /// from - 10,000 through 10,000. If the device does not support separate
        /// positive and negative coefficients, the value of lNegativeCoefficient
        /// is ignored, and the value of lPositiveCoefficient is used as both the
        /// positive and negative coefficients.
        /// </summary>
        public int negativeCoefficient;
        /// <summary>
        /// Maximum force output on the positive side of the offset, in the range
        /// from 0 through 10,000.
        ///
        /// If the device does not support force saturation, the value of this
        /// member is ignored.
        /// </summary>
        public uint positiveSaturation;
        /// <summary>
        /// Maximum force output on the negative side of the offset, in the range
        /// from 0 through 10,000.
        ///
        /// If the device does not support force saturation, the value of this member
        /// is ignored.
        ///
        /// If the device does not support separate positive and negative saturation,
        /// the value of dwNegativeSaturation is ignored, and the value of dwPositiveSaturation
        /// is used as both the positive and negative saturation.
        /// </summary>
        public uint negativeSaturation;
        /// <summary>
        /// Range about the center of the axis that is ignored by the effect.
        /// This value is in the range from 0 through 10,000.
        /// </summary>
        public int deadband;  // Changed from uint to int to match native LONG type
    }

    /// <summary>
    /// Describes the state of a joystick device with extended capabilities. <br/>
    /// See https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416628(v=vs.85)
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct DIJOYSTATE2
    {
        public int lX;
        public int lY;
        public int lZ;
        public int lRx;
        public int lRy;
        public int lRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglSlider;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public int[] rgdwPOV;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] rgbButtons;
        public int lVX;
        public int lVY;
        public int lVZ;
        public int lVRx;
        public int lVRy;
        public int lVRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglVSlider;
        public int lAX;
        public int lAY;
        public int lAZ;
        public int lARx;
        public int lARy;
        public int lARz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglASlider;
        public int lFX;
        public int lFY;
        public int lFZ;
        public int lFRx;
        public int lFRy;
        public int lFRz;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] rglFSlider;
    }

    /// <summary>
    /// A flattend version of DIJOYSTATE2 without nested arrays<br/>
    /// See https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416628(v=vs.85)
    /// </summary>
    [Serializable]
    public struct FlatJoyState2 : IEquatable<FlatJoyState2>
    { // Axis trimmed from "LONG" UInt32 to UInt16 as values range 0-65535?
        public UInt64 buttonsA; // Buttons seperated into banks of 64-Bits to fit into Unsigned 64-bit integer
        public UInt64 buttonsB; // Buttons seperated into banks of 64-Bits to fit into Unsigned 64-bit integer
        public UInt16 lX;       // X-axis
        public UInt16 lY;       // Y-axis
        public UInt16 lZ;       // Z-axis
        public UInt16 lU;       // U-axis
        public UInt16 lV;       // V-axis
        public UInt16 lRx;      // X-axis rotation
        public UInt16 lRy;      // Y-axis rotation
        public UInt16 lRz;      // Z-axis rotation
        public UInt16 lVX;      // X-axis velocity
        public UInt16 lVY;      // Y-axis velocity
        public UInt16 lVZ;      // Z-axis velocity
        public UInt16 lVU;      // U-axis velocity
        public UInt16 lVV;      // V-axis velocity
        public UInt16 lVRx;     // X-axis angular velocity
        public UInt16 lVRy;     // Y-axis angular velocity
        public UInt16 lVRz;     // Z-axis angular velocity
        public UInt16 lAX;      // X-axis acceleration
        public UInt16 lAY;      // Y-axis acceleration
        public UInt16 lAZ;      // Z-axis acceleration
        public UInt16 lAU;      // U-axis acceleration
        public UInt16 lAV;      // V-axis acceleration
        public UInt16 lARx;     // X-axis angular acceleration
        public UInt16 lARy;     // Y-axis angular acceleration
        public UInt16 lARz;     // Z-axis angular acceleration
        public UInt16 lFX;      // X-axis force
        public UInt16 lFY;      // Y-axis force
        public UInt16 lFZ;      // Z-axis force
        public UInt16 lFU;      // U-axis force
        public UInt16 lFV;      // V-axis force
        public UInt16 lFRx;     // X-axis torque
        public UInt16 lFRy;     // Y-axis torque
        public UInt16 lFRz;     // Z-axis torque
        public UInt16 rgdwPOV;  // Store each DPAD in chunks of 4 bits inside 16-bit UInt

        /// <summary>Exact comparison of every field. This is what DIManager.Poll uses to detect a change.</summary>
        public readonly bool Equals(FlatJoyState2 o) =>
            buttonsA == o.buttonsA && buttonsB == o.buttonsB &&
            lX == o.lX && lY == o.lY && lZ == o.lZ && lU == o.lU && lV == o.lV &&
            lRx == o.lRx && lRy == o.lRy && lRz == o.lRz &&
            lVX == o.lVX && lVY == o.lVY && lVZ == o.lVZ && lVU == o.lVU && lVV == o.lVV &&
            lVRx == o.lVRx && lVRy == o.lVRy && lVRz == o.lVRz &&
            lAX == o.lAX && lAY == o.lAY && lAZ == o.lAZ && lAU == o.lAU && lAV == o.lAV &&
            lARx == o.lARx && lARy == o.lARy && lARz == o.lARz &&
            lFX == o.lFX && lFY == o.lFY && lFZ == o.lFZ && lFU == o.lFU && lFV == o.lFV &&
            lFRx == o.lFRx && lFRy == o.lFRy && lFRz == o.lFRz &&
            rgdwPOV == o.rgdwPOV;

        public override readonly bool Equals(object obj) => obj is FlatJoyState2 o && Equals(o);

        /// <summary>
        /// Cheap hash over buttons, the eight primary axes and the POVs. Not a full-state digest:
        /// use Equals for exact comparison, or DIManager.FlatStateMD5 for a digest.
        /// </summary>
        public override readonly int GetHashCode()
        {
            unchecked
            {
                ulong h = buttonsA * 0x9E3779B97F4A7C15UL ^ buttonsB;
                h = h * 31 + ((ulong)lX | (ulong)lY << 16 | (ulong)lZ << 32 | (ulong)lRz << 48);
                h = h * 31 + ((ulong)lU | (ulong)lV << 16 | (ulong)lRx << 32 | (ulong)lRy << 48);
                h = h * 31 + rgdwPOV;
                return (int)h ^ (int)(h >> 32);
            }
        }
    }

    /// <summary>
    /// Describes a DirectInput device's capabilities. <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)
    /// </summary>
    [Serializable]
    public struct DIDEVCAPS
    {
        public UInt32 dwSize;                // Size of this structure, in bytes.
        public DwFlags dwFlags;               // Flags associated with the device.
        public UInt32 dwDevType;             // Device type specifier. The least-significant byte of the device type description code specifies the device type. The next-significant byte specifies the device subtype. This value can also be combined with DIDEVTYPE_HID, which specifies a Human Interface Device (human interface device).
        public UInt32 dwAxes;                // Number of axes available on the device.
        public UInt32 dwButtons;             // Number of buttons available on the device.
        public UInt32 dwPOVs;                // Number of point-of-view controllers available on the device.
        public UInt32 dwFFSamplePeriod;      // Minimum time between playback of consecutive raw force commands, in microseconds.
        public UInt32 dwFFMinTimeResolution; // Minimum time, in microseconds, that the device can resolve. The device rounds any times to the nearest supported increment. For example, if the value of dwFFMinTimeResolution is 1000, the device would round any times to the nearest millisecond.
        public UInt32 dwFirmwareRevision;    // Firmware revision of the device.
        public UInt32 dwHardwareRevision;    // Hardware revision of the device.
        public UInt32 dwFFDriverVersion;     // Version number of the device driver.
    }

    /// <summary>
    /// Describes the Flags associated with a DirectInput device's capabilities. <br/>
    /// More info: https://docs.microsoft.com/en-us/previous-versions/windows/desktop/ee416607(v=vs.85)#:~:text=IDirectInputDevice8%3A%3AGetCapabilities%20method.-,dwFlags,-Flags%20associated%20with
    /// </summary>
    [Flags]
    public enum DwFlags
    {
        DIDC_ALIAS = 0x00010000, // The device is a duplicate of another DirectInput device.
        DIDC_ATTACHED = 0x00000001, // The device is physically attached to the user's computer.
        DIDC_DEADBAND = 0x00004000, // The device supports deadband for at least one force-feedback condition.
        DIDC_EMULATED = 0x00000004, // If this flag is set, the data is coming from a user mode device interface, such as a Human Interface Device (human interface device), or by some other ring 3 means. If it is not set, the data is coming directly from a kernel mode driver.
        DIDC_FORCEFEEDBACK = 0x00000100, // The device supports force feedback.
        DIDC_FFFADE = 0x00000400, // The force-feedback system supports the fade parameter for at least one effect.
        DIDC_FFATTACK = 0x00000200, // The force-feedback system supports the attack parameter for at least one effect.
        DIDC_HIDDEN = 0x00040000, // Fictitious device created by a device driver so that it can generate keyboard and mouse events.
        DIDC_PHANTOM = 0x00020000, // Placeholder. Phantom devices are by default not enumerated.
        DIDC_POLLEDDATAFORMAT = 0x00000008, // At least one object in the current data format is polled, rather than interrupt-driven.
        DIDC_POLLEDDEVICE = 0x00000002, // At least one object on the device is polled, rather than interrupt-driven. HID devices can contain a mixture of polled and nonpolled objects.
        DIDC_POSNEGCOEFFICIENTS = 0x00001000, // The force-feedback system supports two coefficient values for conditions (one for the positive displacement of the axis and one for the negative displacement of the axis) for at least one condition. If the device does not support both coefficients, the negative coefficient in the DICONDITION structure is ignored.
        DIDC_POSNEGSATURATION = 0x00002000, // The force-feedback system supports a maximum saturation for both positive and negative force output for at least one condition. If the device does not support both saturation values, the negative saturation in the DICONDITION structure is ignored.
        DIDC_SATURATION = 0x00000800, // The force-feedback system supports the saturation of condition effects for at least one condition. If the device does not support saturation, the force generated by a condition is limited only by the maximum force that the device can generate.
        DIDC_STARTDELAY = 0x00008000, // The force-feedback system supports the start delay parameter for at least one effect. If the device does not support start delays, the dwStartDelay member of the DIEFFECT structure is ignored.
    }
}
