using Godot;

// A quick expanding, fading sphere used to punctuate AoE hits and boss cleaves.
public partial class BlastEffect : Node3D
{
    public float Radius = 2.5f;
    public float Life = 0.35f;
    public Color Color = new(1f, 0.6f, 0.25f);

    private float _t;
    private StandardMaterial3D _mat;

    public override void _Ready()
    {
        _mat = new StandardMaterial3D
        {
            AlbedoColor = Color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var mesh = new SphereMesh { Radius = 1f, Height = 2f, Material = _mat };
        AddChild(new MeshInstance3D { Mesh = mesh });
        Scale = Vector3.One * 0.15f;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float f = _t / Life;
        if (f >= 1f) { QueueFree(); return; }
        Scale = Vector3.One * Mathf.Lerp(0.2f, Radius, f);
        var c = Color; c.A = 1f - f;
        _mat.AlbedoColor = c;
    }
}
