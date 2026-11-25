# R2 diagnosis plan — 2026-11-03

Discovery is complete for the input path. No trigger/steering behavior will be changed without evidence linking it to the reported held-trigger failure.

Production path: App.OnStartup -> AppHost.Start -> PipelineRunner.Run/Tick -> SdlPhysicalInput.TryPoll -> PolswidProcessor.Process (profile/commands, HelperEngine.Step, ButtonMapper.Map, continuous steering, helper merge, final R2 suppression) -> PipelineRunner submit gate -> ViGEmVirtualPad.SubmitX360/SubmitDs4 -> SubmitReport.

Confirmed observation: the legacy CSV contains exits after normalized R2 drops below the release threshold for five polls. This does not prove the user kept R2 held during those rows. The CSV also contains test-generated sessions, has no device/backend identity, and calls normalized values raw. It cannot establish a hardware root cause. Baseline: 138 tests pass.

Instrument, without changing control behavior:

- Opt-in, bounded asynchronous JSONL writer; no disk writes, formatting, or blocking queue operations on the polling thread. Unique session file, frame sequence, dropped-frame count, build identity, periodic flush, final summary.
- Raw SDL signed R2 sample before normalization, selected SDL instance/key, mapping, axis availability, connection check and SDL error. This is the SDL API boundary, not a USB/Bluetooth wire capture.
- Every successful/failed poll, including all 24 grace frames; input exceptions; actual processor state versus retained state on invalid frames.
- Profile revision and applied command flags, thresholds, raw/consumed physical state, R2Held/R2Active/r2Down/cs, synthetic intents, mapped/pre-final/final R2.
- Final requested state, submission status, last successfully submitted state, encoded R2 slider/digital value, virtual type/backend, faults and shutdown neutral.

Verification: deterministic tests call the same production tick used by the real thread, with an explicit timestamp. Test A–F, complete output state, invalid frames and loss, faults, command/profile causes, diagnostics overflow/disable/serialization. Exercise the actual private Xbox/DS4 report encoders using intercepted controller interfaces without loading the driver. Clearly distinguish these tests from physical hardware and game verification.

Decision: if hardware root cause remains unconfirmed, publish a separate instrumented self-contained win-x64 Release directory, with a session-only launcher and exact collection instructions. Preserve previous builds, profiles, drivers, and Windows settings.
