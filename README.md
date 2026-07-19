# Native DirectInput (and ForceFeedback) Support for Unity

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
Note that everything is adjustable in the native DLL. The Custom Force effect exists but has not been fully implemented, and the collision effects are only in the Unity project. This is optimized to be used in the Unity Game Engine only.

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

- Any Unity version that is using the .NET C# v5 should work

### Windows Version Support

The DirectInputManager should run on Windows 10 22H2 and newer (e.g., Windows 11) out of the box. The DirectInput API is available on these modern Windows versions without additional packages.

### Development Requirements

For developers working on the project:

- Visual Studio 2019 or newer
- .NET 5 SDK
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

While the code enumerates all available FFB axes and includes them in effect definitions, the current API functions do not provide individual control over secondary axes.

To properly support multiple FFB axes, the API would need to be extended with functions that:

- Allow setting different direction values per axis in the `rglDirection` array
- For condition effects, provide access to all elements in the condition array (not just `cond[0]`)
- Include axis index parameters in the update functions

These enhancements would enable support for devices with multiple force feedback axes, such as dual-motor joysticks or specialty controllers.

# Notice

Occasionally, calls to EnumerateDevices will take orders of magnitude longer than usual to execute (up to 60 seconds). This is caused by a Windows bug attempting to load an absent hardware device. USB Audio DACs & Corsair keyboards are known to cause this issue. Try disconnecting and reconnecting the offending USB devices. For more information, see [this](https://stackoverflow.com/questions/10967795/directinput8-enumdevices-sometimes-painfully-slow) StackOverflow post about the issue from 2012. See issue [#1](https://github.com/MrTimcakes/Unity-DirectInput/issues/1) for more info.

# Original Codebase

The original fundamentals coded by Mr.TimCakes, though, got significantly changed.

# License

This project is free Open-Source software released under the LGPL-3.0 License. Further information can be found under the terms specified in the [license](/../../blob/main/LICENSE).

<a href="https://www.flaticon.com/free-icons/drive" title="drive icons">Drive icons created by Freepik - Flaticon</a>
