using Godot;
using Framework;

// Walk into range, then stop and fire projectiles on cooldown.
public class RangedBehavior : IActorBehavior
{
    private float _attackTimer = 0f;

    public void OnUpdate(Actor unit, Actor target, double delta)
    {
        _attackTimer -= (float)delta;

        float dist = unit.Position.DistanceTo(target.Position);

        if (dist > unit.AttackRange)
        {
            unit.SetState(UnitState.Walking);
            Vector3 dir = (target.Position - unit.Position).Normalized();
            unit.FaceToward(target.GlobalPosition);
            unit.MoveToward(dir, delta);
        }
        else
        {
            unit.FaceToward(target.GlobalPosition);
            if (_attackTimer <= 0f)
            {
                unit.SetState(UnitState.Attacking);
                unit.PlayAnim("Shoot_OneHanded");
                unit.FireProjectile(target);
                _attackTimer = unit.RollCooldown;
            }
            else if (unit.State == UnitState.Walking)
            {
                unit.SetState(UnitState.Idle);
            }
        }
    }
}
