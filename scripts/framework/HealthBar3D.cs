using Godot;

namespace Framework;

// Reusable floating HP bar: two unshaded, no-depth-test quads (dark background +
// colored fill) that the owner reorients to face the camera each frame. The fill
// depletes from the right via a left-anchored scale, in real screen space.
public partial class HealthBar3D : Node3D
{
    public float Width = 1.4f;
    public float Height = 0.18f;

    private MeshInstance3D _fill;

    public void Build(Color fillColor)
    {
        TopLevel = true; // the owner's rotation never skews the bar
        _fill = MakeQuad(fillColor, 0.002f, priority: 1);
        AddChild(MakeQuad(new Color(0.05f, 0.05f, 0.05f), 0f, priority: 0)); // background first
        AddChild(_fill);
        SetRatio(1f);
    }

    public void UpdateWorld(Vector3 worldPos, Camera3D cam)
    {
        if (cam != null) GlobalTransform = GodotEx.BillboardTransform(cam, worldPos);
        else GlobalPosition = worldPos;
    }

    public void SetRatio(float ratio)
    {
        if (_fill == null) return;
        ratio = Mathf.Clamp(ratio, 0f, 1f);
        _fill.Scale = new Vector3(Mathf.Max(ratio, 0.0001f), 1f, 1f);
        _fill.Position = new Vector3(-(1f - ratio) * Width * 0.5f, 0f, 0.001f);
    }

    private MeshInstance3D MakeQuad(Color color, float zNudge, int priority)
    {
        var mesh = new QuadMesh { Size = new Vector2(Width, Height) };
        mesh.Material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            DisableReceiveShadows = true,
            NoDepthTest = true,
            RenderPriority = priority, // fill (1) draws after background (0) -> stays on top
        };
        return new MeshInstance3D { Mesh = mesh, Position = new Vector3(0f, 0f, zNudge) };
    }
}
