using Godot;
using Framework;

// Ranged AoE caster (mage): march into range, then lob a splash attack that damages every
// enemy near the target and spawns a blast. A small camera shake sells the impact.
public class AoeBehavior : IActorBehavior
{
    private readonly float _radius;
    private float _timer;

    public AoeBehavior(float radius) => _radius = radius <= 0f ? 2.5f : radius;

    public void OnUpdate(Actor unit, Actor target, double delta)
    {
        _timer -= (float)delta;
        float dist = unit.Position.DistanceTo(target.Position);

        if (dist > unit.AttackRange)
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
            unit.PlayAnim("Shoot_OneHanded");
            Vector3 center = target.GlobalPosition;
            Combat.DamageEnemiesInRadius(unit, center, _radius, unit.Damage);
            Combat.SpawnBlast(unit, center, _radius, new Color(0.7f, 0.4f, 1f));
            SimEvents.Shake?.Invoke(0.15f);
            _timer = unit.AttackCooldown;
        }
        else if (unit.State == UnitState.Walking)
        {
            unit.SetState(UnitState.Idle);
        }
    }
}
