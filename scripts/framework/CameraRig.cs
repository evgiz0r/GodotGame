using Godot;

namespace Framework;

// Angled 3/4 camera controller: mouse-wheel zoom (keeps the angle) + right-drag pan.
// Owns no node of its own; it drives an existing Camera3D. Reusable across games.
public class CameraRig
{
    private readonly Camera3D _cam;
    private Vector3 _target;
    private Vector3 _offset;
    private float _zoom = 1f;

    public float ZoomStep = 0.9f;
    public float ZoomMin = 0.45f;
    public float ZoomMax = 1.6f;
    public float PanSpeed = 0.06f;
    public Vector2 PanClampX = new(-70f, 70f);
    public Vector2 PanClampZ = new(-40f, 50f);

    private bool _panning;

    public CameraRig(Camera3D cam) => _cam = cam;

    // target = look-at point; cameraPos = world position at zoom 1.
    public void Setup(Vector3 target, Vector3 cameraPos, float zoom, float fov)
    {
        _target = target;
        _offset = cameraPos - target;
        _zoom = zoom;
        _cam.Fov = fov;
        Apply();
    }

    // Returns true if the event was consumed by the rig.
    public bool HandleInput(InputEvent e)
    {
        if (e is InputEventMouseButton wheel && wheel.Pressed)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp) { _zoom = Mathf.Clamp(_zoom * ZoomStep, ZoomMin, ZoomMax); Apply(); return true; }
            if (wheel.ButtonIndex == MouseButton.WheelDown) { _zoom = Mathf.Clamp(_zoom / ZoomStep, ZoomMin, ZoomMax); Apply(); return true; }
        }
        if (e is InputEventMouseButton rmb && rmb.ButtonIndex == MouseButton.Right)
        {
            _panning = rmb.Pressed;
            return true;
        }
        if (_panning && e is InputEventMouseMotion motion)
        {
            Pan(motion.Relative);
            return true;
        }
        return false;
    }

    private void Pan(Vector2 mouseDelta)
    {
        Vector3 right = _cam.GlobalTransform.Basis.X; right.Y = 0f; right = right.Normalized();
        Vector3 fwd = -_cam.GlobalTransform.Basis.Z; fwd.Y = 0f; fwd = fwd.Normalized();
        _target += (-mouseDelta.X * right + mouseDelta.Y * fwd) * PanSpeed * _zoom;
        _target.X = Mathf.Clamp(_target.X, PanClampX.X, PanClampX.Y);
        _target.Z = Mathf.Clamp(_target.Z, PanClampZ.X, PanClampZ.Y);
        Apply();
    }

    private void Apply()
    {
        _cam.Position = _target + _offset * _zoom;
        _cam.LookAt(_target, Vector3.Up);
    }

    // ---- Accessors so a game can layer effects (shake, follow, auto-zoom) on top. ----
    public Vector3 Target => _target;
    public Vector3 CameraBasePosition => _target + _offset * _zoom;
    public float ZoomLevel { get => _zoom; set => _zoom = Mathf.Clamp(value, ZoomMin, ZoomMax); }

    // Re-aim the look-at point (clamped to the pan bounds); does not force an Apply so a
    // caller that recomputes the transform each frame stays in control.
    public void SetTarget(Vector3 target)
    {
        _target.X = Mathf.Clamp(target.X, PanClampX.X, PanClampX.Y);
        _target.Y = target.Y;
        _target.Z = Mathf.Clamp(target.Z, PanClampZ.X, PanClampZ.Y);
    }

    // Swap the camera angle/offset (e.g. cinematic toggle) while keeping target + zoom.
    public void SetOffset(Vector3 cameraOffset, float fov)
    {
        _offset = cameraOffset;
        _cam.Fov = fov;
        Apply();
    }
}
