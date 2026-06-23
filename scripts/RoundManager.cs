using Godot;
using System.Collections.Generic;
using System.Text;

public enum EnemyArch { Grunt, Fast, Tank, Archer, Healer, Boss }

// Continuous battlefield director: every interval it spawns an evolving enemy
// batch, enforces a hard per-side unit cap, and ends the game when a castle
// falls. The player's own batch is spawned by GameManager on the BatchSpawned tick.
public partial class RoundManager : Node
{
    [Signal] public delegate void BatchSpawnedEventHandler(int batchNumber);
    [Signal] public delegate void TimerTickEventHandler(float secondsLeft, int batchNumber);
    [Signal] public delegate void GameOverEventHandler(bool victory, int batches);

    public const int MaxPerSide = 40;

    public float BatchInterval = 10f;
    public int BatchNumber { get; private set; } = 0;
    public bool IsGameOver { get; private set; } = false;

    private Node3D _units;
    private PackedScene _gruntScene;
    private readonly List<UnitBase> _enemyArmy = new();
    private UnitBase _playerCastle;
    private UnitBase _enemyCastle;
    private float _timer = 4f; // short delay before the very first batch

    public void Initialize(Node3D units, PackedScene gruntScene)
    {
        _units = units;
        _gruntScene = gruntScene;
    }

    public void RegisterCastles(UnitBase player, UnitBase enemy)
    {
        _playerCastle = player;
        _enemyCastle = enemy;
    }

    // Economy/upgrade hook: shrink (or grow) the seconds between batches.
    public void ScaleInterval(float factor) => BatchInterval = Mathf.Clamp(BatchInterval * factor, 4f, 20f);

    public int LiveEnemyCount()
    {
        int n = 0;
        foreach (var u in _enemyArmy)
            if (IsInstanceValid(u) && u.State != UnitState.Dead) n++;
        return n;
    }

    public override void _Process(double delta)
    {
        if (IsGameOver) return;

        // Lose if our keep falls; win the run by razing theirs.
        if (IsDead(_playerCastle)) { EndGame(false); return; }
        if (IsDead(_enemyCastle)) { EndGame(true); return; }

        _timer -= (float)delta;
        EmitSignal(SignalName.TimerTick, Mathf.Max(0f, _timer), BatchNumber);
        if (_timer <= 0f)
        {
            BatchNumber++;
            _timer = BatchInterval;
            SpawnEnemyBatch(BatchNumber);
            EmitSignal(SignalName.BatchSpawned, BatchNumber);
        }
    }

    private static bool IsDead(UnitBase u) => u == null || !IsInstanceValid(u) || u.State == UnitState.Dead;

    // ---- Enemy composition evolves with the batch number. ----
    public List<(EnemyArch arch, int count)> EnemyComposition(int batch)
    {
        var list = new List<(EnemyArch, int)>
        {
            (EnemyArch.Grunt, 2 + batch),
        };
        if (batch >= 2) list.Add((EnemyArch.Archer, 1 + batch / 3));
        if (batch >= 3) list.Add((EnemyArch.Fast, 1 + batch / 4));
        if (batch >= 4) list.Add((EnemyArch.Tank, batch / 4));
        if (batch >= 5) list.Add((EnemyArch.Healer, 1 + batch / 6));
        if (batch % 5 == 0) list.Add((EnemyArch.Boss, 1));
        return list;
    }

    public string NextEnemyPreview()
    {
        var comp = EnemyComposition(BatchNumber + 1);
        var sb = new StringBuilder("Enemy next: ");
        for (int i = 0; i < comp.Count; i++)
        {
            sb.Append(comp[i].count).Append(' ').Append(comp[i].arch);
            if (i < comp.Count - 1) sb.Append(", ");
        }
        return sb.ToString();
    }

    private void SpawnEnemyBatch(int batch)
    {
        float hpScale = 1f + 0.10f * (batch - 1);
        float dmgScale = 1f + 0.07f * (batch - 1);

        var spawned = new List<UnitBase>();
        foreach (var (arch, count) in EnemyComposition(batch))
            for (int i = 0; i < count; i++)
                spawned.Add(SpawnEnemy(arch, hpScale, dmgScale));

        PruneDead(_enemyArmy);
        _enemyArmy.AddRange(spawned);
        EnforceCap(_enemyArmy);
        PositionBatch(spawned, Team.Enemy);
    }

    private UnitBase SpawnEnemy(EnemyArch arch, float hpScale, float dmgScale)
    {
        var u = _gruntScene.Instantiate<UnitBase>();
        u.UnitTeam = Team.Enemy;
        u.IsBattleUnit = true;
        switch (arch)
        {
            case EnemyArch.Archer: u.Kind = UnitKind.Ranged; break;
            case EnemyArch.Healer: u.Kind = UnitKind.Healer; break;
            case EnemyArch.Fast:
                u.Kind = UnitKind.Melee; u.MoveSpeed = 3.4f; u.BodyScale = 0.8f;
                u.CollisionRadius = 0.4f; hpScale *= 0.6f; break;
            case EnemyArch.Tank:
                u.Kind = UnitKind.Melee; u.MoveSpeed = 1.2f; u.BodyScale = 1.5f;
                u.CollisionRadius = 0.8f; hpScale *= 2.2f; dmgScale *= 0.8f; break;
            case EnemyArch.Boss:
                u.Kind = UnitKind.Melee; u.MoveSpeed = 1.0f; u.BodyScale = 2.4f;
                u.CollisionRadius = 1.1f; hpScale *= 6f; dmgScale *= 2f; break;
            default: u.Kind = UnitKind.Melee; break;
        }
        u.HpScale = hpScale;
        u.DamageScale = dmgScale;
        _units.AddChild(u);
        return u;
    }

    // Spread a freshly spawned batch in rows of 8 at its castle's front edge.
    public static void PositionBatch(List<UnitBase> batch, Team team)
    {
        float frontX = team == Team.Player ? GameManager.PlayerZoneMinX + 4f : GameManager.EnemyZoneMaxX - 4f;
        Vector3 face = team == Team.Player ? Vector3.Right : Vector3.Left;
        for (int i = 0; i < batch.Count; i++)
        {
            var u = batch[i];
            if (!IsInstanceValid(u)) continue;
            int row = i / 8;
            int col = i % 8;
            float z = Mathf.Lerp(GameManager.SpawnMinZ, GameManager.SpawnMaxZ, col / 7f);
            float x = frontX + (team == Team.Player ? -1f : 1f) * row * 1.6f;
            u.GlobalPosition = new Vector3(x, 0f, z);
            u.SetFacing(face);
        }
    }

    // Drop dead/freed units from a side list (frees the dead node).
    public static void PruneDead(List<UnitBase> list)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var u = list[i];
            if (u == null || !IsInstanceValid(u)) { list.RemoveAt(i); continue; }
            if (u.State == UnitState.Dead) { u.QueueFree(); list.RemoveAt(i); }
        }
    }

    // Hard stacking cap: cull the oldest (front of list) until within MaxPerSide.
    public static void EnforceCap(List<UnitBase> list)
    {
        while (list.Count > MaxPerSide)
        {
            var oldest = list[0];
            list.RemoveAt(0);
            if (IsInstanceValid(oldest)) oldest.QueueFree();
        }
    }

    private void EndGame(bool victory)
    {
        IsGameOver = true;
        UnitBase.BattleActive = false;
        EmitSignal(SignalName.GameOver, victory, BatchNumber);
        GD.Print(victory ? $"VICTORY after {BatchNumber} batches" : $"DEFEAT at batch {BatchNumber}");
    }
}
