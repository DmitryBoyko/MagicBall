using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// SUR photo warp from JellyShiftTetris, pinned to a single emitter behind the ball.
/// </summary>
public partial class BackgroundWarp : Node
{
    public const string ShaderPath = "res://shaders/sur_bg_breath.gdshader";

    private const float WarpStrength = 0.016f;
    private const float SwirlStrength = 0.5f;
    private const float HighlightScale = 0.32f;
    private const float RadiusPad = 1.22f;
    private const float PhaseSpeed = 1.55f;
    private const float BreathSpeed = 0.42f;

    private TextureRect? _background;
    private Control? _ball;
    private ShaderMaterial? _mat;
    private bool _enabled;
    private float _phase;

    public void Bind(TextureRect background, Control ball)
    {
        _background = background;
        _ball = ball;
    }

    public void SetEnabled(bool on)
    {
        _enabled = on;
        SetProcess(on);
        if (_background == null)
            return;
        if (!on)
        {
            _background.Material = null;
            return;
        }

        EnsureMaterial();
        _background.Material = _mat;
        PushToShader();
    }

    public override void _Process(double delta)
    {
        if (!_enabled || _mat == null)
            return;
        _phase += PhaseSpeed * (float)delta;
        PushToShader();
    }

    private void EnsureMaterial()
    {
        if (_mat != null)
            return;
        var shader = LoadShader();
        if (shader == null)
        {
            GD.PushError($"[BackgroundWarp] Не загрузился {ShaderPath}");
            return;
        }

        _mat = new ShaderMaterial { Shader = shader };
        _mat.SetShaderParameter("glow_color", UiTheme.Magenta);
        _mat.SetShaderParameter("highlight_scale", HighlightScale);
        _mat.SetShaderParameter("warp_strength", WarpStrength);
        _mat.SetShaderParameter("swirl_strength", SwirlStrength);
        _mat.SetShaderParameter("blob_amp", new Vector4(0f, 0f, 0f, 0f));
        _mat.SetShaderParameter("blob_b", Vector2.Zero);
        _mat.SetShaderParameter("blob_c", Vector2.Zero);
        _mat.SetShaderParameter("blob_d", Vector2.Zero);
        _mat.SetShaderParameter("blob_radius", new Vector4(0.14f, 0f, 0f, 0f));
        _mat.SetShaderParameter("blob_phase", Vector4.Zero);
    }

    private void PushToShader()
    {
        if (_mat == null || _background == null || _ball == null)
            return;

        var bgSize = _background.Size;
        if (bgSize.X < 1f || bgSize.Y < 1f)
            bgSize = _background.GetViewportRect().Size;
        if (bgSize.X < 1f || bgSize.Y < 1f)
            return;

        var ballCenter = _ball.Position + _ball.Size * 0.5f;
        var uv = new Vector2(ballCenter.X / bgSize.X, ballCenter.Y / bgSize.Y);
        var radiusPx = _ball.Size.X * 0.5f * RadiusPad;
        var radiusUv = radiusPx / bgSize.Y;
        var aspect = bgSize.X / bgSize.Y;
        var env = 0.62f + 0.38f * (0.5f + 0.5f * Mathf.Sin(_phase * BreathSpeed));

        _mat.SetShaderParameter("aspect", aspect);
        _mat.SetShaderParameter("blob_a", uv);
        _mat.SetShaderParameter("blob_amp", new Vector4(env, 0f, 0f, 0f));
        _mat.SetShaderParameter("blob_radius", new Vector4(radiusUv, 0f, 0f, 0f));
        _mat.SetShaderParameter("blob_phase", new Vector4(_phase, 0f, 0f, 0f));
    }

    private static Shader? LoadShader()
    {
        if (!FileAccess.FileExists(ShaderPath))
            return GD.Load<Shader>(ShaderPath);

        using var file = FileAccess.Open(ShaderPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return GD.Load<Shader>(ShaderPath);

        var shader = new Shader { Code = file.GetAsText() };
        return string.IsNullOrWhiteSpace(shader.Code) ? null : shader;
    }
}
