using Godot;

// Stays near the front, heals the most-wounded ally in range; marches when none need help.
public class HealerBehavior : IUnitBehavior
{
    private float _timer = 0f;

    public void OnUpdate(UnitBase unit, UnitBase target, double delta)
    {
        _timer -= (float)delta;
        var ally = unit.FindWoundedAlly(unit.AttackRange);
        if (ally != null)
        {
            unit.FaceToward(ally.GlobalPosition);
            if (_timer <= 0f)
            {
                unit.SetState(UnitState.Attacking);
                unit.PlayAnim("SwordSlash");
                ally.Heal(unit.Damage);
                _timer = unit.AttackCooldown;
            }
            else if (unit.State == UnitState.Walking)
            {
                unit.SetState(UnitState.Idle);
            }
        }
        else
        {
            unit.AdvanceStep(delta);
        }
    }
}
