using Godot;

// Trauma-based camera shake: callers Add() trauma on big hits; Offset() returns a
// decaying random positional jitter each frame. Shake^2 falloff feels snappier.
public class CameraShake
{
    public float MaxOffset = 0.7f;
    public float Decay = 1.8f;

    private float _trauma;

    public void Add(float amount) => _trauma = Mathf.Min(1f, _trauma + amount);

    public Vector3 Offset(double delta)
    {
        if (_trauma <= 0f) return Vector3.Zero;
        float shake = _trauma * _trauma;
        _trauma = Mathf.Max(0f, _trauma - Decay * (float)delta);
        return new Vector3(Rand(), Rand() * 0.6f, Rand()) * MaxOffset * shake;
    }

    private static float Rand() => GD.Randf() * 2f - 1f;
}
