# HidHide-compatible discovery — implementation and verification

Historical report for the refresh/guidance build. The subsequent [official .NET API integration](HIDHIDE-OFFICIAL-API.md) adds real configuration inspection and a user-confirmed exact-path authorization action; refer there for current behavior.

2026-11-03. This update adds physical-controller refresh and setup guidance; it does not grant access to a hidden device. **The exact new executable must be authorized manually.** Keep the user's successful Double Input isolation in place.

## Actual backend and reason a controller can be missing

`App.OnStartup` creates `AppHost`, which constructs `SdlPhysicalInput` inside the ASTRO POLSWID process. `PipelineRunner` initializes and polls it on its dedicated thread. SDL3's Gamepad API is the only physical backend used directly by this codebase; SDL chooses its own HIDAPI/XInput/other supported transport. There is no separate GameInput backend and no separate input-reading helper executable. Physical input is normalized into `GamepadState`, processed by `PolswidProcessor`, and submitted as a complete report through the existing ViGEmBus Xbox/DS4 adapter.

HidHide authorization is application-path dependent. A hidden controller need not appear to an unauthorized process. Moving or publishing ASTRO POLSWID to a different folder does not carry an old executable's authorization to the new path. The app cannot solve this by ordinary enumeration or refresh and never bypasses the filter. For the supported allow-list setup, see the [official HidHide guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/).

Missing input can also mean the device is disconnected, SDL has no gamepad mapping, a saved primary is absent, or SDL cannot open it. No controller is not proof that HidHide caused the problem. The screen deliberately says configuration is unverified rather than claiming “HidHide authorized.”

## Read-only configuration inspection

The installed official CLI was located at `C:\Program Files\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe`. Its own help was read first. Then only `--cloak-state --inv-state --app-list --dev-list --cancel` was executed, outside the sandbox after its initial access-denied result. The inspection reported hiding enabled, inverse mode off, Sony physical-device instances hidden, and no `ASTRO POLSWID.exe` path in the application list. Unrelated application entries and full device identifiers are not reproduced here.

This is a configuration snapshot, not a live access test of this build. No registration, unregister, hide, unhide, enable, disable, clean, or other mutation command was used. Runtime application code does not launch the HidHide CLI or query its driver. CLI usage and its exclusive-client limitation are described in the [official CLI reference](https://docs.nefarius.at/projects/HidHide/CLI-Usage/).

## Changes

| File | Change |
|---|---|
| `ASTRO.Polswid.Core/Input/IPhysicalInput.cs` | Optional `IControllerDiscoveryControl` for refresh/error reporting; optional origin metadata on device records. Existing physical-input implementations remain compatible. |
| `ASTRO.Polswid.Core/Input/DeviceDiscoveryCatalog.cs` | Collapse repeated SDL instance IDs or equal case-insensitive device paths; retain an open alias; preserve keys across enumeration order changes/replug with the same path during this input instance's lifetime. Never merge two controllers just because their model/VID:PID matches. Virtual entries do not take physical selection slots. |
| `ASTRO.Polswid.Infrastructure/ControllerDetection/SdlPhysicalInput.cs` | UI refresh becomes a nonblocking request consumed on the polling thread. Automatic enumeration remains every 250 ms; selection/polling/reconnect remain with SDL. Enumeration failures preserve the prior inventory and publish an error; open failures publish SDL's reason. Successful retry clears errors. The native enumeration boundary is injectable internally for tests. |
| `ASTRO.Polswid.Infrastructure/ControllerDetection/WindowsControllerOrigin.cs` | Read-only device-interface and parent-node queries distinguish recognized USB/Bluetooth ancestry from ViGEm ancestry. This prevents the old same-VID:PID heuristic from treating a newly visible physical DS4 as virtual output. The query applies only to devices SDL already exposes; it does not discover or open filtered devices through a second path. Known results are cached per SDL instance. |
| `ASTRO.Polswid.Infrastructure/AssemblyInfo.cs` | Internal discovery boundary available to tests. |
| `ASTRO.Polswid.App/ViewModels/MainViewModel.cs` | Real Refresh Controllers command, exact `Environment.ProcessPath` and copy action, current discovery error and conditional missing-controller guidance. Refresh does not restart the virtual pad or alter selection/settings. It is enabled while the input engine is running. |
| `ASTRO.Polswid.App/Views/SimpleView.xaml`, `Localization/Strings.cs` | Existing physical card shows backend, refresh and error/help text. Optional collapsible setup instructions in Arabic/English show the running executable path. Existing virtual-controller type/status controls are reused. |
| `ASTRO.Polswid.Tests/ControllerDiscoveryTests.cs` | Twelve new test cases for production discovery logic, selection, reconnection, errors, filtering, duplicate identities and Windows origin classification/API failure. |

Device-origin queries use documented Windows Configuration Manager APIs: [interface properties](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_device_interface_propertyw), [device node lookup](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_locate_devnodew), [parent lookup](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_parent), and [instance IDs](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_device_idw). They do not modify devices or protected driver internals. If ancestry cannot be resolved, origin stays Unknown and the existing conservative virtual-filter heuristics remain in effect.

Mapper, Polswid processor, both steering engines, polling pipeline, helpers and virtual encoders match their prior published source hashes. No R2 threshold, delay, latch, state machine, analog normalization, profile or output transformation changed. The independent late-release Square defect remains unfixed and separately documented in [Double Input troubleshooting](DOUBLE-INPUT-HIDHIDE.md).

## Tests and build

- **PASSED:** full suite, 179 passed / 0 failed / 0 skipped. Previous 167 tests remain; 12 discovery cases added. Result: `artifacts/hidhide-compatibility/hidhide-final.trx`.
- **PASSED:** production discovery refresh executes its injected enumeration only on the owner thread; hidden/absent empty inventory is not labeled authorized; newly visible devices appear after request; timed discovery, saved primary/reorder/reconnect, error/retry, exact-identity duplicates, own virtual exclusion and same-model physical/virtual distinction are covered. The Windows invalid-interface test executes the documented native API failure path. These tests do not impersonate a live authorized hidden device.
- **PASSED:** existing production pipeline and actual Xbox/DS4 encoder-call tests, plus Arabic/English localization and XAML resource-key checks. Visual interaction/layout with the new controls was not manually exercised.
- **PASSED:** full Release solution build. Final incremental build: 0 errors, 0 warnings. Publish recompilation: 0 errors and the two pre-existing CA1416 registry-platform warnings in ViGEmDriverProbe. No new analyzer warnings remain.
- **PASSED:** published EXE build-info smoke check, exit 0. All three embedded assembly IDs match latest Release binaries; bundled .NET 8.0.29, native SDL and the ViGEm client load. This mode exits before settings/input/driver startup.
- **NOT EXECUTED:** physical polling through HidHide from the new allowlisted EXE, a live ViGEm output round trip, or in-game verification of this build. Configuration inspection alone is not a compatibility test. The user's earlier successful HidHide gameplay test remains valid for that previous configuration/build.

Release: self-contained Windows x64, net8.0-windows, single-file/native extraction, preserving the existing deployment scheme. EXE:

```text
<project folder>\release-hidhide-20261103\ASTRO POLSWID.exe
```

The executable SHA-256, build IDs and source hashes accompany it in `BUILD-MANIFEST.json`. All earlier release directories were preserved. ViGEmBus must already be installed for virtual output. No drivers or machine settings were installed or modified.

## Manual setup and acceptance test

1. In the existing HidHide Configuration Client, open **Applications** and add the exact new EXE path above to the normal allow-list. Keep existing working entries. Only the standalone ASTRO executable needs this app's input permission; there is no separate helper to add. No generic interpreter authorization is recommended.
2. Keep the original physical controller selected in **Devices**, and leave **Enable device hiding** enabled. Keep the intended virtual controller accessible to the game; do not grant the game/emulator direct physical access. Existing inverse mode was inspected as off; do not reconfigure unrelated settings.
3. Launch the new EXE. Its optional HidHide guidance displays its real running path, which can be copied. Ensure the engine is running, click **Refresh Controllers**, select the physical pad if needed, and check its name/status/backend. If newly granted access is not reflected by SDL, close and restart this app with hiding still enabled.
4. With the game closed or idle, confirm the physical monitor responds, unplug/reconnect the same pad, and refresh. The selected primary should return when its path is identifiable; no own virtual output should appear as a selectable physical source. A missing explicit primary must not silently select a different pad.
5. Confirm the intended virtual controller is connected and selected by the game/emulator. Hold R2 and steer rapidly with LEFT X: Down and delayed Square should remain, neutral should default Left, virtual R2 and LX/LY should remain suppressed, and the right stick should pass through. Release R2 and confirm normal mode. Check gameplay as well as the app monitor.
6. If the physical pad is absent, record the exact app path, selected primary, connection type and displayed error. Check the supported allow-list manually; refresh/restart does not grant permission. Do not disable HidHide or unhide the physical pad as a workaround.

## Limits

SDL exposes only the devices available to this process and supported by its gamepad mappings. It may retain backend discovery/access state until application restart after an authorization change. There is no new raw-HID fallback or backend replacement. Duplicate removal is intentionally limited to a shared SDL instance or exact path; unrelated aliases with different paths are not guessed to be one device. Identical controllers without usable paths, or after restarting/reordering their legacy VID:PID keys, may require manual reselection. Unresolvable origin metadata retains the legacy virtual-filter limits, rather than claiming perfect device identification. Neither SDL enumeration nor HidHide installation proves which device the game consumes.
