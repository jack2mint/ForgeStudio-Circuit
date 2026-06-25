# Architecture

ForgeStudio Circuit is a standalone Avalonia desktop app using MVVM and service-oriented core layers.

## Layers

- App: Avalonia application bootstrapping and dependency wiring.
- Core: Serial, REPL, file validation, board profiles, logging, settings, and security services.
- UI: Views, view models, controls, and themes.
- Tests: Unit tests for security-sensitive logic.

## Dynamic Systems

Board profiles and app settings are JSON-driven so board support is not hardcoded into the UI.
