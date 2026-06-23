using Godot;

// Simple homing arrow: flies to the target's last known position, deals damage on arrival.
public partial class Projectile : Node3D
{
    public UnitBase Target;
    public float Speed = 22f;
    public float Damage = 18f;
    public Color ArrowColor = new(0.9f, 0.9f, 0.9f);

    private Vector3 _aimPoint;
    private bool _hasAim;

    public override void _Ready()
    {
        var mesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.7f) };
        mesh.Material = new StandardMaterial3D
        {
            AlbedoColor = ArrowColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(new MeshInstance3D { Mesh = mesh });
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(Target) && Target.State != UnitState.Dead)
        {
            _aimPoint = Target.GlobalPosition + new Vector3(0f, 1.2f, 0f);
            _hasAim = true;
        }
        else if (!_hasAim)
        {
            QueueFree();
            return;
        }

        Vector3 to = _aimPoint - GlobalPosition;
        float step = Speed * (float)delta;
        if (to.Length() <= step)
        {
            if (IsInstanceValid(Target) && Target.State != UnitState.Dead)
                Target.TakeDamage(Damage);
            QueueFree();
            return;
        }

        GlobalPosition += to.Normalized() * step;
        LookAt(_aimPoint, Vector3.Up);
    }
}
