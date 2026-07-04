using Godot;
using Framework;

// Boss brain: walks to the nearest enemy, then cleaves every enemy within its (large)
// reach on each swing, with a heavy blast + camera shake to sell the weight.
public class BossBehavior : IActorBehavior
{
    private readonly float _range;
    private float _timer;

    public BossBehavior(float range) => _range = range;

    public void OnUpdate(Actor unit, Actor target, double delta)
    {
        _timer -= (float)delta;
        float dist = unit.Position.DistanceTo(target.Position);

        if (dist > _range)
        {
            unit.SetState(UnitState.Walking);
            Vector3 dir = (target.Position - unit.Position).Normalized();
            unit.FaceToward(target.GlobalPosition);
            unit.MoveToward(dir, delta);
            return;
        }

        unit.FaceToward(target.GlobalPosition);
        if (_timer <= 0f)
        {
            unit.SetState(UnitState.Attacking);
            unit.PlayAnim("SwordSlash");
            Combat.DamageEnemiesInRadius(unit, unit.GlobalPosition, _range, unit.Damage);
            Combat.SpawnBlast(unit, unit.GlobalPosition, _range, new Color(1f, 0.4f, 0.2f));
            SimEvents.Shake?.Invoke(0.6f);
            _timer = unit.RollCooldown;
        }
        else if (unit.State == UnitState.Walking)
        {
            unit.SetState(UnitState.Idle);
        }
    }
}
