using Godot;
using System.Collections.Generic;

public partial class GameManager : Node3D
{
    // --- Field zones (bigger). Must match main.tscn meshes. ---
    public const float PlayerZoneMinX = -30f;
    public const float PlayerZoneMaxX = -16f;
    public const float BattleZoneMinX = -16f;
    public const float BattleZoneMaxX =  16f;
    public const float EnemyZoneMinX  =  16f;
    public const float EnemyZoneMaxX  =  30f;
    public const float ZoneMinZ = -9f;
    public const float ZoneMaxZ =  9f;

    // --- Enemy spawn strip (where each wave's attackers appear). ---
    public const float EnemySpawnMinX  =  17f;
    public const float EnemySpawnMaxX  =  25f;
    public const float SpawnMinZ = -8f;
    public const float SpawnMaxZ =  8f;

    // --- Command board = the player's spawn zone itself. Units are PLACED here and
    // also START every battle from here (identical size / rotation / location), so
    // there is no teleport between placement and spawning. Castle sits BEHIND it. ---
    public const float BoardMinX = -25f;
    public const float BoardMaxX = -17f;
    public const float BoardMinZ =  -8f;
    public const float BoardMaxZ =   8f;

    private Camera3D _camera;
    private Node3D _units;
    private PackedScene _gruntScene;
    private PackedScene _castleScene;
    private RoundManager _roundManager;
    private Label _roundLabel;
    private Label _budgetLabel;
    private Label _timerLabel;
    private Label _previewLabel;
    private Button _restartBtn;
    private UnitBase _playerCastle;
    private UnitBase _enemyCastle;
    private readonly List<UnitBase> _playerArmy = new();

    // --- Economy: a growing budget reserves your standing batch composition. ---
    private static readonly int[] BaseCost = { 10, 18, 25 }; // Melee, Ranged, Healer
    private readonly int[] _comp = { 4, 1, 0 };              // current composition (counts per kind)
    private float _budget = 100f;
    private float _incomePerSec = 2f;
    private float _costMult = 1f;
    private int _killCount = 0;
    private const int BuffEveryKills = 12;
    private double _hudAccum = 0;

    // Composition row widgets + temporary batch buffs.
    private readonly Label[] _compCountLabels = new Label[3];
    private readonly Label[] _compCostLabels = new Label[3];
    private readonly List<TempBuff> _temp = new();

    // Camera zoom: camera sits at _camTarget + _camOffset * _zoom.
    private Vector3 _camTarget;
    private Vector3 _camOffset;
    private float _zoom = 1f;
    private const float ZoomStep = 0.9f;
    private const float ZoomMin = 0.45f;
    private const float ZoomMax = 1.6f;

    // Right-click drag to pan.
    private bool _panning = false;
    private const float PanSpeed = 0.06f;

    // Run buffs (permanent), game-speed control, and the buff-pick UI.
    private readonly RunState _run = new();
    private int _speedIndex = 0;
    private static readonly float[] GameSpeeds = { 1f, 2f, 3f };
    private Button _speedBtn;
    private Control _buffPanel;
    private readonly Button[] _buffButtons = new Button[3];
    private List<Buff> _buffPool;
    private readonly List<Buff> _choices = new();
    private bool _choosing = false;

    public override void _Ready()
    {
        UnitBase.BattleActive = true; // continuous battlefield, never resets
        Engine.TimeScale = 1f;
        _camera     = GetViewport().GetCamera3D();
        _units      = GetNode<Node3D>("Units");
        _gruntScene = GD.Load<PackedScene>("res://scenes/units/grunt.tscn");
        _castleScene = GD.Load<PackedScene>("res://scenes/castle.tscn");

        _roundManager = GetNode<RoundManager>("RoundManager");
        _roundManager.Initialize(_units, _gruntScene);
        _roundManager.BatchSpawned += OnBatchSpawned;
        _roundManager.TimerTick += OnTimerTick;
        _roundManager.GameOver += OnGameOver;

        UnitBase.UnitDied = OnUnitDied; // assign (not +=) so it never leaks across reloads

        // Hide the legacy build-phase buttons if they still exist in the scene.
        foreach (var n in new[] { "UI/StartRoundBtn", "UI/GruntBtn", "UI/ArcherBtn" })
        {
            var b = GetNodeOrNull<Button>(n);
            if (b != null) b.Visible = false;
        }

        _restartBtn = GetNodeOrNull<Button>("UI/RestartBtn");
        if (_restartBtn != null)
        {
            _restartBtn.Visible = false;
            _restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
        }

        _roundLabel = GetNode<Label>("UI/RoundLabel");
        _budgetLabel = GetNodeOrNull<Label>("UI/GoldLabel");

        _speedBtn = GetNodeOrNull<Button>("UI/SpeedBtn");
        if (_speedBtn != null) { _speedBtn.Pressed += CycleSpeed; UpdateSpeedLabel(); }

        BuildBuffPool();
        BuildBuffUI();
        BuildComposer();
        BuildInfoLabels();

        SetupCamera();
        SetupSun();
        SpawnCastles();

        _roundLabel.Text = "Defend your castle!";
        if (_previewLabel != null) _previewLabel.Text = _roundManager.NextEnemyPreview();
        RefreshComposer();
        UpdateHud();
    }

    public override void _ExitTree()
    {
        UnitBase.UnitDied = null;
        Engine.TimeScale = 1f;
    }

    public override void _Process(double delta)
    {
        if (_roundManager == null || _roundManager.IsGameOver) return;
        _budget += _incomePerSec * (float)delta; // passive income raises the budget cap
        _hudAccum += delta;
        if (_hudAccum >= 0.2) { _hudAccum = 0; UpdateHud(); }
    }

    private void OnUnitDied(Team team)
    {
        if (team != Team.Enemy) return;
        _killCount++;
        _budget += 1 + _run.GoldPerKill; // base bounty + run buff
        UpdateHud();
        if (!_choosing && _killCount % BuffEveryKills == 0) ShowBuffChoices();
    }

    // --- Game speed (1x / 2x / 3x), via button and number keys 1-3. ---
    private void CycleSpeed() => SetSpeed((_speedIndex + 1) % GameSpeeds.Length);

    private void SetSpeed(int idx)
    {
        _speedIndex = Mathf.Clamp(idx, 0, GameSpeeds.Length - 1);
        if (!_choosing) Engine.TimeScale = GameSpeeds[_speedIndex];
        UpdateSpeedLabel();
    }

    private void UpdateSpeedLabel()
    {
        if (_speedBtn != null) _speedBtn.Text = $"Speed: {GameSpeeds[_speedIndex]:0}x";
    }

    private void SetupSun()
    {
        // Low sun angle -> long shadows raking across the arena.
        var sun = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");
        if (sun == null) return;
        sun.Position = new Vector3(-40f, 16f, 30f);
        sun.LookAt(new Vector3(25f, 0f, -12f), Vector3.Up);
    }

    private void SetupCamera()
    {
        // Angled 3/4 view from the player's side (-X), up high, looking across the field.
        _camTarget = new Vector3(-8f, 0f, 0f);
        _camOffset = new Vector3(-34f, 32f, 44f) - _camTarget;
        _zoom = 0.7f; // start more zoomed in
        _camera.Fov = 58f;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        _camera.Position = _camTarget + _camOffset * _zoom;
        _camera.LookAt(_camTarget, Vector3.Up);
    }

    private void SpawnCastles()
    {
        _playerCastle = SpawnCastle(Team.Player, new Vector3(PlayerZoneMinX + 2f, 0f, 0f), facingRight: true, defenseDamage: 45f);
        _enemyCastle = SpawnCastle(Team.Enemy, new Vector3(EnemyZoneMaxX - 2f, 0f, 0f), facingRight: false, defenseDamage: 28f);
        _roundManager.RegisterCastles(_playerCastle, _enemyCastle);
    }

    private UnitBase SpawnCastle(Team team, Vector3 pos, bool facingRight, float defenseDamage)
    {
        var castle = _castleScene.Instantiate<UnitBase>();
        castle.UnitTeam = team;
        castle.IsBattleUnit = true;   // targetable + joins team group
        castle.IsStructure = true;    // never moves/rotates
        castle.MaxHp = 1000f;
        castle.Damage = defenseDamage;        // >0 = fires a defensive volley
        castle.MoveSpeed = 0f;                // never moves
        castle.AttackRange = defenseDamage > 0f ? 13f : 0.1f;
        castle.AttackCooldown = 0.6f;
        castle.HpBarWidth = 5f;
        castle.HpBarYOffset = 8f;
        castle.HpBarHeight = 0.5f;
        _units.AddChild(castle);
        castle.GlobalPosition = pos;
        castle.SetFacing(facingRight ? Vector3.Right : Vector3.Left);
        return castle;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey k && k.Pressed && !k.Echo)
        {
            if (k.Keycode == Key.Key1) { SetSpeed(0); return; }
            if (k.Keycode == Key.Key2) { SetSpeed(1); return; }
            if (k.Keycode == Key.Key3) { SetSpeed(2); return; }
        }

        if (@event is InputEventMouseButton wheel && wheel.Pressed)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)   { _zoom = Mathf.Clamp(_zoom * ZoomStep, ZoomMin, ZoomMax); ApplyZoom(); return; }
            if (wheel.ButtonIndex == MouseButton.WheelDown) { _zoom = Mathf.Clamp(_zoom / ZoomStep, ZoomMin, ZoomMax); ApplyZoom(); return; }
        }

        // Right-click drag pans the camera (keeps the same angle).
        if (@event is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            _panning = rmb.Pressed;
            return;
        }
        if (_panning && @event is InputEventMouseMotion motion)
        {
            PanCamera(motion.Relative);
            return;
        }
    }

    private void PanCamera(Vector2 mouseDelta)
    {
        // Move the look target along the camera's flattened right/forward axes.
        Vector3 right = _camera.GlobalTransform.Basis.X; right.Y = 0f; right = right.Normalized();
        Vector3 fwd = -_camera.GlobalTransform.Basis.Z; fwd.Y = 0f; fwd = fwd.Normalized();
        _camTarget += (-mouseDelta.X * right + mouseDelta.Y * fwd) * PanSpeed * _zoom;
        _camTarget.X = Mathf.Clamp(_camTarget.X, -70f, 70f);
        _camTarget.Z = Mathf.Clamp(_camTarget.Z, -40f, 50f);
        ApplyZoom();
    }

    // ---- Batch spawning: the player's standing composition deploys every tick. ----
    private void OnBatchSpawned(int batchNumber)
    {
        SpawnPlayerBatch(batchNumber);
        if (_previewLabel != null) _previewLabel.Text = _roundManager.NextEnemyPreview();
        _roundLabel.Text = $"Battle — Batch {batchNumber}";
    }

    private void OnTimerTick(float secondsLeft, int batchNumber)
    {
        if (_timerLabel != null) _timerLabel.Text = $"Next batch: {Mathf.CeilToInt(secondsLeft)}s";
    }

    private void SpawnPlayerBatch(int batchNumber)
    {
        // Aggregate active temporary (batch) buffs.
        float hpMult = 1f, asMult = 1f;
        int exMelee = 0, exRanged = 0; bool dbl = false;
        foreach (var t in _temp)
        {
            hpMult *= t.HpMult; asMult *= t.AtkSpeedMult;
            exMelee += t.ExtraMelee; exRanged += t.ExtraRanged; dbl |= t.DoubleMelee;
        }

        int melee = _comp[0] * (dbl ? 2 : 1) + exMelee;
        int ranged = _comp[1] + exRanged;
        int healer = _comp[2];

        var spawned = new List<UnitBase>();
        for (int i = 0; i < melee; i++)  spawned.Add(SpawnPlayerUnit(UnitKind.Melee, hpMult, asMult));
        for (int i = 0; i < ranged; i++) spawned.Add(SpawnPlayerUnit(UnitKind.Ranged, hpMult, asMult));
        for (int i = 0; i < healer; i++) spawned.Add(SpawnPlayerUnit(UnitKind.Healer, hpMult, asMult));

        RoundManager.PruneDead(_playerArmy);
        _playerArmy.AddRange(spawned);
        RoundManager.EnforceCap(_playerArmy);
        RoundManager.PositionBatch(spawned, Team.Player);

        // Expire temporary buffs after the batch they affected.
        for (int i = _temp.Count - 1; i >= 0; i--)
            if (--_temp[i].BatchesLeft <= 0) _temp.RemoveAt(i);

        UpdateHud();
    }

    private UnitBase SpawnPlayerUnit(UnitKind kind, float hpMult, float asMult)
    {
        var u = _gruntScene.Instantiate<UnitBase>();
        u.UnitTeam = Team.Player;
        u.IsBattleUnit = true;
        u.Kind = kind;
        ApplyRunToUnit(u);
        u.BuffHpMult *= hpMult;       // stack temporary batch buff on top of run buffs
        u.BuffAtkSpeedMult *= asMult;
        _units.AddChild(u);
        return u;
    }

    // Copy current run buffs onto a unit (call before AddChild for fresh units).
    private void ApplyRunToUnit(UnitBase u)
    {
        u.BuffHpMult = _run.HpMult;
        u.BuffDamageMult = _run.DamageMult;
        u.BuffAtkSpeedMult = _run.AtkSpeedMult;
        u.BuffMoveSpeedMult = _run.MoveSpeedMult;
        u.ExtraProjectiles = _run.ExtraProjectiles;
        u.EvolutionTier = _run.EvolutionTier;
    }

    // Push updated buffs onto every living army unit (preserves HP ratio).
    private void ReapplyToArmy()
    {
        foreach (var u in _playerArmy)
        {
            if (!IsInstanceValid(u) || u.State == UnitState.Dead) continue;
            ApplyRunToUnit(u);
            u.ReapplyBuffs();
        }
    }

    // ---- Composition + budget ----
    private int UnitCost(UnitKind kind) => Mathf.Max(1, Mathf.RoundToInt(BaseCost[(int)kind] * _costMult));

    private int Reserved()
    {
        int sum = 0;
        for (int i = 0; i < 3; i++) sum += _comp[i] * UnitCost((UnitKind)i);
        return sum;
    }

    private void ChangeComp(int kindIndex, int delta)
    {
        if (delta > 0 && Reserved() + UnitCost((UnitKind)kindIndex) > _budget)
        {
            _roundLabel.Text = "Not enough budget — earn more gold";
            return;
        }
        _comp[kindIndex] = Mathf.Max(0, _comp[kindIndex] + delta);
        RefreshComposer();
        UpdateHud();
    }

    private void RefreshComposer()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_compCountLabels[i] != null) _compCountLabels[i].Text = _comp[i].ToString();
            if (_compCostLabels[i] != null) _compCostLabels[i].Text = $"{UnitCost((UnitKind)i)}g";
        }
    }

    private int LivePlayerCount()
    {
        int n = 0;
        foreach (var u in _playerArmy)
            if (IsInstanceValid(u) && u.State != UnitState.Dead) n++;
        return n;
    }

    private void BuildComposer()
    {
        var ui = GetNode<CanvasLayer>("UI");
        var panel = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 1f, AnchorBottom = 1f,
            GrowHorizontal = Control.GrowDirection.End,
            GrowVertical = Control.GrowDirection.Begin,
            OffsetLeft = 14f, OffsetBottom = -14f,
        };
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);
        vbox.AddChild(new Label { Text = "Batch composition" });

        string[] names = { "Melee", "Archer", "Healer" };
        for (int i = 0; i < 3; i++)
        {
            int kind = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(new Label { Text = names[i], CustomMinimumSize = new Vector2(64, 0) });
            var minus = new Button { Text = "-", CustomMinimumSize = new Vector2(34, 30) };
            minus.Pressed += () => ChangeComp(kind, -1);
            row.AddChild(minus);
            var count = new Label { Text = "0", CustomMinimumSize = new Vector2(26, 0), HorizontalAlignment = HorizontalAlignment.Center };
            _compCountLabels[i] = count;
            row.AddChild(count);
            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(34, 30) };
            plus.Pressed += () => ChangeComp(kind, +1);
            row.AddChild(plus);
            var cost = new Label { Text = "0g", CustomMinimumSize = new Vector2(40, 0) };
            _compCostLabels[i] = cost;
            row.AddChild(cost);
            vbox.AddChild(row);
        }
        ui.AddChild(panel);
    }

    private void BuildInfoLabels()
    {
        var ui = GetNode<CanvasLayer>("UI");
        _timerLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -150f, OffsetRight = 150f, OffsetTop = 44f, OffsetBottom = 68f,
        };
        ui.AddChild(_timerLabel);

        _previewLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -280f, OffsetRight = 280f, OffsetTop = 70f, OffsetBottom = 94f,
        };
        ui.AddChild(_previewLabel);
    }

    private void UpdateHud()
    {
        if (_budgetLabel == null) return;
        int ec = _roundManager != null ? _roundManager.LiveEnemyCount() : 0;
        _budgetLabel.Text =
            $"Budget {Reserved()}/{(int)_budget}  (+{_incomePerSec:0.#}/s)    You {LivePlayerCount()}  Enemy {ec}  (cap {RoundManager.MaxPerSide})";
    }

    // --- Buff system: every N kills, pick one of three (Army / Batch / Economy). ---
    private void BuildBuffPool()
    {
        _buffPool = new List<Buff>
        {
            // Army buffs (permanent, affect the whole standing army immediately).
            new Buff("[Army] +15% Damage", () => { _run.DamageMult += 0.15f; _run.BuffsTaken++; ReapplyToArmy(); }),
            new Buff("[Army] +15% Max HP", () => { _run.HpMult += 0.15f; _run.BuffsTaken++; ReapplyToArmy(); }),
            new Buff("[Army] +1 Arrow (archers)", () => { _run.ExtraProjectiles += 1; _run.BuffsTaken++; ReapplyToArmy(); }),
            new Buff("[Army] +12% Attack Speed", () => { _run.AtkSpeedMult += 0.12f; _run.BuffsTaken++; ReapplyToArmy(); }),
            new Buff("[Army] +12% Move Speed", () => { _run.MoveSpeedMult += 0.12f; _run.BuffsTaken++; ReapplyToArmy(); }),
            // Batch buffs (temporary, affect the next few spawned batches only).
            new Buff("[Batch] +30% HP x3", () => _temp.Add(new TempBuff { Name = "HP", BatchesLeft = 3, HpMult = 1.3f })),
            new Buff("[Batch] +25% Atk Speed x3", () => _temp.Add(new TempBuff { Name = "AS", BatchesLeft = 3, AtkSpeedMult = 1.25f })),
            new Buff("[Batch] +2 Archers x2", () => _temp.Add(new TempBuff { Name = "Arch", BatchesLeft = 2, ExtraRanged = 2 })),
            new Buff("[Batch] Double Melee x1", () => _temp.Add(new TempBuff { Name = "2xMelee", BatchesLeft = 1, DoubleMelee = true })),
            // Economy buffs (budget / income / costs).
            new Buff("[Econ] +25% Budget", () => { _budget *= 1.25f; UpdateHud(); }),
            new Buff("[Econ] +1 Gold/sec", () => { _incomePerSec += 1f; UpdateHud(); }),
            new Buff("[Econ] -10% Unit Costs", () => { _costMult *= 0.9f; RefreshComposer(); UpdateHud(); }),
            new Buff("[Econ] +3 Gold/Kill", () => _run.GoldPerKill += 3),
            new Buff("[Econ] -10% Batch Timer", () => _roundManager.ScaleInterval(0.9f)),
        };
    }

    private void BuildBuffUI()
    {
        var ui = GetNode<CanvasLayer>("UI");
        var panel = new PanelContainer { Visible = false };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);
        vbox.AddChild(new Label { Text = "Choose a buff", HorizontalAlignment = HorizontalAlignment.Center });
        for (int i = 0; i < _buffButtons.Length; i++)
        {
            var b = new Button { CustomMinimumSize = new Vector2(300, 46) };
            int idx = i;
            b.Pressed += () => OnBuffChosen(idx);
            _buffButtons[i] = b;
            vbox.AddChild(b);
        }
        ui.AddChild(panel);
        _buffPanel = panel;
    }

    private void ShowBuffChoices()
    {
        _choosing = true;
        Engine.TimeScale = 0f; // pause the field while picking
        _choices.Clear();
        var pool = new List<Buff>(_buffPool);
        for (int i = 0; i < _buffButtons.Length && pool.Count > 0; i++)
        {
            int r = (int)(GD.Randi() % (uint)pool.Count);
            _choices.Add(pool[r]);
            pool.RemoveAt(r);
        }
        for (int i = 0; i < _buffButtons.Length; i++)
        {
            bool has = i < _choices.Count;
            _buffButtons[i].Visible = has;
            if (has) _buffButtons[i].Text = _choices[i].Name;
        }
        if (_buffPanel != null) _buffPanel.Visible = true;
    }

    private void OnBuffChosen(int idx)
    {
        if (idx < 0 || idx >= _choices.Count) return;
        _choices[idx].Apply();
        if (_buffPanel != null) _buffPanel.Visible = false;
        _choosing = false;
        Engine.TimeScale = GameSpeeds[_speedIndex]; // resume at chosen speed
        UpdateHud();
    }

    private void OnGameOver(bool victory, int batches)
    {
        _roundLabel.Text = victory
            ? $"VICTORY — razed the enemy keep at batch {batches}!"
            : $"GAME OVER — survived {batches} batches";
        if (_restartBtn != null) _restartBtn.Visible = true;
    }
}

// One pickable buff: a label + the mutation it applies.
public sealed record Buff(string Name, System.Action Apply);

// Temporary batch buff: applies to units spawned over the next BatchesLeft batches.
public sealed class TempBuff
{
    public string Name;
    public int BatchesLeft = 1;
    public float HpMult = 1f;
    public float AtkSpeedMult = 1f;
    public int ExtraMelee = 0;
    public int ExtraRanged = 0;
    public bool DoubleMelee = false;
}

// Cumulative per-run upgrade state. Evolution tier grows units every 2 army buffs.
public sealed class RunState
{
    public float HpMult = 1f;
    public float DamageMult = 1f;
    public float AtkSpeedMult = 1f;
    public float MoveSpeedMult = 1f;
    public int ExtraProjectiles = 0;
    public int GoldPerKill = 0;
    public int BuffsTaken = 0;
    public int EvolutionTier => BuffsTaken / 2;
}
