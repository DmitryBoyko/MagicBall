using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Ритуальный лог сборки промпта в полосе модалки ответа: построчный fade-in и мягкий скролл вверх.
/// </summary>
public partial class CastingLogSheet : CanvasLayer
{
    private const float PanelBgAlpha = 0.78f;
    private const float LineFadeIn = 0.35f;
    private const float TopFadeOut = 0.55f;
    private const int MaxVisibleLines = 8;

    private PanelContainer _panel = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _lines = null!;
    private readonly List<Label> _labels = [];

    public override void _Ready()
    {
        Layer = 27;
        Visible = false;

        var host = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);

        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        host.AddChild(_panel);

        var pad = CyberFrameBorder.CreateContentPad();
        _panel.AddChild(pad);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        pad.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Шар внимает…", UiTheme.FontReadingTitle, UiTheme.Gold));

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddChild(_scroll);

        _lines = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _lines.AddThemeConstantOverride("separation", 10);
        _scroll.AddChild(_lines);

        CyberFrameBorder.SetupModal(_panel, PanelBgAlpha);
    }

    public void LayoutBand(Rect2 band)
    {
        if (_panel == null || band.Size.X < 8f || band.Size.Y < 8f)
            return;
        _panel.Position = band.Position;
        _panel.Size = band.Size;
        _panel.CustomMinimumSize = band.Size;
    }

    public void Begin()
    {
        ClearLines();
        Visible = true;
    }

    public void Dismiss()
    {
        Visible = false;
        ClearLines();
    }

    public void AppendLine(string phrase)
    {
        if (!Visible || string.IsNullOrWhiteSpace(phrase))
            return;

        var label = UiTheme.MakeLabel(phrase, UiTheme.FontReadingBody - 4, UiTheme.Cream);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.Modulate = new Color(1f, 1f, 1f, 0f);
        _lines.AddChild(label);
        _labels.Add(label);

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(label, "modulate:a", 1f, LineFadeIn);

        TrimTopIfNeeded();
        CallDeferred(MethodName.ScrollToBottom);
    }

    private void TrimTopIfNeeded()
    {
        while (_labels.Count > MaxVisibleLines)
        {
            var oldest = _labels[0];
            _labels.RemoveAt(0);
            FadeAndFree(oldest);
        }
    }

    private void FadeAndFree(Label label)
    {
        if (!IsInstanceValid(label))
            return;
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.In);
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(label, "modulate:a", 0f, TopFadeOut);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(label))
                label.QueueFree();
        }));
    }

    private void ScrollToBottom()
    {
        if (_scroll == null || _lines == null)
            return;
        _scroll.ScrollVertical = (int)Mathf.Max(0f, _lines.Size.Y - _scroll.Size.Y + 24f);
    }

    private void ClearLines()
    {
        foreach (var label in _labels)
        {
            if (IsInstanceValid(label))
                label.QueueFree();
        }

        _labels.Clear();
        if (_lines != null)
        {
            foreach (var child in _lines.GetChildren())
            {
                if (child is Node node)
                    node.QueueFree();
            }
        }
    }
}
