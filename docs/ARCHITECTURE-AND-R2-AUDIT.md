# ASTRO POLSWID — architecture audit and R2 diagnostic handoff

Date: 2026-11-03 (Asia/Riyadh). Workspace: `<project folder>`.

**Current status update (2026-11-03):** the user resolved the observed gameplay problem by configuring HidHide to prevent simultaneous physical and virtual input. See [Double Input and HidHide](DOUBLE-INPUT-HIDHIDE.md). This supersedes the unresolved-gameplay status below, without erasing captured SDL/internal transitions or claiming a state-machine fix. The independent late-release Square bug remains unfixed. Preserve the working configuration; no further R2 investigation is requested.

**Historical outcome at the diagnostic build handoff:** a verified diagnostic Release build was ready; the reported held-R2 hardware failure was not confirmed fixed. No speculative trigger or steering fix was applied. At that point an annotated reproduction was still needed. The remaining sections retain that handoff's evidence and verification status; they are not a request to repeat testing or change the now-working setup.

## A. Project understanding

The solution `ASTRO.Polswid.slnx` has four C# projects. Core and Infrastructure target .NET 8; App and Tests target .NET 8 Windows. App is WPF with MVVM, Arabic/English resources, and an asInvoker manifest. There is no Git repository in this supplied directory, so there is no branch, commit history, or trustworthy previous diff. A source inventory is in `artifacts/source-inventory.txt`; copies of the five original input/diagnostic files edited are in `artifacts/source-before`.

Dependencies declared by Infrastructure: SDL3-CS 3.4.18.1, SDL3-CS.Native 3.4.2, Nefarius.ViGEm.Client 1.21.256, Serilog 4.3.1, Serilog.Sinks.File 7.0.0. Tests use xUnit 2.5.3, its Visual Studio runner 2.5.3, Microsoft.NET.Test.Sdk 17.8.0 and coverlet.collector 6.0.0. No new package dependencies were added. Installed SDKs are 8.0.421 and 10.0.302; the unpinned repository selects 10.0.302. The published self-contained runtime reports .NET 8.0.29. Native SDL actually reports 3.5.0 (3005000); the existing Native package's Windows DLL file version also says 3.5.0, despite the package being named 3.4.2. These are observed package contents, not a change of the declared SDL dependency.

### Actual startup and physical-to-virtual path

Directory abbreviations below: App = `ASTRO.Polswid.App`, Core = `ASTRO.Polswid.Core`, Infrastructure = `ASTRO.Polswid.Infrastructure`, Tests = `ASTRO.Polswid.Tests`.

| Stage | Actual implementation and ownership |
|---|---|
| Startup | `App/App.xaml.cs`, `App.OnStartup`: single-instance mutex, `AppHost`, optional pipeline auto-start, `MainWindow`. A new explicit build-info switch exits before this normal startup. |
| Services/settings | `App/ViewModels/AppHost.cs`: logger, `ProfileStore`, settings/profiles, cloned `PolswidProcessor`, SDL input, independently selected DS4/Xbox output or dry run, lifecycle. |
| Polling thread | `Core/Pipeline/PipelineRunner.cs`, `Run` -> `Tick` -> `TickAt`: highest-priority dedicated thread, monotonic clock, poll/process/submit. `TickAt` is the same production implementation exposed internally for deterministic tests. |
| Detection/selection | `Infrastructure/ControllerDetection/SdlPhysicalInput.cs`: `Initialize`, `Enumerate` every 250 ms, `ChooseActive`, `CloseActive`, `DeviceSelector.Choose`. A preferred VID:PID key is exclusive; automatic mode keeps the current instance. New matching virtual IDs are filtered before selection. This filtering does not hide a physical pad from the game. |
| Backend/raw state | `SdlPhysicalInput.TryPoll`: `SDL.UpdateGamepads`, connection/list checks, SDL mapped button/axis reads. SDL selects the underlying device backend. Input is copied into a fresh value-type `GamepadState`. New diagnostics retain the signed SDL R2 sample before normalization and the SDL mapping/instance. |
| Normalization | `Stick`: negative samples /32768, positive /32767. SDL Y signs are inverted so up is positive. `Trigger`: clamp sample/32767 to 0..1. SDL L2/R2 are analog axes; this input implementation does not set their physical digital button bits. |
| Profile/commands first | `Core/Polswid/PolswidProcessor.cs`, `Process`: atomically applies a pending cloned profile and resets dynamic state, then drains queued commands. `HelperEngine.Step` sees physical buttons before mapping, handles emergency stop and helper consumption. |
| Raw bypass | If remapping is disabled, `Process` returns the physical state directly, clears mapper/converter/direction state, and consumes the configured emergency button. R2 can be nonzero here by design. |
| Mapping/physical hold | `Core/Mapping/ButtonMapper.cs`, `Map` -> `UpdateTriggerState`: physical R2 analog/digital state, press/release hysteresis, **five consecutive release polls**, optional Toggle latch, replacement bindings, analog passthrough policy. `R2Held` and `R2Active` are independent of virtual R2. |
| Continuous Polswid | `PolswidProcessor.Process`: `cs = Continuous.Enabled && r2Down`, where Hold uses `R2Held`, Toggle uses `R2Active`. Down is immediate. Square is enabled when monotonic elapsed time reaches `SquareDelayMs` (default 60). No asynchronous delayed Square callback exists. |
| Steering | `Core/Polswid/AnalogDirectionConverter.cs`, `Process`: LEFT X only, deadzone, sensitivity, response curve, optional exponential smoothing and activation/release hysteresis. `DirectionEngine.Resolve`: configured physical D-pad versus analog priority, contradictory horizontal inputs, dwell and optional flip. Neutral defaults to configured continuous fallback, normally Left. In continuous mode with analog enabled, virtual LX/LY are both zero. Right stick stays copied. |
| Merge/final normalized state | `Process`: inject Down, delayed Square and horizontal steering; apply optional helper ForceOff/ForceOn; resolve contradictory D-pad axes; suppress virtual R2 again. The returned `FrameResult.Virtual` is a complete next state. |
| Submission gate | `PipelineRunner.TickAt`: changed states only, bounded by MaxSubmitHz. It records requested output separately from the last successful submission. Failed polls retain state for 24 polls; the 25th resets the processor and requests neutral. Submit faults disconnect and retry after two seconds. |
| Virtual encoding | `Infrastructure/ViGEm/VirtualPads.cs`: `ViGEmVirtualPad.Submit` -> `SubmitX360` or `SubmitDs4`. All controls are set with `AutoSubmitReport=false`, then one `SubmitReport` call. Xbox directions are separate bits. DS4 selects one eight-direction hat value or neutral; Down+Left is Southwest and Down+Right is Southeast. Opposite hat directions cannot coexist. |
| Driver/device | Nefarius client -> existing ViGEmBus -> chosen virtual controller -> consuming program/game. Host submission success is not proof of game receipt. |
| UI observation | `PipelineRunner.Publish` publishes immutable snapshots at up to about 120 Hz. `MainViewModel.Refresh` reads them about every 33 ms. The UI's desired virtual state can differ temporarily from the last driver submission; the new trace records both. |

SDL documents mapped triggers as 0..32767, and also documents that invalid gamepad/axis reads can return zero, which is otherwise a valid value. Therefore zero alone cannot establish a physical release: [SDL_GetGamepadAxis](https://wiki.libsdl.org/SDL3/SDL_GetGamepadAxis). Manual gamepad updates are supported: [SDL_UpdateGamepads](https://wiki.libsdl.org/SDL3/SDL_UpdateGamepads). Installed binding XML was also checked before using diagnostic APIs.

### Remaining source responsibilities

| Files/group | Responsibility |
|---|---|
| Core/Input: `GamepadState.cs`, `IPhysicalInput.cs`, `ControllerInfo.cs` | State, button masks and steering enums/extensions; input contracts/device information; controller family classification and pure selection policy. |
| Core/Profiles: `Profile.cs`, `DefaultProfiles.cs` | JSON schema/settings, deep clones, factory profiles and timing presets. |
| Core/MacroEngine: `MacroEngine.cs` | Queued command types and single-threaded `HelperEngine`: manual enable/disable/emergency controls, user-triggered timed U-turn release and air-switch phases. No independent driving loop. |
| Core/Timing: `HighResolutionTimer.cs` | Stopwatch microseconds, Windows waitable timer and short spin, fallback timer-resolution request, rolling latency statistics. |
| Core/Diagnostics: `Diagnostics.cs`, `R2DiagnosticLogger.cs` | Bounded UI event ring, pipeline snapshots/log abstraction; new opt-in bounded asynchronous frame evidence. |
| Core/Output: `IVirtualPad.cs` | Output backend/type contract. `Core/AssemblyInfo.cs` exposes internal production ticks to the test assembly. |
| Infrastructure/Configuration: `ProfileStore.cs` | Settings/profile load/save/import/export/duplicate/reset, migration, atomic replacement via temp file; invalid JSON quarantine. |
| Infrastructure/ViGEm: `ViGEmDriverProbe.cs`, `VirtualPads.cs` | Existing driver registry/version/reachability probe; Xbox/DS4 encoding, attach/detach and dry-run implementation. |
| Infrastructure/Logging: `SerilogLog.cs` | Rolling structured application logs. |
| App: `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `AssemblyInfo.cs`, `app.manifest` | WPF application/window, resources/startup, language shell rebuild, global hotkeys, screenshot helper, theme metadata and Windows manifest. |
| App/ViewModels: `AppHost.cs`, `MainViewModel.cs`, `Base.cs` | Service lifecycle; commands/profile edits/live monitor/settings/wizard; observable properties, relay commands and display rows. |
| App/Views: `SimpleView`, `ShellView`, `AdvancedView`, `SteeringView`, `MappingView`, `HelpersView`, `ProfilesView`, `DiagnosticsView`, `DeveloperView`, `SettingsView`, `HelpView`, `WizardView` (each XAML and code-behind) | Home/navigation, advanced tabs and editors, input/output displays, profiles/settings/help/onboarding. Code-behind attaches edit commits, handles selection/help rendering and log scrolling; no physical polling implementation. |
| App/Controls: `ControllerView.cs`, `Converters.cs`, `EditHooks.cs` | Drawn interactive controller diagram; binding/localized converters and reusable controls; commit profile on actual UI edits. |
| App/Localization: `Strings.cs`, `Loc.cs`, `HelpContent.cs`; App/Themes: `Dark.xaml`, `Controls.xaml` | Arabic/English resources, text/help selection, styles/templates. |
| Tests | `PolswidProcessorTests.cs` defines the deterministic processor Harness and mapping/analog/direction/helper/safety/profile tests. `ContinuousSteeringTests`, `LeftAnalogCorrectionTests`, `RapidAnalogMovementTests`, `ControlledDiagnosticScenariosTests`, `CompleteInputSystemMatrixTests` cover mostly processor scenarios, with threaded disconnect cases in ContinuousSteeringTests and CompleteInputSystemMatrixTests. `PipelineRunnerTests` and part of `ControllerIndependenceTests` exercise a live thread with fake I/O; the latter also covers classifiers/selection. `LocalizationTests` checks resources/help. New `DiagnosticProductionPipelineTests.cs` exercises the production tick and actual output encoders, plus diagnostic storage behavior. |
| Build/docs | `Directory.Build.props`, four csproj files, slnx, existing README/Arabic README and screenshots. README establishes self-contained single-file win-x64 publish; that deployment shape is preserved. |

Implemented controller support is what SDL recognizes as a mapped gamepad, classified separately as DS4, DualSense, Edge, Xbox One/Series/360 or generic. This is not proof that every HID controller or every advertised feature works. Gyro is only reported as a capability; it is not transformed. Touchpad click exists; touch coordinates, adaptive triggers, paddles and haptics are not independently implemented by this input path. Physical family does not restrict the virtual type.

Profiles live under `%APPDATA%\ASTRO POLSWID\profiles`; settings live in `settings.json`. Profile clones are handed to the pipeline through an atomic pending-profile slot. Commands use a ConcurrentQueue. UI and pipeline do not share their ordinary GamepadState values. The save debounce timer persists the editable profile; it does not control Square timing.

## B. Previous implementation review

All ten historically named source/test files exist. Current source contains the reported R2/virtual separation, double R2 suppression, LX+LY suppression, left-X conversion, neutral fallback, 60 ms Square delay, identity D-pad mappings and profile migration. Baseline execution produced **138 passed, zero failed/skipped**, rather than relying on README's older count of 101.

The common Harness invokes the real processor, but bypasses SDL, PipelineRunner scheduling/submission, backend encoding, the driver and game. Previous tests named for rapid movement primarily vary generated state values; they cannot detect a bad physical SDL report. Existing output-independence tests only compare normalized fake-pad output, not actual DS4 hat/Xbox bit encoding. New tests close the tick/encoding gap, not the hardware/game gap.

Existing workarounds were already present: five-poll trigger release confirmation and 25-poll loss confirmation. Their timing depends on polling frequency (30..4000 Hz). They are not evidence that any physical glitch occurred. No new debounce, sleep, driver replacement or held-state latch was added.

## C. Root cause and available evidence

**The exact cause of the user's held-R2 failure remains unconfirmed.** No new physical manipulation or game test was performed. The archived log lacks device identity, true raw values, read-validity detail and game routing, and contains automated-test sessions mixed with other sessions.

The following are actual archived CSV rows, copied before the new logger replaced it. `RawPhysR2` in that old header is in fact normalized `FrameResult.Physical.R2`. Provenance of an individual session cannot be fully established. These rows are evidence of a reported trigger-condition drop, not proof of an unintended physical release:

| Legacy frame | Normalized R2 | R2Held | Polswid | LX | LY | Requested virtual R2 |
|---|---:|---|---|---:|---:|---:|
| 14897 | .271 | true | active | -1.000 | -.271 | 0 |
| 14898 | .137 | true | active | -1.000 | -.263 | 0 |
| 14899 | 0 | true | active | -1.000 | -.263 | 0 |
| 14900 | 0 | true | active | -1.000 | -.263 | 0 |
| 14901 | 0 | true | active | -1.000 | -.263 | 0 |
| 14902 | 0 | false | normal | -1.000 | -.263 | 0 |

At frame 14902 the mapper has seen five release-zone samples (14898–14902), so its existing filter releases. This sequence does **not** show LX overwriting R2, nor R2 leaking. Ten archived R2Held exit transitions were extracted to `artifacts/evidence/legacy-release-transitions.csv`. Other legacy transitions from test sessions demonstrate deliberate disconnect and raw bypass, and are not presented as hardware faults.

Classification: upstream physical/backend cases A/E remain candidates when a user confirms R2 was continuously held; a genuine physical release would produce this same legacy sequence. There is no confirmed B/C/D failure in these rows, and no proof of F. The physical and game observations still need to be correlated.

### All identified ways to lose activation or see R2

- `ButtonMapper.UpdateTriggerState`: five release-zone polls clear R2Held; Toggle changes its effective state on rising edges. It receives no stick values.
- `PolswidProcessor.Process`: a pending profile resets mapper/continuous/timer state; Continuous.Enabled or TriggerMode determines activation. A profile applied during a hold can restart the Square delay.
- `HelperEngine.Apply/Step`: UI/hotkey commands or a configured physical emergency button can disable remapping. The raw-bypass return then allows physical R2. Default emergency/cancel controller buttons are None. Stick axes themselves do not execute those commands.
- `PipelineRunner.OnPhysicalLost`: after the existing failed-poll grace, resets state and requests neutral. It does not send held physical R2.
- SDL selection/reconnect can replace the selected source; diagnostics now identify the actual SDL instance rather than just its display name.
- Submit rate limiting, output faults, reconnection and game selection can make game-observed state differ from desired processor state. The UI originally displayed desired state without proving the latest report was submitted.
- `AppHost.Start` calls `Processor.ResetAll` on the caller/UI thread after starting the polling thread. This is a real ownership concern, but no evidence connects that startup-only race to rapid steering; it was not patched speculatively.

### Confirmed separate specification gaps, preserved for diagnosis

1. The five-frame filter delays genuine release. A new deterministic test proves that a last held sample 56 ms after activation, followed by release, can still produce synthetic Square at 60 ms before release confirmation. This violates cancellation on any release before the delay. It is **not** classified as the cause of the held-trigger fault.
2. Toggle mode intentionally allows activation after release, contrary to a strict Hold-only interpretation. Disabling analog live allows LX/LY passthrough even during continuous mode. Configurable helpers can force off buttons after the continuous merge. Default Standard settings avoid those latter combinations, but unconditional invariants do not hold for all custom configurations.
3. Migration runs only for schemas older than CurrentSchema=3, and it enables Continuous on every migrated profile; it does not preserve every old profile's mode. A schema-3 file is not migrated again. No profile files were loaded through AppHost or edited during this work.
4. Repeated thrown input exceptions are caught by the original run loop without advancing the failed-poll loss counter. Output can remain stale in that path; the new trace records those exceptions. Failure retention remains frame-based rather than an elapsed-time policy.

These gaps are recorded for follow-up, without pretending that unrelated changes resolve the reported physical fault.

## D. Changes made

| Changed/added file | Purpose |
|---|---|
| `Core/Diagnostics/R2DiagnosticLogger.cs` | Replaces always-on synchronous malformed CSV with opt-in JSONL; unique sessions, 8192-frame bounded Channel, nonblocking TryWrite, background serialization/writes/flushes, drop count, storage-error isolation and bounded shutdown drain. |
| `Core/Input/IPhysicalInput.cs` | Adds optional raw-boundary diagnostic contract without changing IPhysicalInput semantics or requiring fake/other backends to implement it. |
| `Infrastructure/ControllerDetection/SdlPhysicalInput.cs` | Captures the same SDL R2 sample used by normalization, controller instance/key/mapping, axis availability, post-read connectivity and SDL error when enabled. No change to selected controls or normalization. |
| `Core/Polswid/PolswidProcessor.cs` | Adds observational fields for profile revision, applied command bitmask, consumed buttons, mapped and pre-final R2; control transformation is unchanged. |
| `Core/Pipeline/PipelineRunner.cs` | Records every poll including grace/loss/exception frames and post-submit status/last successful output; internal explicit-time tick used by the production thread and deterministic tests. |
| `Core/AssemblyInfo.cs` | Grants test assembly access to that internal tick. |
| `App/App.xaml.cs` | Adds `--r2-build-info <file>` for non-invasive packaged runtime/native dependency/build identity validation, before normal application startup. |
| `Tests/DiagnosticProductionPipelineTests.cs` | 29 new executable cases: 28 traverse the real tick and production encoders with intercepted SDK interfaces; one tests disabled logging/overflow directly. No driver is opened by those tests. |
| `docs/R2-DIAGNOSTIC-PLAN.md`, this report and artifacts | Diagnostic plan, evidence extracts, source inventory/snapshots, TRX reports and build provenance. |
| New release folder | Executable/PDBs, diagnostic launcher, test instructions and build manifest. Existing release folders were preserved. |

Diagnostics are disabled when opening the EXE normally. The launcher sets process-local environment variables; it does not change persistent Windows settings. Physical axes, device instance and mapping are controller data; no unrelated personal data is intentionally recorded. Settings, profiles, drivers and GUI behavior were preserved.

Trace interpretation: top-level `Frame.Physical` is the current read only when ReadOk=true. `Processing` on a failed read is retained prior state during grace, with Processed=false; it is not evidence of a fresh held sample. RawR2=null means unavailable, not zero. The scalar RawR2 is an SDL mapped sample, not a USB/Bluetooth HID capture. `Processing.ContinuousActive` is the synthetic Down intent; `ContinuousSquareActive` is Square intent; `Steering`/`Owner` identify selected steering. Compare these with final Virtual.Buttons to detect helper/merge overrides. The actual SDL backend does not set a physical digital R2 bit; use the analog thresholds and internal hold fields.

`AppliedCommands` is a bitmask of `1 << MacroCommandKind`: EnableRemapping=1, DisableRemapping=2, ToggleRemapping=4, EnableAnalog=8, DisableAnalog=16, ToggleAnalog=32, EmergencyStop=64, ClearEmergency=128, TriggerUTurn=256, TriggerAirSwitch=512, CancelHelpers=1024. ProfileRevision starts at zero and increments when a pending profile is actually applied. Transition reasons describe software conditions, not a claim about what the user's finger did.

ReportStatus distinguishes Submitted, DryRunAccepted, Unchanged, RateLimited, Unavailable, SubmitFault, InvalidPollGrace, InputLost, InputException and Shutdown. For loss/shutdown, neutral is requested; HaveSubmitted and LastSubmitted show whether it succeeded. Encoded R2 slider/button fields are derived from the last accepted normalized state according to the tested encoders, not read back from the game/driver. A missing End record or nonzero DroppedFrames means evidence may be incomplete.

## E. Test results

| Verification layer | Status | Evidence/limits |
|---|---|---|
| Original automated suite | PASSED | Baseline: 138/138, `artifacts/audit-tests/baseline.trx`. Includes processor, profiles, localization and original fake-I/O threaded tests. |
| Final unit/integration suite | PASSED | 167/167, zero failed/skipped; `artifacts/audit-tests/final.trx`. |
| Production pipeline | PASSED | Real TickAt: R2 only, slow/normal/rapid reversals, neutral/deadzone crossing, vertical/diagonal movement, either/both sticks, Square/Down continuity, R2 suppression, release/passthrough and early quick release, for both output types. Trace test covers 24 invalid polls, neutral on 25, recovery, profile reset, emergency command, submit fault and thrown input exception. Original threaded tests still pass. |
| Virtual backend encoding without driver | PASSED | Actual SubmitX360/SubmitDs4 methods invoked against intercepted IXbox360Controller/IDualShock4Controller interfaces; complete control writes and one SubmitReport, R2 slider/digital suppression, Down and supported diagonals checked. This tests calls into the SDK, not its native wire serialization or the bus. |
| Diagnostic integrity | PASSED | JSON round-trip including comma-containing mappings, raw versus normalized values, all grace frames, cause fields, end marker, no drops in normal trace, disabled/no-file mode, observable overflow and isolated storage failure. Synthetic traces in `artifacts/evidence/synthetic-pipeline-*.jsonl` explicitly identify testhost and synthetic input. |
| Strict late-release cancellation specification | FAILED by existing behavior | Two passing characterization tests explicitly demonstrate the violation; they do not certify compliance. No fix claimed. |
| Published executable startup/dependencies | PASSED | Build-info exit 0; loaded bundled .NET, native SDL and ViGEm managed client; embedded build IDs checked against latest compiled assemblies. |
| Live ViGEmBus output/driver receipt | NOT EXECUTED | No live driver/device report round trip was tested. |
| Physical hardware A–F | NOT EXECUTED | Cannot physically manipulate the controller; archived logs are not a fresh annotated reproduction. |
| In-game behavior | NOT EXECUTED | No claim of CTR verification. |

Commands executed: `dotnet test ASTRO.Polswid.slnx -c Release --logger "trx;LogFileName=final.trx" --results-directory artifacts/audit-tests`; `dotnet build ASTRO.Polswid.slnx -c Release --no-restore`; and the publish command below. A new test's initial normal-mode expectation omitted the mapper-derived L2 bit; the test expectation was corrected and the complete suite rerun. Final tests all pass; initial failed attempts are not counted as passes.

## F. Build results

- Configuration: Release; framework: net8.0-windows; target runtime: win-x64; self-contained, single-file, native libraries included for extraction, matching the established README deployment command.
- Filename: `ASTRO POLSWID.exe`.
- Exact EXE: `<project folder>\release-r2-diagnostic-20261103\ASTRO POLSWID.exe`.
- Publish directory: `<project folder>\release-r2-diagnostic-20261103`.
- Full solution Release build succeeded (0 errors; final incremental build 0 warnings). Recompilation during publish/tests reports two pre-existing CA1416 warnings in ViGEmDriverProbe.cs: Windows registry APIs in the platform-neutral Infrastructure target. Publish succeeded with zero errors. No new compiler warning was introduced.
- EXE size: 166,558,596 bytes. SHA-256: `2968F8BEAB8F01E455C88135ACCE6DEA6E171F3C2A88AEE6448F6B4395078F54`.
- Embedded build IDs/runtime details: `artifacts/published-build-info.json`; checksum/source manifest accompanies the new release. PDBs are included; managed/runtime/native dependencies are bundled into the EXE. The build-info smoke test actually loaded native SDL and the ViGEm client.
- ViGEmBus must already be installed for live output; self-contained .NET does not include a kernel driver. Existing logs report an earlier installed/reachable driver, but this session's smoke check does not probe the bus. No driver was installed, replaced or configured.

Publish command:

```text
dotnet publish ASTRO.Polswid.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release-r2-diagnostic-20261103
```

## G. Remaining limitations

The main held-trigger fault still requires raw-boundary evidence during a confirmed continuous physical hold. Diagnostics can localize loss at SDL/normalization/mapper/activation/output boundaries; they cannot prove finger position or identify a lower-level HID fault from an SDL zero alone. If SDL is already wrong, a later targeted backend/report capture may be necessary. No new driver/hiding layer or system setting was introduced.

The trace uses a finite queue and can drop frames under severe storage stalls. It formats/writes only off the polling thread, but tracing still consumes background CPU and disk bandwidth; full-rate logging should be used for short reproductions. There was no physical latency benchmark. Existing synchronous UI-transition/application logs remain unchanged. Abrupt process termination may lose buffered evidence.

## H. Exact user test and return procedure

See `release-r2-diagnostic-20261103/TEST-R2.txt` for the short procedure. Close all older app instances, double-click **Start-R2-Diagnostics.cmd**, keep the Standard profile in Hold mode with continuous/analog steering enabled, and confirm the selected physical and virtual controllers and remapping status. Existing user profiles/settings are reused, not reset.

Run A: R2 only; B: held R2 with slow/normal/rapid LEFT movement, neutral/deadzone crossings, vertical/diagonal/full reversals; C: right stick; D: both; E: genuine R2 release while steering; F: release before the Square delay. The expected held-state invariant is active mode, Down continuously, Square after its delay, zero virtual R2, zero virtual LX/LY, normal right stick. Note the separate known late-release timing gap.

When the original fault occurs, note the approximate seconds since launch, which scenario/output/connection was used, and explicitly whether R2 remained physically held. Continue the physical hold for about two seconds to capture aftermath, then release. Close the app normally and return the entire **diagnostics** folder beside the new EXE, plus those notes and the selected profile exported through the app if customized. Do not return the old mixed CSV as a substitute for the new session JSONL. Return logs even if the fault does not reproduce, and say that it did not occur. Driver and game observations remain manual verification.
