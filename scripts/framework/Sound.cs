using Godot;

namespace Framework;

// One-shot sound hooks the framework fires at world positions; the game assigns a generator
// (see SfxManager). ASSIGN handlers (=), never +=, and clear on scene exit — statics survive
// reloads. Null-safe: with no handler assigned the sim is simply silent.
public static class Sound
{
    public static System.Action<Vector3> Shoot;
    public static System.Action<Vector3> Hit;
    public static System.Action<Vector3> Death;
    public static System.Action<Vector3> Blast;
    public static System.Action Ko;
}
