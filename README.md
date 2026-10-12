# Unity DirectInput Force Feedback W/ Input Manager!
#### Now you can manage your DInput devices natively in addition to FFB Support!

#### Now you can manage your DInput devices natively, in addition to FFB Support!

![Unity-DirectInput Banner](https://github.com/MrTimcakes/Unity-DirectInput/blob/assets/UnityDirectInputBanner.png)

---

### [Try the Unity Windows build Demo here!](https://drive.google.com/file/d/1IsOmUG8XddhxAxufZc0VPJ8WfZ7Fnmte/view)

_! Please bind your DirectInput device keys by pressing the F2 button, or by clicking the Quick Setup button (the top-left side of the screen) after playing the demo._

---

#### This package allows you to easily integrate both the input and ForceFeedback features of DirectX DirectInput from within Unity. This allows you to interface with HID peripherals with ForceFeedback capabilities. This can be used to build vivid simulated experiences.

### Easy Installation via Package Manager + Sample Demos

![image](https://github.com/user-attachments/assets/b9ca5989-623a-48bb-9f98-d0ff99588fbd)

### Fully integrated with Unity's Input System, supports _any_ DirectInput device

[![Made with Unity](https://img.shields.io/badge/Made%20with-Unity-57b9d3.svg?style=for-the-badge&logo=unity)](https://unity3d.com)
![GitHub issues open](https://img.shields.io/github/issues/ImDanOush/Unity-DirectInput?style=for-the-badge)
![GitHub issues close](https://img.shields.io/github/issues-closed/ImDanOush/Unity-DirectInput?style=for-the-badge)
![GitHub package.json version](https://img.shields.io/github/package-json/v/ImDanOush/Unity-DirectInput?style=for-the-badge)
![GitHub](https://img.shields.io/github/license/ImDanOush/Unity-DirectInput?style=for-the-badge)

The package will create a virtual device inside Unity's Input System. This device can then be used like any other device inside the Input System, allowing for easy rebinding. ForceFeedback capabilities can be accessed via the DIManager class. The [DirectInputExplorer](https://github.com/imDanoush/Unity-DirectInput/tree/main/src~/DirectInputExplorer~) is a Windows Forms application built in parallel with the C++ library to enable quick development by avoiding the need to reload Unity after every change. It also functions as an easy way to examine DirectInput devices.

![image](https://github.com/user-attachments/assets/fcd321cb-7b7c-437a-b033-d80a78576f99)

# Quick Start

![image](https://github.com/user-attachments/assets/12feffae-5311-4603-a983-fee9ed45e372)

# Installation

> Prerequisite: This package requires the use of Unity's new Input System to ensure [com.unity.inputsystem](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.0/manual/QuickStartGuide.html) is installed in the project. Install it via the package manager:
> `Window -> Package Manager => Input System`, recommended only to activate and use this Input System.

## Installation Options:

### **Recommended: Unity Package Manager (Git URL)**

**Step-by-step UPM installation:**

1. **Prerequisites**: Ensure you have [Git client](https://git-scm.com/) installed (minimum version 2.14.0)
      - On Windows, add Git to your PATH environment variable
2. **Open Unity**: Launch your Unity project where you want to install the plugin
3. **Access Package Manager**: Go to **Window > Package Manager** in the Unity menu bar
4. **Add package**: Click the **+** (plus) button in the top-left corner of the Package Manager window
5. **Select installation method**: Choose **"Install package from git URL"** from the dropdown menu
6. **Enter Git URL**: In the text box that appears, enter:

```
https://github.com/imDanoush/Unity-DirectInput.git
```

7. **Install**: Click the **Install** button and Unity will download and install the package

**Benefits of UPM Git installation:**

- ✅ Direct installation from GitHub repository
- ✅ Automatic updates when you push new versions
- ✅ Clean project structure with proper dependency management
- ✅ Easy to share across team members
- ✅ Version control integration

### **Alternative: Manual Installation**

If you prefer manual installation or don't have Git installed:

- Download the plugin package from the Releases section
- Copy the `Plugin` folder from this repository directly into your Unity project's `Assets` folder
- Import the example UnityPackage included with the plugin

### **After Installation**

Regardless of installation method, **restart Unity** to ensure proper DLL loading and access to the new runtime UI features.

> **Note:** Folders ending with `~` are excluded by Unity during import and contain the Windows ForceFeedback Explorer application located at `./DirectInputExplorer~\DirectInputExplorer\bin\` for advanced device testing and configuration.

## Supported ForceFeedback Effects

| Effect           | Stat |
| ---------------- | ---- |
| ConstantForce    | ✅   |
| Damper           | ✅   |
| Friction         | ✅   |
| Inertia          | ✅   |
| RampForce        | ✅   |
| SawtoothDown     | ✅   |
| SawtoothUp       | ✅   |
| Sine             | ✅   |
| Spring           | ✅   |
| Square           | ✅   |
| Triangle         | ✅   |
| CustomForce      | ℹ️   |
| Front Collision  | ✅   |
| Rear Collision   | ✅   |
| Left Collision   | ✅   |
| RightCollision   | ✅   |

[comment]: <> (✅ ℹ️ 🔲)
Note that everything is adjustable in the native DLL. The collision effects are only in the Unity project. This is optimized to be used in the Unity Game Engine only.

Custom Force plays a list of samples (-10000..10000 each) at a sample period in microseconds through
`DIManager.UpdateCustomForceNative(guid, samples, samplePeriod)`; the device must enumerate
`GUID_CustomForce` (check with `DIManager.TryGetSupportedFFBEffects`). Ramp Force plays once from
start to end over a duration in microseconds (`UpdateRampForceNative(guid, start, end, duration)`);
a ramp cannot run forever, so every update replays it.

### Behaviour notes (read before upgrading)

- Nothing energises a device on its own: attaching, enumerating, selecting or loading a profile
  creates no effect. An effect starts only when your code (or the Explorer user) enables it, and it is
  created at zero force. A DirectInput magnitude or gain is not a physical torque; a software stop
  cannot guarantee a wheel stops if the driver or the device fails, so keep the wheelbase's own stop
  within reach while testing.
- The native sources in `src~/DirectInputForceFeedback~` are ahead of the shipped
  `Runtime/Plugins/DLL/DirectInputForceFeedback.dll`. Rebuild the DLL (see Additional Installation
  Info) to get hot-plug events, effect status queries, the ramp duration and the fixes listed in
  `docs/engineering-notes.md`. With the old DLL the managed layer still works; the new queries report
  `E_NOTIMPL` with a rebuild hint.
- `UpdateDamperSimple`, `UpdateFrictionSimple` and `UpdateInertiaSimple` now pass full saturation
  (10000) instead of 0, which on compliant drivers meant no force at all.
- Effect direction is a unit vector along the first force-feedback actuator (the steering axis) on
  every device; it used to be a diagonal on two-actuator devices.
- `OnDeviceAdded` / `OnDeviceRemoved` fire again (they had been dead since the hook was removed in
  March 2025). They arrive on the thread that called `DIManager.Initialize`, which must pump Windows
  messages (Unity's main thread and the Explorer's UI thread do).
- **Windows build target only.** Every assembly in this package and in its samples has the define
  constraint `UNITY_STANDALONE_WIN`. When the active build target is anything else (Android, macOS,
  Linux, consoles, WebGL, ...), Unity skips them entirely, in the Editor too. Nothing runs, there are
  no notifications, and nothing is added to the build. Players are 64-bit Windows only (the DLL is x64).
  Your own scripts that call `DIManager` must do the same, or they will not compile for other targets:
  put them in an asmdef with the same constraint, or wrap the calls in `#if UNITY_STANDALONE_WIN`.

### Notifications (on-screen errors)

Problems are reported **on by default**: each report goes to the Unity Console once (as an error,
warning or log entry, prefixed `[DirectInput]`) and, in Play Mode, to a card in the top-right corner of
the Game view (`DirectInputLogger`, IMGUI: no extra packages, works with either input backend and any
render pipeline). A card shows a title; **Show details** adds the operation, device name, GUID and
VID:PID, the HRESULT with its name and meaning, the DLL's last error message when it has one, and what
to do. **Copy** puts the text on the clipboard. Repeats merge into one card with a count. Cards fade
after about 6 s (info), 12 s (warning) or 20 s (error) of real time, and stay while hovered or expanded.

- Turn the cards off: `DirectInputLogger.Enabled = false;` (or the samples' `DIManager.showLogsRuntime`),
  or define `DIRECTINPUT_DISABLE_TOASTS` in Player Settings > Scripting Define Symbols to compile the
  drawing out. Console logging stays either way.
- Report your own: `DirectInputLogger.Report(DirectInputNotificationSeverity.Warning, "title", "details")`
  works from any thread.
- Covered: the plugin failing to load (a non-Windows or Arm64 Editor, DLL missing, a missing
  dependency such as the Visual C++ 2015-2022 x64 runtime or `dinput8.dll`, an older DLL than the
  scripts), DirectInput failing to start, enumeration failing when the DLL reports it as a failure
  (distinct from "no controller found"),
  another program holding the device exclusively, a device lost, unplugged or reconnected, effect
  create/update failures (device full, unsupported, incomplete, not exclusively acquired, playing, not
  downloaded, invalid parameters), success codes that mean the force was not fully applied
  (`DI_DOWNLOADSKIPPED`, `DI_TRUNCATED`, `DI_TRUNCATEDANDRESTARTED`), driver-defined (`0x800403xx`) and
  HID (`0xC011xxxx`) codes. Per-frame calls report once per change of operation, device and result, and
  once more when they work again. In the Editor, a plugin that cannot load is also reported in edit mode,
  with the DLL as the Console context, plus a Scene/Game view notification.
- Only a likely cause: a wheel that enumerates without force feedback gets a hint chosen by its USB
  vendor ID (for example, Logitech: G HUB or Logitech Gaming Software not running; console or
  compatibility mode). Vendor software requirements are not verified here, so treat it as a lead, not a
  diagnosis.
- Not detectable: DirectInput cannot see a force-feedback strength of 0 in the vendor's software, an
  emergency stop, or a force-feedback off switch on the wheel. Calls succeed in those cases, so a quiet
  notification area never means "force feedback is OK".

## Compatible Devices

The community has tested and verified that these devices do indeed work. Although not all devices support all the FFB effects!

| Peripheral                                                                                                                                                                                      | Test Status |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------- |
| [Simucube Ultimate 2](https://simucube.com/simucube-2-ultimate)                                                                                                                                 | ✅ Verified |
| [Fanatec DD1](https://www.fanatec.com/ww/en/p/wheel-bases/p_wb_dd1_qr2_jp/podium-wheel-base-dd1-qr2-jp)                                                                                         | ✅ Verified |
| [Fanatec CSL DD (Both PC & Comp mode + 8NM Kit)](https://fanatec.com/eu-en/csl-dd-8-nm)                                                                                                         | ✅ Verified |
| [Fanatec CSL Elite](https://fanatec.com/eu-en/racing-wheels-wheel-bases/wheel-bases/csl-elite-wheel-base-officially-licensed-for-playstation)                                                   | ✅ Verified |
| [Fanatec CSW V2.0](https://fanatec.com/eu-en/racing-wheels-wheel-bases/wheel-bases/clubsport-wheel-base-v2-servo)                                                                               | ✅ Verified |
| [Fanatec WRC Wheel Rim](https://fanatec.com/eu-en/steering-wheels/csl-elite-steering-wheel-wrc)                                                                                                 | ✅ Verified |
| [Fanatec Formula V2 Wheel Rim](https://fanatec.com/eu-en/steering-wheels/clubsport-steering-wheel-formula-v2) & [APM](https://fanatec.com/eu-en/shifters-others/podium-advanced-paddle-module)  | ✅ Verified |
| [Fanatec CSL LC Pedals](https://fanatec.com/eu-en/pedals/csl-elite-pedals)                                                                                                                      | ✅ Verified |
| [Fanatec ClubSport Pedals V1](https://www.youtube.com/watch?v=jw52Dq3SZaA)                                                                                                                      | ✅ Verified |
| [Fanatec ClubSport Pedals V3](https://fanatec.com/eu-en/pedals/clubsport-pedals-v3)                                                                                                             | ✅ Verified |
| [Fanatec ClubSport Shifter SQ V 1.5](https://fanatec.com/eu-en/shifters-others/clubsport-shifter-sq-v-1.5)                                                                                      | ✅ Verified |
| [Logitech G29 / G920](https://www.logitechg.com/en-gb/products/driving/driving-force-racing-wheel.html)                                                                                         | ✅ Verified |
| [PRO Racing Wheel](https://www.logitechg.com/en-gb/products/driving/pro-racing-wheel.html)                                                                                                      | ✅ Verified |
| [Simagic Alpha-Mini](https://us.sim-motion.com/products/simagic-alpha-mini-wheel-base)                                                                                                          | ✅ Verified |
| [Moza R16](https://mozaracing.com/pages/r16-wheelbase)                                                                                                                                          | ✅ Verified |
| [Moza R12](https://eu.mozaracing.com/products/r12-wheelbase)                                                                                                                                    | ✅ Verified |
| [Moza R9](https://eu.mozaracing.com/products/r9-wheelbase)                                                                                                                                      | ✅ Verified |
| [Moza R5](https://eu.mozaracing.com/collections/r5-bundles)                                                                                                                                     | ✅ Verified |
| [Moza R3](https://eu.mozaracing.com/collections/r3-bundles)                                                                                                                                     | ✅ Verified |
| [Thrustmaster TX](https://eshop.thrustmaster.com/en_us/tx-racing-wheel-leather-edition.html)                                                                                                    | ✅ Verified |

[comment]: <> (✅ 🔲)
Note for pedals: exclusively input readings would work; FFB pedals and haptic devices are completely different!

### Note that all other devices that use DirectInput (from the old Logitech G wheels to the advanced Simucube ones) should work

## Environment

- Unity 2021.2 or newer (the sources use C# 9) with the Input System package 1.0.2 or newer; 64-bit
  Windows Editor and Standalone only (`package.json` declares `"unity": "2021.2"`).

### Windows Version Support

The DirectInputManager should run on Windows 10 22H2 and newer (e.g., Windows 11) out of the box. The DirectInput API is available on these modern Windows versions without additional packages.

### Development Requirements

For developers working on the project:
- Visual Studio 2022 (v143 toolset, the DLL uses C++20)
- .NET 5 SDK for the Explorer (.NET 5 is out of support; a newer SDK builds it with a warning)
- Windows SDK (for DirectInput headers)
- Microsoft Visual C++ Redistributable
- C++ build tools if modifying the native DLL
  You _may_ need to enable "allow unsafe code" in the Player Settings of your Unity project to build your game, or not - not tested.

### User Requirements

For end users running the built application:

- Microsoft Visual C++ Redistributable
- .NET 5 Runtime
- Windows 10 22H2 or newer (Can be used in Windows 7+ theoretically, but not tested)
- The DirectInputForceFeedback.dll must be properly deployed alongside the application
- No special DirectX installation is required on modern Windows, as DirectInput is part of the OS
  The project provides force feedback support for DirectInput-compatible devices like joysticks, wheels, and game controllers. It's designed to work both as a standalone .NET application and within Unity projects.

### Additional Installation Info

For Unitypackage, if you do not have some SDKs installed, you may get an error stating that the DLL is not found. To solve that issue, do the following:

> 0. Clone the [DirectInput repository](https://github.com/imDanoush/Unity-DirectInput/),
> 1. Then go to the `/DirectInputForcefeedback~` folder, where there is a `.sln` Visual Studio project file,
> 2. Open it and press F5 in VS to build a new DLL, where you'll be asked to install the missing SDKs
> 3. The newly built DLL will be available in the directory specified by the `output` setting in Visual Studio; then copy it from where it was created, and paste it over the DLL in Unity's `Asset/Plugin` folder for Direct Input. However, this step should be done by Visual Studio after a build by itself.

### Force Feedback Axis Support

**Important Note:** This plugin was designed primarily for standard DirectInput wheelbases. Thus, it aims to support force feedback on the first/primary axis of FFB-capable devices.

The effect is created across every actuator the device enumerates (`DIDFT_AXIS` objects with `DIDOI_FFACTUATOR`), with a direction vector of 10000 on the first actuator and 0 on the others, and one `DICONDITION` per actuator carrying the same values. The current API functions do not provide individual control over secondary axes. 

To properly support multiple FFB axes, the API would need to be extended with functions that:

- Allow setting different direction values per axis in the `rglDirection` array
- For condition effects, provide access to all elements in the condition array (not just `cond[0]`)
- Include axis index parameters in the update functions

These enhancements would enable support for devices with multiple force feedback axes, such as dual-motor joysticks or specialty controllers.

# Tests and engineering notes

- `src~/DirectInputManager.Tests~`: xunit tests of the managed layer (struct layouts, HRESULT handling,
  poll change detection) that run on any OS with `dotnet test`.
- `Tests/Editor`: the same layout checks as NUnit tests for the Unity Test Runner.
- `src~/DirectInputForceFeedback~/tests/helpers_test.cpp`: tests of the DLL's platform-independent
  helpers (GUID validation, axis clamping, POV to d-pad bits, direction vector); build command in the
  file header.
- `docs/engineering-notes.md`: findings, design decisions, measurements, verification matrix and the
  remaining work; `docs/ledgers/`: the DirectInput, Unity and history source ledgers behind them.

# Open work (for the next developer)

Everything below is known and not done. Items 1 and 2 block a release. For background, see the
"Still open" list in `docs/engineering-notes.md`. Rules for every item:
- never start an effect automatically or push force into a wheel without a user action;
- keep public APIs and asset GUIDs unless the item says otherwise;
- after each change, run the checks in "Tests and engineering notes" above.

1. **Rebuild the native DLL (release blocker).**
   - **Problem:** `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` is older than its source.
     - It lacks the `GetFFBEffectStatus` and `GetSupportedFFBEffects` exports.
     - It still needs the Visual C++ runtime.
     - None of the native fixes reach users until it is rebuilt.
   - **Steps:** on Windows with Visual Studio 2022, open `src~/DirectInputForceFeedback~/DirectInputForceFeedback.sln` and build **Release | x64**. The project now uses the static runtime (`/MT`). The post-build step copies the DLL into `Runtime/Plugins/DLL`; keep the existing `.meta` file.
   - **Verify:** in a VS developer prompt:
     - `dumpbin /exports` must list 32 exports, including the two above.
     - `dumpbin /dependents` must list no `MSVCP140`, `VCRUNTIME140*` or `api-ms-win-crt-*`.
   - **Then:** commit the DLL.

2. **Test in Unity and on real hardware (release blocker).** Nothing has run in Unity, on Windows or on a wheel yet.
   - **Notifications:** check that each card appears once and that details, Copy and Dismiss work. Trigger these cases:
     - rename the DLL (file missing);
     - run on a PC without the VC++ runtime, using the old DLL only;
     - unplug the wheel during play;
     - open a second program that holds the wheel;
     - plug in a wheel whose vendor driver is not installed.
   - **Force feedback:** check every effect in the Explorer and in the samples. Confirm that STOP ALL, Esc, closing the app, unplugging the wheel and pausing Unity all release the force.
   - **Samples:** in a clean project, import each sample on its own, then all of them, then make a Windows player build.
   - **Hardware-dependent fixes:** confirm the `Poll()`-before-read fix and the `DI_DOWNLOADSKIPPED`/`DI_TRUNCATED` handling on real wheels.

3. **Sample files share asset GUIDs across packages.**
   - **Which files:**
     - `DIInputManager.cs` has the same GUID in all three `.unitypackage` files.
     - `DIInputValueMonitor.cs`, `RuntimeFFBConfigUI.cs`, `RuntimeInputMappingUI.cs` and `directinput.asmref` share a GUID in the two 1.0.1 packages.
     - The sample root, `Scripts` and `Scene` folders share a GUID in all three.
   - **Problem:** importing a second sample may overwrite the first one's files. Not yet checked in Unity.
   - **Fix:** give each package's colliding assets new GUIDs, and update every reference to the old GUIDs inside that package's scenes and prefabs (search the YAML for `guid:`).
   - **Verify:** do the item 2 sample import test.

4. **DLL and scripts can be from different versions.** `GetPluginVersion()` in the native code always returns 1 and the C# side never calls it.
   - **Fix:**
     - Bump the native value with every ABI change.
     - P/Invoke `GetPluginVersion` from `DIManager.Initialize`.
     - When it differs from the expected value, report one Error through `DirectInputLogger.Report`.
     - Treat a missing export as "old DLL"; `DIDiagnostics` already has that wording.
   - **Verify:** add a unit test in `src~/DirectInputManager.Tests~`.

5. **A device scan blocks the game.** The native `EnumerateDevices` holds the DLL's lock for both `EnumDevices` passes, so a main-thread poll waits for the whole scan. Scans can take long on some systems; see `docs/ledgers/dinput-ledger.md` #42.
   - **Fix:** run both passes outside `g_apiLock` into a local list, then take the lock only to swap it in and free the old strings.
   - **Keep:** `-1` on failure, and the pointer contract (valid until the next call).

6. **The "likely cause" hints for wheel vendors are unverified.** The vendor websites could not be reached when `DIDiagnostics.VendorHint` (in `Runtime/DirectInputManager.cs`) was written.
   - **Check:** against the vendors' own documentation and real wheels: Logitech G HUB or LGS, Thrustmaster drivers, the Fanatec driver, Moza Pit House, and whether the base needs a PC mode.
   - **Then:** correct the text. Keep it worded as likely causes.

7. **Saved effect toggles in the Fundamental sample are cleared on load** (`RuntimeFFBConfigUI`). This is deliberate: restoring them would start effects at launch.
   - If restoring is wanted, restore them as "armed" only, and start effects only after a user action.

8. **The Explorer hides a failed device scan.**
   - **Problem:** once the DLL is rebuilt (item 1), a failed scan keeps the old device list but still logs "Enumerated N device(s)". The failure only reaches `Debug.WriteLine`.
   - **Fix:** show the failure in the Explorer log and status bar. `DIManager.EnumerateDevices` keeps the old list when the native call returns `-1`.

9. **The NWH sample needs Input System 1.1 or later**, because NWH Vehicle Physics 2 does. The package declares 1.0.2. Either state this in the sample description or raise the dependency.

10. **Smaller native items:**
    - A device that can't be reacquired is retried with one `Acquire` call per API call every frame.
    - If restarting effects fails during a poll, that effect stays stopped until the next reacquire.
    - Fix both with a per-device back-off, and report through the existing once-per-state-change path.

Behaviour note for users of the API: effect updates can now return the success codes 3, 4, 8 or 12, so
`DIManager.LastHResult` may be positive after a success. Test `LastHResult >= 0` for success, not `== 0`.

# Notice

Occasionally, calls to EnumerateDevices will take orders of magnitude longer than usual to execute (up to 60 seconds). This is caused by a Windows bug attempting to load an absent hardware device. USB Audio DACs & Corsair keyboards are known to cause this issue. Try disconnecting and reconnecting the offending USB devices. For more information, see [this](https://stackoverflow.com/questions/10967795/directinput8-enumdevices-sometimes-painfully-slow) StackOverflow post about the issue from 2012. See issue [#1](https://github.com/MrTimcakes/Unity-DirectInput/issues/1) for more info.

# Original Codebase

The original fundamentals coded by Mr.TimCakes, though, got significantly changed.

# License

This project is free Open-Source software released under the LGPL-3.0 License. Further information can be found under the terms specified in the [license](/../../blob/main/LICENSE).

<a href="https://www.flaticon.com/free-icons/drive" title="drive icons">Drive icons created by Freepik - Flaticon</a>
