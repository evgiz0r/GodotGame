using Godot;
using Framework;

// Game-wide one-shot events the presentation layer listens to (camera shake, etc.).
// ASSIGN handlers (= ), never += , and clear on scene exit — statics survive reloads.
public static class SimEvents
{
    public static System.Action<float> Shake;
}

// Small combat helpers shared by AoE / cleave behaviors.
public static class Combat
{
    // Damage every living enemy of `source` within `radius` of `center`.
    public static int DamageEnemiesInRadius(Actor source, Vector3 center, float radius, float dmg)
    {
        string group = source.UnitTeam == Team.Player ? "enemy_units" : "player_units";
        int hits = 0;
        foreach (var node in source.GetTree().GetNodesInGroup(group))
        {
            if (node is Actor a && a.IsAlive && a.GlobalPosition.DistanceTo(center) <= radius)
            {
                a.TakeDamage(dmg);
                hits++;
            }
        }
        return hits;
    }

    public static void SpawnBlast(Actor source, Vector3 center, float radius, Color color)
    {
        var blast = new BlastEffect { Radius = radius, Color = color };
        source.GetParent().AddChild(blast);
        blast.GlobalPosition = center;
    }
}
