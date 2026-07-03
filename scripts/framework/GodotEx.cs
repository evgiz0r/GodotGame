using Godot;

namespace Framework;

// Small, dependency-free Godot helpers that are reusable across projects.
public static class GodotEx
{
    // Quaternius (and many Kenney) models face +Z, but LookAt aligns -Z toward the
    // target, so we look at the point *behind* us in the desired direction.
    public static void FacePlusZ(Node3D node, Vector3 worldDirection)
    {
        var dir = worldDirection.Normalized();
        dir.Y = 0f;
        if (dir.LengthSquared() < 0.001f) return;
        node.LookAt(node.GlobalPosition - dir, Vector3.Up);
    }

    // glTF imports often drop loop flags; force the named clips to loop.
    public static void ForceLoop(AnimationPlayer anim, params string[] names)
    {
        if (anim == null) return;
        foreach (var n in names)
            if (anim.HasAnimation(n))
                anim.GetAnimation(n).LoopMode = Animation.LoopModeEnum.Linear;
    }

    // Manual billboard: orient a pivot to face the camera (no material billboard,
    // so screen-space offsets on child quads stay stable under angled cameras).
    public static Transform3D BillboardTransform(Camera3D cam, Vector3 pos) =>
        new(cam.GlobalBasis, pos);

    // Ray from the screen onto the Y=0 ground plane (no physics/collision needed).
    public static bool RayToGroundPlane(Camera3D cam, Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = Vector3.Zero;
        if (cam == null) return false;
        var from = cam.ProjectRayOrigin(screenPos);
        var dir = cam.ProjectRayNormal(screenPos);
        if (Mathf.Abs(dir.Y) < 0.001f) return false;
        float t = -from.Y / dir.Y;
        if (t < 0f) return false;
        worldPos = from + dir * t;
        return true;
    }
}
