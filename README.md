# ASTRO POLSWID

Open source (MIT) controller translator for CTR:NF Polswid Tech. **[Download the latest release](../../releases/latest)** · [الدليل بالعربية](README.ar.md)

## Quick install (Windows 10/11 x64)

1. Download **`SETUP.exe`** from the [latest release](../../releases/latest) and run it (it asks for administrator rights).
2. Tick what you want and press **Install**. SETUP downloads the official installers of
   - **ViGEmBus** (creates the virtual controller) - <https://github.com/nefarius/ViGEmBus/releases>
   - **HidHide** (hides your real controller from the game) - <https://github.com/nefarius/HidHide/releases>

   verifies their Windows signature (signer must be Nefarius), runs them in front of you, then installs ASTRO POLSWID with Start Menu / Desktop shortcuts.
3. Restart the PC if HidHide asks for it, then start **ASTRO POLSWID**.

Prefer to do it by hand? Install the two drivers from the links above, then just run `ASTRO-POLSWID.exe` (self-contained, no .NET needed).
`SETUP.exe` and `ASTRO-POLSWID.exe` are not code-signed, so SmartScreen may warn; the SHA-256 sums are in the release notes. To uninstall: delete `Program Files\ASTRO POLSWID` and the shortcuts; remove the drivers from Windows "Installed apps".

---
# ASTRO POLSWID – Universal Manual Edition

> **Arabic is the default interface language** (switch to English from the title bar or Basic settings). RTL layout, a simple home screen, an Advanced page and a hidden Developer mode, a first-run setup guide, and a "?" explanation next to every setting. Arabic guide: [README.ar.md](README.ar.md). Screenshots: `docs/`.

Real-time **any physical controller → any virtual controller** translator for CTR:NF Polswid Tech, built on SDL3 (input) and ViGEmBus (output).

**It is not an autonomous driver.** It never presses, holds or steers anything for you. With no helper triggered, the virtual pad is a pure function of what your physical pad is doing plus the mapping.

```
Physical controller → Universal input layer (SDL3) → Normalized state → Mapping engine → Polswid direction processing → Virtual output adapter → Virtual Xbox 360 / DualShock 4
```

Physical input and virtual output are independent. The Polswid engine only ever sees the normalized state (`GamepadState`, PlayStation naming: Cross = Xbox A = logical South, Circle = B, Square = X, Triangle = Y). Nothing in the engine knows which physical model is connected.

## Supported controllers

Through the SDL3 Gamepad API, with automatic detection by USB ids and the API's reported capabilities (never by product name): DualShock 4, DualSense, DualSense Edge, Xbox One / One S / Series X|S, Xbox 360 and other XInput pads, and DirectInput/HID gamepads that SDL has a mapping for. USB and Bluetooth work wherever the OS driver and SDL expose the data.

Limits, stated plainly:
- HID pads SDL has no mapping for are not supported out of the box. You can add one with an SDL mapping file at `%APPDATA%\ASTRO POLSWID\gamecontrollerdb.txt`. A raw-HID fallback is not implemented.
- Gyro/motion is detected and shown but not used.
- Battery and connection type are shown when the device and driver report them.
- Virtual Xbox 360 has no touchpad; the touchpad click is not forwarded in that mode.

Pick the output on the Dashboard (Virtual Xbox 360 or Virtual DualShock 4) regardless of the pad you hold. If DS4 output is unavailable, the error says so and suggests Xbox 360; the output is never switched silently.

## Default mapping

| Physical (normalized) | Virtual |
|---|---|
| R2 | D-Pad Down (analog R2 **not** forwarded) |
| D-Pad Left | D-Pad Left |
| D-Pad Down | D-Pad Right |
| Left stick X left / right | D-Pad Left / Right (optional, X axis suppressed; Y and the whole right stick pass through) |
| Square, Cross, Circle, Triangle, L1, R1, Options/Start, Share/Back/View, L3, R3 | same |
| L2 | L2 (analog preserved) |
| PS/Guide button, Touchpad | same, re-assignable |

Analog defaults: deadzone 0.20, activation 0.60, release 0.35 (hysteresis). Everything is editable live. Two independent switches on the Dashboard: **Controller remapping** and **Left analog steering**. Remapping off = raw passthrough.

## Direction engine

Each frame the engine builds one complete virtual state. Direction sources: physical D-Pad Left, D-Pad Down→Right, and the left stick. Priority is configurable (default: the stick owns steering while active, then returns it to the D-Pad). Left→Right is a single state change with no neutral state in between; neutral occurs only when no source requests a direction. Left+Right never appear together unless the experimental option is enabled.

> Application-level atomic updates do **not** guarantee identical in-game frame behaviour. Test in game.

## Optional helpers (all OFF by default)

Grounded U-Turn (releases a configurable control, default Cross, for N ms when *you* press its trigger) and Air Switch (jump press, then direction flip after N ms when *you* press its trigger). Neither starts by itself; neither detects landing or game state.

## Device handling and safety

- Explicit **primary controller** (Dashboard). With a primary selected, no other controller is ever read; if it is unplugged the app stays disconnected rather than silently switching. In automatic mode the first usable pad is taken and then kept.
- Controller unplugged → every virtual control released, state reset, nothing replayed; replug resumes (the primary is found again by VID:PID).
- Virtual device lost or not creatable → red error, "NOT FORWARDING", retry every 2 s.
- Emergency stop (button, UI, **Ctrl+Alt+F10**) → remapping off, raw passthrough, nothing stuck. Ctrl+Alt+F9 toggles remapping, F8 analog steering.
- Exit → neutral report, virtual pad disconnected, resources disposed.
- Virtual pads (ours, or created by other ViGEm programs) are recognised and never read as physical input.

## Install

The latest release integrates the official HidHide .NET library for a real policy check and an optional, confirmed grant to the exact running executable; see [official-library integration](docs/HIDHIDE-OFFICIAL-API.md) and [HidHide compatibility](docs/HIDHIDE-COMPATIBILITY.md). Allowlist the exact exe path you run; permission for another folder does not transfer. No HidHide setting is changed automatically.

1. Install **ViGEmBus** yourself (retired project; final official build: <https://github.com/nefarius/ViGEmBus/releases>) as administrator. ASTRO POLSWID never installs drivers. Physical input works without it; only the virtual output needs it.
2. Run `ASTRO-POLSWID.exe` (self-contained). Connect any supported controller.
3. Dashboard: choose the primary controller and the output type; check both panels.

**User-confirmed resolution (2026-11-03):** the persistent in-game R2/Polswid problem was resolved by configuring HidHide to prevent simultaneous direct physical and virtual input. Preserve that working setup: ASTRO POLSWID must retain physical-pad access, and the virtual pad must remain accessible to the game. See [Double Input and HidHide troubleshooting](docs/DOUBLE-INPUT-HIDHIDE.md) for verification, diagnostic evidence and the separate unfixed late-release Square issue. This app does not manage HidHide.

## Build / test

```
dotnet test ASTRO.Polswid.Tests
dotnet publish ASTRO.Polswid.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release
```

Config and logs: `%APPDATA%\ASTRO POLSWID\` (profiles/*.json, settings.json, logs/*.log as JSON lines incl. latency summaries).

## Architecture

`Core` (normalized state, mapping, analog converter, direction engine, helper engine, processor, real-time pipeline, device-selection policy, controller classifier – no driver dependencies) · `Infrastructure` (SDL3 input, ViGEm output behind `IVirtualPad`, JSON store, Serilog) · `App` (WPF) · `Tests` (xUnit). The loop runs on its own highest-priority thread with a high-resolution waitable timer plus a short spin (no `Thread.Sleep`), submits only changed states, and publishes immutable snapshots to the UI.

## Manual integration test plan

Run each step once per physical/virtual pairing you care about (e.g. Xbox→DS4, DualSense→Xbox 360, DS4→Xbox 360).

1. Dashboard: physical panel shows name/type/connection/battery; output panel shows "connected (ViGEmBus)". No message may demand a pad of the output's brand.
2. Live Monitor: moving the physical pad moves the left diagram; the right diagram shows the transformed output.
3. Hold R2: virtual D-Pad Down lit, virtual R2 stays 0.
4. D-Pad Left, then D-Pad Down while holding Left: one `VIRTUAL +Right -Left` log line.
5. Left stick past 60% right/left: virtual Right/Left, "Left stick X hidden: yes", left-stick Y and the whole right stick still pass through.
6. Sweep the stick quickly left→right while holding D-Pad Left: no log moment with neither direction.
7. Select a primary controller; connect a second pad: it must be ignored. Unplug the primary: log DISCONNECTED, virtual neutral, no fallback to the second pad. Replug: resumes.
8. Switch the output type on the Dashboard: the old virtual device disappears and the new one appears (`joy.cpl`).
9. Emergency stop: raw passthrough, nothing stuck. Close the app: virtual pad disappears.
10. In CTR:NF: confirm the game reads only one controller (HidHide if not), then test each technique step yourself.

## Test status

Last verified automated run: 167 passed (processor, mapping, direction, helpers, profiles, pipeline, output encoder calls, diagnostics, device policy and localization). No tests were rerun for the documentation-only HidHide update. The user reports that HidHide resolved the gameplay problem in physical-controller testing; this is separate from automated verification. Historical diagnostic evidence and remaining independent issues are recorded in [Double Input troubleshooting](docs/DOUBLE-INPUT-HIDHIDE.md).

## Continuous Steering (R2-activated, optional, OFF by default)

Advanced settings → Analog & direction. Works **only while the physical R2 is held** and reads the **LEFT stick X only** (the right stick is never read or changed and always passes through).

| State | Output |
|---|---|
| R2 released | Nothing synthetic: no forced direction, no Square, the left stick passes through normally |
| R2 held | D-Pad Down at once; Square after a configurable delay (default 60 ms) and kept while R2 is held; stick neutral or left → D-Pad Left; stick right → D-Pad Right (fallback direction configurable, default Left) |
| R2 released again | Square, Down and the synthetic direction are removed in the same update; left-stick passthrough returns |

While active, the horizontal direction is never neutral, and the left stick's X axis is hidden from the game (it is owned by the synthetic steering). R2 → Down, R2 → Square and R2 steering are separate owners that claim disjoint controls (Down / Square / Left+Right); everything else stays physical, so they never release each other's buttons. Stick thresholds (deadzone, activation, release) are shared with the analog settings. Emergency stop, remapping off, controller disconnect and profile changes all clear it immediately.

This proves what is sent to the virtual controller. Whether the game reacts as intended must be tested in the game.
