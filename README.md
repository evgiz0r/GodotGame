# FightSimulations — Godot 4 C# Battle Viewer

A preset-driven army battle simulator built in **Godot 4 (.NET / C#)**. Pick a
matchup, two armies spawn on a procedurally-built battlefield, and you watch them
fight until one side is wiped out.

## Concept

- Choose a preset scenario → two armies deploy → the battle plays out automatically.
- Units are built from **procedural meshes** — no external 3D assets required.
- Roles include Melee, Ranged, Healer, AoE casters, and a Boss.
- Camera pan/zoom, billboard health bars, homing projectiles, and screen shake.

## Run it

1. Open the project in **Godot 4.x** (the **.NET / Mono** editor build).
2. Build the C# solution: `dotnet build .\FightSimulations.sln`.
3. Press **F5** in the Godot editor to play. Main scene: `res://scenes/main.tscn`.

## Project layout

```
scripts/
  framework/   — reusable Godot layer: Actor, CameraRig, Projectile, HealthBar3D,
                 GodotEx, BatchDirector, Economy, BuffSystem, IActorBehavior
  behaviors/   — reusable AI: Melee / Ranged / Healer
  game/        — the simulator: SimUnit, UnitArchetype, Scenario/Presets,
                 BattleEnvironment, SimDirector, BattleHud, Main (orchestrator)
scenes/        — main.tscn
```

## Requirements

- Godot 4.x with .NET support
- .NET 8 SDK
