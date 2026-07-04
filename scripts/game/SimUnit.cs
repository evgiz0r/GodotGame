using System.Collections.Generic;
using Godot;
using Framework;

// A data-driven fighter: one class configured from a UnitArchetype, so scenarios can mix
// any roles without a subclass per unit. Set Archetype + UnitTeam + IsBattleUnit BEFORE
// AddChild; set GlobalPosition + SetFacing AFTER.
//
// The body is an instanced Quaternius character (glTF) whose own AnimationPlayer + skeleton
// drive Walk/Run/Idle/SwordSlash/Shoot_OneHanded/Death (the base Actor finds and plays
// them). A per-unit material overlay gives a translucent team wash + a white hit flash.
public partial class SimUnit : Actor
{
    public UnitArchetype Archetype;

    private Node3D _model;                        // instanced character (or procedural fallback)
    private readonly List<MeshInstance3D> _meshes = new();
    private StandardMaterial3D _overlay;          // team wash + hit flash, shared across meshes
    private Color _teamBase;                       // resting overlay color (team tint)
    private float _flash;                          // 1 -> 0 after a hit (white flash)
    private float _deadT;                          // seconds since death (corpse sink/dim)

    private const float FlashTime = 0.18f;
    private const float DeadSettle = 2.5f;         // time to fully sink + darken a corpse
    private const float DeadSink = 0.7f;           // how far a corpse sinks into the ground

    protected override void ConfigureStats()
    {
        var a = Archetype;
        MaxHp = a.MaxHp;
        Damage = a.Damage;
        AttackRange = a.AttackRange;
        MoveSpeed = a.MoveSpeed;
        AttackCooldown = a.AttackCooldown;
        BodyScale = a.BodyScale;
        CollisionRadius = a.CollisionRadius;
        DetectionRange = a.DetectionRange;
        // Heavy front-liners hold the line; everyone else cycles in/out when hit hard.
        CanRetreat = a.Role != Role.Tank && a.Role != Role.Boss;
    }

    protected override Vector3 ComputeBaseScale() => Vector3.One * Archetype.BodyScale;

    protected override IActorBehavior SelectBehavior() => Archetype.Role switch
    {
        Role.Ranged => new RangedBehavior(),
        Role.AoE => new AoeBehavior(Archetype.SplashRadius),
        Role.Healer => new HealerBehavior(),
        Role.Boss => new BossBehavior(Archetype.AttackRange + Archetype.CollisionRadius),
        _ => new MeleeBehavior(),
    };

    protected override void BuildVisuals()
    {
        // The animated character body. Its own AnimationPlayer is picked up by Actor._Ready.
        _model = LoadModel() ?? BuildFallbackBody(Archetype.Color);
        AddChild(_model);
        CollectMeshes(_model, _meshes);

        // A single translucent overlay tints every body mesh with the team color so blue vs
        // red reads at a glance; the same material flashes white on a hit (see _Process).
        _teamBase = UnitTeam == Team.Player
            ? new Color(0.30f, 0.45f, 1.0f, 0.14f)
            : new Color(1.0f, 0.32f, 0.30f, 0.15f);
        _overlay = new StandardMaterial3D
        {
            AlbedoColor = _teamBase,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        foreach (var m in _meshes) m.MaterialOverlay = _overlay;
    }

    private Node3D LoadModel()
    {
        if (string.IsNullOrEmpty(Archetype.Model)) return null;
        var scene = GD.Load<PackedScene>(Archetype.Model);
        return scene?.Instantiate<Node3D>();
    }

    // Fallback capsule+sphere body (used only if the model can't be loaded), so the sim
    // still runs without art. No AnimationPlayer here, so PlayAnim just no-ops.
    private Node3D BuildFallbackBody(Color roleColor)
    {
        var root = new Node3D();
        var bodyMat = new StandardMaterial3D { AlbedoColor = roleColor, Roughness = 0.85f };
        var body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.3f },
            Position = new Vector3(0f, 1.0f, 0f),
        };
        ((CapsuleMesh)body.Mesh).Material = bodyMat;
        root.AddChild(body);

        var head = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.28f, Height = 0.56f },
            Position = new Vector3(0f, 1.75f, 0f),
        };
        ((SphereMesh)head.Mesh).Material =
            new StandardMaterial3D { AlbedoColor = Lighten(roleColor, 0.15f), Roughness = 0.7f };
        root.AddChild(head);
        return root;
    }

    protected override void OnDamaged(float amount)
    {
        _flash = 1f;
        Fx.Burst(GetParent(), GlobalPosition + new Vector3(0f, 1.1f, 0f),
            new Color(0.85f, 0.2f, 0.2f, 0.9f), amount: 6, speed: 3.2f, life: 0.35f, size: 0.12f);
    }

    // Hit flash: lerp the shared overlay toward opaque white, then settle back to the team
    // wash. The character's own AnimationPlayer handles the actual attack/move/death motion.
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_overlay == null) return;

        // After death, sink the corpse into the ground and darken it so the field of the
        // fallen recedes instead of staying a bright pile.
        if (State == UnitState.Dead)
        {
            if (_deadT < DeadSettle)
            {
                _deadT += (float)delta;
                float k = Mathf.Clamp(_deadT / DeadSettle, 0f, 1f);
                if (_model != null) _model.Position = new Vector3(0f, -DeadSink * k, 0f);
                _overlay.AlbedoColor = _teamBase.Lerp(new Color(0.1f, 0.1f, 0.1f, 0.55f), k);
            }
            return;
        }

        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - (float)delta / FlashTime);
            _overlay.AlbedoColor = _teamBase.Lerp(new Color(1f, 1f, 1f, 0.85f), _flash);
        }
        else if (_overlay.AlbedoColor != _teamBase)
        {
            _overlay.AlbedoColor = _teamBase;
        }
    }

    protected override void OnKilled()
    {
        // The character's Death animation lays the body down; add a dust puff for weight.
        Fx.Burst(GetParent(), GlobalPosition + new Vector3(0f, 0.4f, 0f),
            new Color(0.6f, 0.55f, 0.5f, 0.8f), amount: 10, speed: 2.6f, life: 0.5f, size: 0.16f, gravity: -3f);
    }

    private static void CollectMeshes(Node node, List<MeshInstance3D> into)
    {
        if (node is MeshInstance3D mi) into.Add(mi);
        foreach (var child in node.GetChildren())
            CollectMeshes(child, into);
    }

    private static StandardMaterial3D Unshaded(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private static Color Lighten(Color c, float f) =>
        new(Mathf.Min(1f, c.R + f), Mathf.Min(1f, c.G + f), Mathf.Min(1f, c.B + f), c.A);
}
