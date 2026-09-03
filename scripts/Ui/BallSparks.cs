using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Цветные искорки как FlowerBedSparks в TrueTaro: мелкие ADD-квадраты.
/// Слетают с обода шара по касательной вращения.
/// После «Спросить» поток в стороны удваивается, на стекле вспыхивают короткие искры.
/// </summary>
public partial class BallSparks : Control
{
    private const int MaxSparks = 110;
    private const int MaxSurface = 56;
    private const float SpawnPerSec = 26f;
    private const float SurfacePerSec = 28f;
    private const float FadeStart = 0.62f;

    private static readonly Color Gold = new(1f, 0.9f, 0.32f);
    private static readonly Color White = Colors.White;

    private readonly List<Spark> _sparks = [];
    private readonly List<Spark> _surface = [];
    private readonly RandomNumberGenerator _rng = new();
    private float _spawnAccum;
    private float _surfaceAccum;
    private float _spinSign = 1f;
    private float _spinSpeed = 0.55f;
    private Color _colorA = new(0.05f, 0.92f, 0.85f);
    private Color _colorB = new(0.62f, 0.12f, 0.95f);
    private float _ballRadius = 100f;
    private bool _asking;

    private struct Spark
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Age;
        public float Life;
        public float Px;
        public Color Col;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = false;
        ZIndex = 1;
        _rng.Randomize();
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        SetProcess(true);
    }

    public void Configure(float spinSign, float spinSpeed, Color colorA, Color colorB)
    {
        _spinSign = spinSign >= 0f ? 1f : -1f;
        _spinSpeed = Mathf.Clamp(spinSpeed, 0.2f, 1.4f);
        _colorA = colorA;
        _colorB = colorB;
    }

    public void SetBallRadius(float radius)
    {
        _ballRadius = Mathf.Max(8f, radius);
    }

    public void SetAsking(bool on)
    {
        if (_asking == on)
            return;
        _asking = on;
        if (!on)
            return;

        var flyCap = FlyCap;
        for (var i = 0; i < 18 && _sparks.Count < flyCap; i++)
            SpawnFly();
        for (var i = 0; i < 16 && _surface.Count < MaxSurface; i++)
            SpawnSurface();
    }

    private int FlyCap => _asking ? MaxSparks * 2 : MaxSparks;

    public override void _Process(double delta)
    {
        Tick(Mathf.Min((float)delta, 1f / 30f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawList(_sparks, fadeIn: false);
        DrawList(_surface, fadeIn: true);
    }

    private void DrawList(List<Spark> list, bool fadeIn)
    {
        foreach (var spark in list)
        {
            var u = Mathf.Clamp(spark.Age / Mathf.Max(spark.Life, 0.01f), 0f, 1f);
            var alpha = fadeIn ? TwinkleAlpha(u) : FlyAlpha(u);
            if (alpha <= 0.03f)
                continue;
            var px = spark.Px;
            var col = spark.Col;
            col.A = alpha;
            var half = new Vector2(px, px) * 0.5f;
            DrawRect(new Rect2(spark.Pos - half, new Vector2(px, px)), col, true);
            if (!fadeIn)
                continue;
            var core = px * 0.42f;
            var coreCol = White;
            coreCol.A = alpha;
            DrawRect(new Rect2(spark.Pos - new Vector2(core, core) * 0.5f, new Vector2(core, core)), coreCol, true);
        }
    }

    private static float FlyAlpha(float u)
    {
        if (u <= FadeStart)
            return 0.95f;
        var fade = (u - FadeStart) / Mathf.Max(1f - FadeStart, 0.001f);
        return 0.95f * (1f - fade) * (1f - fade);
    }

    private static float TwinkleAlpha(float u)
    {
        float envelope;
        if (u < 0.18f)
            envelope = u / 0.18f;
        else if (u > 0.48f)
            envelope = 1f - (u - 0.48f) / 0.52f;
        else
            envelope = 1f;
        return 0.92f * Mathf.Clamp(envelope, 0f, 1f);
    }

    private void Tick(float dt)
    {
        Step(_sparks, dt, drag: 0.18f);
        Step(_surface, dt, drag: 0.55f);

        if (Size.X < 16f || _ballRadius < 8f)
            return;

        var intensity = _asking ? 2f : 1f;
        var rate = SpawnPerSec * intensity * (0.65f + _spinSpeed);
        _spawnAccum += rate * dt;
        var burst = _asking ? 10 : 5;
        var toSpawn = Mathf.Min((int)_spawnAccum, burst);
        _spawnAccum -= toSpawn;
        var flyCap = FlyCap;
        for (var i = 0; i < toSpawn && _sparks.Count < flyCap; i++)
            SpawnFly();

        if (!_asking)
            return;

        _surfaceAccum += SurfacePerSec * dt;
        var toTwinkle = Mathf.Min((int)_surfaceAccum, 8);
        _surfaceAccum -= toTwinkle;
        for (var i = 0; i < toTwinkle && _surface.Count < MaxSurface; i++)
            SpawnSurface();
    }

    private static void Step(List<Spark> list, float dt, float drag)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var spark = list[i];
            spark.Age += dt;
            if (spark.Age >= spark.Life)
            {
                list.RemoveAt(i);
                continue;
            }

            spark.Vel *= 1f - drag * dt;
            spark.Pos += spark.Vel * dt;
            list[i] = spark;
        }
    }

    private void SpawnFly()
    {
        var center = Size * 0.5f;
        var ang = _rng.Randf() * Mathf.Tau;
        var rim = _ballRadius * _rng.RandfRange(0.93f, 1.02f);
        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        var tangent = new Vector2(-dir.Y, dir.X) * _spinSign;
        var speed = _rng.RandfRange(38f, 92f) * (0.7f + _spinSpeed);
        var vel = tangent * speed + dir * _rng.RandfRange(6f, 22f);
        vel += new Vector2(_rng.RandfRange(-8f, 8f), _rng.RandfRange(-10f, 4f));

        _sparks.Add(new Spark
        {
            Pos = center + dir * rim,
            Vel = vel,
            Age = 0f,
            Life = _rng.RandfRange(0.55f, 1.45f),
            Px = _rng.RandfRange(2.0f, 4.8f),
            Col = PickColor(),
        });
    }

    private void SpawnSurface()
    {
        var center = Size * 0.5f;
        var ang = _rng.Randf() * Mathf.Tau;
        var rim = _ballRadius * 0.90f * Mathf.Sqrt(_rng.Randf());
        var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        _surface.Add(new Spark
        {
            Pos = center + dir * rim,
            Vel = Vector2.Zero,
            Age = 0f,
            Life = _rng.RandfRange(0.28f, 0.78f),
            Px = _rng.RandfRange(2.4f, 6.2f),
            Col = PickColor(),
        });
    }

    private Color PickColor()
    {
        var roll = _rng.Randf();
        if (roll < 0.22f)
            return Gold.Lerp(White, _rng.RandfRange(0.25f, 0.7f));
        if (roll < 0.42f)
            return _colorA.Lightened(_rng.RandfRange(0.05f, 0.25f));
        if (roll < 0.62f)
            return _colorB.Lightened(_rng.RandfRange(0.0f, 0.2f));
        if (roll < 0.78f)
            return _colorA.Lerp(_colorB, _rng.Randf());
        if (roll < 0.90f)
            return new Color(1f, 0.35f, 0.75f).Lerp(Gold, _rng.RandfRange(0.1f, 0.5f));
        return White;
    }
}
