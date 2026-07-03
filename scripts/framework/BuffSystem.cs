using Godot;
using System.Collections.Generic;

namespace Framework;

// One pickable buff: a label + the mutation it applies.
public sealed record Buff(string Name, System.Action Apply);

// Holds a pool of buffs and offers N distinct random choices. The game owns the pool
// contents (what each buff does) and the presentation; this just does the picking.
public class BuffSystem
{
    private readonly List<Buff> _pool = new();

    public void Add(Buff buff) => _pool.Add(buff);
    public void AddRange(IEnumerable<Buff> buffs) => _pool.AddRange(buffs);

    public List<Buff> Offer(int n)
    {
        var pool = new List<Buff>(_pool);
        var choices = new List<Buff>();
        for (int i = 0; i < n && pool.Count > 0; i++)
        {
            int r = (int)(GD.Randi() % (uint)pool.Count);
            choices.Add(pool[r]);
            pool.RemoveAt(r);
        }
        return choices;
    }
}
