using Godot;
using Framework;

public class MeleeBehavior : IActorBehavior
{
    private float _attackTimer = 0f;
    private float _flank = 2f; // per-unit sideways bias, lazily seeded from the instance id

    public void OnUpdate(Actor unit, Actor target, double delta)
    {
        _attackTimer -= (float)delta;
        if (_flank > 1f) _flank = (unit.GetInstanceId() % 1000) / 1000f * 2f - 1f; // stable -1..1

        float dist = unit.Position.DistanceTo(target.Position);
        float reach = unit.AttackRange + target.CollisionRadius;

        if (dist > reach)
        {
            unit.SetState(UnitState.Walking);
            Vector3 to = target.Position - unit.Position; to.Y = 0f;
            Vector3 dir = to.Normalized();
            // Fan out: approach from a spread angle while still far, collapsing straight onto
            // the target as we close, so a mob doesn't funnel into one zombie stack.
            Vector3 perp = new Vector3(-dir.Z, 0f, dir.X);
            float spread = Mathf.Clamp((dist - reach) / 6f, 0f, 1f) * 0.55f * _flank;
            dir = (dir + perp * spread).Normalized();
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
                _attackTimer = unit.RollCooldown;
            }
            else if (unit.State == UnitState.Walking)
            {
                unit.SetState(UnitState.Idle);
            }
        }
    }
}
