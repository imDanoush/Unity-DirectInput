using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif
#if UNITY_STANDALONE_WIN
using UnityEngine;
using UnityEngine;
#endif

namespace DirectInputManager
{
    class Native
    {
#if UNITY_64 // meaning if it is a Unity build, to avoid Unity in wrong build modes (e.g. unsupported platforms like Android) throwing errors.
             // Note that this a Windows 64 bit only asset! Though any game built for Windows 64bit may be playable via emulators on Linux.
        const string DLLFile = @"DirectInputForceFeedback.dll";
#else
        const string DLLFile = @"..\..\..\..\..\..\Runtime\Plugins\DLL\DirectInputForceFeedback.dll";
#endif
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
        internal static extern int UpdateRampForce([MarshalAs(UnmanagedType.LPStr)] string guidInstance, int startMagnitude, int endMagnitude);

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
        internal static extern int DEBUG1([MarshalAs(UnmanagedType.LPStr)] string guidInstance,
            [MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_BSTR)] out string[] DEBUGDATA);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning restore IDE0079 // Remove unnecessary suppression

        [DllImport(DLLFile)]
        internal static extern int GetActiveDevices(out int count, out IntPtr strings);

        [DllImport(DLLFile)]
        internal static extern void FreeStringArray(IntPtr strings, int count);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        [DllImport(DLLFile)]
        internal static extern void SetDeviceChangeCallback([MarshalAs(UnmanagedType.FunctionPtr)] DeviceChangeCallback onDeviceChange);


    }
#if UNITY_STANDALONE_WIN
    [DefaultExecutionOrder(-1000)]
#endif
    public class DIManager
    {
        // Define all callback delegates at class level with proper attributes
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void DeviceChangeCallback(DBTEvents DBTEvent);

        // Static delegate instances to prevent garbage collection
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0052 // Remove unread private members
        private static DeviceChangeCallback s_deviceChangeCallback;
#pragma warning restore IDE0052 // Remove unread private members
#pragma warning restore IDE0079 // Remove unnecessary suppression


        //////////////////////////////////////////////////////////////
        // Cross Platform "Macros" - Allows lib to work in Visual Studio & Unity
        //////////////////////////////////////////////////////////////
#if UNITY_STANDALONE_WIN
        private static uint ClampAgnostic(uint value, uint min, uint max) => (uint)Mathf.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Mathf.Clamp(value, min, max);

        internal static bool showLogsRuntime;
        private static void DebugLog(string message)
        {
            // Send to console too
            UnityEngine.Debug.Log($"<color=#9416f9>[DirectInputManager]</color> <color=#ff0000>{message}</color>");
            if (Application.isPlaying && showLogsRuntime)
            {
                DirectInputLogger.AddLog(message);
            }

        }
#else
        private static uint ClampAgnostic(uint value, uint min, uint max) => Math.Clamp(value, min, max);
        private static int ClampAgnostic(int value, int min, int max) => Math.Clamp(value, min, max);
        private static void DebugLog(string message) => System.Diagnostics.Debug.WriteLine($"[DirectInputManager]{message}");
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
            try
            {
#if !UNITY_STANDALONE_WIN
                Native.InitializeForStandalone();
#endif
                if (Native.StartDirectInput() != 0) { return _isInitialized = false; }
                s_deviceChangeCallback = OnDeviceChange;
                Native.SetDeviceChangeCallback(OnDeviceChange);
                return _isInitialized = true;
            }
            catch (Exception ex)
            {
                DebugLog($"Failed to initialize DirectInput: {ex.Message}");
                return _isInitialized = false;
            }
        }

        /// <summary>
        /// Fetch currently available devices and populate DIManager.devices<br/>
        /// </summary>
        public static void EnumerateDevices()
        {
            IntPtr ptrDevices = Native.EnumerateDevices(out int deviceCount); // Returns pointer to list of devices and how many are available

            if (deviceCount > 0)
            {
                _devices = new DeviceInfo[deviceCount];

                int deviceSize = Marshal.SizeOf(typeof(DeviceInfo)); // Size of each Device entry
                for (int i = 0; i < deviceCount; i++)
                {
                    IntPtr pCurrent = ptrDevices + i * deviceSize; // Ptr to the current device
                    _devices[i] = Marshal.PtrToStructure<DeviceInfo>(pCurrent); // Transform the Ptr into a C# instance of DeviceInfo
                }
            }
            else
            {
                _devices = Array.Empty<DeviceInfo>(); // empty _devices when no devices are present
            }
            return;
        }

        public static async Task EnumerateDevicesAsync()
        {
            Task enumDevicesTask = Task.Run(EnumerateDevices);
            Task warningTimeout = Task.Delay(1000);

            if (warningTimeout == await Task.WhenAny(enumDevicesTask, warningTimeout))
            {
                DebugLog($"Warning EnumerateDevices is taking longer than expected!");
                await enumDevicesTask; // Continue to wait for EnumerateDevices
            }
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
            int hresult = Native.CreateDevice(guidInstance);
            if (hresult != 0) { DebugLog($"CreateDevice Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)} {guidInstance}"); return false; }
            DeviceInfo device = _devices.Where(device => device.guidInstance == guidInstance).First();
            _activeDevices.Add(guidInstance, new ActiveDeviceInfo() { deviceInfo = device }); // Add device to our C# active device tracker (Dictionary allows us to easily check if GUID already exists)
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
            if (hresult != 0) { DebugLog($"DestroyDevice Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); return false; }
            return true;
        }

        /// <summary>
        /// Retrieve state of the Device, Flattened for easier comparison.<br/>
        /// </summary>
        /// <returns>
        /// FlatJoyState2
        /// </returns>
        public static FlatJoyState2 GetDeviceState(string guidInstance)
        {
            int hresult = Native.GetDeviceState(guidInstance, out FlatJoyState2 DeviceState);
            if (hresult != 0) { DebugLog($"GetDeviceState Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
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
            int hresult = Native.GetDeviceStateRaw(guidInstance, out DIJOYSTATE2 DeviceState);
            if (hresult != 0) { DebugLog($"GetDeviceStateRaw Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
            return DeviceState;
        }

        public static string[] GetActiveDevices()
        {
            int hr = Native.GetActiveDevices(out int count, out IntPtr stringsPtr);

            if (FAILED(hr) || count == 0 || stringsPtr == IntPtr.Zero)
            {
                DebugLog($"GetActiveDevices Failed: 0x{hr:x} {WinErrors.GetSystemMessage(hr)}");
                return Array.Empty<string>();
            }

            try
            {
                string[] result = ReadStringArray(stringsPtr, count);
                if (result.Length != _activeDevices.Count)
                {
                    DebugLog($"Active Device mismatch! DLL:{result.Length}, DIManager:{_activeDevices.Count}");
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

            if (FAILED(hr) || count == 0 || stringsPtr == IntPtr.Zero)
            {
                DebugLog($"GetDeviceFFBCapabilities Failed: 0x{hr:x} {WinErrors.GetSystemMessage(hr)}");
                return Array.Empty<string>();
            }

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

        // Helper method to check HRESULT
        private static bool FAILED(int hr)
        {
            return hr < 0;
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
            int hresult = Native.GetDeviceCapabilities(guidInstance, out DIDEVCAPS DeviceCapabilities);
            if (hresult != 0) { DebugLog($"GetDeviceCapabilities Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); /*return false;*/ }
            return DeviceCapabilities;
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
            int hresult = Native.CreateFFBEffect(guidInstance, effectType);
            if (hresult != 0) { DebugLog($"CreateFFBEffect Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); return false; }
            return true;
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
            int hresult = Native.DestroyFFBEffect(guidInstance, effectType);
            if (hresult != 0) { DebugLog($"DestroyFFBEffect Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); return false; }
            return true;
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
            int hresult = Native.StopAllFFBEffects(guidInstance);
            if (hresult != 0) { DebugLog($"StopAllFFBEffects Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); return false; }
            return true;
        }

        /// <summary>
        /// Stops DirectInput<br/>
        /// </summary>
        /// <returns>
        /// True if DirectInput was stopped successfully
        /// </returns>
        public static bool StopDirectInput()
        {
            int hresult = Native.StopDirectInput();
            if (hresult != 0) { DebugLog($"StopDirectInput Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}"); return false; }
            return true;
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
                Int32 oldHash = ADI.stateHash;
                var state = GetDeviceState(guidInstance);
                ADI.stateHash = state.GetHashCode();

                if (oldHash != ADI.stateHash)
                {
                    ADI.DeviceStateChange(ADI.deviceInfo, state); // Invoke all event listeners for this device
                                                                  //DebugLog($"{ADI.deviceInfo.productName} State Changed!");
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
#if UNITY_STANDALONE_WIN
        [AOT.MonoPInvokeCallback(typeof(DeviceChangeCallback))]
#endif
        private static void OnDeviceChange(DBTEvents DBTEvent)
        {
            //DebugLog($"DeviceChange {DBTEvent.ToString()}");

            ODCDebouncer.Debounce(() => { ScanDevicesForChanges(); });
        }

        private static async void ScanDevicesForChanges()
        {
            DeviceInfo[] oldDevices = _devices;                              // Store currently known devices
            await EnumerateDevicesAsync();                                   // Fetch what devices are available now

            var removedDevices = oldDevices.Except(_devices);
            var addedDevices = _devices.Except(oldDevices);

            foreach (DeviceInfo device in removedDevices)
            {                  // Process removed devices
                if (_activeDevices.TryGetValue(device.guidInstance, out ActiveDeviceInfo ADI))
                {
                    ADI.DeviceRemoved(device);                                   // Invoke event listeners for this device
                }
                DeviceRemoved(device);                                         // Invoke all event listeners all devices
                Destroy(device);                                               // If device was connceted remove it gracefully
                                                                               // DebugLog($"{device.productName} Removed!");
            }

            foreach (DeviceInfo device in addedDevices)
            {                    // Process newly added devices
                DeviceAdded(device);                                           // Invoke event to broadcast a new device is available
            }
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
        /// positiveSaturation: Positive of center saturation [0 - 10,000]<br/>
        /// </summary>
        /// <returns>
        /// A boolean representing the if the Effect updated successfully
        /// </returns>
        public static bool UpdateEffect(string guidInstance, FFBEffects effectType, DICondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
            {
                DebugLog("UpdateEffect: Invalid conditions array");
                return false;
            }

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
                        (uint)conditions[0].positiveSaturation,
                        conditions[0].offset);
                    break;

                case FFBEffects.CustomForce:
                    // For custom force, we extract the data from conditions array
                    int[] forceData = new int[conditions.Length];
                    for (int i = 0; i < conditions.Length; i++)
                    {
                        forceData[i] = conditions[i].positiveCoefficient;
                    }
                    uint samplePeriod = (uint)conditions[0].negativeCoefficient;
                    success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
                    break;

                default:
                    DebugLog($"UpdateEffect: Unsupported effect type {effectType}");
                    return false;
            }

            if (!success)
            {
                DebugLog($"UpdateEffect: Failed to update {effectType}");
            }

            return success;
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
            return UpdateConditionForceNative(guidInstance, FFBEffects.Damper,
                0, magnitude, magnitude, 0, 0, 0);
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
                0, magnitude, magnitude, 0, 0, 0);
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
                0, magnitude, magnitude, 0, 0, 0);
        }

        public static bool UpdatePeriodicSimple(string guidInstance, FFBEffects effectType, int magnitude, uint period = 30000, int rampStart = 0, int rampEnd = 0)
        {
            // For RampForce effect, use UpdateRampForceNative
            if (effectType == FFBEffects.RampForce)
            {
                bool success = UpdateRampForceNative(guidInstance, rampStart, rampEnd);
                if (!success)
                {
                    // If update failed, try creating the effect first
                    int hresult = Native.CreateFFBEffect(guidInstance, effectType);
                    if (hresult != 0)
                    {
                        DebugLog($"CreateFFBEffect Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}");
                        return false;
                    }

                    // Try updating again
                    success = UpdateRampForceNative(guidInstance, rampStart, rampEnd);
                }
                return success;
            }
            // For other periodic effects, use UpdatePeriodicForceNative
            else
            {
                bool success = UpdatePeriodicForceNative(guidInstance, effectType, magnitude, period, 0);
                if (!success)
                {
                    // If update failed, try creating the effect first
                    int hresult = Native.CreateFFBEffect(guidInstance, effectType);
                    if (hresult != 0)
                    {
                        DebugLog($"CreateFFBEffect Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}");
                        return false;
                    }

                    // Try updating again
                    success = UpdatePeriodicForceNative(guidInstance, effectType, magnitude, period, 0);
                }
                return success;
            }
        }

        public static bool UpdateCustomForceSimple(string guidInstance, int[] forceData, uint samplePeriod, int offset = 0, uint deadband = 0)
        {
            if (forceData == null)
            {
                DebugLog("UpdateCustomForceSimple: Invalid input parameters");
                return false;
            }

            bool success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
            if (!success)
            {
                // If update failed, try creating the effect first
                int hresult = Native.CreateFFBEffect(guidInstance, FFBEffects.CustomForce);
                if (hresult != 0)
                {
                    DebugLog($"CreateFFBEffect Failed: 0x{hresult:x} {WinErrors.GetSystemMessage(hresult)}");
                    return false;
                }

                // Try updating again
                success = UpdateCustomForceNative(guidInstance, forceData, samplePeriod);
            }
            return success;
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
            int result = Native.UpdateConstantForce(guidInstance, magnitude);
            return result == 0;
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
            int positiveMagnitude = Math.Abs(magnitude);
            if (positiveMagnitude > 10000) positiveMagnitude = 10000;

            int result = Native.UpdatePeriodicForce(guidInstance, effectType, positiveMagnitude, period, offset);
            return result == 0;
        }

        /// <summary>
        /// Updates a ramp force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="startMagnitude">Starting force [-10000, 10000]</param>
        /// <param name="endMagnitude">Ending force [-10000, 10000]</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateRampForceNative(string guidInstance, int startMagnitude, int endMagnitude)
        {
            int result = Native.UpdateRampForce(guidInstance, startMagnitude, endMagnitude);
            return result == 0;
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
            int result = Native.UpdateConditionForce(guidInstance, effectType,
                offset, positiveCoefficient, negativeCoefficient,
                positiveSaturation, negativeSaturation, deadBand);
            return result == 0;
        }

        /// <summary>
        /// Updates a custom force effect.
        /// </summary>
        /// <param name="guidInstance">Device identifier</param>
        /// <param name="forceData">Array of force values [-10000, 10000]</param>
        /// <param name="samplePeriod">Sample period in microseconds</param>
        /// <returns>True if the update was successful</returns>
        public static bool UpdateCustomForceNative(string guidInstance, int[] forceData, uint samplePeriod)
        {
            int result = Native.UpdateCustomForce(guidInstance, forceData, forceData.Length, samplePeriod);
            return result == 0;
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

        [DllImport("kernel32.dll", SetLastError = true)]
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

                string sRet = PtrToStringUTF8(lpMsgBuf);

                // Free the buffer.
                lpMsgBuf = LocalFree(lpMsgBuf);
                return sRet;
            }
            catch (Exception e)
            {
                return "Unable to get error code string from System -> " + e.ToString();
            }
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
    /// Only execute an Action after it hasn't been called for a timeout period <br/>
    /// Setup: private static Debouncer DebouncerName = new Debouncer(300); // 300ms<br/>
    /// Invocation: DebouncerName.Debounce(() => { Console.WriteLine("Executed"); });<br/>
    /// Source: https://stackoverflow.com/a/47933557/3055031 (Modifed)
    /// </summary>
#if UNITY_STANDALONE_WIN
    [DefaultExecutionOrder(-750)]
#endif
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
            Task.Delay(TimeoutMs, tokenSource.Token).ContinueWith(task =>
            {    // (Note: All Tasks continue)
                if (!tokenSource.IsCancellationRequested)
                {                      // if this is the task that hasn't been canceled
                    CancelAllTokens();                                             // Clear
                    CancelTokens = new List<CancellationTokenSource>();            // Empty List
                    lock (_lockThis) { TargetAction(); }                           // Excute Action
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());             // Perform on current thread
        }

        private void CancelAllTokens()
        {
            foreach (var token in CancelTokens)
            {
                if (!token.IsCancellationRequested) { token.Cancel(); }
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
        public bool FFBCapable;
    }

    public delegate void deviceInfoEvent(DeviceInfo device); // delegate for handling events that pass DeviceInfo
    public delegate void deviceStateEvent(DeviceInfo device, FlatJoyState2 state); // delegate for handling events that pass DeviceInfo & FlatJoyState2
    /// <summary>
    /// Like DeviceInfo but allows for events per device<br/>
    /// </summary>
    public class ActiveDeviceInfo
    {
        public DeviceInfo deviceInfo;                     // Hold the info about the device
        public Int32 stateHash;                           // Hold the hash of the last known state
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
    public struct FlatJoyState2
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
        public override readonly int GetHashCode()
        {
            return BitConverter.ToInt32(DIManager.FlatStateMD5(this), 0);
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
