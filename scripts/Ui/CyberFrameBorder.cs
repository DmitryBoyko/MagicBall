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

    public const string ContentMarginName = "ContentMargin";
    public const int ModalContentPadMin = 32;

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

        var pad = EnsureContentMargin(panel);
        var frame = AttachTo(panel);
        ApplyContentInset(pad, frame);
        frame.FrameInsetChanged += _ => ApplyContentInset(pad, frame);
        frame.CallDeferred(MethodName.ReapplySiblingPad);
    }

    public static MarginContainer CreateContentPad()
    {
        var pad = new MarginContainer
        {
            Name = ContentMarginName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        ApplyPad(pad, ModalContentPadMin);
        return pad;
    }

    private void ReapplySiblingPad()
    {
        if (GetParent() is not PanelContainer panel)
            return;
        var pad = panel.GetNodeOrNull<MarginContainer>(ContentMarginName);
        if (pad == null)
            return;
        ApplyContentInset(pad, this);
    }

    private static void ApplyContentInset(MarginContainer pad, CyberFrameBorder frame)
    {
        if (pad.GetParent() is PanelContainer panel)
            SyncModalCorners(panel, frame);
        var m = Mathf.Max(frame.GetFrameInsetPx() + 8, ModalContentPadMin);
        ApplyPad(pad, m);
    }

    private static void ApplyPad(MarginContainer pad, int px)
    {
        pad.AddThemeConstantOverride("margin_left", px);
        pad.AddThemeConstantOverride("margin_top", px);
        pad.AddThemeConstantOverride("margin_right", px);
        pad.AddThemeConstantOverride("margin_bottom", px);
    }

    private static MarginContainer EnsureContentMargin(PanelContainer panel)
    {
        var pad = panel.GetNodeOrNull<MarginContainer>(ContentMarginName);
        if (pad != null)
            return pad;

        pad = new MarginContainer
        {
            Name = ContentMarginName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        var moved = new List<Node>();
        foreach (var child in panel.GetChildren())
        {
            if (child is CyberFrameBorder)
                continue;
            moved.Add(child);
        }

        panel.AddChild(pad);
        foreach (var child in moved)
        {
            panel.RemoveChild(child);
            if (child is Control control)
            {
                control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                control.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            }

            pad.AddChild(child);
        }

        return pad;
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
