using Godot;

public class MeleeBehavior : IUnitBehavior
{
    private float _attackTimer = 0f;

    public void OnUpdate(UnitBase unit, UnitBase target, double delta)
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
            if (_attackTimer <= 0f)
            {
                unit.SetState(UnitState.Attacking);
                unit.PlayAnim("SwordSlash"); // force replay even if already Attacking
                target.TakeDamage(unit.Damage);
                _attackTimer = unit.AttackCooldown;
            }
            else if (unit.State == UnitState.Walking)
            {
                unit.SetState(UnitState.Idle);
            }
        }
    }
}
