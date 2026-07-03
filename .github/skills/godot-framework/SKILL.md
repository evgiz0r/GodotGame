---
name: godot-framework
description: 'Reusable Godot 4 C# framework layer + general Godot knowledge (engine-agnostic patterns). Use when working on: the Framework namespace (Actor, HealthBar3D, Projectile, CameraRig, Economy, BuffSystem, BatchDirector, GodotEx, IActorBehavior, IDamageable); generic Godot 4 C# patterns; .tscn rules; billboard HP bars; homing projectiles; camera pan/zoom; soft separation; animation/facing quirks; or reusing these systems in another game on top of Godot.'
argument-hint: 'What general Godot 4 / framework topic do you need help with?'
---

# Godot 4 C# — Reusable Framework Knowledge Base

> **Keep this skill up to date.** When you discover a reusable pattern, Godot quirk, or
> framework improvement, ask Copilot to record it here. This file is for knowledge that
> is NOT specific to any single game — see the game skill for Direct-Strike specifics.

---

## Environment / Build

- **Engine**: Godot 4.7 stable mono, Forward+ renderer, D3D12, Jolt Physics
- **Language**: C# (.NET 8)
- **Build**: `dotnet build` from project root — must run before F5 or after any C# change
- **Scene reload**: after editing `.tscn` externally, use **Scene → Reload Saved Scene** then F5
- **C# solution missing**: run **Project → Tools → C# → Create C# Solution** if `dotnet build` finds no project

---

## The `Framework` layer (`scripts/framework/`, `namespace Framework`)

Reusable, game-agnostic building blocks. Game code lives in the global namespace and
does `using Framework;`. None of these are attached directly in a `.tscn` (they are base
classes or created in code), so the namespace never breaks scene loading.

| File | Type | Purpose |
|------|------|---------|
| `FrameworkTypes.cs` | `enum Team`, `enum UnitState`, `interface IDamageable` | shared vocab |
| `IActorBehavior.cs` | interface | pluggable per-frame "brain" (`OnUpdate(Actor, Actor, delta)`) |
| `Actor.cs` | `partial Node3D, IDamageable` | HP, state machine, movement/facing, separation, HP bar, buff-driven effective stats, team groups, projectiles |
| `HealthBar3D.cs` | `partial Node3D` | camera-facing HP bar (bg + fill quads); `Build/SetRatio/UpdateWorld` |
| `Projectile.cs` | `partial Node3D` | homing arrow; targets any `IDamageable` |
| `CameraRig.cs` | plain class | angled 3/4 camera: wheel zoom + right-drag pan; `Setup/HandleInput` |
| `Economy.cs` | plain class | growing budget reserves a standing composition; cost table + `CostMult`; `TrySetCount` (cascade-remove cheapest to afford), `AutoFillCheapest` (spend spare on cheapest) |
| `BuffSystem.cs` | `record Buff` + class | holds a buff pool, `Offer(n)` distinct random choices |
| `BatchDirector.cs` | `partial Node` | timed batch loop: timer, batch#, signals, hard cap, prune; hooks `CheckGameOver`/`SpawnBatch` |
| `GodotEx.cs` | static | `FacePlusZ`, `ForceLoop`, `BillboardTransform`, `RayToGroundPlane` |

### Extending `Actor` (the core pattern)
A game unit derives from `Actor` and overrides virtual hooks instead of `_Ready`/`_Process`:
```csharp
public partial class Unit : Actor
{
    protected override void ConfigureStats() { /* set MaxHp/Damage/... per archetype */ }
    protected override void BuildVisuals() { /* attach gear meshes */ }
    protected override IActorBehavior SelectBehavior() => new MeleeBehavior();
    protected override Vector3 ComputeBaseScale() => /* per-archetype size */;
    protected override void Tick(double delta) { /* extra per-frame logic */ base.Tick(delta); }
    protected override void OnStructureTick(double delta) { /* stationary logic */ }
}
```
- `Actor._Ready` orchestrates: `ConfigureStats → MaxHp*=HpScale → capture base stats →
  ComputeBaseScale → ComputeEffectiveStats → groups → ApplyTeamTint → BuildVisuals →
  CreateHpBar → find AnimationPlayer → SelectBehavior → SetState`.
- **Only `Actor` overrides `_Ready`/`_Process`.** Subclasses use the hooks so there is no
  fragile `base._Ready()` chaining.
- Set stat/flag fields (`Kind`, `IsStructure`, `HpScale`, `BodyScale`, `HpBarWidth`, …)
  **before** `AddChild`; set `GlobalPosition`/`SetFacing` **after** `AddChild`.

### Extending `BatchDirector`
```csharp
public partial class WaveDirector : BatchDirector
{
    protected override bool CheckGameOver(out bool victory) { ... }
    protected override void SpawnBatch(int batch) { ... }
}
```
Signals (`BatchSpawned`, `TimerTick`, `GameOver`) are declared on the base and are
accessible on the derived instance (`director.BatchSpawned += ...`). `PruneDead<T>` /
`EnforceCap<T>` are generic `where T : Actor`, so `List<MyUnit>` works directly.

### Effective stats vs base stats
`Actor` captures `_baseMaxHp/_baseDamage/_baseCooldown/_baseMoveSpeed` once in `_Ready`
(after `ConfigureStats`). `ComputeEffectiveStats()` derives live stats from base × buff
each call: `MaxHp = base*BuffHpMult`, `AttackCooldown = base / max(0.25, BuffAtkSpeedMult)`,
etc. `ReapplyBuffs()` preserves the current HP **ratio** across a MaxHp change.
**Always edit base fields + buff mults, never the live stat** or the next recompute wipes it.
`Scale = BaseScaleVec * (1 + 0.12*EvolutionTier)` grows units with an evolution tier.

---

## General C# / Godot Patterns

### Setting properties before `_Ready` runs
```csharp
var unit = scene.Instantiate<Actor>();
unit.UnitTeam = Team.Enemy;   // BEFORE AddChild — _Ready sees these
unit.IsBattleUnit = true;
parent.AddChild(unit);        // _Ready runs synchronously here
unit.GlobalPosition = ...;    // AFTER AddChild (needs scene tree)
unit.SetFacing(Vector3.Left); // AFTER AddChild (needs GlobalPosition)
```

### Facing for Quaternius/Kenney models (forward = +Z)
`LookAt(target)` aligns **-Z** toward target, but these models face **+Z**. Use
`GodotEx.FacePlusZ(node, dir)` → `LookAt(GlobalPosition - dir, Up)`.

### Ray vs Y=0 plane (no physics)
`GodotEx.RayToGroundPlane(cam, screenPos, out world)` — `ProjectRayOrigin/Normal`,
`t = -from.Y/dir.Y`.

### Force animation loops (glTF drops loop flags)
`GodotEx.ForceLoop(anim, "Walk", "Run", "Idle")`.

### State machine guard + forced replay
`SetState` guards with `if (State == state) return`. To force an attack replay, call
`PlayAnim("SwordSlash")` directly. Use a sentinel so the first `SetState` always fires:
`public UnitState State { get; private set; } = (UnitState)(-1);`.

### Static events across scene reloads
`Actor.UnitDied` is a `static Action<Team>`. **ASSIGN it (`= handler`), never `+=`** —
statics survive `ReloadCurrentScene`, so `+=` would stack duplicate handlers. Clear in
`_ExitTree` (`= null`). Same rule for `Engine.TimeScale` and `Actor.BattleActive`.

### Soft separation (no physics)
`Actor.ApplySeparation()` pushes apart overlapping non-structure, non-dead units by the
sum of `CollisionRadius` × `SeparationStrength` (0.7, clamped per frame) each frame.
Structures are excluded so melee can reach them. Bigger `CollisionRadius` = more spacing.

---

## Scene Format (.tscn) Rules

- `load_steps` = total `[ext_resource]` + `[sub_resource]` entries
- Setting `transform` on a **child of an instanced PackedScene** inside `.tscn` does **not**
  work — apply transforms in code after `AddChild()`
- **Enum/`unit_team` exports from `.tscn` are unreliable** — set them in code before `AddChild`
- `.tscn` references scripts by **path**, not class name/namespace — moving a `.cs` file
  changes its path (update the `[ext_resource] path=` + regenerate its `.uid`); renaming the
  class while keeping the path is safe

---

## Character Assets (Quaternius pack)

- **Pack**: `C:\Users\evgbo\OneDrive\Documents\CharacterPack\` (outside project — avoids
  importing 50+ models). Copy only needed `.gltf` into `res://assets/characters/`.
- **`Knight_Male.gltf` clips**: Death, Defeat, Idle, Jump, PickUp, Punch, RecieveHit, Roll,
  Run, Run_Carry, Shoot_OneHanded, SitDown, StandUp, SwordSlash, Victory, Walk, Walk_Carry
- **AnimationPlayer** is nested in the gltf instance: `FindChild("AnimationPlayer", true, false)`
- Quaternius Walk/Run are **in-place** (no root motion) — movement comes from `MoveToward`
- **`.blend` in project folder**: Godot prompts for Blender — click "Disable '.blend' Import" then "Reload from disk"

---

## HP Bars & Rendering Gotchas (`HealthBar3D`)

- **Manual billboard, not material billboard**: a billboarded material ignores node scale
  and its world-X offset does NOT match the camera-right axis under an angled camera →
  the fill drifts sideways. Instead orient the whole pivot to the camera each frame
  (`GlobalTransform = new Transform3D(cam.GlobalBasis, pos)`) so the fill's left-anchor
  (`Scale.X = ratio` + `Position.X = -(1-ratio)*W/2`) lives in true screen space.
- **"Empty but alive" draw order**: two `NoDepthTest` quads at ~same position get sorted by
  distance, so the nearer fill can draw *before* the farther background → looks empty. Fix:
  `Transparency = Alpha` + `RenderPriority` (background 0, fill 1) so fill always draws on top.
- Bar pivot is `TopLevel`, so the owner's `Scale`/rotation never distorts the bar.

## Projectiles (`Projectile`)
- `Projectile : Node3D` (code-built BoxMesh arrow) homes to the target's last known
  position, deals `Damage` via `IDamageable.TakeDamage` on arrival, then `QueueFree`s.
  Handles the target dying mid-flight via a cached `_aimPoint`.
- Multi-shot: `Actor.FireProjectile` fires `1 + max(0, ExtraProjectiles)` arrows in a small
  Z spread (`zOff = (i-(shots-1)/2)*0.45`). Structures spawn them higher (`3.0` vs `1.4`).

## Camera (`CameraRig`)
- Plain class driving an existing `Camera3D`. `Setup(target, cameraPos, zoom, fov)`.
  `HandleInput(e)` returns true if it consumed the event (wheel zoom keeps the angle;
  right-drag pans `target` along the camera's flattened right/forward axes × zoom).
- The owner forwards mouse events: `if (_camRig.HandleInput(@event)) return;` after handling
  its own keys.

## Team tint
- `Actor.ApplyTeamTint` sets a translucent `MaterialOverlay` on every mesh recursively.
  Called BEFORE `BuildVisuals`/`CreateHpBar` so gear keeps its own colors and HP bars stay
  untinted. Override for custom colors.

---

## Known Godot Gotchas
- **AnimationTree vs AnimationPlayer**: official demos use `AnimationTree` state machines for
  smooth blending — worth upgrading to when walk→attack transitions need blending.
- **Base-class `[Export]`/`[Signal]`**: work through inheritance; the source generator
  processes every `partial : GodotObject` regardless of namespace. Derived types expose
  inherited static members/consts via `Derived.Member`.

---

## Visuals (lightweight, web-friendly)
- **Environment**: ProceduralSky, depth `fog_enabled` (not volumetric), Filmic tonemap,
  subtle glow, `adjustment_*` color correction. No SSAO/GI to stay cheap.
- **Ground/props**: `FastNoiseLite` → `NoiseTexture2D` + `color_ramp` Gradient as albedo
  (no image files); low-poly cone "mountains" ringed at the horizon; fog fades them for depth.
- **Long shadows**: low sun angle set in code (light ~12° elevation); keep
  `directional_shadow_max_distance` high enough to fit stretched shadows. Warm sun color
  (`Color(1, 0.93, 0.8)`) + energy set in the scene (runtime code overrides only the transform).
- **Vignette**: full-rect `ColorRect` as FIRST child of a `CanvasLayer` with `mouse_filter =
  Ignore` and a `canvas_item` shader (`smoothstep(0.45,0.88,length(UV-0.5))*0.5` alpha).
