# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

TankAttack is a Unity 6 game (editor `6000.5.10f1`, URP 17, Input System package installed but scripts use the legacy `Input` API). Single scene: `Assets/01.Scenes/scBattleField.unity`. Code comments are in Korean; match that when editing existing files.

There is no CLI build, lint, or test setup. Open the project in the Unity Editor and press Play. No test assemblies and no `.asmdef` files exist; all game scripts compile into `Assembly-CSharp`.

## Layout

- `Assets/02.Scripts/`: the project's own gameplay scripts (about 7 small MonoBehaviours). This is where almost all hand-written code lives.
- `Assets/Resources/`: `Cannon.prefab` and `CannonFire.wav`. `FireCannon` loads them by name with `Resources.Load("Cannon")` / `Resources.Load<AudioClip>("CannonFire")`. Renaming or moving these breaks firing silently.
- `Assets/Editor/`: `FixTanksGraphics.cs` and `TanksDiagnostic.cs` (menu `Tools/Fix Tanks Graphics`, `Tools/Diagnose Tanks Graphics`). Both are `[InitializeOnLoad]` and run on every editor domain reload via `EditorApplication.delayCall`. The fixer reimports models and edits materials under `Assets/_Tanks`; the diagnostic writes a report to `scratch/tanks_diagnostic.txt` at the project root. Expect them to touch `.meta` and `.mat` files on load, which explains many modified assets in `git status`.
- Third-party content, not hand-written, avoid editing: `Assets/Photon` (PUN2 / Photon Realtime / Chat), `Assets/_Tanks` (Unity "Tanks!" sample), `Assets/HQP STUDIOS`, `Assets/Skyden_Games`, `Assets/Standard Assets`, `Assets/New Folder` (nature and tank model pack), `Assets/03.Prefabs/ExplosionPack`.
- `Assets/_Recovery/0.unity` is an editor auto-recovery scene, not a real scene.

## Gameplay architecture

The tank is a prefab with a hull and a turret. Behavior is split into small scripts, each reading input directly:

- `TankMove`: hull movement. Arrow keys drive `Transform.Rotate` / `Translate` in `Update`, so it ignores physics even though a `Rigidbody` is required (only `centerOfMass` is set).
- `TurretCtrl`, `CannonCtrl`: aim turret and barrel.
- `FireCannon`: left mouse button instantiates the `Cannon` prefab at `firePos` and plays a sound.
- `Cannon`: projectile. Applies forward force on `Start`, explodes on trigger enter or after 3 seconds via `ExplosionCannon`, spawns `expEffect`, and destroys itself.
- `TankAnim`, `SmoothFollow`: animation and follow camera.

Photon (PUN2) is imported but no script in `02.Scripts` references it yet, so the game is currently single-player. Do not assume networking is wired up.

## Tooling notes

- Unity MCP is connected (`com.coplaydev.unity-mcp` and `com.unity.ai.assistant`). Use the `mcp__unity-mcp__*` tools (console logs, scene capture, `Unity_RunCommand`) to inspect or drive the open Editor.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, and `*.sln`. Do not commit generated project files.
