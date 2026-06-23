using Godot;

public enum Team { Player, Enemy }
public enum UnitState { Walking, Idle, Attacking, Dead }
public enum UnitKind { Melee, Ranged, Healer }

public partial class UnitBase : Node3D
{
    [Export] public Team UnitTeam = Team.Player;
    [Export] public float MaxHp = 100f;
    [Export] public float Damage = 25f;
    [Export] public float AttackRange = 1.5f;
    [Export] public float MoveSpeed = 2f;
    [Export] public float AttackCooldown = 1.2f;
    // Class of the unit (drives stats + behavior). Set before AddChild.
    public UnitKind Kind = UnitKind.Melee;
    // Only engage enemies within this radius; otherwise keep advancing.
    public float DetectionRange = 14f;
    // Per-unit stat multipliers for escalating enemy waves. Set before AddChild.
    public float HpScale = 1f;
    public float DamageScale = 1f;
    // Soft-body collision radius used for separation (so units don't stack).
    public float CollisionRadius = 0.5f;
    // false = inert placeholder; true = active fighter that marches + fights
    public bool IsBattleUnit = false;
    // true = stationary structure (castle): targetable + takes damage, never acts
    public bool IsStructure = false;

    // --- Soft stacking: battle units slowly decay after a grace period so armies
    // grow over time but never live forever (keeps perf + tension stable). ---
    public float Age = 0f;
    public static float AgingStartSec = 22f;
    public static float AgingDrainPerSec = 5f;

    // --- Run buffs / evolution (set by GameManager before AddChild, or via ReapplyBuffs). ---
    public float BuffHpMult = 1f;
    public float BuffDamageMult = 1f;
    public float BuffAtkSpeedMult = 1f;
    public float BuffMoveSpeedMult = 1f;
    public int ExtraProjectiles = 0;
    public int EvolutionTier = 0;
    // Enemy archetype body-size multiplier (tank/boss big, fast small). Set before AddChild.
    public float BodyScale = 1f;

    // Global gate: units only fight while a wave is active (frozen during Build phase).
    public static bool BattleActive = false;

    // Raised whenever any unit dies (arg = the dead unit's team). GameManager ASSIGNS
    // this (not +=) so it never leaks/accumulates across scene reloads.
    public static System.Action<Team> UnitDied;

    public float Hp { get; private set; }
    public UnitState State { get; private set; } = (UnitState)(-1);

    private AnimationPlayer _anim;
    private IUnitBehavior _behavior;
    private float _structTimer = 0f; // structure defensive-attack cooldown timer
    private float _baseMaxHp, _baseDamage, _baseCooldown, _baseMoveSpeed;
    private Vector3 _baseScale = Vector3.One;

    // HP bar (billboard quads created in code)
    public float HpBarWidth = 1.4f;
    public float HpBarHeight = 0.18f;
    public float HpBarYOffset = 2.6f;
    private Node3D _hpBarPivot;
    private MeshInstance3D _hpFill;
    private Camera3D _cam;

    public override void _Ready()
    {
        ApplyKindStats();
        MaxHp *= HpScale;
        Damage *= DamageScale;
        _baseMaxHp = MaxHp;
        _baseDamage = Damage;
        _baseCooldown = AttackCooldown;
        _baseMoveSpeed = MoveSpeed;
        _baseScale = IsStructure
            ? Vector3.One
            : Vector3.One * (Kind == UnitKind.Ranged ? 0.9f : 1.15f) * BodyScale;
        ComputeEffectiveStats();
        Hp = MaxHp;
        if (IsBattleUnit)
            AddToGroup(UnitTeam == Team.Player ? "player_units" : "enemy_units");
        ApplyTeamTint();
        if (!IsStructure)
        {
            // Make the classes read very differently at a glance.
            if (Kind == UnitKind.Ranged) AddRangedGear();
            else if (Kind == UnitKind.Healer) AddHealerGear();
            else AddMeleeGear();
        }
        CreateHpBar();
        _anim = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (_anim != null)
        {
            foreach (var animName in new[] { "Walk", "Run", "Idle" })
                if (_anim.HasAnimation(animName))
                    _anim.GetAnimation(animName).LoopMode = Animation.LoopModeEnum.Linear;
        }
        _behavior = Kind switch
        {
            UnitKind.Ranged => new RangedBehavior(),
            UnitKind.Healer => new HealerBehavior(),
            _ => new MeleeBehavior(),
        };
        SetState(IsBattleUnit ? UnitState.Walking : UnitState.Idle);
    }

    // Distinct silhouette for archers: pointed hood + a big bow + back quiver (kept un-tinted).
    private void AddRangedGear()
    {
        var wood = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.22f, 0.1f), Roughness = 1f };
        var bowMesh = new TorusMesh { InnerRadius = 0.6f, OuterRadius = 0.72f, Rings = 16, RingSegments = 6 };
        bowMesh.Material = wood;
        var bow = new MeshInstance3D { Mesh = bowMesh };
        bow.Position = new Vector3(0.45f, 1.1f, 0.35f);
        bow.RotationDegrees = new Vector3(90f, 0f, 0f);
        AddChild(bow);

        var quiverMat = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.13f, 0.07f), Roughness = 1f };
        var quiverMesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.7f };
        quiverMesh.Material = quiverMat;
        var quiver = new MeshInstance3D { Mesh = quiverMesh };
        quiver.Position = new Vector3(-0.22f, 1.35f, -0.28f);
        quiver.RotationDegrees = new Vector3(18f, 0f, 22f);
        AddChild(quiver);

        // Pointed ranger hood.
        var hoodMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.34f, 0.16f), Roughness = 1f };
        var hoodMesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.34f, Height = 0.6f, RadialSegments = 8 };
        hoodMesh.Material = hoodMat;
        var hood = new MeshInstance3D { Mesh = hoodMesh };
        hood.Position = new Vector3(0f, 1.95f, 0f);
        AddChild(hood);
    }

    // Distinct silhouette for grunts: steel helmet + sword + shield (kept un-tinted).
    private void AddMeleeGear()
    {
        var steel = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.64f, 0.68f), Metallic = 0.6f, Roughness = 0.4f };

        var helmMesh = new BoxMesh { Size = new Vector3(0.5f, 0.42f, 0.5f) };
        helmMesh.Material = steel;
        var helm = new MeshInstance3D { Mesh = helmMesh };
        helm.Position = new Vector3(0f, 1.8f, 0f);
        AddChild(helm);

        var bladeMesh = new BoxMesh { Size = new Vector3(0.1f, 1.1f, 0.1f) };
        bladeMesh.Material = steel;
        var sword = new MeshInstance3D { Mesh = bladeMesh };
        sword.Position = new Vector3(0.4f, 1.2f, 0.3f);
        sword.RotationDegrees = new Vector3(20f, 0f, -10f);
        AddChild(sword);

        var shieldMat = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.32f, 0.18f), Roughness = 0.8f };
        var shieldMesh = new BoxMesh { Size = new Vector3(0.12f, 0.7f, 0.5f) };
        shieldMesh.Material = shieldMat;
        var shield = new MeshInstance3D { Mesh = shieldMesh };
        shield.Position = new Vector3(-0.38f, 1.05f, 0.05f);
        AddChild(shield);
    }

    // Distinct silhouette for healers: a hood + glowing green orb on a staff.
    private void AddHealerGear()
    {
        var robe = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.5f, 0.35f), Roughness = 1f };
        var hatMesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 0.32f, Height = 0.7f, RadialSegments = 8 };
        hatMesh.Material = robe;
        var hat = new MeshInstance3D { Mesh = hatMesh, Position = new Vector3(0f, 1.95f, 0f) };
        AddChild(hat);

        var orbMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 1f, 0.6f),
            EmissionEnabled = true,
            Emission = new Color(0.3f, 1f, 0.5f),
            EmissionEnergyMultiplier = 2.5f,
        };
        var orbMesh = new SphereMesh { Radius = 0.18f, Height = 0.36f };
        orbMesh.Material = orbMat;
        var orb = new MeshInstance3D { Mesh = orbMesh, Position = new Vector3(0.42f, 1.55f, 0.3f) };
        AddChild(orb);
    }

    // Ranged units override their base stats; melee/structures keep what was set.
    private void ApplyKindStats()
    {
        if (Kind == UnitKind.Ranged && !IsStructure)
        {
            MaxHp = 70f;
            Damage = 18f;
            AttackRange = 9f;
            MoveSpeed = 1.8f;
            AttackCooldown = 1.5f;
        }
        else if (Kind == UnitKind.Healer && !IsStructure)
        {
            MaxHp = 60f;
            Damage = 14f;        // = heal-per-tick
            AttackRange = 8f;    // = heal radius
            MoveSpeed = 1.6f;
            AttackCooldown = 1.0f;
        }
    }

    // Recompute effective stats from captured base values * current run buffs.
    private void ComputeEffectiveStats()
    {
        MaxHp = _baseMaxHp * BuffHpMult;
        Damage = _baseDamage * BuffDamageMult;
        AttackCooldown = _baseCooldown / Mathf.Max(0.25f, BuffAtkSpeedMult);
        MoveSpeed = _baseMoveSpeed * BuffMoveSpeedMult;
        if (!IsStructure)
            Scale = _baseScale * (1f + 0.12f * EvolutionTier);
    }

    // Apply updated run buffs to an already-living unit, preserving its HP ratio.
    public void ReapplyBuffs()
    {
        float ratio = MaxHp > 0f ? Mathf.Clamp(Hp / MaxHp, 0f, 1f) : 1f;
        ComputeEffectiveStats();
        Hp = Mathf.Clamp(MaxHp * ratio, 1f, MaxHp);
        UpdateHpBar();
    }

    // Default march direction when no enemy is in detection range.
    public Vector3 AdvanceDir => UnitTeam == Team.Player ? Vector3.Right : Vector3.Left;

    public override void _Process(double delta)
    {
        UpdateHpBarPosition();
        if (!IsBattleUnit || State == UnitState.Dead) return;
        if (IsStructure)
        {
            if (BattleActive && Damage > 0f) StructureDefense(delta);
            return;
        }
        if (!BattleActive) { SetState(UnitState.Idle); return; }

        Age += (float)delta;
        if (Age > AgingStartSec) AgeDecay(delta);
        if (State == UnitState.Dead) return; // aging may have killed us

        if (Kind == UnitKind.Healer)
        {
            _behavior.OnUpdate(this, null, delta); // healer finds wounded allies itself
        }
        else
        {
            UnitBase target = FindNearestEnemy();
            if (target != null)
                _behavior.OnUpdate(this, target, delta);
            else
                Advance(delta);
        }

        ApplySeparation();
    }

    // No enemy within range yet: keep pushing toward the enemy side.
    private void Advance(double delta)
    {
        SetState(UnitState.Walking);
        Vector3 dir = AdvanceDir;
        FaceToward(GlobalPosition + dir);
        MoveToward(dir, delta);
    }

    // Public wrapper so behaviors (e.g. healer with no patient) can keep marching.
    public void AdvanceStep(double delta) => Advance(delta);

    // Castles with Damage > 0 fire a heavy volley at every enemy in range, so a
    // player who would otherwise lose can fight attackers off and keep the run
    // going until the escalating waves eventually overwhelm the keep.
    private void StructureDefense(double delta)
    {
        _structTimer -= (float)delta;
        if (_structTimer > 0f) return;

        string group = UnitTeam == Team.Player ? "enemy_units" : "player_units";
        bool fired = false;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is UnitBase e && e.State != UnitState.Dead
                && Position.DistanceTo(e.Position) <= AttackRange)
            {
                FireProjectile(e);
                fired = true;
            }
        }
        _structTimer = fired ? AttackCooldown : 0.2f;
    }

    // Lightweight soft separation so living units don't stack on one cell.
    private void ApplySeparation()
    {
        Vector3 push = Vector3.Zero;
        foreach (var group in new[] { "player_units", "enemy_units" })
        {
            foreach (var node in GetTree().GetNodesInGroup(group))
            {
                if (node is not UnitBase other || other == this) continue;
                if (other.IsStructure || other.State == UnitState.Dead) continue;
                Vector3 d = Position - other.Position; d.Y = 0f;
                float dist = d.Length();
                float minDist = CollisionRadius + other.CollisionRadius;
                if (dist > 0.0001f && dist < minDist)
                    push += d / dist * (minDist - dist);
                else if (dist <= 0.0001f)
                    push += new Vector3((GetInstanceId() % 2 == 0) ? 0.1f : -0.1f, 0f, 0.07f);
            }
        }
        Position += push * 0.5f;
    }

    // Spawn a projectile toward a target (used by ranged units + castles).
    public void FireProjectile(UnitBase target)
    {
        var color = UnitTeam == Team.Player ? new Color(0.4f, 0.7f, 1f) : new Color(1f, 0.5f, 0.35f);
        int shots = 1 + Mathf.Max(0, ExtraProjectiles);
        float baseY = IsStructure ? 3.0f : 1.4f;
        for (int i = 0; i < shots; i++)
        {
            var proj = new Projectile { Target = target, Damage = Damage, ArrowColor = color };
            GetParent().AddChild(proj);
            float zOff = (i - (shots - 1) / 2f) * 0.45f;
            proj.GlobalPosition = GlobalPosition + new Vector3(0f, baseY, zOff);
        }
    }

    public void SetState(UnitState state)
    {
        if (State == state) return;
        State = state;
        switch (state)
        {
            case UnitState.Walking:   PlayAnim("Walk"); break;
            case UnitState.Idle:      PlayAnim("Idle"); break;
            case UnitState.Attacking: PlayAnim("SwordSlash"); break;
            case UnitState.Dead:      PlayAnim("Death"); break;
        }
    }

    public void MoveToward(Vector3 direction, double delta)
    {
        Position += direction * MoveSpeed * (float)delta;
    }

    // Quaternius models face +Z; LookAt aligns -Z, so invert.
    public void SetFacing(Vector3 worldDirection)
    {
        var dir = worldDirection.Normalized();
        dir.Y = 0;
        if (dir.LengthSquared() < 0.001f) return;
        LookAt(GlobalPosition - dir, Vector3.Up);
    }

    public void FaceToward(Vector3 worldPos)
    {
        var dir = (worldPos - GlobalPosition);
        dir.Y = 0;
        if (dir.LengthSquared() < 0.001f) return;
        SetFacing(dir.Normalized());
    }

    public void TakeDamage(float amount)
    {
        if (State == UnitState.Dead) return;
        Hp -= amount;
        if (Hp <= 0f) Die(bounty: true);
        UpdateHpBar();
    }

    // Slow soft-stacking decay (aging). Dying this way pays no kill bounty.
    private void AgeDecay(double delta)
    {
        Hp -= AgingDrainPerSec * (float)delta;
        if (Hp <= 0f) Die(bounty: false);
        UpdateHpBar();
    }

    private void Die(bool bounty)
    {
        Hp = 0f;
        SetState(UnitState.Dead);
        if (_hpBarPivot != null) _hpBarPivot.Visible = false;
        if (bounty) UnitDied?.Invoke(UnitTeam);
    }

    // Restore HP (used by healers); never revives the dead.
    public void Heal(float amount)
    {
        if (State == UnitState.Dead) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        UpdateHpBar();
    }

    // Nearest same-team, non-structure, living, wounded unit within range (for healers).
    public UnitBase FindWoundedAlly(float range)
    {
        string group = UnitTeam == Team.Player ? "player_units" : "enemy_units";
        UnitBase best = null;
        float bestMissing = 0f;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is UnitBase u && u != this && !u.IsStructure && u.State != UnitState.Dead
                && u.Hp < u.MaxHp && Position.DistanceTo(u.Position) <= range)
            {
                float missing = u.MaxHp - u.Hp;
                if (missing > bestMissing) { bestMissing = missing; best = u; }
            }
        }
        return best;
    }

    // Subtle translucent wash so you can read team at a glance without hiding the model.
    private void ApplyTeamTint()
    {
        float a = IsStructure ? 0.16f : 0.22f; // lighter on castles so stone reads
        var color = UnitTeam == Team.Player
            ? new Color(0.25f, 0.45f, 1.0f, a)
            : new Color(1.0f, 0.25f, 0.25f, a);
        var overlay = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        TintMeshesRecursive(this, overlay);
    }

    private static void TintMeshesRecursive(Node node, Material overlay)
    {
        if (node is MeshInstance3D mi)
            mi.MaterialOverlay = overlay;
        foreach (var child in node.GetChildren())
            TintMeshesRecursive(child, overlay);
    }

    private void CreateHpBar()
    {
        // Top-level so the unit's own rotation never flips/skews the bar.
        _hpBarPivot = new Node3D { Name = "HpBar", TopLevel = true };
        AddChild(_hpBarPivot);

        _hpFill = MakeBarQuad(
            UnitTeam == Team.Player ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.85f, 0.3f, 0.3f),
            0.002f, priority: 1);
        _hpBarPivot.AddChild(MakeBarQuad(new Color(0.05f, 0.05f, 0.05f), 0f, priority: 0)); // background behind
        _hpBarPivot.AddChild(_hpFill);
        UpdateHpBarPosition();
        UpdateHpBar();
    }

    private void UpdateHpBarPosition()
    {
        if (_hpBarPivot == null) return;
        _cam ??= GetViewport().GetCamera3D();
        Vector3 pos = GlobalPosition + new Vector3(0f, HpBarYOffset, 0f);
        // Orient the pivot to match the camera (manual billboard) so the fill's
        // left-anchor offset lives in real screen space and never drifts.
        if (_cam != null)
            _hpBarPivot.GlobalTransform = new Transform3D(_cam.GlobalBasis, pos);
        else
            _hpBarPivot.GlobalPosition = pos;
    }

    private MeshInstance3D MakeBarQuad(Color color, float zNudge, int priority)
    {
        var mesh = new QuadMesh { Size = new Vector2(HpBarWidth, HpBarHeight) };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            DisableReceiveShadows = true,
            NoDepthTest = true,
            RenderPriority = priority, // fill (1) draws after background (0) -> stays on top
        };
        mesh.Material = mat;
        return new MeshInstance3D { Mesh = mesh, Position = new Vector3(0f, 0f, zNudge) };
    }

    private void UpdateHpBar()
    {
        if (_hpFill == null) return;
        float ratio = MaxHp > 0f ? Mathf.Clamp(Hp / MaxHp, 0f, 1f) : 0f;
        // Shrink fill from the left: scale X then shift so left edge stays fixed.
        _hpFill.Scale = new Vector3(Mathf.Max(ratio, 0.0001f), 1f, 1f);
        _hpFill.Position = new Vector3(-(1f - ratio) * HpBarWidth * 0.5f, 0f, 0.001f);
    }

    private UnitBase FindNearestEnemy()
    {
        string group = UnitTeam == Team.Player ? "enemy_units" : "player_units";
        UnitBase nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is UnitBase u && u.State != UnitState.Dead)
            {
                float d = Position.DistanceTo(u.Position);
                if (d <= DetectionRange && d < nearestDist) { nearestDist = d; nearest = u; }
            }
        }
        return nearest;
    }

    public void PlayAnim(string name)
    {
        if (_anim != null && _anim.HasAnimation(name))
            _anim.Play(name);
    }
}
