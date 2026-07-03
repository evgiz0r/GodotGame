using Godot;

namespace Framework;

// Two-side combat. Reusable across games built on this framework.
public enum Team { Player, Enemy }

// Generic actor animation/logic state.
public enum UnitState { Walking, Idle, Attacking, Dead }

// Anything an attack or projectile can hit.
public interface IDamageable
{
    Vector3 GlobalPosition { get; }
    bool IsAlive { get; }
    void TakeDamage(float amount);
}
