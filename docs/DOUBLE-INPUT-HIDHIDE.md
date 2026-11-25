# Double Input and HidHide — user-confirmed resolution

For the subsequent controller-refresh and path-specific guidance implementation, see [HidHide compatibility](HIDHIDE-COMPATIBILITY.md). The review below records the earlier documentation-only task; the later feature adds UI guidance without managing HidHide or changing Polswid processing.

Updated 2026-11-03. **The user reports that configuring HidHide resolved the persistent in-game R2/Polswid problem.** The game had access to both the physical controller and ASTRO POLSWID's virtual controller. This is the confirmed gameplay resolution; no defect in the R2 state machine was established as its cause. Preserve the working configuration. No further R2 investigation or processing change is required for that resolved symptom.

## Why it looked like Polswid deactivation

ASTRO POLSWID reads the physical pad through SDL, transforms its state, and sends a virtual report through ViGEmBus. Creating a virtual pad does not by itself stop the game from reading the physical pad. If the game or its input host also uses that physical device, it can receive original R2 and analog inputs alongside the transformed output. Depending on device selection and routing, gameplay can look like Normal Mode even when the virtual report is correct.

The intended route is:

```text
Physical controller → ASTRO POLSWID → ViGEmBus virtual controller → game
Physical controller ── direct access blocked for the game/input host
```

The application's virtual-device filter prevents its own output from being selected as physical input. It does not control which devices the game can open. A connected virtual pad, a correct Live Monitor, and passing processor tests therefore cannot prove correct game routing.

## Visibility requirements

HidHide controls which applications can access selected devices. In its normal allow-list configuration, a permitted feeder application can access the hidden physical pad while other applications cannot. This is the mechanism relevant here. See the [official setup guide](https://docs.nefarius.at/projects/HidHide/Simple-Setup-Guide/).

For this project, the required result is:

| Consumer | Physical controller | Intended virtual controller |
|---|---|---|
| ASTRO POLSWID | Accessible so SDL can read it | Excluded from physical-source selection by the app |
| Game or emulator hosting CTR:NF | Direct input excluded | Accessible and selected for gameplay |

Do not hide the intended virtual output or remove ASTRO POLSWID's physical-device access. Authorization refers to the executable path: a build launched from a different folder may need separate permission. USB/Bluetooth device instances can also differ. These are considerations for a future move or connection change, not instructions to alter the configuration that already works. HidHide also has an inverse-list mode, so the presence of a list entry alone does not establish effective access. See the [official FAQ](https://docs.nefarius.at/projects/HidHide/FAQ/).

ASTRO POLSWID does not install or manage HidHide. This documentation update does not inspect or change the user's installed driver, cloak state, application list, device list, permissions, or Windows settings. Any future automatic management requires explicit user approval.

## Verify behavior without disturbing the working setup

1. Launch the same ASTRO POLSWID executable already authorized in the working configuration. Confirm its physical panel receives input and its intended virtual output is connected.
2. In the game or emulator's existing controller/input configuration, confirm the virtual device is selected and direct physical-device input is excluded. Inspect the program that actually consumes controller input; a launcher's device list alone may not describe the game or emulator's route.
3. In the app monitor, hold R2 and move the LEFT stick rapidly across neutral and left/right. Check Down, delayed Square, horizontal steering, zero virtual R2 and suppressed virtual LX/LY; the right stick should pass through normally under the intended Standard/Hold configuration.
4. Confirm the same sequence behaves correctly in gameplay, then verify genuine R2 release restores normal control. Record app state and game behavior separately if any future discrepancy appears.

A Windows controller listing or ASTRO POLSWID's device list describes that observer's view. It is not proof that every other process sees the same devices. The user has already performed the decisive gameplay check and reported success after HidHide configuration; no disable/re-enable experiment or driver reconfiguration is requested.

## Historical diagnostic findings remain separate evidence

The saved analysis is `artifacts/r2-followup/capture-analysis.json`, derived from the preserved `captured-session.jsonl` snapshot of session `r2-20261103-123407-0b74634b685a4992a31ea1a835e0410d.jsonl`.

| Captured frame | Time from first input frame | SDL R2 / normalized R2 | R2Held / R2Active / Polswid | LX / LY | Requested and last-submitted virtual R2 |
|---|---:|---|---|---|---|
| 2599 | 5.492402 s | 0 / 0 | true / true / active | .607837 / .074524 | 0 / 0 |
| 2600 | 5.494402 s | 0 / 0 | false / false / normal | .725486 / .082367 | 0 / 0 |
| 17071 | 34.436401 s | 0 / 0 | true / true / active | -1 / -.380383 | 0 / 0 |
| 17072 | 34.438402 s | 0 / 0 | false / false / normal | -1 / -.388226 | 0 / 0 |

These are actual internal transitions, previously identified by the user as occurring during an intended continuous physical hold. The captured mapped SDL R2 was already below the release threshold; the fifth consecutive release-zone sample cleared the mapper's hold state. Connectivity remained true, normalization matched the raw SDL sample, no command/profile reset was recorded, and virtual R2 remained suppressed. SDL's mapped axis is not a wire-level physical sensor capture. The other exits at approximately 12.61, 25.65 and 53.38 seconds remain unclassified as intentional or unintentional releases.

The frozen snapshot contains 106,678 complete frames, no sequence gaps and no reported drops; it has no End record and ends with one incomplete JSON record. It was copied while the original file was growing. These limits do not erase the complete event windows, but they prevent claiming a fully closed session.

**HidHide's successful gameplay result does not explain or erase these SDL/internal transitions.** Conversely, those transitions do not establish what device the game was consuming. There is no paired post-HidHide trace demonstrating that SDL's samples changed, and none is required to accept the user's resolution of the gameplay symptom. The earlier inference that fixing the observed gameplay required a backend or R2 code change is superseded. No backend defect, output R2 leak, or state-machine root cause is asserted.

## Review of investigation changes

Current source hashes match the diagnostic release's `BUILD-MANIFEST.json`. The interrupted follow-up added analysis scripts, a snapshot/summary and a JSON replay fixture, but did not change application or test source or implement a Square fix.

| Work | Classification and disposition |
|---|---|
| `Core/Diagnostics/R2DiagnosticLogger.cs` | Opt-in diagnostic instrumentation: bounded asynchronous JSONL replaces synchronous CSV; disabled for normal launches. Retain for targeted future diagnostics. |
| `Core/Input/IPhysicalInput.cs`, `Infrastructure/ControllerDetection/SdlPhysicalInput.cs` | Optional input-boundary evidence: SDL sample, mapping/instance, connectivity and errors. Retain; no visibility management or input transformation fix. |
| `Core/Polswid/PolswidProcessor.cs` additions | Observational fields for commands, profile revision and R2 at merge boundaries. Retain; no R2/steering behavior change. |
| `Core/Pipeline/PipelineRunner.cs`, `Core/AssemblyInfo.cs` | Poll/submit evidence and an explicit-time entry into the production tick for tests. Retain; no new release delay or activation latch. |
| `App/App.xaml.cs` build-info switch | Non-invasive packaged-build identity/dependency check before normal settings/controller/driver startup. Retain. |
| `Tests/DiagnosticProductionPipelineTests.cs` | Useful production-tick and actual encoder-call tests, plus diagnostic integrity tests. Retain. Passing tests verify their assertions, not game routing. |
| `Tests/Fixtures/confirmed-r2-exits.json` | Captured windows prepared during the interrupted follow-up. Preserved as evidence; currently not connected to an executable test. Do not describe it as a passing regression test. |
| Existing five-poll trigger release and 25-poll input-loss filters | Pre-existing policies, not new investigation fixes. Their presence does not prove a physical glitch. Preserved without tuning or endorsing them as a Double Input solution. |

No speculative R2 patch or new delay was introduced during the investigation. No rollback is justified by the routing resolution alone. Normal launches keep tracing disabled; the separate diagnostic launcher remains optional and full-rate traces can be large.

## Independent issues and integration assessment

The **late-R2-release Square activation bug remains independently confirmed and unfixed**. In `DiagnosticProductionPipelineTests.Existing_late_release_filter_can_activate_square_after_physical_release`, releasing at 58 ms can still allow Square at the 60 ms deadline. `ButtonMapper.UpdateTriggerState` waits for five release-zone polls; `PolswidProcessor.Process` uses that held state for the pending Square timer. The existing tests characterize this violation; they do not certify correct cancellation. HidHide does not change that application-level timing. No fix is made in this documentation task.

The earlier audit also records startup cross-thread reset ownership, stale-output handling after repeated thrown input exceptions, and profile-migration policy concerns. These are separate source-level concerns with different evidence and scope, not established causes of this resolved gameplay issue. Configurable Toggle/analog/helper behavior likewise should not be mislabeled as a newly demonstrated Double Input defect.

Optional help text is practical: “If gameplay differs from the virtual monitor, check whether the game also receives direct physical-controller input. See Double Input troubleshooting.” It could live on the Help/Diagnostics page without querying or managing HidHide. **No UI change is made now.** Both devices being visible to ASTRO POLSWID is not reliable proof of a conflict, because an authorized feeder is expected to see the physical pad. The app cannot infer every game's or emulator's effective input route. An automatic warning must not claim detection from device count or driver installation alone.

Verification for this update: documentation and read-only source/manifest review only. No source changes, test run, build, publish, configuration change, or new hardware/game test was performed. The previous automated run remains 167 passed; the new gameplay resolution is explicitly user-tested evidence.
