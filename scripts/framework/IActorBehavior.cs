namespace Framework;

// A pluggable "brain" for an actor: called each frame with the actor and (optionally)
// its current target. Games provide concrete behaviors (melee, ranged, healer, ...).
public interface IActorBehavior
{
    void OnUpdate(Actor unit, Actor target, double delta);
}
