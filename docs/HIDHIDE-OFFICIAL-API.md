# Official HidHide library integration

2026-11-03. This supersedes the previous guidance-only build. The application now references the official **Nefarius.Drivers.HidHide 3.4.0** NuGet package. No driver was installed. After the user explicitly approved the exact Release EXE path, that one path was added through the installed official CLI and verified through the .NET library. Hiding, inverse mode and the hidden-device list matched the pre-authorization snapshot.

The user's screenshot shows DualSense Edge selected for hiding. The [HidHide repository](https://github.com/nefarius/HidHide) describes access through application authorization; [issue 5](https://github.com/nefarius/HidHide/issues/5) concerns switching between allow-list and deny-list behavior. It does not provide a mechanism to read a filtered controller without authorization. The [.NET wrapper](https://github.com/nefarius/Nefarius.Drivers.HidHide) exposes configuration access, not controller input reports. SDL continues to read actual input after HidHide permits this executable.

## What changed

- `HidHideAccessService` uses the official `HidHideControlService`: installation/interface availability, active hiding, inverse mode, application paths and blocked instance IDs. It evaluates normal/inverse policy and reports errors as unverified, not as absence or successful authorization.
- **Check HidHide** reads that state asynchronously, outside the polling thread. DualSense Edge is recognized in the configured hidden list by VID 054C / PID 0DF2. The displayed count includes configured instances and does not prove they are currently connected or readable. Blocked instances are not injected into the selectable SDL inventory as phantom controllers.
- **Allow this executable only…** displays a confirmation with the exact running EXE path. Only after confirmation may the official library add that path. The service refuses disabled hiding, inverse-list mode, unverified access, missing/invalid EXE paths, or absence of explicit approval. It does not set the global cloak state, inverse mode or hidden-device list, and does not clear application lists. Already-listed paths produce no write. Access is read back after the operation rather than assumed successful.
- UI, localization and startup diagnostic code were updated. `--hidhide-status <file>` is a read-only packaged diagnostic that exits before settings, SDL polling or ViGEm creation. No actual allow-list mutation was executed as a test; write-path tests use a fake official interface.

## Verification and limitation

The full automated suite passed **189 tests** after the final wording change; the final run is recorded in `artifacts/hidhide-api/hidhide-api-final.trx`. Ten added cases cover normal/inverse policy, Edge-entry identification, unavailable/busy driver, approval enforcement, refusal under unsafe modes, and an exact-path append that preserves fake configuration entries. Existing input/output behavior tests remain intact. Native/controller/game behavior is separate from those tests.

A first real read-only query through the packaged official library returned **Access denied (5)** when opening the control device; the installed CLI returned the same error. After the user closed HidHide Configuration Client, the library query succeeded: Installed=true, Active=true, Inverted=false, four configured Edge entries, and the new executable was not listed/permitted. This before/after result is consistent with the [documented exclusive control-device access](https://docs.nefarius.at/projects/HidHide/API-Documentation/). The initial errors are retained rather than described as successful queries.

After the user's explicit approval, only `<install folder>\ASTRO POLSWID.exe` was registered with `HidHideCLI.exe --app-reg`. The official .NET library then reported ApplicationListed=true and ApplicationAllowed=true. Active/inverse states and every blocked instance ID matched the prior snapshot. The UI's authorization button was not used for this live change; its official-library write behavior is covered by fake-interface tests.

The newly authorized standalone EXE was then launched. Its actual live UI reported **Connected: DualSense Edge Wireless Controller** through SDL3 and **Connected: DualShock 4** virtual output, with virtual R2 suppressed at 0%. Clicking the read-only HidHide check in that same UI confirmed hiding enabled, inverse mode off, and policy permission for the running path. Evidence: `artifacts/hidhide-api/live-after-client-close.json`, `live-after-approved-allow.json`, and `live-ui-accessibility.txt`.

This verifies live discovery/poll success of the hidden physical controller by the authorized process, not merely a configured hidden-device entry. Manual stick/trigger manipulation, sustained R2 behavior, game receipt and in-game technique execution were not tested by the agent. The application is left open for the user. No changes were made to R2 mapping, steering, Square timing, the Polswid state machine or virtual report composition. The independent late-release Square issue remains unchanged.

## Use this build

1. Close HidHide Configuration Client while leaving its hiding configuration enabled.
2. Run `release-hidhide-api-20261103/ASTRO POLSWID.exe` and expand HidHide setup guidance on the home screen.
3. Click **فحص HidHide / Check HidHide**. Read the permission result and configured Edge-entry count. An access-denied/busy result remains unresolved; do not interpret zero entries in an unverified result as no hidden controller.
4. This exact Release path has already been authorized with the user's explicit approval; no repeat grant is needed. If a future build is moved to a new path and is blocked, click **السماح لهذه النسخة فقط… / Allow this executable only…**, review the exact path and confirm if desired. Alternatively authorize that path manually through the official client.
5. Restart ASTRO POLSWID if SDL still cannot see newly authorized input, then refresh controllers. Verify actual physical input and the virtual output. Keep the physical pad hidden from the game and the virtual pad visible.

All prior builds remain available. Build identities, checksum and the actual final test result are recorded with this release. Registry platform warnings in ViGEmDriverProbe are pre-existing. A read-only check itself does not change configuration. The only live configuration write in this session was the single application entry explicitly approved by the user; there was no new driver installation or device-unhiding operation.
