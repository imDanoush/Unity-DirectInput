# DirectInput Explorer

A Windows Forms test bench for the `DirectInputForceFeedback.dll` native library and the `DIManager`
managed layer (`Runtime/DirectInputManager.cs`, compiled into this project by link). It lets you
enumerate DirectInput devices, attach one, watch its raw state, and drive every force-feedback effect
the DLL exposes without opening Unity.

## Build

Requirements on Windows: Visual Studio 2022 (or the .NET SDK) with the Windows Desktop workload.
The project targets `net5.0-windows10.0.22000.0`, x64. .NET 5 is out of support; building with a newer
SDK works and prints warning NETSDK1138. Changing the target framework is a project decision and has
not been made here.

```
cd src~/DirectInputExplorer~/DirectInputExplorer
dotnet build -c Release
```

On Linux or macOS the project compiles (not runs) with `-p:EnableWindowsTargeting=true`, which is how
it was checked in CI-less environments:

```
dotnet build DirectInputExplorer.csproj -c Release -p:EnableWindowsTargeting=true
```

The build copies `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` next to the exe. The managed
layer calls exports that only exist in a DLL built from the current `src~/DirectInputForceFeedback~`
sources (`GetFFBEffectStatus`, `GetSupportedFFBEffects`, the four-argument `UpdateRampForce`). With
an older DLL the Explorer still starts; effect panels say the DLL predates the export ("support not
verified", "status unavailable") and ramp duration is ignored. Rebuild the DLL with Visual Studio
(x64, Release) first; its post-build step copies it into `Runtime/Plugins/DLL`.

## Layout

The window is built in code (`MainForm.cs`, `EffectPanel.cs`, `CustomForcePanel.cs`); there is no
designer file.

- Top bar: device list, Refresh (runs enumeration on a worker thread with polling paused), Attach/Detach,
  Autocenter, and a red STOP ALL button. Escape also triggers STOP ALL, even while a grid cell is being
  edited. The autocenter box shows an unknown (filled) state after attach and after a failed change,
  because the Explorer does not read the device's setting back. The line under the bar shows the attach
  state, input-read failures and the disconnect warning.
- "Info" tab: the device capabilities reported by `GetDeviceCapabilities` and the effects the driver
  enumerates. "Input" tab: live input state (axes in the 0..65535 DirectInput range, buttons, POV hats).
- "Effects" tab: one panel per standard effect. The enable box creates the effect at zero force;
  each parameter has a slider and a numeric box showing the DirectInput range and unit (magnitudes and
  coefficients -10000..10000, saturations and dead band 0..10000, periods and ramp duration in
  microseconds). The status line only ever shows what the API returned; an update error stays until the
  next successful update of that effect. Panels for effect types the device does not enumerate are
  greyed with the reason.
- "Custom force" tab: sample count, sample period, a grid of samples validated to -10000..10000, and an
  explicit Apply button (or "send on every edit").
- "Diagnostics" tab: Device report (does not change forces), and a log of every call result with
  HRESULT text.

## Safety

- Nothing is sent to a device until you attach it and enable an effect yourself. Enabling an effect
  panel creates the effect with zero force, then applies the values the panel shows (zero force by
  default; values you set earlier stay after STOP ALL). The custom force starts with zero samples. After
  that, forces change only when you move a control or press Apply.
- STOP ALL, Detach, device loss (including a Refresh that no longer lists the attached device) and
  closing the window stop and destroy every effect the DLL holds and reset the panels.
- A DirectInput gain or magnitude does not tell you the physical torque a wheelbase will produce.
  Software stop commands cannot guarantee a wheel stops if the driver, the USB link or the device
  itself fails; keep the wheelbase's own power or emergency stop within reach.

## Known limits

- Device names are marshaled as ANSI on .NET 5, while the DLL emits UTF-8; non-ASCII product names can
  display garbled in the Explorer. Whether Unity's Mono and IL2CPP runtimes decode `LPStr` as UTF-8
  (which would leave the Unity side unaffected) was not verified here.
- The Explorer has no automated UI tests. The managed logic tests live in
  `src~/DirectInputManager.Tests~` (`dotnet test`), and the native helper tests in
  `src~/DirectInputForceFeedback~/tests` (see the header of `helpers_test.cpp`).
