#pragma once

#ifdef DIRECTINPUTFORCEFEEDBACK_EXPORTS
#define DIRECTINPUTFORCEFEEDBACK_API __declspec(dllexport)
#else
#define DIRECTINPUTFORCEFEEDBACK_API __declspec(dllimport)
#endif

// Define Unity plugin interface macros if not already defined
#ifndef UNITY_INTERFACE_API
#if defined(_MSC_VER)
#define UNITY_INTERFACE_API __stdcall
#else
#define UNITY_INTERFACE_API
#endif
#endif

#ifndef UNITY_INTERFACE_EXPORT
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#endif

// Custom error codes (FACILITY_ITF, outside the 0x800402xx block DirectInput uses). The previous values
// 0x8007000D / 0x8007000E were the Win32 HRESULTs for ERROR_INVALID_DATA and E_OUTOFMEMORY (= DIERR_OUTOFMEMORY).
#define DIERR_NOWINDOWHANDLE      0x8004F300L  // The DLL could not create its hidden window
#define DIERR_UNITYNOTINITIALIZED 0x8004F301L  // UnityPluginLoad / InitializeForStandalone has not run

// Define constants for string buffer sizes
#define MAX_GUID_STRING_LENGTH 39  // {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX} + null
#define MAX_DEVICE_NAME_LENGTH 260 // MAX_PATH equivalent for device names

extern "C" { // Everything to be made available by the DLL

	// Unity plugin lifecycle functions
	DIRECTINPUTFORCEFEEDBACK_API void UNITY_INTERFACE_API UnityPluginLoad(void* unityInterfaces);
	DIRECTINPUTFORCEFEEDBACK_API void UNITY_INTERFACE_API UnityPluginUnload();
	DIRECTINPUTFORCEFEEDBACK_API int UNITY_INTERFACE_API GetPluginVersion();

	// Add safer memory management for DeviceInfo
	struct DeviceInfo {
		DWORD deviceType;
		LPSTR guidInstance;    // Caller must not free this memory - owned by the DLL
		LPSTR guidProduct;     // Caller must not free this memory - owned by the DLL
		LPSTR instanceName;    // Caller must not free this memory - owned by the DLL
		LPSTR productName;     // Caller must not free this memory - owned by the DLL
		bool FFBCapable;
	};

	struct FlatJoyState2 {
		uint64_t buttonsA; // Buttons seperated into banks of 64-Bits to fit into 64-bit integer
		uint64_t buttonsB; // Buttons seperated into banks of 64-Bits to fit into 64-bit integer
		uint16_t lX;       // X-axis
		uint16_t lY;       // Y-axis
		uint16_t lZ;       // Z-axis
		uint16_t lU;       // U-axis
		uint16_t lV;       // V-axis
		uint16_t lRx;      // X-axis rotation
		uint16_t lRy;      // Y-axis rotation
		uint16_t lRz;      // Z-axis rotation
		uint16_t lVX;      // X-axis velocity
		uint16_t lVY;      // Y-axis velocity
		uint16_t lVZ;      // Z-axis velocity
		uint16_t lVU;      // U-axis velocity
		uint16_t lVV;      // V-axis velocity
		uint16_t lVRx;     // X-axis angular velocity
		uint16_t lVRy;     // Y-axis angular velocity
		uint16_t lVRz;     // Z-axis angular velocity
		uint16_t lAX;      // X-axis acceleration
		uint16_t lAY;      // Y-axis acceleration
		uint16_t lAZ;      // Z-axis acceleration
		uint16_t lAU;      // U-axis acceleration
		uint16_t lAV;      // V-axis acceleration
		uint16_t lARx;     // X-axis angular acceleration
		uint16_t lARy;     // Y-axis angular acceleration
		uint16_t lARz;     // Z-axis angular acceleration
		uint16_t lFX;      // X-axis force
		uint16_t lFY;      // Y-axis force
		uint16_t lFZ;      // Z-axis force
		uint16_t lFU;      // U-axis force
		uint16_t lFV;      // V-axis force
		uint16_t lFRx;     // X-axis torque
		uint16_t lFRy;     // Y-axis torque
		uint16_t lFRz;     // Z-axis torque
		uint16_t rgdwPOV;  // Store each DPAD in chunks of 4 bits inside 16-bit short     
	};

	struct DIDEVCAPS; // Transfer device capabilities across the interop boundary

	struct Effects {
		enum class Type {
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
		};
	};

	//////////////////////////////////////////////////////////////
	// DLL Functions
	//////////////////////////////////////////////////////////////
	DIRECTINPUTFORCEFEEDBACK_API HRESULT StartDirectInput();
	DIRECTINPUTFORCEFEEDBACK_API HRESULT StopDirectInput();
	// deviceCount is -1 when the enumeration failed (the managed side then keeps its previous list), 0 when no device is attached.
	DIRECTINPUTFORCEFEEDBACK_API DeviceInfo* EnumerateDevices(/*[out]*/ int& deviceCount);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT CreateDevice(LPCSTR guidInstance);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT DestroyDevice(LPCSTR guidInstance);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetDeviceState(LPCSTR guidInstance, /*[out]*/ FlatJoyState2& deviceState);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetDeviceStateRaw(LPCSTR guidInstance, /*[out]*/ DIJOYSTATE2& deviceState);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetDeviceCapabilities(LPCSTR guidInstance, /*[out]*/ DIDEVCAPS& deviceCapabilitiesOut);

	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetActiveDevices(int* count, const char*** outStrings);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT EnumerateFFBEffects(LPCSTR guidInstance, int* count, const char*** outStrings);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT EnumerateFFBAxes(LPCSTR guidInstance, int* count, const char*** outStrings);

	DIRECTINPUTFORCEFEEDBACK_API void FreeStringArray(const char** strings, int count);

	// Also fails with the first failed effect restart after reacquiring (it used to report S_OK for that).
	DIRECTINPUTFORCEFEEDBACK_API HRESULT SetAutocenter(LPCSTR guidInstance, bool AutocenterState);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT CreateFFBEffect(LPCSTR guidInstance, Effects::Type effectType);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT DestroyFFBEffect(LPCSTR guidInstance, Effects::Type effectType);
	// *status receives the IDirectInputEffect::GetEffectStatus flags (DIEGES_PLAYING = 1). E_ABORT when the effect was never created.
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetFFBEffectStatus(LPCSTR guidInstance, Effects::Type effectType, /*[out]*/ DWORD* status);
	// *mask bit n is set when the device enumerates the standard effect with Effects::Type value n (vendor effects are ignored).
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetSupportedFFBEffects(LPCSTR guidInstance, /*[out]*/ DWORD* mask);

	// Update* return the SetParameters HRESULT, success codes included (they used to collapse to S_OK): DI_DOWNLOADSKIPPED (3)
	// = stored but not sent, the device stayed without exclusive acquisition after one reacquire; DI_EFFECTRESTARTED (4);
	// DI_TRUNCATED (8) = clipped by the device; DI_TRUNCATEDANDRESTARTED (12). Test with SUCCEEDED / hr >= 0, never hr == S_OK. A lost device is
	// reacquired and the call retried once; a failed effect restart during that reacquire is returned as the error.
	DIRECTINPUTFORCEFEEDBACK_API HRESULT UpdateConstantForce(LPCSTR guidInstance, int magnitude);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT UpdatePeriodicForce(LPCSTR guidInstance, Effects::Type effectType, int magnitude, DWORD period, int offset);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT UpdateRampForce(LPCSTR guidInstance, int startMagnitude, int endMagnitude, DWORD durationMicroseconds);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT UpdateConditionForce(LPCSTR guidInstance, Effects::Type effectType,
		int offset, int positiveCoefficient, int negativeCoefficient,
		DWORD positiveSaturation, DWORD negativeSaturation, int deadBand);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT UpdateCustomForce(LPCSTR guidInstance, int* forceData, int sampleCount, DWORD samplePeriod);

	DIRECTINPUTFORCEFEEDBACK_API HRESULT StopAllFFBEffects(LPCSTR guidInstance);
	DIRECTINPUTFORCEFEEDBACK_API HRESULT DEBUG1(LPCSTR guidInstance, /*[out]*/ SAFEARRAY** DebugData);

	// Plugin state and diagnostics
	DIRECTINPUTFORCEFEEDBACK_API bool IsUnityInitialized();
	DIRECTINPUTFORCEFEEDBACK_API bool IsDirectInputInitialized();
	DIRECTINPUTFORCEFEEDBACK_API HRESULT GetDILastError(char* buffer, int bufferSize);

	enum DBTEvents {
		DBTDEVNODESCHANGED = 0x0007,
		DBTDEVICEARRIVAL = 0x8000,
		DBTDEVICEREMOVECOMPLETE = 0x8004
	};
	typedef void(__stdcall* DeviceChangeCallback)(DBTEvents state);

	extern DeviceChangeCallback g_deviceCallback;

	DIRECTINPUTFORCEFEEDBACK_API void SetDeviceChangeCallback(DeviceChangeCallback CB);

	// Add helper for safer string handling
	char* SafeStrDup(const char* source);
	void SafeStrFree(char* str);
	bool IsValidGUIDString(LPCSTR guidString);
}

//////////////////////////////////////////////////////////////
// Internal Callbacks
//////////////////////////////////////////////////////////////
BOOL CALLBACK _EnumDevicesCallback(const DIDEVICEINSTANCE* pInst, void* pContext);
BOOL CALLBACK _EnumDevicesCallbackFFB(const DIDEVICEINSTANCE* DIDI, void* pContext);
BOOL CALLBACK _EnumFFBEffectsCallback(LPCDIEFFECTINFO pdei, LPVOID pvRef);
BOOL CALLBACK _EnumFFBAxisCallback(const DIDEVICEOBJECTINSTANCE* pdidoi, LPVOID pvRef);

//////////////////////////////////////////////////////////////
// Helper Functions
//////////////////////////////////////////////////////////////
std::wstring string_to_wstring(const std::string& str);
std::string wstring_to_string(const std::wstring& str);
std::string GUID_to_string(GUID guidInstance);
GUID LPCSTRGUIDtoGUID(LPCSTR guidInstance);
FlatJoyState2 FlattenDIJOYSTATE2(DIJOYSTATE2 DeviceState);
bool GUIDMatch(LPCSTR guidInstance, LPDIRECTINPUTDEVICE8 Device);
GUID Device2GUID(LPDIRECTINPUTDEVICE8 Device);
inline CComBSTR ToBstr(const std::wstring& s);
void DestroyDeviceIfExists(LPCSTR guidInstance);
HRESULT BuildSafeArray(std::vector<std::wstring> sourceData, /*[out]*/ SAFEARRAY** SafeArrayData);
DWORD AxisTypeToDIJOFS(GUID axisType);
GUID EffectTypeToGUID(Effects::Type effectType);
bool IsDuplicateHID(const DIDEVICEINSTANCE* DIDI);
void LogMessage(const char* format, ...);