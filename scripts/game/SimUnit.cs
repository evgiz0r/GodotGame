using Godot;
using Framework;

// A data-driven fighter: one class configured from a UnitArchetype, so scenarios can mix
// any roles without a subclass per unit. Set Archetype + UnitTeam + IsBattleUnit BEFORE
// AddChild; set GlobalPosition + SetFacing AFTER.
public partial class SimUnit : Actor
{
    public UnitArchetype Archetype;

    private Node3D _rig;                        // holds body + head so they animate together
    private MeshInstance3D _body;
    private StandardMaterial3D _bodyMat, _headMat;
    private Color _bodyBase, _headBase;
    private float _attack;                      // 1 -> 0 across one swing (lunge)
    private float _flash;                       // 1 -> 0 after a hit (flash + squash)

    private const float AttackTime = 0.26f;
    private const float LungeDist = 0.5f;
    private const float FlashTime = 0.16f;

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
    }

    protected override Vector3 ComputeBaseScale() => Vector3.One * Archetype.BodyScale;

    protected override IActorBehavior SelectBehavior() => Archetype.Role switch
    {
        Role.Ranged => new RangedBehavior(),
        Role.AoE => new AoeBehavior(Archetype.SplashRadius),
        Role.Healer => new HealerBehavior(),
        Role.Boss => new BossBehavior(Archetype.AttackRange),
        _ => new MeleeBehavior(),
    };

    protected override void BuildVisuals()
    {
        var roleColor = Archetype.Color;

        // Team disc at the feet so sides read instantly from a top-down angle. Stays put
        // (not part of the animated rig) so it reads as the unit's "ground marker".
        var teamColor = UnitTeam == Team.Player
            ? new Color(0.25f, 0.5f, 1.0f)
            : new Color(1.0f, 0.3f, 0.28f);
        var disc = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 0.06f },
            Position = new Vector3(0f, 0.04f, 0f),
        };
        ((CylinderMesh)disc.Mesh).Material = Unshaded(teamColor);
        AddChild(disc);

        // Rig pivots at the unit origin; its local +Z is "forward" (toward the target when
        // facing), so lunging along +Z reads as a melee/ranged swing.
        _rig = new Node3D();
        AddChild(_rig);

        _bodyBase = roleColor;
        _bodyMat = new StandardMaterial3D { AlbedoColor = roleColor, Roughness = 0.85f };
        _body = new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.3f },
            Position = new Vector3(0f, 1.0f, 0f),
        };
        ((CapsuleMesh)_body.Mesh).Material = _bodyMat;
        _rig.AddChild(_body);

        _headBase = Lighten(roleColor, 0.15f);
        _headMat = new StandardMaterial3D { AlbedoColor = _headBase, Roughness = 0.7f };
        var head = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.28f, Height = 0.56f },
            Position = new Vector3(0f, 1.75f, 0f),
        };
        ((SphereMesh)head.Mesh).Material = _headMat;
        _rig.AddChild(head);
    }

    // Every swing (melee/ranged/boss) routes through PlayAnim with an attack clip name;
    // there's no skeleton, so we trigger a procedural lunge instead.
    public override void PlayAnim(string name)
    {
        base.PlayAnim(name);
        if (name is "SwordSlash" or "Punch" or "Shoot_OneHanded")
            _attack = 1f;
    }

    protected override void OnDamaged(float amount) => _flash = 1f;

    // Procedural animation layered on top of the base combat logic.
    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_rig == null || State == UnitState.Dead) return;
        float dt = (float)delta;

        // Attack lunge: quick push forward along local +Z, springing back.
        if (_attack > 0f)
        {
            _attack = Mathf.Max(0f, _attack - dt / AttackTime);
            float p = 1f - _attack;
            _rig.Position = new Vector3(0f, 0f, Mathf.Sin(p * Mathf.Pi) * LungeDist);
        }
        else if (_rig.Position != Vector3.Zero)
        {
            _rig.Position = Vector3.Zero;
        }

        // Hit reaction: flash toward white + a brief squash.
        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - dt / FlashTime);
            _bodyMat.AlbedoColor = _bodyBase.Lerp(Colors.White, _flash * 0.85f);
            _headMat.AlbedoColor = _headBase.Lerp(Colors.White, _flash * 0.85f);
            float sq = 1f + 0.22f * _flash;
            _rig.Scale = new Vector3(sq, 1f - 0.18f * _flash, sq);
        }
        else if (_rig.Scale != Vector3.One)
        {
            _bodyMat.AlbedoColor = _bodyBase;
            _headMat.AlbedoColor = _headBase;
            _rig.Scale = Vector3.One;
        }
    }

    // Tip the corpse over so dead bodies read differently from the living.
    protected override void OnKilled()
    {
        RotateObjectLocal(Vector3.Right, Mathf.Pi * 0.5f);
        Position += new Vector3(0f, -0.25f, 0f);
    }

    private static StandardMaterial3D Unshaded(Color c) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private static Color Lighten(Color c, float f) =>
        new(Mathf.Min(1f, c.R + f), Mathf.Min(1f, c.G + f), Mathf.Min(1f, c.B + f), c.A);
}
