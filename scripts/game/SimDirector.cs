using System;
using Godot;
using Framework;

public enum BattleResult { PlayerWins, EnemyWins, Draw }

// Watches unit deaths and ends the battle when a side is wiped out. No economy, no waves —
// just: are there living units left on each side? Reset() before each new fight.
public partial class SimDirector : Node
{
    public event Action<BattleResult> BattleEnded;

    private bool _finished;

    public override void _Ready() => Actor.UnitDied = OnUnitDied;   // ASSIGN, never +=
    public override void _ExitTree() => Actor.UnitDied = null;

    public void Reset() => _finished = false;

    private void OnUnitDied(Team _)
    {
        if (_finished) return;
        int player = LivingCount("player_units");
        int enemy = LivingCount("enemy_units");
        if (player == 0 && enemy == 0) Finish(BattleResult.Draw);
        else if (enemy == 0) Finish(BattleResult.PlayerWins);
        else if (player == 0) Finish(BattleResult.EnemyWins);
    }

    private int LivingCount(string group)
    {
        int count = 0;
        foreach (var node in GetTree().GetNodesInGroup(group))
            if (node is Actor a && a.IsAlive) count++;
        return count;
    }

    private void Finish(BattleResult result)
    {
        _finished = true;
        Actor.BattleActive = false;
        BattleEnded?.Invoke(result);
    }
}
