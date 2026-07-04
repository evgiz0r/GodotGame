using Godot;

// A unit "role" drives which behavior (AI brain) the unit uses and hints at its look.
public enum Role { Melee, Ranged, AoE, Tank, Healer, Boss, Swarm }

// Immutable data describing one kind of fighter. Games pick these into armies; a single
// SimUnit is configured from one of these, so we don't need a subclass per unit type.
public sealed record UnitArchetype(
    string Name,
    Role Role,
    float MaxHp,
    float Damage,
    float AttackRange,
    float MoveSpeed,
    float AttackCooldown,
    float BodyScale,
    Color Color,
    float CollisionRadius = 0.5f,
    float DetectionRange = 14f,
    float SplashRadius = 0f,
    // Quaternius character model instanced as the unit's body. Null -> procedural fallback.
    string Model = null);

// The catalog of fighters available to scenarios. Tune stats here; add new entries freely.
public static class Arch
{
    public static readonly UnitArchetype Grunt = new(
        "Grunt", Role.Melee, MaxHp: 40, Damage: 8, AttackRange: 1.6f, MoveSpeed: 2.4f,
        AttackCooldown: 1.0f, BodyScale: 1.0f, Color: new Color(0.60f, 0.62f, 0.66f),
        CollisionRadius: 0.95f, Model: "res://assets/characters/Soldier_Male.gltf");

    public static readonly UnitArchetype Peasant = new(
        "Peasant", Role.Melee, MaxHp: 18, Damage: 4, AttackRange: 1.4f, MoveSpeed: 2.2f,
        AttackCooldown: 1.2f, BodyScale: 0.9f, Color: new Color(0.55f, 0.4f, 0.26f),
        CollisionRadius: 0.82f, Model: "res://assets/characters/Worker_Male.gltf");

    public static readonly UnitArchetype Knight = new(
        "Knight", Role.Melee, MaxHp: 90, Damage: 16, AttackRange: 1.8f, MoveSpeed: 2.3f,
        AttackCooldown: 0.9f, BodyScale: 1.1f, Color: new Color(0.80f, 0.82f, 0.9f),
        CollisionRadius: 1.0f, Model: "res://assets/characters/Knight_Male.gltf");

    public static readonly UnitArchetype Tank = new(
        "Tank", Role.Tank, MaxHp: 220, Damage: 12, AttackRange: 1.9f, MoveSpeed: 1.4f,
        AttackCooldown: 1.4f, BodyScale: 1.5f, Color: new Color(0.35f, 0.45f, 0.55f),
        CollisionRadius: 1.35f, Model: "res://assets/characters/Knight_Golden_Male.gltf");

    public static readonly UnitArchetype Archer = new(
        "Archer", Role.Ranged, MaxHp: 26, Damage: 7, AttackRange: 9f, MoveSpeed: 2.2f,
        AttackCooldown: 1.1f, BodyScale: 0.95f, Color: new Color(0.3f, 0.62f, 0.32f),
        CollisionRadius: 0.82f, DetectionRange: 18f, Model: "res://assets/characters/Elf.gltf");

    public static readonly UnitArchetype Mage = new(
        "Mage", Role.AoE, MaxHp: 24, Damage: 7, AttackRange: 8f, MoveSpeed: 1.9f,
        AttackCooldown: 2.3f, BodyScale: 1.0f, Color: new Color(0.6f, 0.35f, 0.85f),
        CollisionRadius: 0.84f, DetectionRange: 18f, SplashRadius: 2.1f, Model: "res://assets/characters/Wizard.gltf");

    public static readonly UnitArchetype Healer = new(
        "Healer", Role.Healer, MaxHp: 34, Damage: 10, AttackRange: 6f, MoveSpeed: 2.0f,
        AttackCooldown: 1.2f, BodyScale: 0.95f, Color: new Color(0.95f, 0.9f, 0.55f),
        CollisionRadius: 0.84f, DetectionRange: 16f, Model: "res://assets/characters/Witch.gltf");

    public static readonly UnitArchetype Bug = new(
        "Bug", Role.Swarm, MaxHp: 12, Damage: 4, AttackRange: 1.2f, MoveSpeed: 3.3f,
        AttackCooldown: 0.7f, BodyScale: 0.6f, Color: new Color(0.4f, 0.5f, 0.18f),
        CollisionRadius: 0.55f, Model: "res://assets/characters/Goblin_Male.gltf");

    public static readonly UnitArchetype Boss = new(
        "Boss", Role.Boss, MaxHp: 900, Damage: 32, AttackRange: 3.0f, MoveSpeed: 1.7f,
        AttackCooldown: 1.3f, BodyScale: 3.0f, Color: new Color(0.75f, 0.15f, 0.18f),
        CollisionRadius: 2.3f, DetectionRange: 24f, Model: "res://assets/characters/Viking_Male.gltf");
}
