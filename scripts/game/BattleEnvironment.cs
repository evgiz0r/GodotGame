using Godot;

// Builds a minimal-but-alive battlefield in code: a colored ground plane, a procedural
// sky, fog, an angled sun, and a scatter of simple props — all themed per EnvironmentKind
// with no external assets. Returns a single Node3D the caller adds/removes as one unit.
public static class BattleEnvironment
{
    private readonly struct Palette
    {
        public readonly Color Ground, Fog, Sun, SkyTop, SkyHorizon;
        public Palette(Color ground, Color fog, Color sun, Color skyTop, Color skyHorizon)
        { Ground = ground; Fog = fog; Sun = sun; SkyTop = skyTop; SkyHorizon = skyHorizon; }
    }

    private static Palette For(EnvironmentKind kind) => kind switch
    {
        EnvironmentKind.Forest => new(new Color(0.18f, 0.33f, 0.16f), new Color(0.6f, 0.72f, 0.6f),
            new Color(1f, 0.95f, 0.82f), new Color(0.3f, 0.5f, 0.8f), new Color(0.7f, 0.82f, 0.9f)),
        EnvironmentKind.Desert => new(new Color(0.78f, 0.67f, 0.42f), new Color(0.85f, 0.78f, 0.6f),
            new Color(1f, 0.93f, 0.75f), new Color(0.45f, 0.6f, 0.85f), new Color(0.9f, 0.82f, 0.62f)),
        EnvironmentKind.Snow => new(new Color(0.82f, 0.87f, 0.93f), new Color(0.85f, 0.9f, 0.96f),
            new Color(0.9f, 0.94f, 1f), new Color(0.55f, 0.68f, 0.85f), new Color(0.85f, 0.9f, 0.96f)),
        EnvironmentKind.Arena => new(new Color(0.5f, 0.46f, 0.38f), new Color(0.7f, 0.66f, 0.58f),
            new Color(1f, 0.96f, 0.85f), new Color(0.4f, 0.55f, 0.8f), new Color(0.78f, 0.75f, 0.68f)),
        _ => new(new Color(0.4f, 0.4f, 0.42f), new Color(0.62f, 0.6f, 0.6f),
            new Color(0.95f, 0.9f, 0.82f), new Color(0.35f, 0.4f, 0.55f), new Color(0.65f, 0.62f, 0.6f)),
    };

    public static Node3D Build(EnvironmentKind kind)
    {
        var p = For(kind);
        var root = new Node3D { Name = "Environment" };

        var ground = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(200f, 150f) } };
        ((PlaneMesh)ground.Mesh).Material = GroundMaterial(p.Ground);
        root.AddChild(ground);

        var sun = new DirectionalLight3D
        {
            LightColor = p.Sun,
            LightEnergy = 1.15f,
            ShadowEnabled = true,
            RotationDegrees = new Vector3(-48f, -55f, 0f),
        };
        root.AddChild(sun);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky
            {
                SkyMaterial = new ProceduralSkyMaterial
                {
                    SkyTopColor = p.SkyTop,
                    SkyHorizonColor = p.SkyHorizon,
                    GroundHorizonColor = p.SkyHorizon,
                    GroundBottomColor = p.Ground,
                },
            },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 0.7f,
            FogEnabled = true,
            FogLightColor = p.Fog,
            FogDensity = 0.006f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        root.AddChild(new WorldEnvironment { Environment = env });

        AddProps(root, kind, p.Ground);
        return root;
    }

    private static void AddProps(Node3D root, EnvironmentKind kind, Color ground)
    {
        switch (kind)
        {
            case EnvironmentKind.Forest:
                Scatter(root, 26, () => Tree(new Color(0.15f, 0.34f, 0.16f)));
                Scatter(root, 8, () => Rock(new Color(0.4f, 0.4f, 0.4f)));
                Scatter(root, 5, () => Hill(Darken(ground, 0.9f)), boundX: 92f, boundZ: 60f, clearX: 46f, clearZ: 30f);
                break;
            case EnvironmentKind.Desert:
                Scatter(root, 14, () => Rock(new Color(0.7f, 0.6f, 0.4f)));
                Scatter(root, 6, () => Hill(Lighten(ground, 1.04f)), boundX: 92f, boundZ: 60f, clearX: 46f, clearZ: 30f);
                break;
            case EnvironmentKind.Snow:
                Scatter(root, 12, () => Tree(new Color(0.8f, 0.85f, 0.9f)));
                Scatter(root, 8, () => Rock(new Color(0.8f, 0.83f, 0.88f)));
                Scatter(root, 5, () => Hill(Lighten(ground, 1.03f)), boundX: 92f, boundZ: 60f, clearX: 46f, clearZ: 30f);
                break;
            case EnvironmentKind.Arena:
                Scatter(root, 16, () => Pillar(new Color(0.55f, 0.52f, 0.46f), 3f, 5f));
                break;
            case EnvironmentKind.Ruins:
                Scatter(root, 16, () => Pillar(new Color(0.45f, 0.45f, 0.47f), 1.5f, 6f));
                Scatter(root, 6, () => Rock(new Color(0.42f, 0.42f, 0.44f)));
                Scatter(root, 3, () => Hill(Darken(ground, 0.92f)), boundX: 92f, boundZ: 60f, clearX: 46f, clearZ: 30f);
                break;
        }
    }

    private static void Scatter(Node3D root, int count, System.Func<Node3D> make,
        float boundX = 80f, float boundZ = 55f, float clearX = 22f, float clearZ = 15f)
    {
        for (int i = 0; i < count; i++)
        {
            var m = make();
            float x, z;
            do { x = (GD.Randf() * 2f - 1f) * boundX; z = (GD.Randf() * 2f - 1f) * boundZ; }
            while (Mathf.Abs(x) < clearX && Mathf.Abs(z) < clearZ); // keep the battle area clear
            m.Position += new Vector3(x, 0f, z);
            m.RotateY(GD.Randf() * Mathf.Tau);
            root.AddChild(m);
        }
    }

    private static Node3D Tree(Color foliage)
    {
        var root = new Node3D { Scale = Vector3.One * (0.8f + GD.Randf() * 0.7f) };
        var bark = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.22f, 0.14f), Roughness = 1f };
        var leaf = new StandardMaterial3D { AlbedoColor = foliage, Roughness = 1f };

        var trunk = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.34f, Height = 1.6f },
            Position = new Vector3(0f, 0.8f, 0f),
        };
        ((CylinderMesh)trunk.Mesh).Material = bark;
        root.AddChild(trunk);

        var low = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 1.5f, Height = 2.4f },
            Position = new Vector3(0f, 2.5f, 0f),
        };
        ((CylinderMesh)low.Mesh).Material = leaf;
        root.AddChild(low);

        var high = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0f, BottomRadius = 1.0f, Height = 1.8f },
            Position = new Vector3(0f, 3.9f, 0f),
        };
        ((CylinderMesh)high.Mesh).Material = leaf;
        root.AddChild(high);
        return root;
    }

    // A big half-buried dome = a soft rolling hill for backdrop relief (kept off the field).
    private static Node3D Hill(Color c)
    {
        var root = new Node3D();
        float r = 7f + GD.Randf() * 10f;
        var dome = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = r, Height = r * 2f },
            Position = new Vector3(0f, -r * 0.72f, 0f),
        };
        ((SphereMesh)dome.Mesh).Material = new StandardMaterial3D { AlbedoColor = c, Roughness = 1f };
        root.AddChild(dome);
        return root;
    }

    private static MeshInstance3D Rock(Color c)
    {
        float s = 0.8f + GD.Randf() * 1.4f;
        var m = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(s, s * 0.7f, s) },
            Position = new Vector3(0f, s * 0.35f, 0f),
        };
        ((BoxMesh)m.Mesh).Material = new StandardMaterial3D { AlbedoColor = c, Roughness = 1f };
        return m;
    }

    private static MeshInstance3D Pillar(Color c, float minH, float maxH)
    {
        float h = minH + GD.Randf() * (maxH - minH);
        var m = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.7f, Height = h },
            Position = new Vector3(0f, h * 0.5f, 0f),
        };
        ((CylinderMesh)m.Mesh).Material = new StandardMaterial3D { AlbedoColor = c, Roughness = 1f };
        return m;
    }

    // Ground albedo driven by tiled Perlin noise between a dark and light shade of the base
    // color -> organic patchiness instead of a flat slab, reading as real terrain.
    private static StandardMaterial3D GroundMaterial(Color baseCol)
    {
        var noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = 0.9f,
            FractalOctaves = 4,
            FractalLacunarity = 2.1f,
        };
        var grad = new Gradient();
        grad.SetColor(0, Darken(baseCol, 0.78f));
        grad.SetColor(1, Lighten(baseCol, 1.14f));
        var tex = new NoiseTexture2D
        {
            Width = 512,
            Height = 512,
            Seamless = true,
            ColorRamp = grad,
            Noise = noise,
        };
        return new StandardMaterial3D
        {
            AlbedoTexture = tex,
            Roughness = 1f,
            Uv1Scale = new Vector3(26f, 20f, 1f),
        };
    }

    private static Color Darken(Color c, float f) => new(c.R * f, c.G * f, c.B * f, c.A);

    private static Color Lighten(Color c, float f) =>
        new(Mathf.Min(1f, c.R * f), Mathf.Min(1f, c.G * f), Mathf.Min(1f, c.B * f), c.A);
}
