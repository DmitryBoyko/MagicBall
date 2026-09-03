using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Цветные искорки как FlowerBedSparks в TrueTaro: мелкие ADD-квадраты.
/// Слетают с обода шара по касательной вращения.
/// </summary>
public partial class BallSparks : Control
{
    private const int MaxSparks = 110;
    private const float SpawnPerSec = 26f;
    private const float FadeStart = 0.62f;

    private static readonly Color Gold = new(1f, 0.9f, 0.32f);
    private static readonly Color White = Colors.White;

    private readonly List<Spark> _sparks = [];
    private readonly RandomNumberGenerator _rng = new();
    private float _spawnAccum;
    private float _spinSign = 1f;
    private float _spinSpeed = 0.55f;
    private Color _colorA = new(0.05f, 0.92f, 0.85f);
    private Color _colorB = new(0.62f, 0.12f, 0.95f);
    private float _ballRadius = 100f;

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

    public override void _Process(double delta)
    {
        Tick(Mathf.Min((float)delta, 1f / 30f));
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var spark in _sparks)
        {
            var u = Mathf.Clamp(spark.Age / Mathf.Max(spark.Life, 0.01f), 0f, 1f);
            float alpha;
            if (u <= FadeStart)
                alpha = 0.95f;
            else
            {
                var fade = (u - FadeStart) / Mathf.Max(1f - FadeStart, 0.001f);
                alpha = 0.95f * (1f - fade) * (1f - fade);
            }

            if (alpha <= 0.03f)
                continue;
            var px = spark.Px;
            var col = spark.Col;
            col.A = alpha;
            DrawRect(new Rect2(spark.Pos - new Vector2(px, px) * 0.5f, new Vector2(px, px)), col, true);
        }
    }

    private void Tick(float dt)
    {
        for (var i = _sparks.Count - 1; i >= 0; i--)
        {
            var spark = _sparks[i];
            spark.Age += dt;
            if (spark.Age >= spark.Life)
            {
                _sparks.RemoveAt(i);
                continue;
            }

            spark.Vel *= 1f - 0.18f * dt;
            spark.Pos += spark.Vel * dt;
            _sparks[i] = spark;
        }

        if (Size.X < 16f || _ballRadius < 8f)
            return;

        var rate = SpawnPerSec * (0.65f + _spinSpeed);
        _spawnAccum += rate * dt;
        var toSpawn = Mathf.Min((int)_spawnAccum, 5);
        _spawnAccum -= toSpawn;
        for (var i = 0; i < toSpawn && _sparks.Count < MaxSparks; i++)
            Spawn();
    }

    private void Spawn()
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
