using Godot;

// One-shot particle bursts for hits and deaths — cheap, self-freeing CpuParticles3D so the
// simulator never leaks emitters. Color drives a fade-out ramp; the quad billboards to camera.
public static class Fx
{
    public static void Burst(Node parent, Vector3 pos, Color color,
        int amount = 8, float speed = 3.5f, float life = 0.45f, float size = 0.14f, float gravity = -6f)
    {
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;

        var p = new CpuParticles3D
        {
            Emitting = true,
            OneShot = true,
            Amount = amount,
            Lifetime = life,
            Explosiveness = 0.85f,
            Direction = Vector3.Up,
            Spread = 180f,
            InitialVelocityMin = speed * 0.4f,
            InitialVelocityMax = speed,
            Gravity = new Vector3(0f, gravity, 0f),
            LocalCoords = false,
            Mesh = Quad(size),
            ColorRamp = Fade(color),
        };
        parent.AddChild(p);
        p.GlobalPosition = pos;

        var tree = p.GetTree();
        if (tree != null)
        {
            var timer = tree.CreateTimer(life + 0.3f);
            timer.Timeout += () => { if (GodotObject.IsInstanceValid(p)) p.QueueFree(); };
        }
    }

    private static Gradient Fade(Color c)
    {
        var g = new Gradient();
        var start = c; var end = c; end.A = 0f;
        g.SetColor(0, start);
        g.SetColor(1, end);
        return g;
    }

    private static QuadMesh Quad(float s)
    {
        var m = new QuadMesh { Size = new Vector2(s, s) };
        m.Material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,
        };
        return m;
    }
}
