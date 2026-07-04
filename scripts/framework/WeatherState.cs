using Godot;

namespace Framework;

// Global, weather-driven combat modifiers. The game maps its own Weather enum onto these
// scalar fields; Actor / Projectile read them each frame so a scenario's weather changes
// how the whole field behaves without per-unit bookkeeping. ASSIGN, reset on menu.
public static class WeatherState
{
    public static float MoveMult = 1f;           // rain/snow slow movement
    public static float RangeMult = 1f;          // fog shortens sight/target acquisition
    public static float AttackCooldownMult = 1f; // snow slows attacks
    public static Vector3 Wind = Vector3.Zero;   // world drift applied to projectiles

    public static void Clear()
    {
        MoveMult = 1f;
        RangeMult = 1f;
        AttackCooldownMult = 1f;
        Wind = Vector3.Zero;
    }
}
