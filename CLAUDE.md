# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

TankAttack is a Unity 6 third-person tank **defense game** (editor `6000.5.10f1`). The player drives a yellow Original tank and defends the `EnergyCore` on an island against 7 waves of red tanks that arrive over two bridges. Code comments are in Korean; match that when editing existing files.

- **Render pipeline: Built-in.** No render pipeline asset is assigned, so URP shaders render pink. Create materials with the `Standard` shader. The URP package is installed but unused.
- **Input: legacy `Input` API** (`Input.GetAxis`, `Input.GetMouseButton`). The Input System package is installed at **1.20.1** because 1.13.0 does not compile on 6000.5; no game script uses it.
- `com.unity.ai.navigation` 2.0.16 is installed (NavMesh + `NavMeshLink`).
- There is no CLI build, lint, or test setup. Open the project in the Unity Editor and press Play. No `.asmdef` files exist; all game scripts compile into `Assembly-CSharp`.

## Scenes

- **`Assets/01.Scenes/Depence Mode.unity`** is the scene all current gameplay lives in (originally a Skyden asset scene, moved into our folder).
- The old prototype scene `scBattleField.unity` was deleted; its legacy scripts (`TankMove`, `TurretCtrl`, `FireCannon`) remain unused.
- NavMesh data for Depence Mode: `Assets/01.Scenes/Depence Mode/NavMesh-NavMesh Surface.asset`.
- Object names in the Skyden scene often have trailing spaces (`"Gate "`, `"building "`, `"Bridge  (2)"`). Never use `GameObject.Find` on them; compare `t.name.Trim()`. The scene objects we added are `Gate A`, `Gate B`, `EnergyCore`, `WaveManager`, `PlayerSpawner`, `NavMesh Surface`, `ShoreZone`.

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
- `AITurretCtrl` only picks targets it can see from the turret hub (terrain, props and water block), fires only when the muzzle is not inside a collider and the line of fire is clear, and leads moving targets for straight projectiles (Armor, Shell).
- Per tank: Original = armor shots, Heavy = splash shell every 8 s (shell 40 m/s), MegaBall = bouncing ball, UFO = flying homing plasma, Light = no gun, drops `Mine` via `MineLayer`. `Alternative` is the legacy mouse-aim tank (`MouseTurretCtrl`).
- `GroundAlign` tilts ground tanks to the terrain normal in `LateUpdate`.
- `HealthBar` is a world-space bar (quads + `TextMesh`) on the core, gates and tanks.

**Player.** `PlayerSpawner` (scene object) spawns `Tank_Player_Alternative` (the Alternative model with CharacterController; `Tank_Player_Original` is unused, Original tanks come only from ally spawns) in front of the building and respawns it 5 s after death. The prefab uses a `CharacterController` instead of the `BoxCollider`. `PlayerTankController`: W/S move, A/D rotate. `ThirdPersonCamera` is on `Main Camera`: with `followHull` (default) it stays behind the hull and looks where the hull faces, mouse Y only changes pitch (turn with A/D); with `followHull` off the mouse orbits. The cursor is locked (Esc releases, click relocks), the screen-center crosshair computes `AimPoint`. `PlayerTurretCtrl` aims the turret at `AimPoint` and fires on left mouse. The scene `Main Camera` has a pre-existing Missing Script component; leave it.

**Waves.** `WaveManager` runs 5 waves (`FillDefaultWaves` / inspector `Waves`, each with enemy `entries` per gate and ally `allies` auto-spawned at the building at wave start). W1 is 2 Original from Gate A only, player alone (tutorial); W2 to W5 add allies and mix Heavy, Light, UFO, MegaBall from both gates. Speed grows 8% per wave. It tracks only the enemies it spawned, then declares victory or defeat (core `Health.onDeath`). A temporary IMGUI HUD shows wave/enemies/core HP/gold.

**Economy.** `GoldWallet` (static) holds gold plus all reward/cost numbers (kill reward per kind, wave bonus, summon cost, repair, upgrade). `ShopUI` (on the WaveManager object, IMGUI): keys 1-5 summon Original/Heavy/UFO... (see `summonKinds`), R repairs the core, U upgrades allies (`AllyUpgrades`: +15% damage, +20% HP per level, max 5, applies to the player too). `AllySpawner.maxAllies` caps purchases. Enemy kills also drop random `ItemPickup`s (15%): Damage, FireRate, Speed (timed, via `PlayerBuffs` on the player) and Heal.

**Target priority.** `TankInfo.PrefersCore` (Original/Heavy/MegaBall): enemy goes straight to the core and `AITurretCtrl` shoots the core first. Light/UFO hunt the nearest ally tank anywhere (`EnemyMover.aggroRadius`, `FlyingMover`), core only as fallback.

**Spawn gates and enemy movement.** `SpawnGate.Spawn(prefab, hpScale, speedScale, sizeScale)` creates an enemy and adds a mover.
- Ground enemies use `EnemyMover`: first follow `Route` waypoints (roads and bridges) with **no NavMesh**, because the bridges have collider and NavMesh gaps. The waypoints are rounded into a quadratic Bezier path (`EnemyMover.SmoothPath`, `cornerCut`) so corners are curves. Tanks stop when another tank is ahead (`followGap`). At the end of the route a `NavMeshAgent` is added; the tank chases the nearest ally tank within `aggroRadius` or goes to the core's `ApproachPoint`.
- UFOs use `FlyingMover` (straight line over the lake at core height + `flyHeight`).
- Route waypoints are children of `Gate A/Route`, `Gate B/Route`. Never hand-edit them: after moving, rotating or raising a bridge run the menu **TankAttack/Rebuild Gate Routes** (`Assets/Editor/GateRouteBuilder.cs`). It fits `Bridge Deck Fill`, rebuilds both routes (road curve as a 25 m-radius arc every 3 m, 6 m-radius turn onto the bridge, deck points every 2 m at deck height, end 5 m inside the island), moves `HeavyPost_A/B` beside each bridge exit (to the right, off the enemy path, away from water and props) and rebakes `ShoreZone`. Road layout constants are at the top of that file.

**Allies.** `AllySpawner` is on the `building` object (spawn point on the building's world -Z side). Heavy allies hold `HeavyPost_A/B` (beside each bridge exit). Every other ground ally has `AllyMover.assault`: it defends against enemies on the island first (island = `islandMin/islandMax` rect, bridge decks excluded, because the NavMesh now also covers roads and bridges), otherwise it walks to a bridge entrance, turns its NavMeshAgent off, follows the gate route in reverse to `gateStandOff` (18 m) before the gate spawn point, holds when enemy tanks are in range and shoots the gate. Allies alternate gates by spawn order. When its gate is destroyed it drives back, re-enables the agent and goes to the remaining gate. UFO allies fly at enemy tanks, then gates.

**Gates.** `SpawnGate` is not destroyed on death: `IsDestroyed` turns on, its renderers/colliders/`TeamMember` are disabled (routes stay usable), and the player gets `GoldWallet.gateReward` (150). `WaveManager` moves a destroyed gate's spawns to the other gate and skips them if both are down. Gates are hollow arches, so AI and homing plasma aim at the upper half of the gate collider, and `Projectile` sweeps a sphere from its last position each physics step (fast shells tunnel through thin mesh colliders otherwise).

**Shore.** `ShoreZone` (scene object) holds a baked distance-to-water grid (`Bake` context menu). Water = ground below a `Lake*` plane; deck = top surface is a `Bridge*` collider. Deck cells and land within `bridgeMargin` (5 m) of a deck are exempt, but water beside a bridge is not. `PlayerTankController` refuses moves that bring the hull center or a point 1.5 m ahead closer than `blockDistance` (3 m) to water, and its `LateUpdate` teleports the player to the nearest safe land if it is ever over water. Each bridge has a child `Bridge Deck Fill` BoxCollider that covers the gaps in the bridge mesh collider. Rebake `ShoreZone` after moving bridges, lakes or terrain.

**Props.** `PropGuard` (name prefixes Tree, Rock, House, Tent, Warehouse, Doghouse, Mill, Watch Tower, building, Car) tells whether the ground below a point is a prop. `PlayerTankController` refuses moves onto props, `NavUtil.SampleNearest` avoids prop-top NavMesh points (AI is kept off props by their Not Walkable modifiers). The terrain meshes are named `Cliff*`.

**Core.** `EnergyCore` is a yellow structure (HP 1000, no ally multiplier, not destroyed on death). `CoreAnimator` rotates and hovers it. The `building` has a `TeamMember` (structure), a `BoxCollider` and a `Health` with `redirect` = core, so hitting the building damages the core. Its child `ApproachPoint` is a ground-level target for pathing because the core floats above the building.

## NavMesh (important)

- The island terrain (`Cliff*` meshes) was lowered so the island is now roughly one level (y about -5.5); the old plateau/terrace split and the `ClimbLinks` object are gone. Props were re-snapped to the new surface.
- The NavMesh is baked with the `NavMesh Surface` Bake button (`NavMeshSurface.BuildNavMesh()`, whole scene, render meshes, agent type settings). Every tree, rock, building, fence, car, lake and the waterfall has a `NavMeshModifier` (override area = Not Walkable), and `EnergyCore` has one with Ignore From Build, so a normal Bake never puts NavMesh on them. Keep adding a Not Walkable modifier to any new prop.
- The bake now covers roads and bridges too, not only the island. Use `NavUtil.SampleNearest` rather than a large-radius `NavMesh.SamplePosition`.
- Baking is memory heavy. The machine has little free RAM and Unity has run out of memory once during a large bake.

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
