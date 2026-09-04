using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Tap-ripples как в epkids WaterRipples: до 8 источников → ripple0..7 в шейдере шара.
/// </summary>
public sealed class BallRipples
{
    public const int MaxRipples = 8;
    public const float RippleLife = 4.8f;

    private static readonly string[] UniformNames =
    [
        "ripple0", "ripple1", "ripple2", "ripple3",
        "ripple4", "ripple5", "ripple6", "ripple7",
    ];

    private readonly List<Vector4> _ripples = [];
    private float _time;
    private readonly RandomNumberGenerator _rng = new();

    public BallRipples()
    {
        _rng.Randomize();
    }

    public float Time => _time;

    public void Reset()
    {
        _ripples.Clear();
        _time = 0f;
    }

    public void Tick(double delta, ShaderMaterial? material)
    {
        _time += (float)delta;
        Prune();
        Apply(material);
    }

    /// <param name="uv">UV шара 0..1; только внутри круга.</param>
    public bool TryAddAtUv(Vector2 uv)
    {
        var centered = (uv - new Vector2(0.5f, 0.5f)) * 2f;
        if (centered.Length() > 0.96f)
            return false;

        var seed = _rng.RandfRange(0.12f, 0.98f);
        _ripples.Add(new Vector4(uv.X, uv.Y, _time, seed));
        while (_ripples.Count > MaxRipples)
            _ripples.RemoveAt(0);
        return true;
    }

    private void Prune()
    {
        for (var i = _ripples.Count - 1; i >= 0; i--)
        {
            if (_time - _ripples[i].Z >= RippleLife)
                _ripples.RemoveAt(i);
        }
    }

    private void Apply(ShaderMaterial? material)
    {
        if (material == null)
            return;

        material.SetShaderParameter("ripple_time", _time);
        for (var i = 0; i < MaxRipples; i++)
        {
            var v = i < _ripples.Count ? _ripples[i] : Vector4.Zero;
            material.SetShaderParameter(UniformNames[i], v);
        }
    }
}
