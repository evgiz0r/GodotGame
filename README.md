# GodotGame — Direct Strike: Batch Commander

A continuous-battlefield autobattler built in **Godot 4 (C# / .NET 8)**.

Two castles, one endless front line. Every ~10 seconds both sides deploy a *batch*
of units that march out and fight until they die. You shape your standing army with a
**gold budget**, counter the enemy's evolving compositions, and bank **buffs** to tip
the war in your favor. Win by razing the enemy keep; lose if your castle falls.

## Core systems

- **Continuous fight** — the battlefield never resets; units persist until they die.
- **Budget economy** — a growing gold budget reserves your batch composition
  (Melee / Archer / Healer). Passive income + per-kill bounties let you field bigger batches.
- **Timed batches** — both sides spawn at their castle every interval and march forward.
- **Enemy evolution** — later batches add Archers, Fast, Tanks, Healers, and a Boss every 5th,
  with an on-screen preview of the next enemy wave for counter-picking.
- **Soft + hard stacking** — units age and decay over time; a per-side cap culls the oldest.
- **Three buff categories** — `[Army]` permanent, `[Batch]` temporary (next 1–3 batches),
  and `[Economy]` (budget / income / costs / timer).

## Run it

1. Open the project in **Godot 4.x (.NET / Mono build)**.
2. Build the C# solution: `dotnet build`.
3. Press **F5** to play. Speed: `1` / `2` / `3` (also via the on-screen button).

## Project layout

```
scripts/
  GameManager.cs     — economy/budget, composition + UI, batch spawning, buffs, camera, castles
  RoundManager.cs    — continuous batch director: timer, evolving enemy batches, cap, game over
  units/
    UnitBase.cs      — HP, state machine, animation, movement, separation, soft-stacking aging
    behaviors/       — Melee / Ranged / Healer (Caster is a stub)
scenes/              — main.tscn, castle, unit scenes
assets/              — character models
```
