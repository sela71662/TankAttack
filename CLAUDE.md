# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

TankAttack is a Unity 6 third-person tank **defense game** (editor `6000.5.10f1`). The player drives a yellow Original tank and defends the `EnergyCore` on an island against 7 waves of red tanks that arrive over two bridges. Code comments are in Korean; match that when editing existing files.

- **Render pipeline: Built-in.** No render pipeline asset is assigned, so URP shaders render pink. Create materials with the `Standard` shader. The URP package is installed but unused.
- **Input: legacy `Input` API** (`Input.GetAxis`, `Input.GetMouseButton`). The Input System package is installed at **1.20.1** because 1.13.0 does not compile on 6000.5; no game script uses it.
- `com.unity.ai.navigation` 2.0.16 is installed (NavMesh + `NavMeshLink`).
- There is no CLI build, lint, or test setup. Open the project in the Unity Editor and press Play. No `.asmdef` files exist; all game scripts compile into `Assembly-CSharp`.

## Scenes

- **`Assets/Skyden_Games/Low Poly Environment/Scenes/Depence Mode.unity`** is the scene all current gameplay lives in. It is a third-party asset scene that we modified directly.
- `Assets/01.Scenes/scBattleField.unity` is the older prototype scene (legacy `TankMove`, `TurretCtrl`, `FireCannon`). Do not extend it.
- NavMesh data for Depence Mode: `Assets/01.Scenes/NavMesh_DepenceMode.asset`.
- Object names in the Skyden scene often have trailing spaces (`"Gate "`, `"building "`, `"Bridge  (2)"`). Never use `GameObject.Find` on them; compare `t.name.Trim()`. The scene objects we added are `Gate A`, `Gate B`, `EnergyCore`, `WaveManager`, `PlayerSpawner`, `NavMesh Surface`, `ClimbLinks`.

## Layout

- `Assets/02.Scripts/`: all hand-written gameplay code (see below).
- `Assets/Resources/`: `Cannon.prefab`, `CannonFire.wav`. `TankShooter` falls back to `Resources.Load("Cannon")` and always loads `CannonFire` by name. Renaming or moving these breaks firing silently.
- `Assets/_Tanks/Prefabs/Tanks(Red)/`: `Tank_Original|Light|Heavy|MegaBall|UFO|Alternative_Model.prefab` and `Tank_Player_Original.prefab`. The folder name is historical: team color is applied at runtime, not by the prefab.
- `Assets/_Tanks/Art/Materials/TankPack/`: tank materials (`TankColor N`, `TankGrey N`, `TankLights N`, `TankWindow`) moved here from the deleted nature pack. `TeamMember` recolors any material whose name contains `TankColor`.
- `Assets/03.Prefabs/Projectiles/`: `Proj_Armor`, `Proj_Shell`, `Proj_Ball`, `Proj_Plasma`, `Mine`, `BallBounce.asset` (physics material). Materials are in `Assets/04.Images/Materials/`.
- `Assets/03.Prefabs/Base/`: the `building` (ally spawn building), `canon`, `wall` models, prefabs and texture used by the scene (moved out of the deleted `New Folder` pack).
- `Assets/Editor/`: `FixTanksGraphics.cs` and `TanksDiagnostic.cs` (`[InitializeOnLoad]`, run on every domain reload; they touch `.meta`/`.mat` files under `Assets/_Tanks` and write `scratch/tanks_diagnostic.txt`).
- Third-party content, avoid editing: `Assets/_Tanks` (Unity "Tanks!" sample), `Assets/Skyden_Games`, `Assets/Standard Assets`, `Assets/03.Prefabs/ExplosionPack`.
- Removed: Photon (PUN2), `HQP STUDIOS` trees pack, `New Folder/Nature and Tank Model Pack`. The game is single-player; no networking exists.
- `Assets/_Recovery/0.unity` is an editor auto-recovery scene, not a real scene.

## Gameplay architecture

**Teams.** `TeamConfig.playerTeam = Team.Yellow` (allies); red is the enemy. Allies have 3x HP (`allyHpMultiplier`), enemies deal 0.6x damage. Base tank HP is 100. `TeamMember` (team, `isStructure`, static `All` list) must be on everything that can be shot; call `SetTeam` after `Instantiate`, which also refreshes `Health`. Projectiles and mines only affect the opposing team.

**Tanks.** Every tank prefab has `TeamMember`, `Health`, `HealthBar`, `TankInfo` (kind, speed, stop distance, flying), a `BoxCollider` for hits, and (gun tanks) `TurretAimer` + `TankShooter` + `AITurretCtrl`.
- `TurretAimer` rotates the turret parts (`turretParts`, including `FirePos`) around a `hub` point with yaw/pitch limits. It cannot reparent because the turrets are inside FBX model instances.
- `TankShooter` instantiates `projectilePrefab` (falls back to `Cannon`) and calls `Projectile.Init(team)`.
- `Projectile` kinds: `Armor` (fast straight), `Shell` (splash), `Ball` (bounces, frozen Y), `Plasma` (homing; targets enemy tanks, then structures if `attackStructures`).
- Per tank: Original = armor shots, Heavy = splash shell every 10 s, MegaBall = bouncing ball, UFO = flying homing plasma, Light = no gun, drops `Mine` via `MineLayer`. `Alternative` is the legacy mouse-aim tank (`MouseTurretCtrl`).
- `GroundAlign` tilts ground tanks to the terrain normal in `LateUpdate`.
- `HealthBar` is a world-space bar (quads + `TextMesh`) on the core, gates and tanks.

**Player.** `PlayerSpawner` (scene object) spawns `Tank_Player_Original` in front of the building and respawns it 5 s after death. The prefab uses a `CharacterController` instead of the `BoxCollider`. `PlayerTankController`: W/S move, A/D rotate. `ThirdPersonCamera` is on `Main Camera`: mouse orbits, cursor is locked (Esc releases, click relocks), the screen-center crosshair computes `AimPoint`. `PlayerTurretCtrl` aims the turret at `AimPoint` and fires on left mouse. The scene `Main Camera` has a pre-existing Missing Script component; leave it.

**Waves.** `WaveManager` runs 7 waves (`FillDefaultWaves` / inspector `Waves`): W1 is 2 Original from Gate A only; later waves mix Heavy, UFO, Light, MegaBall from both gates; W7 is giants (size x2, HP x10). Speed grows 8% per wave. It tracks only the enemies it spawned, then declares victory or defeat (core `Health.onDeath`). A temporary IMGUI HUD shows wave/enemies/core HP. `autoStart` starts waves 10 s after Play.

**Spawn gates and enemy movement.** `SpawnGate.Spawn(prefab, hpScale, speedScale, sizeScale)` creates an enemy and adds a mover.
- Ground enemies use `EnemyMover`: first follow `Route` waypoints (roads and bridges) with **no NavMesh**, because the bridges have collider and NavMesh gaps. The waypoints are rounded into a quadratic Bezier path (`EnemyMover.SmoothPath`, `cornerCut`) so corners are curves. Tanks stop when another tank is ahead (`followGap`). At the end of the route a `NavMeshAgent` is added; the tank chases the nearest ally tank within `aggroRadius` or goes to the core's `ApproachPoint`.
- UFOs use `FlyingMover` (straight line over the lake at core height + `flyHeight`).
- Route waypoints are children of `Gate A/Route`, `Gate B/Route`. Do not add waypoints with short gaps near bridge entrances; the corner cut is limited to half of the neighboring segments.

**Allies.** `AllySpawner` is on the `building` object (spawn point is on the building's local +Z side, which is world -Z because the building is rotated 180 degrees). `startingAllies` is currently empty, so the only yellow tank at the start is the player's. `AllyMover` guards a bridge post (`HeavyPost_A/B`) and engages nearby enemies; Heavy holds its post. Rewards/spawn UI (stage 4) are not built yet.

**Core.** `EnergyCore` is a yellow structure (HP 1000, no ally multiplier, not destroyed on death). `CoreAnimator` rotates and hovers it. Its child `ApproachPoint` is a ground-level target for pathing because the core floats above the building.

## NavMesh (important)

- The island has **two overlapping terrain levels** (plateau around y=0 and a lower terrace around y=-6). Never call `NavMesh.SamplePosition` with a large radius; it can return the wrong level. Use `NavUtil.SampleNearest`.
- The NavMesh is baked **only over the island** (bounds center (-140,0,132), size 130x80x100), so paths never leave the island over the bridges. It is baked manually with `NavMeshBuilder.CollectSources` / `BuildNavMeshData` (agent slope capped at 60 by the baker, climb 2.0, radius 0.8, height 3.0) with markups: lakes and waterfall are not walkable, the core and non-structure tanks are ignored. Using `NavMeshSurface.BuildNavMesh()` would reset these settings.
- Steep faces and steps are crossed with **209 auto-generated `NavMeshLink`s** under `ClimbLinks`. They were generated by scanning the island on a 1 m grid for rises of 1.4 to 7 m. To change climbing behavior, regenerate them rather than editing the NavMesh settings.
- Baking is memory heavy; keep the bounds small. The machine has little free RAM and Unity has run out of memory once during a large bake.

## Tooling notes

- Unity MCP is connected. Use `mcp__unity-mcp__Unity_RunCommand` (class must be `internal class CommandScript : IRunCommand`) and `Unity_GetConsoleLogs`. Calling a tool right after a script edit can fail with "Unity not detected" while the editor recompiles/reloads; wait for `Library/ScriptAssemblies/Assembly-CSharp.dll` to update and retry.
- `RunCommand` rejects `System.Reflection`, and `AssetDatabase.DeleteAsset` is blocked as a "user interaction". Delete files with the shell and let Unity refresh. `FindObjectsByType<T>()` takes no sort argument in this editor version.
- Unity objects do not work with `??`. Use an explicit `GetComponent`/`AddComponent` helper.
- **Play mode freezes when the Unity window loses focus** (`Application.runInBackground` is false). When testing through MCP, set `Application.runInBackground = true` and `Time.timeScale` at runtime (not saved to the project). Per-frame measurement is done by adding an `EditorApplication.update` handler from `RunCommand`.
- After editing a prefab through `PrefabUtility.LoadPrefabContents`, saved changes may not show on a stale `LoadAssetAtPath`; call `AssetDatabase.ImportAsset(..., ForceUpdate)` before verifying. Removing a component that others `[RequireComponent]` fails; remove dependents first.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, and `*.sln`. Do not commit generated project files.
- The `Assets/_TerrainAutoUpgrade/` terrain layer files are deleted in the working tree (part of the user's asset cleanup) but are intentionally not committed, because `05.Models/New Terrain.asset` still referenced them. Confirm before committing that deletion.

## Known gaps

- Waves 3 to 7 were verified only for spawn composition (via a skipped-to W7 test), not full combat balance. Gate B movement, Light mines, MegaBall bounces and UFO homing have not been verified in actual play.
- Enemy tanks arriving by Bridge A land on the lower terrace and reach the plateau only through climb links; verify after any terrain or NavMesh change.
- The player is alone at the start; 2 Original red tanks can destroy an idle player tank. Difficulty and reward/spawn systems are still to be designed.
