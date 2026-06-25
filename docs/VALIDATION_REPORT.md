# ForgeStudio Circuit Validation Report

Package target: `0.1.0-dev`  
Next planned release: `0.1.1` after user says `publish`.

## Build-environment note

This sandbox does not include the .NET SDK or Inno Setup, so this package was statically validated and packaged for Visual Studio testing. It has not been compiled inside this environment.

## Applied recommended fix order

1. Serial state machine + crash-proof exception handling
2. Device-first responsive UI layout
3. Full tabbed IDE editor polish
4. CircuitPython USB-drive mode baseline
5. Problems/logs diagnostics panel
6. Board profile/status visibility
7. Security hardening pass
8. Installer/version lock retained

## Runtime stability changes

- Added `ConnectionState` enum and UI state display.
- Added device-operation lock to prevent overlapping serial reads/writes.
- Added operation timeouts with recoverable errors instead of app exits.
- Connect now falls back to Serial Monitor mode if raw REPL does not respond.
- Stop and Soft Reboot are guarded and report recoverable problems.
- Device actions no longer use UI-thread-breaking `ConfigureAwait(false)` in the ViewModel.

## Serial/raw REPL changes

- Raw REPL entry uses staged Ctrl-B, Ctrl-C, Ctrl-C, Ctrl-A flow.
- Raw REPL detection requires `raw REPL` banner text and avoids treating normal `>>>` as raw mode.
- All MicroPython file/run actions remain serialized by the device lock.
- Serial read limit and write limit safeguards remain in place.

## UI changes

- Device Files promoted to the primary left workflow.
- Project Files moved into secondary tab.
- Added CircuitPython tab for mounted `CIRCUITPY` drive workflow.
- Added Status/Help inspector tabs.
- Bottom drawer now has REPL, Problems, and Logs tabs.
- Added Copy Diagnostics, Clear Console, and Clear Problems actions.
- Editor remains AvaloniaEdit-backed with syntax highlighting, line numbers, snippets, validation, run file, and run-selection command.

## Security checks

- Device path traversal guarded.
- CircuitPython mounted-drive path traversal guarded.
- Local project explorer filters build/runtime outputs.
- Firmware/destructive tooling remains guarded/disabled.
- No shell command execution added.
- Invalid Avalonia `ColumnSpacing`/`RowSpacing` properties checked and absent.

## Manual test checklist for Visual Studio

- Build solution in Visual Studio 2022.
- Connect ESP32/Pico MicroPython on COM port.
- Confirm app does not crash if raw REPL fails.
- Press Stop, then Read Files.
- Open `boot.py`, `config.py`, `main.py`.
- Save a harmless test file to device.
- Run current file and REPL command.
- Unplug board while connected and verify recoverable error.
- Test CircuitPython board mounted as `CIRCUITPY`.


## Internal Editor Patch

- Removed external AvaloniaEdit dependency.
- Removed AvaloniaEdit XML namespace and code-behind references.
- Added internal Avalonia TextBox-based IDE editor baseline with line number gutter.
- Version remains 0.1.0-dev until publish.
