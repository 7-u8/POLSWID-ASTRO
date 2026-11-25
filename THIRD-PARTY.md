# Third-party software

ASTRO POLSWID is MIT licensed (see `LICENSE`). It uses these open-source libraries, each under its own license:

| Component | Use | License |
|---|---|---|
| [SDL3](https://libsdl.org) via SDL3-CS | Reading physical controllers | zlib |
| [Nefarius.ViGEm.Client](https://github.com/nefarius/ViGEm.NET) | Talking to ViGEmBus (virtual controller) | MIT |
| [Nefarius.Drivers.HidHide](https://github.com/nefarius/HidHide) | Official HidHide management library | MIT |
| [Serilog](https://serilog.net) | Structured logging | Apache-2.0 |

## Drivers (not redistributed)

`SETUP.exe` **downloads** the official installers of [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) and
[HidHide](https://github.com/nefarius/HidHide/releases) from their authors' GitHub releases at install time. They are
separate projects by Nefarius Software Solutions, are not part of this repository, and keep their own licenses.
Before running a downloaded installer, `SETUP.exe` verifies its Windows digital signature and that the signer is Nefarius.
