# ForgeStudio Circuit Security Model

ForgeStudio Circuit handles local files, serial devices, and future tool execution. The first build is designed to be secure-by-default.

## Guardrails

- No automatic firmware flashing.
- No automatic erase operations.
- No network/WebREPL features enabled by default.
- No shell command string concatenation.
- Helper tools must use strict argument arrays and allowlists.
- Project/device paths are normalized and traversal-checked.
- Device file names block `..`, absolute paths, drive roots, null bytes, and control characters.
- Serial input is bounded and decoded safely.
- Logs are redacted for tokens, passwords, and common secret patterns.
- App settings are loaded from user-writable AppData, not install directory.
- Install script uses lowest privileges by default.

## Known Disabled Features

Firmware flashing UI is intentionally represented as a guarded shell until the hardware workflow is validated.
