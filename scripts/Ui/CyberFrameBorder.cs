using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Animated neon rainbow frame. Port of MagicCrystalClash cyber_frame_border.gd.
/// Center is transparent — overlays panels and the main screen.
/// </summary>
public partial class CyberFrameBorder : ColorRect
{
    public const string ShaderPath = "res://shaders/cyber_screen_frame.gdshader";
    public const string NodeName = "CyberFrame";

    private const float BorderWidthPx = 3.5f;
    private const float GlowWidthPx = 7f;
    private const float HueSpeed = 0.14f;
    private const float ContentGapPx = 10f;
    private const float CornerRadiusRatio = 0.041f;
    private const float CornerRadiusMinPx = 16f;
    private const float CornerRadiusMaxPx = 48f;
    private const float ContentCurveInsetRatio = 0.04f;

    public event Action<int>? FrameInsetChanged;

    private float _hueSeed = -1f;
    private bool _initializedSize;

    public float CornerRadiusPx { get; private set; } = 30f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Color = new Color(0f, 0f, 0f, 0f);
        SetupMaterial();
        ApplyMotionSetting();
        Resized += OnResized;
        OnResized();
        CallDeferred(MethodName.OnResized);
        SetProcess(true);
    }

    public int GetFrameInsetPx() => InsetFromCornerRadius(CornerRadiusForSize(Size));

    public void ApplyMotionSetting()
    {
        if (Material is ShaderMaterial mat)
            mat.SetShaderParameter("motion_mul", 1f);
    }

    public void SetHueSeed(float hue)
    {
        _hueSeed = Mathf.Clamp(hue, 0f, 1f);
        if (Material is ShaderMaterial mat)
            mat.SetShaderParameter("hue_offset", _hueSeed);
    }

    public static CyberFrameBorder AttachTo(Control host)
    {
        var existing = host.GetNodeOrNull<CyberFrameBorder>(NodeName);
        if (existing != null)
        {
            BringToFront(host, existing);
            return existing;
        }

        var frame = new CyberFrameBorder { Name = NodeName };
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        host.AddChild(frame);
        BringToFront(host, frame);
        return frame;
    }

    public static void SetupModal(PanelContainer panel)
    {
        if (panel == null)
            return;
        panel.AddThemeStyleboxOverride("panel", UiTheme.ModalShell());
        var frame = AttachTo(panel);
        frame.FrameInsetChanged += _ => SyncModalCorners(panel, frame);
        SyncModalCorners(panel, frame);
    }

    private static void BringToFront(Control host, CyberFrameBorder frame)
    {
        frame.ZAsRelative = true;
        frame.ZIndex = 20;
        frame.MouseFilter = MouseFilterEnum.Ignore;
        host.MoveChild(frame, host.GetChildCount() - 1);
        frame.ApplyMotionSetting();
    }

    private static void SyncModalCorners(PanelContainer panel, CyberFrameBorder frame)
    {
        var radius = Mathf.RoundToInt(Mathf.Max(12f, frame.CornerRadiusPx));
        panel.AddThemeStyleboxOverride("panel", UiTheme.ModalShell(radius));
    }

    private void OnResized()
    {
        UpdateShaderPixelSize();
        UpdateCornerRadius();
        FrameInsetChanged?.Invoke(GetFrameInsetPx());
        if (Size.X >= 1f && Size.Y >= 1f)
            _initializedSize = true;
    }

    public override void _Process(double delta)
    {
        if (_initializedSize)
        {
            SetProcess(false);
            return;
        }

        if (Size.X >= 1f && Size.Y >= 1f)
            OnResized();
    }

    private static int InsetFromCornerRadius(float cornerRadius)
    {
        var curveExtra = cornerRadius * ContentCurveInsetRatio;
        return Mathf.CeilToInt(BorderWidthPx + GlowWidthPx + ContentGapPx + curveExtra);
    }

    private static float CornerRadiusForSize(Vector2 pxSize)
    {
        if (pxSize.X < 1f || pxSize.Y < 1f)
            pxSize = new Vector2(720f, 1600f);
        return Mathf.Clamp(
            Mathf.Min(pxSize.X, pxSize.Y) * CornerRadiusRatio,
            CornerRadiusMinPx,
            CornerRadiusMaxPx);
    }

    private void UpdateShaderPixelSize()
    {
        if (Material is not ShaderMaterial mat || Size.X < 1f || Size.Y < 1f)
            return;
        mat.SetShaderParameter("control_pixel_size", Size);
    }

    private void UpdateCornerRadius()
    {
        if (Material is not ShaderMaterial mat)
            return;
        CornerRadiusPx = CornerRadiusForSize(Size);
        mat.SetShaderParameter("corner_radius_px", CornerRadiusPx);
    }

    private void SetupMaterial()
    {
        var shader = LoadShader();
        if (shader == null)
        {
            GD.PushError($"[CyberFrame] Не загрузился {ShaderPath}");
            return;
        }

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("border_width_px", BorderWidthPx);
        mat.SetShaderParameter("glow_width_px", GlowWidthPx);
        mat.SetShaderParameter("hue_speed", HueSpeed);
        mat.SetShaderParameter("corner_radius_px", CornerRadiusForSize(new Vector2(720f, 1600f)));
        if (_hueSeed < 0f)
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();
            _hueSeed = rng.Randf();
        }

        mat.SetShaderParameter("hue_offset", _hueSeed);
        Material = mat;
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
