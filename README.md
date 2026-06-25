# ForgeStudio Circuit

**Version:** `0.1.0-dev`
**By:** StaticTechGroup / STG

ForgeStudio Circuit is a standalone desktop microcontroller IDE inspired by Thonny. It is designed for writing, reading, syncing, and running MicroPython/CircuitPython files on boards such as Raspberry Pi Pico, Pico W, ESP32, ESP8266, and compatible serial devices.

---

## Locked Direction

* Standalone application, not part of ForgeStudio Hub
* C# / .NET 8 LTS
* Avalonia UI
* MVVM architecture
* Inno Setup installer
* Dynamic board profiles and JSON configuration
* Security-first handling around:

  * File paths
  * Serial input
  * Tool execution
  * Destructive actions

---

## First Build Scope

This `v0.1.0-dev` source package includes:

* Standalone app shell
* Modern ForgeStudio Circuit UI
* Local/device explorer views
* Serial REPL service layer
* Board profile loader
* Settings system
* Structured logging hooks
* Validation/security services
* Build scripts
* Package scripts
* Installer script

Firmware flashing and destructive tools are intentionally guarded/disabled in this initial build until serial/file workflows are validated on hardware.

---

## Build Requirements

* Windows 10/11 recommended for first build
* .NET 8 SDK
* Inno Setup 6, required only when building the installer

---

## Build

```powershell
.\build.ps1
```

---

## Package Installer

```powershell
.\package.ps1
```

---

## Default Paths

| Purpose        | Path                                                    |
| -------------- | ------------------------------------------------------- |
| Install target | `C:\Program Files\StaticTechGroup\ForgeStudio Circuit\` |
| User data      | `%AppData%\StaticTechGroup\ForgeStudio Circuit\`        |
| Projects       | `%UserProfile%\Documents\ForgeStudio Circuit\Projects\` |

---

## Security Notes

See:

* `docs/SECURITY.md`
* `docs/VALIDATION_REPORT.md`

---

## Device Filesystem Behavior

When you click **Connect**, ForgeStudio Circuit attempts to enter MicroPython raw REPL mode and read the root device filesystem using:

```python
os.ilistdir
```

Use the following controls for device file operations:

| Action             | Behavior                                                        |
| ------------------ | --------------------------------------------------------------- |
| **Read Files**     | Refreshes the current device path                               |
| **Open**           | Reads a selected device file into the editor                    |
| **Save to Device** | Writes editor content back to the selected or typed device path |
| **Run**            | Executes the editor buffer through raw REPL                     |

For best results, stop any currently running program on the board before connecting.

The app sends interrupt commands before raw REPL operations, but some boards may need a manual reset if user code is holding the serial port busy.

---

## Full IDE Editor Baseline

ForgeStudio Circuit now uses an AvaloniaEdit-backed editor so the dev build can be treated as a real IDE baseline instead of a plain multiline text box.

Included editor features:

* Line numbers
* Syntax highlighting by file type
* Python/MicroPython highlighting
* JSON highlighting
* TOML/config highlighting
* Markdown highlighting
* Plain-text detection
* Filetype-specific assist text
* Find text workflow
* Snippet insertion
* Comment toggle
* Indent cleanup
* Local save
* Save to device
* Run code
* Validate editor buffer

---

## Versioning

Version remains:

```text
0.1.0-dev
```

until the user explicitly says:

```text
publish
```

---

## Repository Name

```text
ForgeStudio-Circuit
```
