using System.Collections.Generic;
using Framework;

// The visual setting a battle is fought in.
public enum EnvironmentKind { Forest, Desert, Snow, Arena, Ruins }

// Weather modifiers layered on any environment (see WeatherFx / WeatherState).
public enum Weather { Clear, Rain, Fog, Wind, Snow }

// How an army is arranged when it spawns.
public enum Formation { Grid, Line, Wedge, Spread, Block }

// One block of identical units within an army.
public sealed record ArmyEntry(UnitArchetype Archetype, int Count);

// A timed wave that joins `Side` from the back: `MaxWaves` drops of `Units`, `Interval`
// seconds apart, starting `FirstDelay` seconds after "FIGHT!".
public sealed record Reinforcement(
    Team Side,
    ArmyEntry[] Units,
    float Interval,
    int MaxWaves,
    float FirstDelay = 6f);

// A pickable matchup: two armies, the environment + weather they fight in, how each side is
// arranged, an optional defended castle, and optional reinforcement waves.
public sealed record Scenario(
    string Name,
    string Description,
    EnvironmentKind Env,
    ArmyEntry[] Player,
    ArmyEntry[] Enemy,
    Weather Weather = Weather.Clear,
    Formation PlayerFormation = Formation.Grid,
    Formation EnemyFormation = Formation.Grid,
    Team? CastleSide = null,
    Reinforcement[] Reinforcements = null);

// The list of battles the player can choose to watch. Add freely.
public static class Presets
{
    public static readonly IReadOnlyList<Scenario> All = new List<Scenario>
    {
        new("Mixed Skirmish", "Balanced arms clash — melee, archers & healers each.",
            EnvironmentKind.Forest,
            new[] { new ArmyEntry(Arch.Grunt, 24), new ArmyEntry(Arch.Archer, 8), new ArmyEntry(Arch.Healer, 2) },
            new[] { new ArmyEntry(Arch.Grunt, 24), new ArmyEntry(Arch.Archer, 8), new ArmyEntry(Arch.Healer, 2) }),

        new("Numbers vs Magic", "55 grunts charge grunts backed by mages & archers. Wind bends the arrows.",
            EnvironmentKind.Forest,
            new[] { new ArmyEntry(Arch.Grunt, 55) },
            new[] { new ArmyEntry(Arch.Grunt, 16), new ArmyEntry(Arch.Mage, 4), new ArmyEntry(Arch.Archer, 6) },
            Weather: Weather.Wind,
            PlayerFormation: Formation.Wedge, EnemyFormation: Formation.Line),

        new("Mage Bombardment", "8 mages + a grunt guard against a 45-grunt horde in thick fog.",
            EnvironmentKind.Ruins,
            new[] { new ArmyEntry(Arch.Mage, 8), new ArmyEntry(Arch.Grunt, 14) },
            new[] { new ArmyEntry(Arch.Grunt, 45) },
            Weather: Weather.Fog,
            PlayerFormation: Formation.Block, EnemyFormation: Formation.Spread),

        new("Tank Wall", "5 tanks + 12 archers hold the line against peasants led by knights. Rain.",
            EnvironmentKind.Arena,
            new[] { new ArmyEntry(Arch.Tank, 5), new ArmyEntry(Arch.Archer, 12) },
            new[] { new ArmyEntry(Arch.Peasant, 60), new ArmyEntry(Arch.Knight, 6) },
            Weather: Weather.Rain,
            PlayerFormation: Formation.Line, EnemyFormation: Formation.Wedge),

        new("Swarm vs Elite", "90 bugs swarm 14 knights and 4 mages formed up tight.",
            EnvironmentKind.Desert,
            new[] { new ArmyEntry(Arch.Bug, 90) },
            new[] { new ArmyEntry(Arch.Knight, 14), new ArmyEntry(Arch.Mage, 4) },
            PlayerFormation: Formation.Spread, EnemyFormation: Formation.Block),

        new("Boss Challenge", "1 boss versus 80 peasants in the falling snow. Can numbers win?",
            EnvironmentKind.Snow,
            new[] { new ArmyEntry(Arch.Boss, 1) },
            new[] { new ArmyEntry(Arch.Peasant, 80) },
            Weather: Weather.Snow,
            EnemyFormation: Formation.Spread),

        new("Duel of Archmages", "10 mages mirror-match — pure AoE chaos through the fog.",
            EnvironmentKind.Arena,
            new[] { new ArmyEntry(Arch.Mage, 10) },
            new[] { new ArmyEntry(Arch.Mage, 10) },
            Weather: Weather.Fog,
            PlayerFormation: Formation.Spread, EnemyFormation: Formation.Spread),

        new("Storm the Castle", "Wedge assault on a defended keep — grunts pour in over the rain.",
            EnvironmentKind.Arena,
            new[] { new ArmyEntry(Arch.Grunt, 18), new ArmyEntry(Arch.Knight, 4) },
            new[] { new ArmyEntry(Arch.Archer, 10), new ArmyEntry(Arch.Grunt, 8) },
            Weather: Weather.Rain,
            PlayerFormation: Formation.Wedge, EnemyFormation: Formation.Line,
            CastleSide: Team.Enemy,
            Reinforcements: new[]
            {
                new Reinforcement(Team.Player,
                    new[] { new ArmyEntry(Arch.Grunt, 8), new ArmyEntry(Arch.Archer, 3) },
                    Interval: 9f, MaxWaves: 5, FirstDelay: 7f),
            }),

        new("Endless Assault", "Defenders dig in as fresh attackers keep arriving through the snow.",
            EnvironmentKind.Snow,
            new[] { new ArmyEntry(Arch.Knight, 8), new ArmyEntry(Arch.Archer, 6), new ArmyEntry(Arch.Healer, 2) },
            new[] { new ArmyEntry(Arch.Grunt, 20) },
            Weather: Weather.Snow,
            PlayerFormation: Formation.Line, EnemyFormation: Formation.Wedge,
            Reinforcements: new[]
            {
                new Reinforcement(Team.Enemy,
                    new[] { new ArmyEntry(Arch.Grunt, 10), new ArmyEntry(Arch.Peasant, 6) },
                    Interval: 8f, MaxWaves: 6, FirstDelay: 6f),
            }),
    };
}
