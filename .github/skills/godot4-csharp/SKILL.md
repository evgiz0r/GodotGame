---
name: godot4-csharp
description: 'Godot 4 C# project knowledge for DirectStrikePort autobattler. Use when working on: Godot 4 scenes (.tscn), C# scripts, animations, unit behavior, spawning, raycasting, 3D transforms, camera setup, character models, AnimationPlayer, RoundManager, GameManager, UnitBase, MeleeBehavior, or any Godot-specific patterns in this project.'
argument-hint: 'What Godot 4 topic or problem do you need help with?'
---

# Godot 4 C# — DirectStrikePort Knowledge Base

> **Keep this skill up to date.** When you discover something new (a bug fix, a pattern that works, a Godot quirk), please ask Copilot to update this file so future sessions benefit from it.

---

## Project Setup

- **Engine**: Godot 4.7 stable mono, Forward+ renderer, D3D12, Jolt Physics
- **Language**: C# (.NET 8)
- **GPU**: NVIDIA RTX 3060 — Forward+ features (SSAO, SSIL, glow, shadows) are all available
- **Build**: `dotnet build` from project root — must be done before F5 or after any C# change
- **Scene reload**: After editing `.tscn` externally, use **Scene → Reload Saved Scene** in Godot editor, then F5

---

## Scene Format (.tscn) Rules

- `load_steps` must equal total number of `[ext_resource]` + `[sub_resource]` entries
- `[ext_resource]` = external files (scripts, PackedScenes, gltf)
- `[sub_resource]` = inline resources (meshes, materials, environment)
- Setting `transform` on a **child node of an instanced PackedScene** inside `.tscn` does **not work** — apply transforms in code after `AddChild()`
- Enum exports (`[Export] public Team UnitTeam`) may not apply from `.tscn` `unit_team = 1` reliably — set them in code before `AddChild()` instead
- `instance=ExtResource("id")` instantiates a PackedScene as a node

---

## C# / Godot Patterns That Work

### Setting properties before _Ready runs
```csharp
var unit = scene.Instantiate<UnitBase>();
unit.UnitTeam = Team.Enemy;   // set BEFORE AddChild
unit.IsBattleUnit = true;     // _Ready sees these values
_units.AddChild(unit);        // _Ready runs synchronously here
unit.GlobalPosition = ...;    // set AFTER AddChild (needs scene tree)
unit.SetFacing(Vector3.Left); // set AFTER AddChild (needs GlobalPosition)
```

### Ray vs Y=0 plane (no physics/collision needed)
```csharp
var from = _camera.ProjectRayOrigin(screenPos);
var dir  = _camera.ProjectRayNormal(screenPos);
if (Mathf.Abs(dir.Y) < 0.001f) return;
float t = -from.Y / dir.Y;
var worldPos = from + dir * t;
```

### Facing direction for Quaternius models (forward = +Z, not -Z)
Godot's `LookAt(target)` aligns **-Z** toward target. Quaternius models face **+Z**.
Fix: pass the point *behind* you in the target direction:
```csharp
public void SetFacing(Vector3 worldDirection)
{
    var dir = worldDirection.Normalized(); dir.Y = 0;
    if (dir.LengthSquared() < 0.001f) return;
    LookAt(GlobalPosition - dir, Vector3.Up); // -dir makes +Z face the target
}
```

### Forcing animation loop (gltf files don't always set loop flags)
```csharp
foreach (var name in new[] { "Walk", "Run", "Idle" })
    if (_anim.HasAnimation(name))
        _anim.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
```

### State machine guard + forced replay
`SetState` guards with `if (State == state) return` — call `PlayAnim()` directly when you need to force a replay (e.g. attack animation repeat):
```csharp
unit.SetState(UnitState.Attacking);
unit.PlayAnim("SwordSlash"); // force replay even if already Attacking
```

### State sentinel to ensure first SetState always fires
```csharp
public UnitState State { get; private set; } = (UnitState)(-1);
```

---

## Character Assets

- **Pack location**: `C:\Users\evgbo\OneDrive\Documents\CharacterPack\` (outside project — avoids importing all 50+ models)
- **Format**: `.gltf` (self-contained, no companion `.bin`)
- **Copy only needed models** into `res://assets/characters/`
- **Animations in Knight_Male.gltf**: Death, Defeat, Idle, Jump, PickUp, Punch, RecieveHit, Roll, Run, Run_Carry, Shoot_OneHanded, SitDown, StandUp, SwordSlash, Victory, Walk, Walk_Carry
- **AnimationPlayer location**: nested inside the gltf instance — find with `FindChild("AnimationPlayer", true, false)`

---

## Board Layout (GameManager constants)

```
PlayerZone    | BattleZone        | EnemyZone
X: -26 to -16 | X: -16 to +16     | X: +16 to +26
center: -21   | center: 0 (32 wide)| center: +21
Z: -9 to +9 across all zones (18 deep)
```

- **Command board** (separate placement platform, toward the viewer):
  X: -23..-1, Z: 12..20, center (-12, 0, 16), tan material
- You place build units **on the command board**, not on the field.
- `GameManager.BoardToField(boardPos)` maps a board position → player spawn strip
  (X -23..-17, Z -8..8) preserving relative formation (lerp on both axes).
- Enemies spawn **mirrored** across center: `(-fieldPos.X, 0, fieldPos.Z)`.
- Castles sit at the back of each zone: player X = -24.5, enemy X = +24.5.
- Camera is set in code (`GameManager.SetupCamera`): angled 3/4 from the player
  side — pos (-34, 32, 46), `LookAt(-2, 0, 6)`, FOV 58.
- Player units spawned at round start face `Vector3.Right`, enemies `Vector3.Left`.


---

## Architecture

```
scenes/
  main.tscn          — GameManager script, 3 zone meshes, UI, Units container, RoundManager node
  units/
    grunt.tscn        — Node3D + UnitBase.cs + Knight_Male.gltf child
scripts/
  GameManager.cs     — economy/budget, batch composition + UI, batch spawning, buffs, camera, castles
  RoundManager.cs    — continuous batch director: timer, evolving enemy batches, hard cap, game over
  units/
    UnitBase.cs      — HP, state machine, animation, movement, separation, kind stats, soft-stacking aging
    behaviors/
      IUnitBehavior.cs
      MeleeBehavior.cs   — walk toward + SwordSlash attack loop
      RangedBehavior.cs  — walk into range + fire projectiles (Shoot_OneHanded)
      CasterBehavior.cs  — TODO
      HealerBehavior.cs  — march, find wounded ally in range, heal on cooldown (DONE)
assets/characters/
  Knight_Male.gltf
```

---

## Game Flow (Direct Strike: Batch Commander)

> **Mode: continuous battlefield.** It never resets. Both sides spawn a *batch* every
> `BatchInterval` seconds (default 10). Units persist and fight until they die. You lose
> when your castle falls; you win the run by razing the enemy keep.

1. **Continuous fight**: `UnitBase.BattleActive = true` for the whole run (set in
   `GameManager._Ready`, cleared only on game over). There is NO Build/Battle phase.
2. **Budget economy** (GameManager): one growing `_budget` (gold) reserves your standing
   batch composition. `_budget` grows from `_incomePerSec` (passive, in `_Process`) and a
   per-kill bounty (`OnUnitDied`). Composition = `int[3] _comp` (Melee/Ranged/Healer counts);
   `Reserved()` = Σ count×`UnitCost`. `UnitCost = round(BaseCost × _costMult)`, BaseCost
   `{10,18,25}`. `ChangeComp` blocks adds that exceed `_budget`; the SAME composition respawns
   every batch for free (it stays reserved) — to field a bigger batch, grow the budget.
3. **Batch tick**: `RoundManager._Process` counts down `_timer`; on zero it `BatchNumber++`,
   spawns the evolving enemy batch, and emits `BatchSpawned`. GameManager's `OnBatchSpawned`
   spawns the player's composition (applying temp + run buffs), refreshes the enemy preview.
   `RoundManager.PositionBatch` lays a batch in rows of 8 at its castle's front edge.
4. **Enemy evolution**: `RoundManager.EnemyComposition(batch)` adds archetypes as batches climb
   (Archer @2, Fast @3, Tank @4, Healer @5, Boss every 5th) with growing counts; `NextEnemyPreview()`
   feeds the on-screen "Enemy next: …" label for counter-picking.
5. **Soft stacking**: `UnitBase` battle units age (`Age += delta`); past `AgingStartSec` (22s) they
   lose `AgingDrainPerSec` (5) HP/s and die naturally (no bounty — `Die(bounty:false)`). Healers can
   offset this. **Hard stacking**: `RoundManager.MaxPerSide` (40); `EnforceCap` culls the oldest
   (front of each side's list). Player list lives in GameManager, enemy list in RoundManager.
6. **Game speed**: `Engine.TimeScale` 1/2/3 via SpeedBtn + number keys; guarded by `_choosing` so
   it doesn't override the buff-pick pause.
7. Restart button (on game over) calls `GetTree().ReloadCurrentScene()`; `_Ready` re-sets the
   static `BattleActive`/`TimeScale` because statics survive reloads.

---

## Known Gotchas

- **`.blend` files in project folder**: Godot asks for Blender path. Click "Disable '.blend' Import" then "Reload from disk"
- **C# solution missing**: Run **Project → Tools → C# → Create C# Solution** if `dotnet build` says no project found
- **Enum export not applying from .tscn**: Set in code before `AddChild()`, don't rely on `.tscn` property
- **Build units joining groups**: Set `IsBattleUnit = false` before `AddChild()` so `_Ready()` skips `AddToGroup()`
- **`unit_team = 1` in .tscn not reliable**: Always set team programmatically
- **AnimationTree vs AnimationPlayer**: Official demos use `AnimationTree` with state machines for smooth blending — worth upgrading to when animations need to blend (walk→attack transition)
- **Root motion**: Quaternius Walk animation is in-place (no root motion). Movement comes from `MoveToward()` in behavior
- **Billboard HP bars**: a billboarded `StandardMaterial3D` IGNORES node scale unless
  `BillboardKeepScale = true`. **Better approach (current):** drop the material billboard entirely
  and instead orient the `TopLevel` pivot to match the camera each frame
  (`pivot.GlobalTransform = new Transform3D(cam.GlobalBasis, pos)`). Now the fill's left-anchor
  (`Scale.X = ratio` + `Position.X = -(1-ratio)*W/2`) lives in true screen space, so the bar
  depletes from the right with NO sideways drift. A material billboard's world-X position offset
  does NOT match the camera-right axis under an angled camera → that caused the fill to slide left.
- **HP bar "empty but alive" bug (draw order)**: two `NoDepthTest=true` quads (dark background +
  colored fill) at nearly the same position get sorted front-to-back by the renderer, so the nearer
  fill draws FIRST and the farther background draws OVER it → bar looks empty. Fix: set
  `Transparency = Alpha` and `RenderPriority` on the materials (background 0, fill 1) so the fill is
  guaranteed to render after/on-top of the background regardless of camera distance.
- **Class distinction (one model)**: with only one character mesh, differentiate classes by node
  `Scale` (melee 1.15, ranged 0.9) + code-built un-tinted gear meshes (melee: steel helmet box +
  sword + shield; ranged: pointed hood cone + big bow torus + quiver). Gear is added AFTER
  `ApplyTeamTint()` so it keeps its own colors. HP bar pivot is `TopLevel`, so unit `Scale` does
  not distort the bar.
- **Unit kind system**: `UnitKind { Melee, Ranged, Healer }` field set before `AddChild()`. `_Ready()`
  calls `ApplyKindStats()` (Ranged & Healer override stats; Melee/structures keep set values) and
  picks behavior (`RangedBehavior` / `HealerBehavior` / `MeleeBehavior`). For Healer, `Damage` is the
  per-tick heal amount and `AttackRange` is the heal/search radius.
- **Soft separation (no physics)**: `ApplySeparation()` in `_Process` (after behavior) pushes
  apart overlapping non-structure, non-dead units by `CollisionRadius` sum × 0.5 each frame.
  Structures (castle) are EXCLUDED so melee can still reach them to attack. Cheap, stable.
- **Projectiles**: `Projectile : Node3D` (code-built BoxMesh arrow) homes to target's last known
  position; deals `Damage` via `TakeDamage` on arrival, `QueueFree`s. Handles target dying
  mid-flight via cached `_aimPoint`. `UnitBase.FireProjectile(target)` adds it under `GetParent()`.
- **Camera pan**: right-mouse drag. Move `_camTarget` along camera's flattened
  `Basis.X` (right) and `-Basis.Z` (forward), scaled by `_zoom`, then `ApplyZoom()` (keeps angle).
- **Per-side army lists + caps**: player units live in `GameManager._playerArmy`, enemies in
  `RoundManager._enemyArmy`. Both are pruned (`RoundManager.PruneDead`) and capped
  (`EnforceCap`, oldest-first) on every batch spawn. There is NO command board / placement / build
  phase anymore — units spawn at their castle's front edge via `PositionBatch` and march. Castles sit
  at the very back of each zone (`PlayerZoneMinX+2`, `EnemyZoneMaxX-2`); both have a defensive volley
  (`Damage>0`) so each side can shoot back.
- **Castle defensive attack**: structures with `Damage > 0` run `StructureDefense()` from `_Process`
  (it's called before the structure `return`). Every `AttackCooldown` it fires a `FireProjectile`
  volley at EVERY enemy within `AttackRange` (player castle: Damage 45, range 13, cd 0.6). Lets a
  losing player fight attackers off so the run continues until escalating waves overwhelm the keep.
  Projectiles spawn higher for structures (`IsStructure ? 3.0f : 1.4f`).

- **Permanent buff / run-upgrade system**: `GameManager` owns a `RunState` (top-level class) with
  cumulative multipliers (`HpMult`, `DamageMult`, `AtkSpeedMult`, `MoveSpeedMult`), `ExtraProjectiles`,
  `GoldPerKill`, and `BuffsTaken`; `EvolutionTier => BuffsTaken / 2`. Buffs are offered every
  `BuffEveryKills` (12) enemy kills via `ShowBuffChoices()` (pauses with `Engine.TimeScale = 0`,
  resumes on pick). The pool spans **three categories**: `[Army]` permanent (mutate `RunState` +
  `ReapplyToArmy()`), `[Batch]` temporary (add a `TempBuff` with `BatchesLeft`), `[Econ]` economy
  (mutate `_budget`/`_incomePerSec`/`_costMult`/batch interval). `ApplyRunToUnit(u)` copies run buffs
  BEFORE `AddChild`; temp buffs are multiplied on top per spawned unit; `u.ReapplyBuffs()` updates
  living units in place.
- **Temporary batch buffs**: `TempBuff { BatchesLeft, HpMult, AtkSpeedMult, ExtraMelee, ExtraRanged,
  DoubleMelee }`. `SpawnPlayerBatch` aggregates all active temp buffs, builds the spawn counts
  (`melee = comp×(dbl?2:1)+extra`, etc.), bakes the per-unit mults into the spawned units, then
  decrements `BatchesLeft` and drops expired ones — so the bonus only affects the next N batches.
- **Effective stats vs base stats**: `UnitBase` captures `_baseMaxHp/_baseDamage/_baseCooldown/
  _baseMoveSpeed` once in `_Ready` (after `ApplyKindStats`). `ComputeEffectiveStats()` derives the
  live stats from base × buff each time: `MaxHp = base*BuffHpMult`, `AttackCooldown = base /
  max(0.25, BuffAtkSpeedMult)`, etc. `ReapplyBuffs()` preserves the current HP *ratio* across a
  MaxHp change, then clamps and refreshes the HP bar. Always edit base fields + buffs, never the
  live stat directly, or the next recompute will overwrite it.
- **Evolution size scaling**: units grow with `EvolutionTier`. `_baseScale` is captured in `_Ready`
  (`Vector3.One * (Kind==Ranged?0.9:1.15) * BodyScale`), and `ComputeEffectiveStats` sets
  `Scale = _baseScale * (1 + 0.12*EvolutionTier)` for non-structures. `BodyScale` is a per-archetype
  size multiplier set BEFORE `AddChild` (e.g. enemy Tank 1.5, Boss 2.4, Fast 0.8).
- **Gold per kill via static event**: `UnitBase.UnitDied` is a `static System.Action<Team>` invoked in
  `TakeDamage` when a unit dies. `GameManager._Ready` ASSIGNS it (`UnitBase.UnitDied = OnUnitDied;`,
  NOT `+=`) and `_ExitTree` clears it (`= null;`) — statics survive scene reloads, so `+=` would stack
  duplicate handlers across reloads. `OnUnitDied(Team)` pays `1 + GoldPerKill` gold for enemy deaths.
- **Multi-projectile**: `FireProjectile` fires `1 + max(0, ExtraProjectiles)` arrows in a small Z
  spread (`zOff = (i-(shots-1)/2)*0.45`). Drives the "+1 Arrow" buff and lets the castle volley scale.
- **Enemy archetypes** (`RoundManager`): `enum EnemyArch { Grunt, Fast, Tank, Archer, Healer, Boss }`.
  `PickArchetype(wave, i)` unlocks variety by wave (Archer @2, Fast @3, Tank @4, Healer @5; Boss every
  5th wave). `SpawnEnemy(arch, hpScale, dmgScale)` sets `Kind`/`MoveSpeed`/`BodyScale`/`CollisionRadius`
  and tweaks the scales per role (Fast: hp×0.6 fast/small; Tank: hp×2.2 slow/big; Boss: hp×6 huge).
- **Game speed control**: `Engine.TimeScale` cycles `1/2/3` via a `SpeedBtn` (top-right) and number
  keys 1/2/3 (`GameManager._Input` → `SetSpeed`). Reset to `1f` in `_Ready` and `_ExitTree` so a
  reloaded scene never inherits a fast clock.

---

## Next Steps (as of session 2026-06-20)

- [x] Archer unit (RangedBehavior, projectile system)
- [x] Unit selection UI (Grunt / Archer buttons)
- [x] Economy (gold, unit costs, army cap) + endless survival waves + castle-as-lives + restart
- [x] Ranger distinct look (code-built bow + quiver; no separate model yet)
- [ ] Dedicated archer model (drop a .gltf in assets/characters and swap in grunt.tscn / per-kind scene)
- [x] Per-unit XP / level-ups + heroes — done via run-wide permanent buffs + evolution size tiers
- [ ] AnimationTree upgrade for smooth transitions
- [x] HP bars above units — billboard quads built in code in `UnitBase` (configurable `HpBarWidth/Height/YOffset`)- [x] Team color tint — translucent `material_overlay` on all mesh instances (`UnitBase.ApplyTeamTint`)
- [x] Enemy variety (Grunt/Fast/Tank/Archer/Healer/Boss) + Healer behavior
- [x] Game speed control (1x/2x/3x via button + number keys)
- [x] Continuous batch-commander loop: budget composition, timed batches, soft+hard stacking,
      evolving enemy batches w/ preview, 3-category buffs (Army/Batch/Economy)
- [ ] Round counter / lives system
- [ ] Implement `CasterBehavior` (AoE/buff caster — still a stub)

## Visuals (lightweight, web-friendly)

- **Environment** (`main.tscn` `Environment_1`): ProceduralSky, depth `fog_enabled`
  (NOT volumetric), Filmic tonemap, subtle glow/bloom, `adjustment_*` color correction.
  No SSAO/GI to stay cheap.
- **Shadows**: DirectionalLight `shadow_blur=1.5`, `light_angular_distance=1.5` for
  soft penumbra; `project.godot` directional_shadow size=2048 (low-res), filter quality=1.
- **Ground**: `FastNoiseLite` → `NoiseTexture2D` with a `color_ramp` Gradient as
  albedo (grass on BattleZone, dirt on CommandBoard), tiled via `uv1_scale`. No image files.
- **Props**: `scenes/props/` — `tree.tscn`, `rock.tscn`, `flag_blue/red.tscn` (low-poly,
  no physics). Instanced under `Props` node; set transforms on the instance ROOT (works),
  keep them out of the field/board footprint.
- **Team tint**: blue overlay for player, red for enemy; applied BEFORE `CreateHpBar()`
  so HP bar quads stay untinted.
- **Arena backdrop** (`Backdrop` node in `main.tscn`): large 280×280 ground plane at
  y=-0.05 (grass noise, big `uv1_scale`), plus a ring of 9 low-poly cone "mountains"
  (`Mesh_Mountain`, atmospheric blue-grey) at radius ~140. Trees/rocks moved to the far
  edges (radius ~40-60), out of the play field. Fog fades the mountains for depth.
- **Long shadows**: sun angle set low in code (`GameManager.SetupSun`) — light at
  (-40,16,30) looking at (25,0,-12), ~12° elevation. Keep `directional_shadow_max_distance`
  high enough (80) to fit the stretched shadows.
- **Warm sun**: `DirectionalLight3D` uses `light_color = Color(1, 0.93, 0.8)` (warm) and
  `light_energy = 1.5` in `main.tscn`. `SetupSun` overrides the light TRANSFORM at runtime but
  NOT its color/energy, so set those in the scene.
- **Vignette (post-processing)**: a full-rect `ColorRect` named `Vignette` as the FIRST child of the
  `UI` CanvasLayer with `mouse_filter = 2` (Ignore) and an embedded `canvas_item` `Shader` +
  `ShaderMaterial`. Fragment: `vec2 uv = UV-0.5; float d = length(uv); COLOR = vec4(0,0,0,
  smoothstep(0.45,0.88,d)*0.5);`. First child = drawn behind later UI buttons; Ignore = no click
  blocking.
- **Arena border**: an `Arena` node holds 4 `BoxMesh` border rails framing the field
  (N/S `size (62,1,1)` at `z=±9.5`, E/W `size (1,1,20)` at `x=±30.5`, raised `y=0.5`, shared stone
  `Mat_Border`). Pure decoration, no collision.

