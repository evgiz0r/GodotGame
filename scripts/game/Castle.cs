using Godot;
using Framework;

// A stationary fortress: high HP, never moves, and lobs arrows at the nearest attacker on a
// cooldown. Built from stacked boxes/cylinders (no assets). A scenario that defends a castle
// ends when the castle's HP hits zero (SimDirector tracks it). Set UnitTeam + IsBattleUnit +
// IsStructure before AddChild; GlobalPosition after.
public partial class Castle : Actor
{
    private float _timer;

    protected override void ConfigureStats()
    {
        MaxHp = 1600f;
        Damage = 16f;
        AttackRange = 30f;
        AttackCooldown = 1.1f;
        MoveSpeed = 0f;
        DetectionRange = 34f;
        CollisionRadius = 3.5f;
        CanRetreat = false;

        HpBarWidth = 5f;
        HpBarHeight = 0.32f;
        HpBarYOffset = 7.5f;
    }

    protected override void BuildVisuals()
    {
        var stone = new Color(0.52f, 0.52f, 0.57f);
        var dark = new Color(0.4f, 0.4f, 0.45f);

        // Main keep.
        AddBox(new Vector3(6.5f, 5f, 6.5f), new Vector3(0f, 2.5f, 0f), stone);
        // Battlement cap.
        AddBox(new Vector3(7.2f, 0.9f, 7.2f), new Vector3(0f, 5.4f, 0f), dark);
        // Gate.
        AddBox(new Vector3(1.8f, 2.6f, 0.4f), new Vector3(0f, 1.3f, 3.35f), new Color(0.28f, 0.2f, 0.14f));

        // Corner towers.
        foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
        {
            AddCylinder(1.1f, 7f, new Vector3(3.4f * sx, 3.5f, 3.4f * sz), stone);
            AddCylinder(1.35f, 0.7f, new Vector3(3.4f * sx, 7.3f, 3.4f * sz), dark);
        }
    }

    protected override void OnStructureTick(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0f) return;
        var target = FindNearestEnemy();
        if (target == null) return;
        FireProjectile(target);
        _timer = AttackCooldown;
    }

    private void AddBox(Vector3 size, Vector3 pos, Color color)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos };
        ((BoxMesh)m.Mesh).Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f };
        AddChild(m);
    }

    private void AddCylinder(float radius, float height, Vector3 pos, Color color)
    {
        var m = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
            Position = pos,
        };
        ((CylinderMesh)m.Mesh).Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f };
        AddChild(m);
    }
}
