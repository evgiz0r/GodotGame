using System.Collections.Generic;
using Godot;
using Framework;

// Root of the battle simulator. Builds the camera/HUD, spawns the two armies for a chosen
// scenario, runs a short "get ready → FIGHT" beat, and layers camera shake / boss-follow /
// cinematic angle on top of the reusable CameraRig. Drives everything from code.
public partial class Main : Node3D
{
    private Node3D _armies;                 // parent for units, projectiles & blasts
    private Node3D _envNode;                // current environment (swapped per scenario)
    private Camera3D _cam;
    private CameraRig _rig;
    private CameraShake _shake;
    private SimDirector _director;
    private BattleHud _hud;

    private int _currentIndex = -1;
    private float _countdown;
    private float _fightFlash;
    private float _zoomTarget = 1f;
    private bool _autoZoom;
    private bool _followBoss;
    private bool _cinematic;
    private SimUnit _bossRef;

    // Live reinforcement waves for the current scenario.
    private sealed class ReinfRun { public Reinforcement Cfg; public float Timer; public int Spawned; }
    private readonly List<ReinfRun> _reinf = new();

    // Army-bar peaks (rise with reinforcements) + the KO → result-card beat.
    private float _pHpRef, _eHpRef;
    private float _koDelay;
    private string _pendingResult;

    // Two camera angles: standard 3/4 and a lower, flatter cinematic framing.
    private static readonly Vector3 StdOffset = new(0f, 24f, 26f);
    private static readonly Vector3 CineOffset = new(0f, 11f, 34f);

    public override void _Ready()
    {
        Actor.BattleActive = false;
        SimEvents.Shake = amount => _shake.Add(amount);

        _armies = new Node3D { Name = "Armies" };
        AddChild(_armies);

        _cam = new Camera3D { Fov = 50f, Current = true };
        AddChild(_cam);
        _rig = new CameraRig(_cam);
        _rig.Setup(Vector3.Zero, StdOffset, zoom: 1f, fov: 50f);
        _rig.ZoomMax = 2.3f;               // allow pulling further out for the bigger field
        _rig.PanClampX = new Vector2(-95f, 95f);
        _rig.PanClampZ = new Vector2(-55f, 65f);
        _shake = new CameraShake();

        _director = new SimDirector();
        AddChild(_director);
        _director.BattleEnded += OnBattleEnded;

        AddChild(new SfxManager());

        _hud = new BattleHud();
        AddChild(_hud);
        _hud.BuildMenu(Presets.All);
        _hud.ScenarioSelected += StartScenario;
        _hud.MenuRequested += ReturnToMenu;

        BuildEnvironment(EnvironmentKind.Forest);
        _hud.ShowMenu();
    }

    public override void _ExitTree()
    {
        SimEvents.Shake = null;
        Actor.BattleActive = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // A manual wheel zoom hands control to the user — stop the startup auto-zoom.
        if (e is InputEventMouseButton mb && mb.Pressed &&
            (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            _autoZoom = false;

        // A manual pan (right-drag) hands the camera to the user — stop following the boss.
        if (e is InputEventMouseButton rmb && rmb.Pressed && rmb.ButtonIndex == MouseButton.Right)
            _followBoss = false;

        if (_rig.HandleInput(e)) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            switch (k.Keycode)
            {
                case Key.C: ToggleCinematic(); break;
                case Key.R when _currentIndex >= 0: StartScenario(_currentIndex); break;
                case Key.Escape: ReturnToMenu(); break;
            }
        }
    }

    public override void _Process(double delta)
    {
        UpdateCountdown(delta);

        // Gentle one-shot zoom-in at the start of a fight; ends on arrival or manual zoom.
        if (_autoZoom)
        {
            _rig.ZoomLevel = Mathf.MoveToward(_rig.ZoomLevel, _zoomTarget, (float)delta * 0.35f);
            if (Mathf.IsEqualApprox(_rig.ZoomLevel, _zoomTarget)) _autoZoom = false;
        }

        // Follow the boss if there is one; otherwise leave the target where the user panned.
        if (_followBoss && _bossRef != null && GodotObject.IsInstanceValid(_bossRef) && _bossRef.IsAlive)
            _rig.SetTarget(_rig.Target.Lerp(_bossRef.GlobalPosition, (float)delta * 1.5f));

        UpdateReinforcements(delta);
        UpdateArmyBars();
        UpdateKo(delta);

        _cam.Position = _rig.CameraBasePosition + _shake.Offset(delta);
        _cam.LookAt(_rig.Target, Vector3.Up);
    }

    // Sum each side's living HP into the top bars; the reference peak rises with reinforcements.
    private void UpdateArmyBars()
    {
        if (_currentIndex < 0) return;
        float p = SumHp("player_units");
        float e = SumHp("enemy_units");
        _pHpRef = Mathf.Max(_pHpRef, p);
        _eHpRef = Mathf.Max(_eHpRef, e);
        _hud.SetArmyHp(_pHpRef > 0f ? p / _pHpRef : 0f, _eHpRef > 0f ? e / _eHpRef : 0f);
    }

    private float SumHp(string group)
    {
        float total = 0f;
        foreach (var node in GetTree().GetNodesInGroup(group))
            if (node is Actor a && a.IsAlive) total += a.Hp;
        return total;
    }

    // Hold the "K.O." splash briefly, then reveal the result card.
    private void UpdateKo(double delta)
    {
        if (_koDelay <= 0f) return;
        _koDelay -= (float)delta;
        if (_koDelay <= 0f && _pendingResult != null)
        {
            _hud.ShowResult(_pendingResult);
            _pendingResult = null;
        }
    }

    // Timed waves that march in from the back once the fight is live.
    private void UpdateReinforcements(double delta)
    {
        if (!Actor.BattleActive) return;
        foreach (var run in _reinf)
        {
            if (run.Spawned >= run.Cfg.MaxWaves) continue;
            run.Timer -= (float)delta;
            if (run.Timer <= 0f)
            {
                SpawnReinforcement(run.Cfg);
                run.Spawned++;
                run.Timer = run.Cfg.Interval;
            }
        }
    }

    private void UpdateCountdown(double delta)
    {
        if (_countdown > 0f)
        {
            _countdown -= (float)delta;
            _hud.SetStatus(_countdown > 0f ? "Get ready…" : "");
            if (_countdown <= 0f)
            {
                Actor.BattleActive = true;
                _hud.SetStatus("FIGHT!");
                _fightFlash = 1.1f;
            }
        }
        else if (_fightFlash > 0f)
        {
            _fightFlash -= (float)delta;
            if (_fightFlash <= 0f) _hud.SetStatus("");
        }
    }

    private void StartScenario(int index)
    {
        _currentIndex = index;
        var scenario = Presets.All[index];

        ClearBattlefield();
        _director.Reset();
        BuildEnvironment(scenario.Env);
        WeatherFx.Apply(scenario.Weather, _envNode);

        _bossRef = null;
        SpawnArmy(scenario.Player, Team.Player, scenario.PlayerFormation);
        SpawnArmy(scenario.Enemy, Team.Enemy, scenario.EnemyFormation);

        if (scenario.CastleSide is Team castleSide)
            SpawnCastle(castleSide);

        _reinf.Clear();
        if (scenario.Reinforcements != null)
            foreach (var r in scenario.Reinforcements)
                _reinf.Add(new ReinfRun { Cfg = r, Timer = r.FirstDelay, Spawned = 0 });

        _followBoss = _bossRef != null;
        _rig.SetTarget(Vector3.Zero);
        _rig.ZoomLevel = 1.45f;
        _zoomTarget = 1f;
        _autoZoom = true;

        _pHpRef = 0f;
        _eHpRef = 0f;
        _koDelay = 0f;
        _pendingResult = null;

        Actor.BattleActive = false;
        _countdown = 1.2f;
        _fightFlash = 0f;
        _hud.ShowBattle();
        _hud.SetInfo($"{scenario.Name}    {WeatherFx.Label(scenario.Weather)}");
    }

    private void ReturnToMenu()
    {
        Actor.BattleActive = false;
        _currentIndex = -1;
        _followBoss = false;
        _reinf.Clear();
        _koDelay = 0f;
        _pendingResult = null;
        WeatherState.Clear();
        ClearBattlefield();
        _hud.ShowMenu();
    }

    private void OnBattleEnded(BattleResult result)
    {
        _followBoss = false;
        _pendingResult = result switch
        {
            BattleResult.PlayerWins => "BLUE ARMY WINS",
            BattleResult.EnemyWins => "RED ARMY WINS",
            _ => "MUTUAL DESTRUCTION",
        };
        _hud.ShowKo();
        _koDelay = 1.3f;
        SimEvents.Shake?.Invoke(0.7f);
        Sound.Ko?.Invoke();
    }

    private void ClearBattlefield()
    {
        foreach (Node child in _armies.GetChildren()) child.QueueFree();
    }

    private void BuildEnvironment(EnvironmentKind kind)
    {
        _envNode?.QueueFree();
        _envNode = BattleEnvironment.Build(kind);
        AddChild(_envNode);
    }

    // Flatten an army's blocks into individual units with a soft "front-line" sort key —
    // melee/tanks bias forward, casters back, but with random slack so ranks blend — then
    // place them in the chosen formation on their side of the field facing the enemy.
    private void SpawnArmy(ArmyEntry[] entries, Team team, Formation formation)
    {
        var roster = new List<(UnitArchetype arch, float key)>();
        foreach (var e in entries)
            for (int i = 0; i < e.Count; i++)
                roster.Add((e.Archetype, FrontOrder(e.Archetype.Role) + GD.Randf() * 1.7f));
        roster.Sort((a, b) => a.key.CompareTo(b.key));

        int n = roster.Count;
        var offsets = FormationOffsets(n, formation);
        const float gap = 20f;
        float side = team == Team.Player ? -1f : 1f;

        for (int i = 0; i < n; i++)
        {
            var arch = roster[i].arch;
            float z = offsets[i].X;
            float depth = offsets[i].Y;
            float x = side * (gap + depth);

            var unit = new SimUnit { Archetype = arch, UnitTeam = team, IsBattleUnit = true };
            // Per-unit pace so a rank doesn't march in lockstep (also helps break clumps).
            unit.BuffMoveSpeedMult = 0.8f + GD.Randf() * 0.4f;
            _armies.AddChild(unit);
            unit.GlobalPosition = new Vector3(x, 0f, z);
            // Slight facing jitter so they don't stare in perfect lockstep.
            unit.SetFacing((unit.AdvanceDir + new Vector3(0f, 0f, (GD.Randf() * 2f - 1f) * 0.25f)).Normalized());

            if (arch.Role == Role.Boss && team == Team.Player) _bossRef = unit;
        }
    }

    // Local (lateral z, forward depth) placement per formation. depth 0 = closest to enemy.
    private static List<Vector2> FormationOffsets(int n, Formation formation)
    {
        var list = new List<Vector2>(n);
        float J(float f) => (GD.Randf() * 2f - 1f) * f;

        switch (formation)
        {
            case Formation.Block:
            {
                int file = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n)), 1, 14);
                for (int i = 0; i < n; i++)
                    list.Add(new Vector2((i % file - (file - 1) / 2f) * 2.7f + J(0.5f), (i / file) * 2.7f + J(0.5f)));
                break;
            }
            case Formation.Spread:
            {
                int file = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * 1.5f)), 1, 18);
                for (int i = 0; i < n; i++)
                    list.Add(new Vector2((i % file - (file - 1) / 2f) * 4.7f + J(2.1f), (i / file) * 4.5f + J(2.0f)));
                break;
            }
            case Formation.Line:
            {
                int ranks = n > 40 ? 3 : 2;
                int file = Mathf.Max(1, Mathf.CeilToInt(n / (float)ranks));
                for (int i = 0; i < n; i++)
                    list.Add(new Vector2((i % file - (file - 1) / 2f) * 2.9f + J(0.6f), (i / file) * 2.9f + J(0.6f)));
                break;
            }
            case Formation.Wedge:
            {
                int r = 0, placed = 0;
                while (placed < n)
                {
                    int width = 2 * r + 1;
                    for (int c = 0; c < width && placed < n; c++, placed++)
                        list.Add(new Vector2((c - (width - 1) / 2f) * 2.9f + J(0.6f), r * 2.9f + J(0.6f)));
                    r++;
                }
                break;
            }
            default: // Grid
            {
                int file = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * 2f)), 1, 16);
                for (int i = 0; i < n; i++)
                    list.Add(new Vector2((i % file - (file - 1) / 2f) * 2.9f + J(1.1f), (i / file) * 3.1f + J(1.0f)));
                break;
            }
        }
        return list;
    }

    // Place a defended castle well behind `side`'s starting line and register it so the
    // battle ends the moment it falls.
    private void SpawnCastle(Team side)
    {
        float x = side == Team.Player ? -40f : 40f;
        var castle = new Castle { UnitTeam = side, IsBattleUnit = true, IsStructure = true };
        _armies.AddChild(castle);
        castle.GlobalPosition = new Vector3(x, 0f, 0f);
        _director.RegisterCastle(castle, side);
    }

    // Spawn one reinforcement wave marching in from the far back of its side.
    private void SpawnReinforcement(Reinforcement r)
    {
        var roster = new List<UnitArchetype>();
        foreach (var e in r.Units)
            for (int i = 0; i < e.Count; i++)
                roster.Add(e.Archetype);

        int n = roster.Count;
        float side = r.Side == Team.Player ? -1f : 1f;
        int file = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * 2f)), 1, 10);

        for (int i = 0; i < n; i++)
        {
            int rank = i / file, col = i % file;
            float z = (col - (file - 1) / 2f) * 1.6f + (GD.Randf() * 2f - 1f) * 0.5f;
            float x = side * (44f + rank * 1.6f);

            var unit = new SimUnit { Archetype = roster[i], UnitTeam = r.Side, IsBattleUnit = true };
            unit.BuffMoveSpeedMult = 0.8f + GD.Randf() * 0.4f;
            _armies.AddChild(unit);
            unit.GlobalPosition = new Vector3(x, 0f, z);
            unit.SetFacing(unit.AdvanceDir);
        }
    }

    // Lower value = closer to the front line.
    private static int FrontOrder(Role role) => role switch
    {
        Role.Tank => 0,
        Role.Boss => 0,
        Role.Melee => 1,
        Role.Swarm => 1,
        Role.Ranged => 2,
        Role.AoE => 3,
        Role.Healer => 4,
        _ => 2,
    };

    private void ToggleCinematic()
    {
        _cinematic = !_cinematic;
        _rig.SetOffset(_cinematic ? CineOffset : StdOffset, _cinematic ? 42f : 50f);
    }
}
