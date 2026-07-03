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
        _shake = new CameraShake();

        _director = new SimDirector();
        AddChild(_director);
        _director.BattleEnded += OnBattleEnded;

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

        _cam.Position = _rig.CameraBasePosition + _shake.Offset(delta);
        _cam.LookAt(_rig.Target, Vector3.Up);
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

        _bossRef = null;
        SpawnArmy(scenario.Player, Team.Player);
        SpawnArmy(scenario.Enemy, Team.Enemy);

        _followBoss = _bossRef != null;
        _rig.SetTarget(Vector3.Zero);
        _rig.ZoomLevel = 1.45f;
        _zoomTarget = 1f;
        _autoZoom = true;

        Actor.BattleActive = false;
        _countdown = 1.2f;
        _fightFlash = 0f;
        _hud.ShowBattle();
    }

    private void ReturnToMenu()
    {
        Actor.BattleActive = false;
        _currentIndex = -1;
        _followBoss = false;
        ClearBattlefield();
        _hud.ShowMenu();
    }

    private void OnBattleEnded(BattleResult result)
    {
        _followBoss = false;
        string text = result switch
        {
            BattleResult.PlayerWins => "BLUE ARMY WINS",
            BattleResult.EnemyWins => "RED ARMY WINS",
            _ => "MUTUAL DESTRUCTION",
        };
        _hud.ShowResult(text);
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
    // place them in a loose, jittered grid on their side of the field facing the enemy.
    private void SpawnArmy(ArmyEntry[] entries, Team team)
    {
        var roster = new List<(UnitArchetype arch, float key)>();
        foreach (var e in entries)
            for (int i = 0; i < e.Count; i++)
                roster.Add((e.Archetype, FrontOrder(e.Archetype.Role) + GD.Randf() * 1.7f));
        roster.Sort((a, b) => a.key.CompareTo(b.key));

        int n = roster.Count;
        int file = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(n * 2f)), 1, 16);
        const float gap = 9f, dz = 1.5f, dx = 1.7f;

        for (int i = 0; i < n; i++)
        {
            var arch = roster[i].arch;
            int rank = i / file;
            int col = i % file;
            float z = (col - (file - 1) / 2f) * dz + (GD.Randf() * 2f - 1f) * 0.7f;
            float depth = rank * dx + (GD.Randf() * 2f - 1f) * 0.6f;
            float x = team == Team.Player ? -(gap + depth) : (gap + depth);

            var unit = new SimUnit { Archetype = arch, UnitTeam = team, IsBattleUnit = true };
            _armies.AddChild(unit);
            unit.GlobalPosition = new Vector3(x, 0f, z);
            // Slight facing jitter so they don't stare in perfect lockstep.
            unit.SetFacing((unit.AdvanceDir + new Vector3(0f, 0f, (GD.Randf() * 2f - 1f) * 0.25f)).Normalized());

            if (arch.Role == Role.Boss && team == Team.Player) _bossRef = unit;
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
