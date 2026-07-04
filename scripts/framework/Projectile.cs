using Godot;

namespace Framework;

// Simple homing arrow: flies to the target's last known position, deals damage on
// arrival. Targets anything IDamageable, so it is reusable outside a specific unit type.
public partial class Projectile : Node3D
{
    public IDamageable Target;
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
        AddChild(BuildTrail());
    }

    // A short, self-following streak so arrows read as moving fast.
    private CpuParticles3D BuildTrail()
    {
        var trailColor = ArrowColor; trailColor.A = 0.5f;
        return new CpuParticles3D
        {
            Amount = 14,
            Lifetime = 0.28f,
            LocalCoords = false,
            Direction = Vector3.Zero,
            Spread = 0f,
            Gravity = Vector3.Zero,
            InitialVelocityMin = 0f,
            InitialVelocityMax = 0f,
            Mesh = new SphereMesh
            {
                Radius = 0.08f,
                Height = 0.16f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = trailColor,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            },
        };
    }

    public override void _Process(double delta)
    {
        if (TargetValid())
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
            if (TargetValid()) Target.TakeDamage(Damage);
            QueueFree();
            return;
        }

        // Homing step, plus weather wind so arrows visibly drift in a gale.
        GlobalPosition += to.Normalized() * step + WeatherState.Wind * (float)delta;
        LookAt(_aimPoint, Vector3.Up);
    }

    private bool TargetValid() =>
        Target is GodotObject go && GodotObject.IsInstanceValid(go) && Target.IsAlive;
}
