using System.Collections.Generic;
using Godot;
using Framework;

// All game audio is generated in code (no asset files): short procedural PCM clips for
// shooting / hits / deaths / blasts + a KO stinger. Positional 3D players use the battle
// camera as the listener, so sounds are quieter the farther the action is / the more the
// player zooms out — keeping the whole mix subtle. A per-clip cooldown stops the wall of
// hits from machine-gunning.
public partial class SfxManager : Node
{
    private const int Rate = 44100;
    private const int PoolSize = 16;

    private AudioStreamWav _shoot, _hit, _death, _blast, _ko;
    private AudioStreamPlayer3D[] _pool;
    private AudioStreamPlayer _ui;
    private int _next;

    private readonly System.Random _rng = new(1234);
    private readonly Dictionary<AudioStreamWav, ulong> _last = new();

    public override void _Ready()
    {
        GenerateClips();

        _pool = new AudioStreamPlayer3D[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var p = new AudioStreamPlayer3D
            {
                UnitSize = 18f,
                MaxDistance = 220f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                VolumeDb = -12f,
            };
            AddChild(p);
            _pool[i] = p;
        }

        _ui = new AudioStreamPlayer { VolumeDb = -14f };
        AddChild(_ui);

        // ASSIGN (never +=) so nothing accumulates across scene reloads.
        Sound.Shoot = pos => Play(_shoot, pos, -15f, 0.13f, 40);
        Sound.Hit = pos => Play(_hit, pos, -12f, 0.16f, 45);
        Sound.Death = pos => Play(_death, pos, -11f, 0.12f, 35);
        Sound.Blast = pos => Play(_blast, pos, -8f, 0.10f, 25);
        Sound.Ko = () => { _ui.Stream = _ko; _ui.Play(); };
    }

    public override void _ExitTree()
    {
        Sound.Shoot = null;
        Sound.Hit = null;
        Sound.Death = null;
        Sound.Blast = null;
        Sound.Ko = null;
    }

    private void Play(AudioStreamWav stream, Vector3 pos, float baseDb, float pitchVar, uint minMs)
    {
        ulong now = Time.GetTicksMsec();
        if (_last.TryGetValue(stream, out var last) && now - last < minMs) return;
        _last[stream] = now;

        var p = _pool[_next];
        _next = (_next + 1) % _pool.Length;
        p.Stream = stream;
        p.GlobalPosition = pos;
        p.VolumeDb = baseDb;
        p.PitchScale = 1f + ((float)_rng.NextDouble() * 2f - 1f) * pitchVar;
        p.Play();
    }

    // ---- Procedural clip synthesis ----
    private void GenerateClips()
    {
        // Arrow release: a bright noise whoosh sweeping down.
        _shoot = Build(0.09f, (t, p) =>
        {
            float f = Mathf.Lerp(1500f, 480f, p);
            float tone = Mathf.Sin(Mathf.Tau * f * t);
            float noise = Noise();
            return (noise * 0.55f + tone * 0.45f) * Mathf.Exp(-t * 38f) * 0.5f;
        });

        // Melee impact: low body + a short click.
        _hit = Build(0.08f, (t, p) =>
        {
            float tone = Mathf.Sin(Mathf.Tau * 180f * t);
            float click = Noise() * Mathf.Exp(-t * 120f);
            return (tone * 0.7f + click * 0.5f) * Mathf.Exp(-t * 34f) * 0.55f;
        });

        // Death: a descending thud.
        _death = Build(0.22f, (t, p) =>
        {
            float f = Mathf.Lerp(220f, 70f, p);
            float tone = Mathf.Sin(Mathf.Tau * f * t);
            return (tone + Noise() * 0.3f) * Mathf.Exp(-t * 9f) * 0.55f;
        });

        // Blast: a rumbly noise burst with a low sine under it.
        _blast = Build(0.30f, (t, p) =>
        {
            float rumble = Mathf.Sin(Mathf.Tau * 60f * t);
            return (Noise() * 0.7f + rumble * 0.5f) * Mathf.Exp(-t * 7.5f) * 0.6f;
        });

        // KO stinger: a stepped square-wave fanfare.
        _ko = Build(0.5f, (t, p) =>
        {
            float f = p < 0.25f ? 660f : p < 0.5f ? 550f : 440f;
            float sq = Mathf.Sin(Mathf.Tau * f * t) >= 0f ? 1f : -1f;
            return sq * Mathf.Exp(-t * 3.2f) * 0.4f;
        });
    }

    private float Noise() => (float)(_rng.NextDouble() * 2.0 - 1.0);

    // Render a mono 16-bit clip from a per-sample function value(t, progress).
    private static AudioStreamWav Build(float duration, System.Func<float, float, float> fn)
    {
        int count = Mathf.Max(1, (int)(duration * Rate));
        var data = new byte[count * 2];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Clamp(fn(t, t / duration), -1f, 1f);
            short s = (short)(v * 32767f);
            data[i * 2] = (byte)(s & 0xFF);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
        };
    }
}
