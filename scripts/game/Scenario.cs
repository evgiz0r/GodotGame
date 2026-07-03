using System.Collections.Generic;

// The visual setting a battle is fought in.
public enum EnvironmentKind { Forest, Desert, Snow, Arena, Ruins }

// One block of identical units within an army.
public sealed record ArmyEntry(UnitArchetype Archetype, int Count);

// A pickable matchup: two armies + the environment they fight in.
public sealed record Scenario(
    string Name,
    string Description,
    EnvironmentKind Env,
    ArmyEntry[] Player,
    ArmyEntry[] Enemy);

// The list of battles the player can choose to watch. Add freely.
public static class Presets
{
    public static readonly IReadOnlyList<Scenario> All = new List<Scenario>
    {
        new("Mixed Skirmish", "Balanced arms clash — melee, archers & healers each.",
            EnvironmentKind.Forest,
            new[] { new ArmyEntry(Arch.Grunt, 24), new ArmyEntry(Arch.Archer, 8), new ArmyEntry(Arch.Healer, 2) },
            new[] { new ArmyEntry(Arch.Grunt, 24), new ArmyEntry(Arch.Archer, 8), new ArmyEntry(Arch.Healer, 2) }),

        new("Numbers vs Magic", "55 grunts charge 18 grunts backed by 4 mages.",
            EnvironmentKind.Forest,
            new[] { new ArmyEntry(Arch.Grunt, 55) },
            new[] { new ArmyEntry(Arch.Grunt, 18), new ArmyEntry(Arch.Mage, 4) }),

        new("Mage Bombardment", "8 mages + a grunt guard against a 45-grunt horde.",
            EnvironmentKind.Ruins,
            new[] { new ArmyEntry(Arch.Mage, 8), new ArmyEntry(Arch.Grunt, 14) },
            new[] { new ArmyEntry(Arch.Grunt, 45) }),

        new("Tank Wall", "5 tanks + 12 archers hold against peasants led by knights.",
            EnvironmentKind.Arena,
            new[] { new ArmyEntry(Arch.Tank, 5), new ArmyEntry(Arch.Archer, 12) },
            new[] { new ArmyEntry(Arch.Peasant, 60), new ArmyEntry(Arch.Knight, 6) }),

        new("Swarm vs Elite", "90 bugs swarm 14 knights and 4 mages.",
            EnvironmentKind.Desert,
            new[] { new ArmyEntry(Arch.Bug, 90) },
            new[] { new ArmyEntry(Arch.Knight, 14), new ArmyEntry(Arch.Mage, 4) }),

        new("Boss Challenge", "1 boss versus 80 peasants. Can numbers win?",
            EnvironmentKind.Snow,
            new[] { new ArmyEntry(Arch.Boss, 1) },
            new[] { new ArmyEntry(Arch.Peasant, 80) }),

        new("Duel of Archmages", "10 mages mirror-match — pure AoE chaos.",
            EnvironmentKind.Arena,
            new[] { new ArmyEntry(Arch.Mage, 10) },
            new[] { new ArmyEntry(Arch.Mage, 10) }),
    };
}
