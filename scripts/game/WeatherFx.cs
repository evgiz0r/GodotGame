using Godot;
using Framework;

// Maps a scenario's Weather onto (a) the global WeatherState combat modifiers and (b) visual
// dressing: tweaked fog on the current environment plus a falling/blowing particle overlay.
// Attach the returned particles under the environment so they're cleared with it.
public static class WeatherFx
{
    public static string Label(Weather w) => w switch
    {
        Weather.Rain => "Rain — slower movement",
        Weather.Fog => "Fog — shortened sight",
        Weather.Wind => "Wind — projectiles drift",
        Weather.Snow => "Snow — sluggish attacks",
        _ => "Clear skies",
    };

    // Set the global combat modifiers + dress the environment for `w`. Call after the
    // environment is (re)built. Resets WeatherState first so Clear truly clears.
    public static void Apply(Weather w, Node3D envRoot)
    {
        WeatherState.Clear();
        var env = FindEnv(envRoot);

        switch (w)
        {
            case Weather.Rain:
                WeatherState.MoveMult = 0.82f;
                WeatherState.Wind = new Vector3(0f, 0f, 1.4f);
                if (env != null) { env.FogDensity = 0.02f; env.FogLightColor = new Color(0.5f, 0.55f, 0.62f); }
                envRoot.AddChild(Rain());
                break;

            case Weather.Fog:
                WeatherState.RangeMult = 0.55f;
                if (env != null) { env.FogDensity = 0.011f; env.FogLightColor = new Color(0.78f, 0.8f, 0.84f); }
                break;

            case Weather.Wind:
                WeatherState.MoveMult = 0.95f;
                WeatherState.Wind = new Vector3(0f, 0f, 5f);
                envRoot.AddChild(Wind());
                break;

            case Weather.Snow:
                WeatherState.AttackCooldownMult = 1.35f;
                WeatherState.MoveMult = 0.9f;
                if (env != null) env.FogDensity = 0.007f;
                envRoot.AddChild(Snow());
                break;
        }
    }

    private static Godot.Environment FindEnv(Node node)
    {
        if (node is WorldEnvironment we) return we.Environment;
        foreach (var child in node.GetChildren())
        {
            var found = FindEnv(child);
            if (found != null) return found;
        }
        return null;
    }

    private static CpuParticles3D Rain() => new()
    {
        Amount = 340,
        Lifetime = 0.7f,
        Preprocess = 0.7f,
        Position = new Vector3(0f, 18f, 0f),
        EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = new Vector3(46f, 0.5f, 34f),
        Direction = Vector3.Down,
        Spread = 0f,
        Gravity = new Vector3(2f, -40f, 6f),
        InitialVelocityMin = 8f,
        InitialVelocityMax = 10f,
        LocalCoords = false,
        Mesh = Streak(new Color(0.62f, 0.72f, 0.95f, 0.55f), new Vector2(0.03f, 0.55f), billboard: false),
    };

    private static CpuParticles3D Snow() => new()
    {
        Amount = 170,
        Lifetime = 6f,
        Preprocess = 6f,
        Position = new Vector3(0f, 15f, 0f),
        EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = new Vector3(46f, 0.5f, 34f),
        Direction = Vector3.Down,
        Spread = 25f,
        Gravity = new Vector3(0.5f, -1.8f, 0.3f),
        InitialVelocityMin = 0.3f,
        InitialVelocityMax = 0.6f,
        LocalCoords = false,
        Mesh = Streak(new Color(0.85f, 0.88f, 0.95f, 0.5f), new Vector2(0.1f, 0.1f), billboard: true),
    };

    private static CpuParticles3D Wind() => new()
    {
        Amount = 60,
        Lifetime = 3.5f,
        Preprocess = 3.5f,
        Position = new Vector3(0f, 3.5f, 0f),
        EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = new Vector3(48f, 3.5f, 34f),
        Direction = new Vector3(0f, 0f, 1f),
        Spread = 20f,
        Gravity = new Vector3(0f, -0.4f, 0f),
        InitialVelocityMin = 6f,
        InitialVelocityMax = 9f,
        LocalCoords = false,
        Mesh = Streak(new Color(0.75f, 0.68f, 0.4f, 0.5f), new Vector2(0.14f, 0.09f), billboard: true),
    };

    private static QuadMesh Streak(Color color, Vector2 size, bool billboard)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        if (billboard)
        {
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mat.BillboardKeepScale = true;
        }
        return new QuadMesh { Size = size, Material = mat };
    }
}
