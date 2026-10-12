# Engineering notes

Durable record of the audit and repair of this repository (branch
`claude/eloquent-curie-78h3gn`). Every statement carries one of: VERIFIED
(read in source or documentation, or reproduced here), HYPOTHESIS (follows
from corroborated API semantics but was not executed on hardware), UNKNOWN,
DESIGN DECISION. The source ledgers behind the API claims are in
`docs/ledgers/` (DirectInput: 42 entries, Unity: 34 entries, repository
history: 19 entries). Each ledger entry says whether the page itself was read
(VERIFIED) or only a search snippet (PROVISIONAL, treated as HYPOTHESIS here).

## 1. Executive summary

- The package could not compile at the branch base; two commits were reverted
  with the user's approval (F1, F2).
- Hot-plug notification had been dead since March 2025 (the window hook was
  removed and nothing replaced it); the cooperative level was bound to whatever
  window was in the foreground, possibly another process's. Both are fixed by
  one hidden top-level window owned by the DLL (N7, N8).
- After an input loss the poll path reacquired the device but never restarted
  the effects, so every later update reported success on a wheel producing no
  force (N1). Fixed.
- The simple damper, friction and inertia helpers sent saturation 0, which on
  compliant drivers means no force (B3). Fixed.
- The Explorer's Custom FFB tab could not work: every control shared one Tag and
  the shared handlers dereferenced null on the first interaction (F4/X1). The
  designer form is replaced by a code-built form with one panel per effect and
  a dedicated custom-force editor.
- Poll change detection cost 4.8 us and 432 bytes per device per frame (MD5 of a
  marshaled copy); it is now a 19 ns field comparison with no allocation (F7).
- The DLL had no synchronisation while managed code enumerates devices on a
  thread-pool thread (N2). One recursive lock now serialises every export.
- 13 further defects were fixed in the managed layer and 7 in the DLL; 8 items
  are documented or proposed rather than changed because they alter public
  behaviour or need a decision (section 8).
- The shipped `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` is now behind
  its sources. It must be rebuilt on Windows (BLOCKED here). Everything that
  could be compiled or executed on Linux was: managed logic tests (11 PASSED),
  Explorer compile (PASSED), native helper tests (PASSED).

## 2. Repository and architecture

| Area | Files | Notes (VERIFIED) |
|------|-------|------------------|
| Native DLL | `src~/DirectInputForceFeedback~/**` | C++20, MSVC v143, x64 (Win32 configs cannot build, N12). Release x64 post-build copies the DLL into `Runtime/Plugins/DLL`. New: `DirectInputHelpers.h` (platform-independent helpers), `tests/helpers_test.cpp`. |
| Shipped binary | `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` | PE32+ x64, 30 exports, built from the sources at `31fcbc6`. Stale relative to this branch. |
| Unity runtime | `Runtime/DirectInputManager.cs`, `Runtime/DirectInputLogger.cs`, `Runtime/Plugins/VirtualInputSystemDevice.cs` | asmdef: Editor + WindowsStandalone64. |
| Unity tests | `Tests/Editor/AbiLayoutTests.cs` + asmdefs | NUnit layout checks; NOT RUN here (no Unity). |
| Managed tests | `src~/DirectInputManager.Tests~` | xunit, net8.0, links the real `Runtime/DirectInputManager.cs`; runs on any OS. |
| Explorer | `src~/DirectInputExplorer~/DirectInputExplorer/{Program,MainForm,EffectPanel,CustomForcePanel}.cs` | WinForms, `net5.0-windows10.0.22000.0`, x64. `Form1.*` removed. Tracked `bin/` and `obj/` output still present (proposal P4). |
| Docs | `README.md`, `src~/DirectInputExplorer~/README.md`, this file, `docs/ledgers/` | |

```
Explorer MainForm (UI thread, 20 ms Timer; enumeration on a worker with the timer paused)
   |                                   Unity: DirectInputDevice (Input System device)
   v                                     |  FinishSetup -> DIManager.Attach ; OnUpdate -> DIManager.Poll
DIManager (static, Runtime/DirectInputManager.cs)
   |  P/Invoke: LPStr GUID strings, HRESULT as int, Check() records LastHResult/LastError
   v
DirectInputForceFeedback.dll  (global std::map state keyed by GUID string, one recursive_mutex)
   |  hidden top-level window: cooperative level + RegisterDeviceNotification(GUID_DEVINTERFACE_HID)
   v
dinput8.dll -> driver -> wheelbase
```

Device lifecycle (VERIFIED from source): `StartDirectInput` -> `EnumerateDevices`
(two `EnumDevices` passes, second flags FFB) -> `CreateDevice(guid)`
(`c_dfDIJoystick2`, `SetCooperativeLevel(hiddenWindow, EXCLUSIVE|BACKGROUND)`,
`Acquire`) -> `GetDeviceState` each poll (on `DIERR_INPUTLOST`/`NOTACQUIRED`:
`ReacquireDeviceAndEffects`) -> `CreateFFBEffect(type)` builds one `DIEFFECT`
across every `DIDFT_AXIS` object flagged `DIDOI_FFACTUATOR`, direction
(10000, 0, ...), INFINITE duration (ramp: 1 s), gain 10000, zero type-specific
parameters, then `Start(1, 0)` -> `Update*` calls `SetParameters` ->
`DestroyFFBEffect` Stop/Unload/Release -> `DestroyDevice` -> `StopDirectInput`
(destroys the window last).

Threading contract (DESIGN DECISION): every export that touches DirectInput
state takes `g_apiLock` (recursive); the version and initialised-flag getters
do not. A hot-plug rescan holds the lock for the whole `EnumDevices` run, so a
main-thread poll waits for it (moving enumeration outside the lock is open work). The hidden window is created on the thread that first calls
`SetDeviceChangeCallback` (from `DIManager.Initialize`) or `CreateDevice`;
device-change messages are delivered only while that thread pumps messages
(Unity main thread, WinForms UI thread). `StopDirectInput` from another thread
leaves the window until process exit (logged, shortcut noted in code).

## 3. Findings and status

Severity: C critical, H high, M medium, L low. "Fixed" names the commit.
Refuter column: independent adversarial verification run in a fresh context
(workflow). The refuters ran while the fixes were landing, so several verdicts
read "the quoted code no longer exists": those are recorded as "fix present".
The remaining refuters (N12 onward, managed, Explorer) were still running at
handoff time and were not waited for, since each would re-check code already
replaced; the managed and Explorer findings were verified against the source
by the lead instead (section 3 states each one as VERIFIED only after that
read).

### Baseline and managed layer

| ID | Sev | Where | Finding (VERIFIED unless noted) | Resolution |
|----|-----|-------|----------------------------------|------------|
| F1 | C | `DirectInputManager.cs` @7ae2121 | Commit deleted every type the manager uses; nothing compiled. | Reverted (29bc674, 457654d), user approved. |
| F2 | H | same @7ae2121/8a27281 | 16 ms rate limiter dropped "set force to 0"; axis validation compared against a string count and leaked it. | Reverted. |
| F6 | M | `DeviceInfo.FFBCapable` | 4-byte BOOL marshaling over a 1-byte native bool (read 3 padding bytes). | Fixed 53eb4f3 (`UnmanagedType.I1`), test red before/green after. |
| F7 | M | `DIManager.Poll` | MD5 over a marshaled copy per poll. | Fixed 53eb4f3, measured (section 6). |
| F9/M14 | L | `package.json` | `"unity": "2017.0"` with C# 9 sources. | Fixed f704de1 (`2021.2`). |
| B1 | M | `Check` | `!= 0` treated `DI_DOWNLOADSKIPPED`/`DI_EFFECTRESTARTED`/`DI_TRUNCATED` as failures. | Fixed 53eb4f3 (`hresult >= 0`). |
| B2 | M | `UpdateCustomForceNative` | Empty array reached the DLL. | Fixed 53eb4f3 (E_INVALIDARG). |
| B3 | H | `UpdateDamperSimple` etc. | Saturation 0 sent (HYPOTHESIS that drivers then produce no force; ledger entry 12). | Fixed 53eb4f3 (10000). |
| M1 | H | `Poll` | A failed read left a zeroed struct that was published; Unity turned it into full-lock axis input. | Fixed f704de1 (bail out on failure). |
| M2 | H | `GenerateISLayouts` | `WithProduct` treated product names as regex; metacharacters meant no match and the device was dropped. | Fixed f704de1 (`supportRegex: false`). |
| M3 | M | `DirectInputDevice.Initialize` | `Initialize()` result ignored; DllNotFoundException escaped the static constructor. | Fixed f704de1. |
| M4 | M | native `StartDirectInput` | Returned S_OK when `DirectInput8Create` failed. | Fixed ab18e34. |
| M5 | M | `UpdateEffect` | Clamped the caller's array; periodic period and custom sample period went through a 0..10000 / signed clamp. | Fixed f704de1 (clamp a copy, periods unclamped, field reuse documented). |
| M6 | M | `TryGetFFBEffectStatus` | Export did not exist in the DLL. | Fixed 5d3804d/ab18e34 (exports added); DLL rebuild required. |
| M7 | M | `Update*Simple` | Any failure triggered a raw `Native.CreateFFBEffect` (which also starts the effect), bypassing `Check`. | Fixed f704de1 (only on E_ABORT, via `EnableFFBEffect`). |
| M8 | M | `Debouncer`, `ScanDevicesForChanges` | Unlocked token list, `async void` without a catch, callback pointer kept across domain unload. | Fixed f704de1 (lock, catch, `DomainUnload` clears the native callback). Delivery thread documented in section 2. |
| M9 | L | `DIDeviceAdded`, `OnRemoved` | Every hot-plug re-registered all layouts (recreating all devices); handlers never unsubscribed. | Fixed f704de1. |
| M10/N11 | L | `DeviceInfo` strings | DLL emits UTF-8; `.NET` decodes `LPStr` as ANSI. | Documented (Explorer README). Unity decoding UNKNOWN. Not changed: public struct. |
| M11 | L | `#if UNITY_STANDALONE_WIN` | Used as "inside Unity"; off in the editor with a non-Windows target. | Fixed f704de1 (`UNITY_5_3_OR_NEWER`). |
| M12 | L | `DLLFile` | Six-level relative path only valid from the in-repo build output. | Fixed f704de1 (bare name; Explorer csproj copies the DLL). |
| M13 | L | editor static constructor | Every controller is acquired exclusively at project load for the editor's lifetime. | Proposal P1 (behaviour change). |

### Native DLL

| ID | Sev | Finding | Resolution | Refuter |
|----|-----|---------|------------|---------|
| N1 | H | Poll-path reacquire used a bare `Acquire()`; effects stayed stopped, later updates returned S_OK. | Fixed ab18e34 (`ReacquireDeviceAndEffects`). | not refuted (medium) |
| N2 | H | No synchronisation; `g_LastErrorMessage` and the maps shared across threads. | Fixed 0261d06 (`std::recursive_mutex` in every export). | not refuted |
| N3 | M | Direction 10000 on every axis: 45 degree diagonal on two actuators. | Fixed b43826d (10000 on the first actuator, 0 elsewhere). DESIGN DECISION below. | not refuted (medium) |
| N4 | M | Ramp created with INFINITE duration (ledger 8: a ramp cannot be INFINITE, PROVISIONAL). | Fixed 0261d06 (1 s at creation; `UpdateRampForce` takes the duration, sets `DIEP_DURATION | DIEP_START`). | code facts confirmed; the driver rejection path is unconfirmed by any readable source (stays HYPOTHESIS, low) |
| N5 | M | `DEBUG1` applied 50 % constant force as a side effect. | Fixed ab18e34 (reports `GetEffectStatus` instead). | not refuted, fix present |
| N6 | M | POV diagonals collapsed to one cardinal bit. | Fixed ab18e34 (`PovToDpadBits`, unit-tested). | fix present |
| N7 | M | Cooperative level bound to `GetForegroundWindow()`. | Fixed ab18e34 (owned hidden window). | fix present |
| N8 | M | No `RegisterDeviceNotification`; hot-plug dead. | Fixed ab18e34. | fix present |
| N9 | L | Custom-force buffer freed before the new one was allocated. | Fixed 0261d06. | fix present |
| N10 | L | `EnumObjects` failure cached an empty axis list until `DestroyDevice`. | Fixed 0261d06. | fix present |
| N12 | L | Win32 configurations cannot build (C++17 setting, `std::map::contains`); calling convention would mismatch. | Proposal P2. | not run |
| N13 | L | `GetDILastError` never cleared, not set on most failures, not declared in managed code. | Documented; proposal P3. | not run |
| N14 | L | `FlattenDIJOYSTATE2` clamps every field to 0..65535, dropping negative velocity/acceleration/force values. | Documented contract: axes 0..65535 (DirectInput default range, ledger 38 PROVISIONAL). HYPOTHESIS that no supported device reports the signed fields. | not run |
| S1 | M | Custom-force sample period 0 passed to the driver with undocumented meaning. | Fixed 78e3831 (0 keeps the `DIDEVCAPS` period used at creation). | n/a |
| S2 | L | `_EnumFFBAxisCallback` accepted any `DIDOI_FFACTUATOR` object, not only axes. | Fixed ab18e34 (`DIDFT_AXIS` required, as SDL does). | n/a |

### Explorer (old `Form1`; all superseded by `MainForm`, commit 7399d95)

| ID | Sev | Finding | Where it went |
|----|-----|---------|---------------|
| X1/F4 | H | All custom-tab controls shared Tag `CustomForceMagnitude`; handlers dereferenced null. | `CustomForcePanel` (section 4). |
| X2 | H | Every periodic value change destroyed all six periodic effects and recreated one. | One `EffectPanel` per effect; updates only update. |
| X3/X4/F5 | H/M | `Devices[SelectedIndex]` with index -1 on every timer tick and button. | `SelectedDevice()` returns null; every path checks it. |
| X5 | M | Numeric minima -10000 against slider minima 0/1000 threw on entry. | One `EffectParameter` range drives both controls. |
| X6 | M | Debug button energised the wheel; `string.Join` misuse. | Native N5; report lines logged with HRESULT text. |
| X7 | M | UI never reconciled with API results; failures only in `Debug.WriteLine`. | Status lines show API results; STOP ALL, detach, device loss and close reset the panels. |
| X8 | M | Failure path looked up `CB`+type but boxes were `checkBox1..6`. | Gone. |
| X9 | M | Ramp driven as -m..+m on an INFINITE effect. | Start, end and duration exposed; native N4. |
| X10 | L | Each Attach re-subscribed state handlers. | Unsubscribe before subscribe. |
| X11 | L | Enumeration on a worker thread concurrent with the UI poll. | Native lock (N2); poll timer paused during enumeration. |
| X13 | L | Enable did not push samples; period box 0 vs slider 1000. | Explicit Apply; period from `DIDEVCAPS` shown. |

## 4. Custom FFB Effects: root cause and traceability

Root cause (VERIFIED): `Form1.Designer.cs` gave all 22 value controls of the
custom tab the Tag `"CustomForceMagnitude"`. `FFB_UpDown_ValueChanged` and
`FFB_Slider_Scroll` resolved their partner control as `"UD" + Tag` /
`"Slider" + Tag`, which did not exist, and dereferenced null; the custom path
also destroyed and recreated the effect on every edit and ignored the result.
No sample ever reached the DLL from that tab. The native custom-force path
itself (`CreateFFBEffect(CustomForce)`, `UpdateCustomForce`) was sound apart
from N9 and S1.

| Control (MainForm) | Intended meaning, units | Managed representation | Native / API representation | Validation | Capability required | Observed defect (old tab) | Fix | Verification |
|---|---|---|---|---|---|---|---|---|
| Enabled | Create the effect at zero force | `DIManager.EnableFFBEffect(guid, CustomForce)` | `CreateFFBEffect`: `DICUSTOMFORCE{cChannels=1, cSamples=10, rglForceData=zeros, dwSamplePeriod=DIDEVCAPS.dwFFSamplePeriod}`, `DIEFFECT.dwSamplePeriod` same, INFINITE duration, `Start(1,0)` | Device attached; box greyed unless `GetSupportedFFBEffects` reports CustomForce (or old DLL: "support not verified") | `GUID_CustomForce` enumerated (`DIEFT_CUSTOMFORCE`) | Checkbox worked but nothing else did | Panel owns the box; failure text comes from `LastError` | Compile PASSED; runtime NOT RUN |
| Samples (count 1..256) | Number of force samples per loop | `int[] Samples` | `cSamples`, `rglForceData` reallocated (allocate-then-swap) | 1..256 in the UI; DLL rejects `sampleCount <= 0` | same | Fixed 10 controls, count not adjustable | Grid rebuilt on count change | helpers n/a; runtime NOT RUN |
| Sample N | Force at step N, -10000..10000 (`DI_FFNOMINALMAX`) | `int` per row | `LONG rglForceData[N]`, clamped in the DLL | `CellValidating` rejects non-integers and out-of-range | same | NullReferenceException on any edit (X1) | `CustomForcePanel` grid | Compile PASSED |
| Sample period | Microseconds per sample; 0 keeps the device period | `uint samplePeriod` | `DICUSTOMFORCE.dwSamplePeriod` (0 leaves the creation value) | 0..1,000,000 in the UI | same | Negative values allowed, slider/box ranges disagreed (X5) | S1 + UI range | Compile PASSED |
| Apply / send on edit | Download the samples | `UpdateCustomForceNative(guid, samples, period)` | `SetParameters(DIEP_TYPESPECIFICPARAMS)`; on `DIERR_INPUTLOST` reacquire and retry | `Check` records HRESULT | same | Effect destroyed and recreated per edit, result ignored (X13) | Explicit Apply, status from API | Compile PASSED |
| Device period label | What `DIDEVCAPS.dwFFSamplePeriod` reported | `caps.dwFFSamplePeriod` | `GetCapabilities` | none | FFB device | not shown | `ShowDevicePeriod` | Compile PASSED |

UNKNOWN: whether `DICUSTOMFORCE.dwSamplePeriod` is microseconds or
milliseconds on every driver (ledger entry 16 notes two Microsoft pages
disagree); the DLL treats it as microseconds, consistent with `DIEFFECT`.

## 5. Explorer UI changes (Phase 4)

Within WinForms, no new dependencies, no designer files:

- Hierarchy: devices and global actions on top, live input and capabilities
  left, effects and custom force right, log at the bottom; resizable, DPI
  aware (`SystemAware`, auto-sized rows, fixed slider width to avoid layout
  feedback loops in the scrolling effect list).
- Keyboard and accessibility: every control has an `AccessibleName`; Escape is
  STOP ALL; double-clicking a parameter label resets it to the default.
- Units: every parameter shows its DirectInput range and unit.
- Capability-aware: effect panels are greyed with a reason when the device does
  not enumerate the type; autocenter only for FFB devices.
- Prominent stop: red STOP ALL button, also on Escape, detach, device loss and
  close.
- Honest status: status lines only show API results and `WinErrors.Describe`
  text; no success is assumed.
- No UI-thread blocking enumeration: `EnumerateAsync` runs on a worker with the
  poll timer stopped; device callbacks marshal with `BeginInvoke`.
- Screenshots: BLOCKED (no Windows runtime here).

## 6. Measurements (Phase 5)

| Path | Baseline | Change | Method | After | Checks | Limits |
|------|----------|--------|--------|-------|--------|--------|
| Poll change detection | 4814 ns, 432 B/op (MD5 of marshaled copy) | explicit field-wise `Equals`, cheap hash | `PollChangeDetectionBenchmark` (.NET 8, Linux x64, Release, 200k iterations) | 18.8 ns, 0 B/op | 11 managed tests PASSED | Runtime differs from Mono/IL2CPP; the 250x ratio and zero allocation transfer, the absolute numbers do not. |
| Reflection `ValueType.Equals` (rejected) | 7595 ns | not used | same | n/a | | |
| Per-update native logging | one `LogMessage` per successful `Update*` | removed (failures still log) | code read | unmeasured | | NOT MEASURED |
| Layout re-registration on hot-plug | all devices recreated per event | skip registered layouts | code read | unmeasured | | NOT MEASURED |
| Export lock | none | `recursive_mutex` per call | code read | unmeasured (tens of ns uncontended; a poll blocks for a full `EnumDevices` during a rescan) | | NOT MEASURED |

## 7. Target, build and test matrix

| Target | Build | Tests | Status |
|--------|-------|-------|--------|
| Managed logic, net8.0 on Linux (links `Runtime/DirectInputManager.cs`) | PASSED | 11 PASSED (`dotnet test src~/DirectInputManager.Tests~`) | VERIFIED |
| Explorer, `net5.0-windows10.0.22000.0` x64, cross-compiled with .NET SDK 8.0.425 + `EnableWindowsTargeting` | PASSED, 0 errors, 3 warnings (NETSDK1138 .NET 5 end of life) | none exist | runtime NOT RUN |
| Native helpers (`DirectInputHelpers.h`) | clang 18 and gcc 13 PASSED | `helpers_test` PASSED (both compilers) | VERIFIED |
| Native DLL (MSVC v143, Windows SDK) | BLOCKED (no Windows SDK here) | | shipped DLL stale |
| Unity Editor tests (`Tests/Editor`) | NOT RUN (no Unity) | | asmdefs and sources JSON/C# checked |
| Unity runtime (Input System device, domain reload) | NOT RUN | | |
| Hardware force feedback | BLOCKED (no device) | | section 8 lists the checks |

Reproducible commands (Linux, from the repository root; the SDK locations are
the ones used here):

```
# managed tests
dotnet test src~/DirectInputManager.Tests~/DirectInputManager.Tests.csproj
# Explorer compile check (needs a Microsoft-built SDK with the WindowsDesktop pack)
dotnet build src~/DirectInputExplorer~/DirectInputExplorer/DirectInputExplorer.csproj -c Release -p:EnableWindowsTargeting=true
# native helper tests
cd src~/DirectInputForceFeedback~/tests && clang++ -std=c++17 -Wall -Wextra -I../DirectInputForceFeedback helpers_test.cpp -o helpers_test && ./helpers_test
```

On Windows: open `src~/DirectInputForceFeedback~/DirectInputForceFeedback.sln`,
build Release x64 (post-build copies the DLL into `Runtime/Plugins/DLL`); open
`src~/DirectInputExplorer~/DirectInputExplorer.sln` and run; in Unity open
Window > General > Test Runner for `Tests/Editor`.

## 8. API, configuration and packaging impacts

- Native export `UpdateRampForce` gained a fourth parameter (duration in
  microseconds). Managed and native must be rebuilt together. HYPOTHESIS: an
  old DLL with the new managed code ignores the extra x64 register argument and
  keeps the old (INFINITE) behaviour.
- New exports: `GetFFBEffectStatus`, `GetSupportedFFBEffects`; managed wrappers
  return false with `E_NOTIMPL` and a rebuild hint on an old DLL.
- Managed additions: `SetAutocenter`, `TryGetFFBEffectStatus`,
  `TryGetSupportedFFBEffects`, `LastHResult`, `LastError`,
  `WinErrors.Describe`; `UpdateRampForceNative` optional duration;
  `FlatJoyState2 : IEquatable`. No public member renamed or removed.
- Behaviour changes: condition helper saturation 10000; direction vector;
  hot-plug events live again; failed polls publish nothing; `UpdateEffect` no
  longer mutates the caller's array; ramp plays once per update.
- `package.json` `"unity": "2021.2"`; DLL `.meta` Editor platform OS = Windows;
  test asmdefs reference the test runner; `UNITY_5_3_OR_NEWER` guards.
- Explorer: `Form1.*` deleted, three code-built files added, csproj copies the
  DLL; target framework unchanged (net5.0, out of support: proposal P5).

## 9. Remaining defects, blockers and proposals

Hardware checks that must run before release (all BLOCKED here):

1. Hot-plug: unplug and replug a wheel; `OnDeviceRemoved`/`OnDeviceAdded` and
   the Explorer list must follow (needs the rebuilt DLL).
2. Cooperative level: attach while a game window is in the foreground and
   while another application owns the foreground.
3. After `DIERR_INPUTLOST` (focus loss with another exclusive user) effects
   resume on the next poll.
4. Ramp plays once from start to end over the duration; a second update
   replays it.
5. Custom force on a device that enumerates `GUID_CustomForce`; confirm the
   sample-period unit.
6. Direction on a two-actuator device (force along the steering axis only).
7. Autocenter toggle while attached (unacquire, set, reacquire, effects resume).
8. Unity: domain reload with a device attached, then a hot-plug event (callback
   cleared on `DomainUnload`).

Proposals (not implemented; each changes public behaviour or packaging):

- P1 (M13): in the editor, attach devices on play-mode enter and release them
  on exit instead of holding exclusive background acquisition for the editor's
  lifetime; keep layout registration at load.
- P2 (N12): delete the Win32 configurations from the vcxproj and sln.
- P3 (N13): remove `GetDILastError`/`SetLastErrorMessage` (managed code maps
  HRESULTs itself).
- P4: stop tracking `src~/DirectInputExplorer~/DirectInputExplorer/bin` and
  `obj` (7 files, including a 25 MB SDK DLL); the tracked exe predates this work.
- P5: move the Explorer to a supported target framework (`net8.0-windows`);
  not done because changing target frameworks needs approval.
- P6 (M10/N11): marshal `DeviceInfo` strings as `IntPtr` and decode UTF-8 so
  the Explorer shows non-ASCII names; changes a public struct's field types.

## 10. Design decisions

- Direction vector: (10000, 0, ...) for every actuator count. Repository
  history went from all zeros to all 10000 (commit 8a4e3d7); SDL uses all zeros
  for a steering axis and Microsoft's FFConst sample uses 0 for one axis and a
  unit vector for two (ledger 5, 7, PROVISIONAL). A unit vector along the
  first actuator is valid for every count and keeps the single-axis behaviour
  users have been running since 8a4e3d7. HYPOTHESIS until run on hardware.
- Ramp: finite duration (1 s default) and `DIEP_START` on every update, so an
  update is "play this ramp now". Previous behaviour (INFINITE ramp) could not
  work as a ramp.
- Lock: one recursive mutex rather than fine-grained locking; the DLL is small
  and enumeration (the only cross-thread caller) is rare.
- Custom force sample period 0 keeps the creation value instead of passing 0.
- Tests on Linux link the real source file so the layout and logic assertions
  cannot drift from the shipped code.
- Not changed on purpose: public API names, target frameworks, UI framework,
  dependencies, tracked build output, the DEBUG1 export (now side-effect free).

## 11. Environment notes

Linux container, no Windows, no Unity, no MSVC, no device. The managed builds
used a Microsoft-built .NET SDK 8.0.425 extracted into the session scratch
directory (the WindowsDesktop pack is missing from Ubuntu's source-built SDK);
nothing was installed system-wide. Microsoft documentation hosts were not
reachable; API claims rest on headers (Wine, mingw-w64), the SDL and FFConst
sources, and search snippets, as recorded in `docs/ledgers/`.

## 12. Error-reporting round (2026-10-12)

Input: an adversarially verified review of 76 findings in five areas (74
confirmed; SAMPLES-05 and SAMPLES-23 refuted and not implemented) and research
on Windows and vendor failure modes. "VERIFIED" below means compiled, tested or
read in this Linux container; everything that needs Windows, MSVC, Unity or a
wheel is HYPOTHESIS.

### Notification design

- `DirectInputLogger.Report(severity, title, details)` logs to the Console once
  per report (`LogError`/`LogWarning`/`Log`, prefix `[DirectInput]`) and, in
  Play Mode, queues an IMGUI card for the top-right of the Game view. Callable
  from any thread; cards are drained on the main thread.
- On by default: `DirectInputLogger.Enabled` (`DIManager.showLogsRuntime` maps
  to it). `DIRECTINPUT_DISABLE_TOASTS` compiles the drawing out. Console
  logging stays in both cases.
- Card: title; Show details (operation, device name, GUID, VID:PID, HRESULT
  name and meaning, the DLL's last error, what to do); Copy; repeats merged
  with a count; fades after 6/12/20 s of unscaled time (info/warning/error),
  kept while hovered or expanded; at most 20 stored and 5 shown.
- Rate: a per-frame call reports once per change of (operation, device, effect,
  HRESULT) and once when it works again; repeats only refresh `LastError`.
- Edit mode: `Editor/PluginSelfCheck.cs` reports a plugin that cannot load once,
  with the DLL asset as Console context.
- DESIGN DECISION: IMGUI needs no package, EventSystem, input backend or render
  pipeline. A quiet card area never means force feedback works.

### Coverage

| Class | Case | Signal |
|-------|------|--------|
| Deterministic | DLL cannot load: non-Windows or Arm64 Editor, DLL missing, dependency missing (VC++ runtime, `dinput8.dll`), wrong architecture, DLL older than the scripts | exception type, `LoadLibraryExW` error 126/193 plus per-dependency probe, missing export |
| Deterministic | DirectInput does not start (including `DIERR_UNITYNOTINITIALIZED`) | `StartDirectInput` HRESULT |
| Deterministic | Enumeration failed, as opposed to no controller found | `deviceCount` -1 vs 0 (needs the rebuilt DLL) |
| Deterministic | Another program holds the device exclusively | `E_ACCESSDENIED` / `DIERR_OTHERAPPHASPRIO` |
| Deterministic | Device lost, unplugged or reconnected | HRESULT, hot-plug callback |
| Deterministic | Effect create/update fails: device full, unsupported, incomplete, not exclusively acquired, playing, not downloaded, invalid parameters | HRESULT |
| Deterministic (warning) | Call succeeded but the force is not what was asked: `DI_DOWNLOADSKIPPED`, `DI_TRUNCATED`, `DI_TRUNCATEDANDRESTARTED` | success codes 3, 8, 0xC |
| Deterministic (raw) | Driver-defined `0x800403xx` and HID `0xC011xxxx` codes: shown, meaning not guessed | HRESULT range |
| Heuristic | A wheel (driving type, or joystick/flight from a wheel vendor) that enumerates without force feedback: hint by USB VID (Logitech, Thrustmaster, Fanatec, Moza, Cammus, Asetek) naming vendor driver/software or console mode as likely causes | `FFBCapable`, `guidProduct` |
| Heuristic | `E_FAIL` described with its possible causes | HRESULT |
| Not detectable | Force-feedback strength 0 or off in vendor software, overheat, safety or torque modes, emergency stop, an FFB switch on the wheel, Steam Input | every call succeeds |
| Not detectable | Which program holds the wheel; why no controller is visible (power, hub, filter drivers) | |
| Not detectable | A crash inside the DLL; whether a software stop physically stopped the motor | |

Vendor-software requirements and the vendor hint texts are UNVERIFIED (vendor
sites were unreachable), so the hints say "likely causes", never a diagnosis.

### Fixes by finding id

VERIFIED by reading the diff; runtime behaviour is HYPOTHESIS.

- Native: NATIVE-1 (Poll before reads), NATIVE-2 (Update* return the
  SetParameters HRESULT including success codes; `DI_DOWNLOADSKIPPED` gets one
  reacquire), NATIVE-3 (a failed effect restart is returned), NATIVE-4
  (lost-device logging once per transition), NATIVE-7 (Fanatec duplicate
  probe), NATIVE-8 (DEBUG1 prints hex). NATIVE-6 partial: a failed second pass
  no longer returns no devices; the pass stays, with a `shortcut:` comment.
  NATIVE-5 not changed (severity none). vcxproj: static CRT in all four
  configurations. Review fix: a read error is returned unless Acquire failed in
  the same call.
- Managed and logger: MANAGED-01 to 11 and LOGGER-AND-ERRORS-01 to 18.
  LOGGER-10 is partial: a missing export is reported, `GetPluginVersion` is not
  compared. Review fixes: no per-frame report flood from the samples'
  Enable/Destroy loop; only DLL-load exceptions get the load probe; edit-mode
  enumeration notes suppressed; `[Out]` on the `GetDILastError` buffer.
- Explorer: EXPLORER-1, 3 to 8, 10 and 12. EXPLORER-9 shows the autocenter box
  as unknown until the user sets it. EXPLORER-2 and 11 through DIManager, plus
  the Explorer side of 11.
- Samples: SAMPLES-01 (namespaces; GUIDs unchanged), 02 to 04, 06 to 17, 19 to
  22, 24 to 27. SAMPLES-18 not done: restoring saved enable states at launch
  would start effects. Review fixes: collision jolt runs on real time; a
  disabled NWH provider cannot jolt; UGUI launch stops all effects; Load
  reconnects before applying; `*WasEnabled` flags reset when effects are
  destroyed.
- Integration fixes in the final pass:
  - The 1.0.2 UGUI sample merged into `directinput.unity` through an asmref,
    and that assembly does not reference `UnityEngine.UI`, so the sample never
    compiled (27 errors). It now has its own asmdef `DirectInputSamples.UGUI`
    (references `directinput.unity`, `Unity.InputSystem`, `UnityEngine.UI`;
    Editor and WindowsStandalone64; new GUID
    `e4935e0ac25c43d08932e94158cf0758`). This also drops the asmref GUID the
    UGUI package shared with the 1.0.1 packages.
  - The managed side treated `deviceCount < 0` as "could not be listed, keep the
    list" (MANAGED-07), but native `EnumerateDevices` returned 0 on failure, so
    a failed scan read as "no devices" and a hot-plug rescan detached every
    device. Native now returns -1 on every failure path.

### Still open

1. The checked-in `Runtime/Plugins/DLL/DirectInputForceFeedback.dll` was NOT
   rebuilt. VERIFIED with objdump: it exports 30 functions (no
   `GetFFBEffectStatus`, no `GetSupportedFFBEffects`) and imports `MSVCP140`,
   `VCRUNTIME140`, `VCRUNTIME140_1` and `api-ms-win-crt-*` (dynamic CRT). Until
   it is rebuilt on Windows (VS2022, Release x64, /MT), none of the native
   fixes reach users; the managed side reports the old DLL once.
2. Update* can return 3, 4, 8 or 12 on success. The public wrappers return
   bool (`hr >= 0`), but `DIManager.LastHResult` can hold those values, so user
   code testing `LastHResult == 0` must test `>= 0`.
3. Explorer: after a failed enumeration (rebuilt DLL) it keeps the previous
   list and logs "Enumerated N device(s)"; the failure only reaches
   `Debug.WriteLine`.
4. NATIVE-6 second pass; one Acquire per call per frame while a device stays
   unacquirable; a failed restart on the poll path leaves that effect stopped
   until the next reacquire.
5. Sample GUID collisions across packages (kept by rule): `DIInputManager.cs`
   in all three; `DIInputValueMonitor.cs`, `RuntimeFFBConfigUI.cs`,
   `RuntimeInputMappingUI.cs` and `directinput.asmref` in the two 1.0.1
   packages; the sample root, `Scripts` and `Scene` folders in all three.
   HYPOTHESIS: importing the NWH package and then the Fundamental one can
   overwrite the NWH copies (other namespace) and break `NWHFFB`.
6. NWH Vehicle Physics 2 itself needs Input System 1.1 or later (VERIFIED by
   compiling it), so the NWH sample cannot run on the declared minimum 1.0.2.
   The UGUI sample needs `com.unity.ugui` in the project, as it always did.
7. Hardware: the section 9 checks, plus each card on a real failure,
   `DI_DOWNLOADSKIPPED`/`DI_TRUNCATED` on a real wheel, and the load-failure
   classification on a Windows machine without the VC++ runtime.

### Verification results

VERIFIED, run here after the last edit:

- `dotnet test src~/DirectInputManager.Tests~`: `Passed! - Failed: 0, Passed:
  31, Skipped: 0, Total: 31`.
- `dotnet build src~/DirectInputExplorer~/DirectInputExplorer
  -p:EnableWindowsTargeting=true --no-incremental`: Build succeeded, 0
  Error(s), 3 Warning(s) (NETSDK1138, net5.0 end of life).
- `clang++` and `g++ -std=c++20 -Wall -Wextra -IDirectInputForceFeedback
  tests/helpers_test.cpp`: `all helper tests passed` with both.
- MinGW-w64 `x86_64-w64-mingw32-g++ -std=c++20 -fsyntax-only -Wall -Wextra`
  with scratch stubs for `atlbase.h`/`atlsafe.h`: DirectInputForceFeedback.cpp
  0 errors, 25 warnings (the same 16 distinct messages as before the round);
  dllmain.cpp 0 errors, 2 warnings; pch.cpp 0/0. `-shared -static` link OK;
  32 exports, identical to the pre-round build.
- Unity compile harness (Roslyn, C# 9, .NET Standard 2.1, UnityEngine 2021.3.33
  and UnityEditor 2021.1.14 reference DLLs, Input System 1.0.2 and uGUI built
  from source): Runtime and Editor, PLAYER and EDITOR, 0 errors (2 CS0618
  SafeArray warnings). With the samples extracted from the current `Samples~`
  packages, each sample set alone and all together, PLAYER and EDITOR: 0
  errors (NWHFFB: 3 CS0414 warnings). Exit code 0.
- The same assemblies with `UNITY_2023_1_OR_NEWER` defined: 0 errors.
  `Tests/Editor` against an NUnit stub: 0 errors.
- `.meta`: present for every file and folder under Runtime, Editor and Tests;
  no orphans; 20 GUIDs, no duplicates; no sample GUID equals a repo GUID.
- P/Invoke: the 28 managed declarations (27 entry points) all name exports of
  the source build with matching parameter types. Success codes count as
  success (`hr >= 0`); 3, 8 and 12 are reported once as "only partly applied".

HYPOTHESIS: the MSVC /MT build; every runtime behaviour above; card rendering;
Unity importing the repacked `.unitypackage` and its new asmdef; the harness's
known false positives and negatives (UnityEditor 2021.1 surface, uGUI 2018.4
source, player-flavour engine DLLs).
