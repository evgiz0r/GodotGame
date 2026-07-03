using Godot;

namespace Framework;

// A growing budget that reserves a standing composition of unit "kinds". The same
// composition can be respawned for free each batch; the budget just caps how big it
// can be. Costs scale with CostMult. Reusable across games that use a budget economy.
public class Economy
{
    public float Budget;
    public float IncomePerSec;
    public float CostMult = 1f;

    private readonly int[] _baseCost;
    private readonly int[] _comp;

    public Economy(int[] baseCost, int[] startComp, float startBudget, float income)
    {
        _baseCost = baseCost;
        _comp = startComp;
        Budget = startBudget;
        IncomePerSec = income;
    }

    public int Kinds => _comp.Length;
    public int Count(int kind) => _comp[kind];
    public int UnitCost(int kind) => Mathf.Max(1, Mathf.RoundToInt(_baseCost[kind] * CostMult));

    public int Reserved()
    {
        int sum = 0;
        for (int i = 0; i < _comp.Length; i++) sum += _comp[i] * UnitCost(i);
        return sum;
    }

    public bool CanAdd(int kind) => Reserved() + UnitCost(kind) <= Budget;

    // Returns false if a positive change can't be afforded.
    public bool ChangeComp(int kind, int delta)
    {
        if (delta > 0 && !CanAdd(kind)) return false;
        _comp[kind] = Mathf.Max(0, _comp[kind] + delta);
        return true;
    }

    // Index of the cheapest kind (the auto-buy target).
    public int CheapestKind()
    {
        int best = 0, bestCost = int.MaxValue;
        for (int i = 0; i < _comp.Length; i++)
        {
            int c = UnitCost(i);
            if (c < bestCost) { bestCost = c; best = i; }
        }
        return best;
    }

    // Set a kind to a desired count. When raising it beyond budget, remove the cheapest
    // OTHER reserved units (cascading) to free budget; caps the value if impossible.
    public bool TrySetCount(int kind, int value)
    {
        value = Mathf.Max(0, value);
        if (value <= _comp[kind]) { _comp[kind] = value; return true; }
        while (_comp[kind] < value)
        {
            if (CanAdd(kind)) { _comp[kind]++; continue; }
            if (!RemoveCheapestOther(kind)) break; // nothing left to free
        }
        return _comp[kind] == value;
    }

    private bool RemoveCheapestOther(int kind)
    {
        int best = -1, bestCost = int.MaxValue;
        for (int i = 0; i < _comp.Length; i++)
        {
            if (i == kind || _comp[i] <= 0) continue;
            int c = UnitCost(i);
            if (c < bestCost) { bestCost = c; best = i; }
        }
        if (best < 0) return false;
        _comp[best]--;
        return true;
    }

    // Spend spare budget on the cheapest kind. Returns how many were added.
    public int AutoFillCheapest()
    {
        int k = CheapestKind(), added = 0;
        while (CanAdd(k)) { _comp[k]++; added++; }
        return added;
    }

    public void Tick(double delta) => Budget += IncomePerSec * (float)delta;
}
