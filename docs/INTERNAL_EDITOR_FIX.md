# Internal Editor Dependency Fix

This patch removes the external `AvaloniaEdit` package reference because NuGet only exposed an older 0.10.x package while the app uses Avalonia 11.x.

ForgeStudio Circuit now uses an internal editor-safe baseline built from Avalonia controls:

- multiline code editor
- line number gutter
- language/filetype detection
- snippets
- find
- validation
- format indentation
- toggle comment
- run file
- run selection
- save local
- save to device

The package version remains `0.1.0-dev`. The next planned release version is still `0.1.1`, but it must not be applied until the user says `publish`.
