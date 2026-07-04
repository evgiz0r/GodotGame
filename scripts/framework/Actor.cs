using Godot;

namespace Framework;

// General combat actor: HP, a state machine, movement/facing, soft separation, a
// floating HP bar, buff-driven effective stats, team groups and projectiles.
//
// Game-specific units derive from this and override the virtual hooks:
//   ConfigureStats   - set per-archetype base stats
//   BuildVisuals     - attach gear/meshes
//   SelectBehavior   - choose the AI brain
//   ComputeBaseScale - per-archetype body size
//   Tick             - per-frame combat step (call base.Tick for the default)
//   OnStructureTick  - per-frame logic for stationary structures
public partial class Actor : Node3D, IDamageable
{
    // Grunt-ish baseline; subclasses/scenes override via ConfigureStats or the scene.
    [Export] public Team UnitTeam = Team.Player;
    [Export] public float MaxHp = 40f;
    [Export] public float Damage = 6f;
    [Export] public float AttackRange = 1.5f;
    [Export] public float MoveSpeed = 2f;
    [Export] public float AttackCooldown = 1.0f;

    // Only engage enemies within this radius; otherwise keep advancing.
    public float DetectionRange = 14f;
    // Per-unit stat multipliers (escalating waves). Set before AddChild.
    public float HpScale = 1f;
    public float DamageScale = 1f;
    // Soft-body radius used for separation so units don't stack.
    public float CollisionRadius = 0.5f;
    // false = inert placeholder; true = active fighter that marches + fights.
    public bool IsBattleUnit = false;
    // true = stationary structure: targetable + takes damage, never moves.
    public bool IsStructure = false;
    // true = this unit backpedals + slow-regens after a heavy hit (tanks/bosses set false).
    public bool CanRetreat = true;

    // Run buffs / evolution (set before AddChild, or applied live via ReapplyBuffs).
    public float BuffHpMult = 1f;
    public float BuffDamageMult = 1f;
    public float BuffAtkSpeedMult = 1f;
    public float BuffMoveSpeedMult = 1f;
    public int ExtraProjectiles = 0;
    public int EvolutionTier = 0;
    // Body-size multiplier (tank/boss big, fast small). Set before AddChild.
    public float BodyScale = 1f;

    // Global gate: actors only fight while combat is active.
    public static bool BattleActive = false;
    // Raised whenever any actor dies (arg = dead actor's team), regardless of cause
    // (combat OR soft decay) so a freed slot can pull the next queued reinforcement.
    // ASSIGN (not +=) so it never leaks/accumulates across scene reloads.
    public static System.Action<Team> UnitDied;

    public float Hp { get; protected set; }
    public UnitState State { get; private set; } = (UnitState)(-1);
    public bool IsAlive => State != UnitState.Dead;

    protected AnimationPlayer Anim;
    protected IActorBehavior Behavior;
    private float _baseMaxHp, _baseDamage, _baseCooldown, _baseMoveSpeed;
    protected Vector3 BaseScaleVec = Vector3.One;

    // HP bar sizing (structures set these bigger before AddChild).
    public float HpBarWidth = 1.4f;
    public float HpBarHeight = 0.18f;
    public float HpBarYOffset = 2.6f;
    private HealthBar3D _healthBar;
    private Camera3D _cam;

    protected float BaseMaxHp => _baseMaxHp;

    public override void _Ready()
    {
        ConfigureStats();
        MaxHp *= HpScale;
        Damage *= DamageScale;
        _baseMaxHp = MaxHp;
        _baseDamage = Damage;
        _baseCooldown = AttackCooldown;
        _baseMoveSpeed = MoveSpeed;
        BaseScaleVec = ComputeBaseScale();
        ComputeEffectiveStats();
        Hp = MaxHp;
        if (IsBattleUnit)
            AddToGroup(UnitTeam == Team.Player ? "player_units" : "enemy_units");
        ApplyTeamTint();
        BuildVisuals();       // gear added AFTER tint so it keeps its own colors
        CreateHpBar();        // bar created AFTER tint so its quads stay untinted
        Anim = FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        GodotEx.ForceLoop(Anim, "Walk", "Run", "Idle");
        Behavior = SelectBehavior();
        SetState(IsBattleUnit ? UnitState.Walking : UnitState.Idle);
    }

    // ---- Hooks for game subclasses ----
    protected virtual void ConfigureStats() { }
    protected virtual void BuildVisuals() { }
    protected virtual IActorBehavior SelectBehavior() => null;
    protected virtual Vector3 ComputeBaseScale() =>
        IsStructure ? Vector3.One : Vector3.One * 1.15f * BodyScale;
    protected virtual void OnStructureTick(double delta) { }
    // Called once when this actor dies (any cause). Games override for corpse FX.
    protected virtual void OnKilled() { }
    // Called after a non-fatal hit lands. Games override for hit flashes / knockback.
    protected virtual void OnDamaged(float amount) { }

    // Derive live stats from captured base values * current buffs.
    protected void ComputeEffectiveStats()
    {
        MaxHp = _baseMaxHp * BuffHpMult;
        Damage = _baseDamage * BuffDamageMult;
        AttackCooldown = _baseCooldown / Mathf.Max(0.25f, BuffAtkSpeedMult);
        MoveSpeed = _baseMoveSpeed * BuffMoveSpeedMult;
        if (!IsStructure)
            Scale = BaseScaleVec * (1f + 0.12f * EvolutionTier);
    }

    // Apply updated buffs to an already-living unit, preserving its HP ratio.
    public void ReapplyBuffs()
    {
        float ratio = MaxHp > 0f ? Mathf.Clamp(Hp / MaxHp, 0f, 1f) : 1f;
        ComputeEffectiveStats();
        Hp = Mathf.Clamp(MaxHp * ratio, 1f, MaxHp);
        UpdateHpBar();
    }

    public Vector3 AdvanceDir => UnitTeam == Team.Player ? Vector3.Right : Vector3.Left;

    public override void _Process(double delta)
    {
        UpdateHpBarPosition();
        if (!IsBattleUnit || State == UnitState.Dead) return;
        if (IsStructure)
        {
            if (BattleActive && Damage > 0f) OnStructureTick(delta);
            return;
        }
        if (!BattleActive) { SetState(UnitState.Idle); return; }
        Tick(delta);
    }

    // Default mobile combat step. Subclasses may override (e.g. to add aging or a
    // healer path) and call base.Tick for the standard march-and-fight behaviour.
    protected virtual void Tick(double delta)
    {
        if (_retreat > 0f)
        {
            Retreat(delta);
            ApplySeparation();
            return;
        }
        Actor target = FindNearestEnemy();
        if (target != null)
            Behavior?.OnUpdate(this, target, delta);
        else
        {
            // No enemy in detection range: steer toward the nearest enemy anywhere on the
            // field so stragglers/flankers converge on the fight instead of marching off
            // the far edge. Only march straight ahead if the enemy army is truly gone.
            Actor far = FindNearestEnemyAnywhere();
            if (far != null) AdvanceToward(far.GlobalPosition, delta);
            else Advance(delta);
        }
        ApplySeparation();
    }

    // Backpedal toward our own side while still facing the enemy, healing a little as we go.
    private void Retreat(double delta)
    {
        _retreat -= (float)delta;
        SetState(UnitState.Walking);
        Actor enemy = FindNearestEnemy();
        if (enemy != null) FaceToward(enemy.GlobalPosition);
        Position += -AdvanceDir * MoveSpeed * WeatherState.MoveMult * RetreatSpeedMult * (float)delta;
        if (Hp < MaxHp) Heal(RetreatRegenPerSec * (float)delta);
    }

    // No enemy in range: keep pushing toward the enemy side.
    protected void Advance(double delta)
    {
        SetState(UnitState.Walking);
        Vector3 dir = AdvanceDir;
        FaceToward(GlobalPosition + dir);
        MoveToward(dir, delta);
    }

    // March toward a specific world position (used to converge on the nearest enemy when
    // none are within detection range yet).
    protected void AdvanceToward(Vector3 worldPos, double delta)
    {
        SetState(UnitState.Walking);
        Vector3 dir = worldPos - GlobalPosition; dir.Y = 0f;
        if (dir.LengthSquared() < 0.0001f) { Advance(delta); return; }
        dir = dir.Normalized();
        FaceToward(worldPos);
        MoveToward(dir, delta);
    }

    // Public wrapper so behaviors (e.g. an idle healer) can keep marching.
    public void AdvanceStep(double delta) => Advance(delta);

    // Fire 1 + ExtraProjectiles homing arrows toward a target.
    public void FireProjectile(Actor target)
    {
        Sound.Shoot?.Invoke(GlobalPosition);
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

    public void MoveToward(Vector3 direction, double delta) =>
        Position += direction * MoveSpeed * WeatherState.MoveMult * (float)delta;

    public void SetFacing(Vector3 worldDirection) => GodotEx.FacePlusZ(this, worldDirection);

    public void FaceToward(Vector3 worldPos)
    {
        var dir = worldPos - GlobalPosition;
        dir.Y = 0;
        if (dir.LengthSquared() < 0.001f) return;
        SetFacing(dir.Normalized());
    }

    public void TakeDamage(float amount)
    {
        if (State == UnitState.Dead) return;
        Hp -= amount;
        if (Hp <= 0f) Kill(bounty: true);
        else
        {
            OnDamaged(amount);
            Sound.Hit?.Invoke(GlobalPosition);
            if (CanRetreat && !IsStructure && amount >= MaxHp * RetreatHitFraction)
                _retreat = RetreatDuration;
        }
        UpdateHpBar();
    }

    public void Heal(float amount)
    {
        if (State == UnitState.Dead) return;
        Hp = Mathf.Min(MaxHp, Hp + amount);
        UpdateHpBar();
    }

    // Drain HP (used by subclasses, e.g. soft-stacking aging); can kill without a bounty.
    protected void DrainHp(float amount, bool bountyOnDeath)
    {
        Hp -= amount;
        if (Hp <= 0f) Kill(bountyOnDeath);
        UpdateHpBar();
    }

    protected void Kill(bool bounty)
    {
        Hp = 0f;
        SetState(UnitState.Dead);
        if (_healthBar != null) _healthBar.Visible = false;
        Sound.Death?.Invoke(GlobalPosition);
        OnKilled();
        // Always notify: the batch queue needs every death (combat or decay) to refill
        // a freed slot. The `bounty` flag is forwarded for optional reward/kill counting.
        UnitDied?.Invoke(UnitTeam);
    }

    public Actor FindNearestEnemy()
    {
        string group = UnitTeam == Team.Player ? "enemy_units" : "player_units";
        Actor nearest = null;
        float nearestDist = float.MaxValue;
        float range = DetectionRange * WeatherState.RangeMult;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is Actor u && u.State != UnitState.Dead)
            {
                float d = Position.DistanceTo(u.Position);
                if (d <= range && d < nearestDist) { nearestDist = d; nearest = u; }
            }
        }
        return nearest;
    }

    // Nearest living enemy at ANY distance (used to steer advancing units toward the fight).
    public Actor FindNearestEnemyAnywhere()
    {
        string group = UnitTeam == Team.Player ? "enemy_units" : "player_units";
        Actor nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is Actor u && !u.IsStructure && u.State != UnitState.Dead)
            {
                float d = Position.DistanceTo(u.Position);
                if (d < nearestDist) { nearestDist = d; nearest = u; }
            }
        }
        return nearest;
    }

    // Nearest same-team, non-structure, living, wounded ally within range (for healers).
    public Actor FindWoundedAlly(float range)
    {
        string group = UnitTeam == Team.Player ? "player_units" : "enemy_units";
        Actor best = null;
        float bestMissing = 0f;
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is Actor u && u != this && !u.IsStructure && u.State != UnitState.Dead
                && u.Hp < u.MaxHp && Position.DistanceTo(u.Position) <= range)
            {
                float missing = u.MaxHp - u.Hp;
                if (missing > bestMissing) { bestMissing = missing; best = u; }
            }
        }
        return best;
    }

    // Retreat: after a hit >= this fraction of MaxHp, a unit backpedals for a moment while
    // slowly regenerating, then re-engages. Creates spacing + in/out cycling on the line.
    public static float RetreatHitFraction = 0.22f;
    public static float RetreatDuration = 0.28f;
    public static float RetreatSpeedMult = 1.12f;
    public static float RetreatRegenPerSec = 4f;
    private float _retreat;

    // Effective attack cooldown after weather (snow slows attacks). Behaviors reset timers
    // from this instead of the raw AttackCooldown so weather is applied in one place.
    public float RollCooldown => AttackCooldown * WeatherState.AttackCooldownMult;

    // Lightweight soft separation so living units don't stack on one cell. Firmness is
    // tunable and the per-frame push is clamped so a very crowded unit can't teleport.
    public static float SeparationStrength = 0.95f;

    protected void ApplySeparation()
    {
        Vector3 push = Vector3.Zero;
        foreach (var group in new[] { "player_units", "enemy_units" })
        {
            foreach (var node in GetTree().GetNodesInGroup(group))
            {
                if (node is not Actor other || other == this) continue;
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
        float max = CollisionRadius;
        if (push.Length() > max) push = push.Normalized() * max;
        Position += push * SeparationStrength;
    }

    // Subtle translucent team wash so you can read sides at a glance.
    protected virtual void ApplyTeamTint()
    {
        float a = IsStructure ? 0.16f : 0.22f;
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
        if (node is MeshInstance3D mi) mi.MaterialOverlay = overlay;
        foreach (var child in node.GetChildren())
            TintMeshesRecursive(child, overlay);
    }

    private void CreateHpBar()
    {
        _healthBar = new HealthBar3D { Width = HpBarWidth, Height = HpBarHeight };
        AddChild(_healthBar);
        _healthBar.Build(UnitTeam == Team.Player ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.85f, 0.3f, 0.3f));
        UpdateHpBarPosition();
        UpdateHpBar();
    }

    private void UpdateHpBarPosition()
    {
        if (_healthBar == null) return;
        _cam ??= GetViewport().GetCamera3D();
        _healthBar.UpdateWorld(GlobalPosition + new Vector3(0f, HpBarYOffset, 0f), _cam);
    }

    private void UpdateHpBar() =>
        _healthBar?.SetRatio(MaxHp > 0f ? Mathf.Clamp(Hp / MaxHp, 0f, 1f) : 0f);

    public virtual void PlayAnim(string name)
    {
        if (Anim != null && Anim.HasAnimation(name))
            Anim.Play(name);
    }
}
