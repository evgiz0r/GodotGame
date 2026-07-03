using Godot;
using System.Collections.Generic;

namespace Framework;

// Continuous "batch" director: every BatchInterval seconds it advances the batch
// counter, calls SpawnBatch, and emits signals; it also ends the run when a game
// subclass reports a win/lose condition. Games derive from this and override
// CheckGameOver + SpawnBatch. Reusable across any timed-wave game.
public partial class BatchDirector : Node
{
    [Signal] public delegate void BatchSpawnedEventHandler(int batchNumber);
    [Signal] public delegate void TimerTickEventHandler(float secondsLeft, int batchNumber);
    [Signal] public delegate void GameOverEventHandler(bool victory, int batches);

    public const int MaxPerSide = 30;

    public float BatchInterval = 10f;
    public int BatchNumber { get; private set; } = 0;
    public bool IsGameOver { get; private set; } = false;

    protected float Timer = 4f; // short delay before the very first batch

    // Economy/upgrade hook: shrink (or grow) the seconds between batches.
    public void ScaleInterval(float factor) => BatchInterval = Mathf.Clamp(BatchInterval * factor, 4f, 20f);

    public override void _Process(double delta)
    {
        if (IsGameOver) return;
        if (CheckGameOver(out bool victory)) { EndGame(victory); return; }

        Timer -= (float)delta;
        EmitSignal(SignalName.TimerTick, Mathf.Max(0f, Timer), BatchNumber);
        if (Timer <= 0f)
        {
            BatchNumber++;
            Timer = BatchInterval;
            SpawnBatch(BatchNumber);
            EmitSignal(SignalName.BatchSpawned, BatchNumber);
        }
    }

    // ---- Hooks for game subclasses ----
    protected virtual bool CheckGameOver(out bool victory) { victory = false; return false; }
    protected virtual void SpawnBatch(int batch) { }

    protected void EndGame(bool victory)
    {
        IsGameOver = true;
        Actor.BattleActive = false;
        EmitSignal(SignalName.GameOver, victory, BatchNumber);
    }

    // ---- Shared per-side army list helpers ----
    // Drop dead/freed actors from a side list (frees the dead node).
    public static void PruneDead<T>(List<T> list) where T : Actor
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var u = list[i];
            if (u == null || !GodotObject.IsInstanceValid(u)) { list.RemoveAt(i); continue; }
            if (u.State == UnitState.Dead) { u.QueueFree(); list.RemoveAt(i); }
        }
    }

    // Hard cap: cull the oldest (front of list) until within MaxPerSide.
    public static void EnforceCap<T>(List<T> list) where T : Actor
    {
        while (list.Count > MaxPerSide)
        {
            var oldest = list[0];
            list.RemoveAt(0);
            if (GodotObject.IsInstanceValid(oldest)) oldest.QueueFree();
        }
    }
}
