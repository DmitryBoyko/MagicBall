using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Pulsing sun and rays from TrueTaro ReadingSunSparks. Origin is the control center.
/// </summary>
public partial class SunSparks : Control
{
    private const int MaxParticles = 96;
    private const float EmitInterval = 0.032f;
    private const float RayChance = 0.55f;
    private const float LenMul = 3.4f;
    private const float LenMulUp = 6.5f;
    private const float BaseDiameter = 96f;
    private const float PulsePeriod = 2.4f;
    private const float HaloMulMin = 2.15f;
    private const float HaloMulMax = 2.75f;
    private const float OuterHaloMul = 3.35f;
    private const float DesignSize = 200f;

    private readonly List<Spark> _particles = [];
    private readonly RandomNumberGenerator _rng = new();
    private Texture2D _glow = null!;
    private Texture2D _streak = null!;
    private float _emitCd;
    private float _pulse;

    private struct Spark
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public float Len;
        public float Thick;
        public float Ang;
        public float Spin;
        public bool Hot;
        public bool Ray;
    }

    private Tween? _fade;
    private bool _windingDown;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        _rng.Randomize();
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _glow = MakeSoftDisc(64);
        _streak = MakeStreak(16, 64);
        _pulse = _rng.Randf() * PulsePeriod;
        Visible = false;
        SetProcess(false);
    }

    public void SetActive(bool on)
    {
        _fade?.Kill();
        _fade = null;
        _windingDown = false;
        if (!on)
        {
            HideNow();
            return;
        }

        Modulate = Colors.White;
        Visible = true;
        SetProcess(true);
        if (_particles.Count == 0)
        {
            for (var i = 0; i < 16; i++)
            {
                Spawn(true);
                Spawn(false);
            }
        }

        QueueRedraw();
    }

    public void FadeOut(float seconds, Action? finished = null)
    {
        _fade?.Kill();
        if (!Visible)
        {
            HideNow();
            finished?.Invoke();
            return;
        }

        _windingDown = true;
        var dur = Mathf.Max(0.2f, seconds);
        _fade = CreateTween();
        _fade.SetEase(Tween.EaseType.In);
        _fade.SetTrans(Tween.TransitionType.Cubic);
        _fade.TweenProperty(this, "modulate", new Color(1f, 1f, 1f, 0f), dur);
        _fade.Finished += () =>
        {
            HideNow();
            finished?.Invoke();
        };
    }

    private void HideNow()
    {
        _windingDown = false;
        _fade = null;
        Visible = false;
        SetProcess(false);
        _particles.Clear();
        Modulate = Colors.White;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Visible || Size.X < 8f)
            return;

        var dt = (float)delta;
        _pulse += dt;
        if (!_windingDown)
        {
            _emitCd -= dt;
            while (_emitCd <= 0f && _particles.Count < MaxParticles)
            {
                _emitCd += EmitInterval * _rng.RandfRange(0.7f, 1.35f);
                Spawn(_rng.Randf() < RayChance);
            }
        }

        UpdateParticles(dt);
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSun();
        if (_streak == null)
            return;

        var spark = new Color(1f, 0.82f, 0.28f);
        var hot = new Color(1f, 0.94f, 0.65f);
        var core = new Color(1f, 0.96f, 0.78f);
        foreach (var p in _particles)
        {
            var t = 1f - p.Life / Mathf.Max(p.MaxLife, 0.001f);
            float envelope;
            if (t < 0.12f)
                envelope = t / 0.12f;
            else if (t > 0.55f)
                envelope = 1f - (t - 0.55f) / 0.45f;
            else
                envelope = 1f;
            envelope = Mathf.Clamp(envelope, 0f, 1f);
            var a = envelope * (p.Ray ? 0.5f : 0.9f);
            var col = p.Hot ? hot : spark;
            col.A = a;
            var length = p.Len * (0.75f + 0.45f * envelope);
            var thick = p.Thick * (0.9f + 0.2f * (1f - t));
            var half = new Vector2(thick * 0.5f, length * 0.5f);
            DrawSetTransform(p.Pos, p.Ang + Mathf.Pi * 0.5f, Vector2.One);
            DrawTextureRect(_streak, new Rect2(-half, new Vector2(thick, length)), false, col);
            if (p.Ray)
            {
                var coreW = thick * 0.4f;
                var coreH = length * 0.65f;
                var coreCol = new Color(core.R, core.G, core.B, a * 0.85f);
                DrawTextureRect(
                    _streak,
                    new Rect2(new Vector2(-coreW * 0.5f, -coreH * 0.5f), new Vector2(coreW, coreH)),
                    false,
                    coreCol);
            }

            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }

    private Vector2 Origin => Size * 0.5f;

    private float Unit => Mathf.Max(Size.X, Size.Y) / DesignSize;

    private void Spawn(bool wideRay)
    {
        if (Size.X < 8f)
            return;

        var origin = Origin;
        var scale = Unit;
        var ang = _rng.Randf() * Mathf.Tau;
        if (_rng.Randf() < 0.62f)
            ang = -Mathf.Pi + _rng.Randf() * Mathf.Pi;
        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        var goingUp = dir.Y < -0.15f;
        var lenMul = goingUp ? LenMulUp : LenMul;
        float speed;
        float life;
        float length;
        float thickness;
        if (wideRay)
        {
            speed = _rng.RandfRange(55f, 130f) * scale;
            life = _rng.RandfRange(0.75f, 1.4f);
            length = _rng.RandfRange(28f, 58f) * lenMul * scale;
            thickness = _rng.RandfRange(2.8f, 6.2f) * scale;
        }
        else
        {
            speed = _rng.RandfRange(90f, 220f) * scale;
            life = _rng.RandfRange(0.3f, 0.75f);
            length = _rng.RandfRange(9f, 18f) * (goingUp ? lenMul * 0.35f : 1f) * scale;
            thickness = _rng.RandfRange(2f, 4.2f) * scale;
        }

        _particles.Add(new Spark
        {
            Pos = origin + dir * _rng.RandfRange(2f, 14f) * scale,
            Vel = dir * speed,
            Life = life,
            MaxLife = life,
            Len = length,
            Thick = thickness,
            Ang = dir.Angle(),
            Spin = _rng.RandfRange(-1.2f, 1.2f),
            Hot = _rng.Randf() < 0.35f,
            Ray = wideRay,
        });
    }

    private void UpdateParticles(float dt)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Vel *= 1f - (p.Ray ? 0.35f : 0.55f) * dt;
            p.Pos += p.Vel * dt;
            p.Ang += p.Spin * dt * 0.35f;
            _particles[i] = p;
        }
    }

    private void DrawSun()
    {
        if (_glow == null)
            return;

        var origin = Origin;
        var scale = Unit;
        var phase = _pulse / PulsePeriod * Mathf.Tau;
        var breathe = 0.5f + 0.5f * Mathf.Sin(phase);
        var breathe2 = 0.5f + 0.5f * Mathf.Sin(phase * 0.57f + 1.1f);
        var diam = BaseDiameter * scale * Mathf.Lerp(0.92f, 1.18f, breathe);
        var outerDiam = diam * Mathf.Lerp(OuterHaloMul * 0.92f, OuterHaloMul, breathe2);
        var haloDiam = diam * Mathf.Lerp(HaloMulMin, HaloMulMax, breathe2);
        DrawGlow(origin, outerDiam, new Color(1f, 0.62f, 0.12f, Mathf.Lerp(0.10f, 0.22f, breathe)));
        DrawGlow(origin, haloDiam, new Color(1f, 0.72f, 0.18f, Mathf.Lerp(0.22f, 0.42f, breathe)));
        DrawGlow(origin, diam, new Color(1f, 0.86f, 0.35f, Mathf.Lerp(0.40f, 0.68f, breathe)));
        DrawGlow(origin, diam * 0.58f, new Color(1f, 0.96f, 0.78f, Mathf.Lerp(0.55f, 0.82f, breathe2)));
    }

    private void DrawGlow(Vector2 origin, float diam, Color color)
    {
        var half = diam * 0.5f;
        DrawTextureRect(_glow, new Rect2(origin - new Vector2(half, half), new Vector2(diam, diam)), false, color);
    }

    private static Texture2D MakeSoftDisc(int side)
    {
        var img = Image.CreateEmpty(side, side, false, Image.Format.Rgba8);
        var c = new Vector2(side * 0.5f, side * 0.5f);
        var rMax = side * 0.5f;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var d = new Vector2(x + 0.5f, y + 0.5f).DistanceTo(c) / rMax;
                float a;
                if (d < 0.35f)
                    a = 1f;
                else if (d < 1f)
                    a = Mathf.Pow(1f - (d - 0.35f) / 0.65f, 2.2f);
                else
                    a = 0f;
                img.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    private static Texture2D MakeStreak(int width, int height)
    {
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var cx = width * 0.5f;
        for (var y = 0; y < height; y++)
        {
            var v = y / (float)Mathf.Max(height - 1, 1);
            var along = Mathf.Pow(1f - Mathf.Abs(v * 2f - 1f), 1.6f);
            for (var x = 0; x < width; x++)
            {
                var u = Mathf.Abs((x + 0.5f - cx) / cx);
                var across = Mathf.Pow(1f - Mathf.Clamp(u, 0f, 1f), 2.2f);
                img.SetPixel(x, y, new Color(1f, 1f, 1f, along * across));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
