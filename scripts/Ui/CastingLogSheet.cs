using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Ритуальный лог сборки промпта: построчный fade-in без модалки и рамки.
/// Лёгкое затемнение под текстом; вихрь рисуется слоем выше.
/// </summary>
public partial class CastingLogSheet : CanvasLayer
{
    private const float DimAlpha = 0.32f;
    private const float LineFadeIn = 0.35f;
    private const float TopFadeOut = 0.55f;
    private const int MaxVisibleLines = 8;
    private const int TextPadPx = 20;

    /// <summary>Выше вихря — иначе 2600 частиц полностью закрывают фразы.</summary>
    public const int CanvasLayerIndex = 28;

    private Control _band = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _lines = null!;
    private readonly List<Label> _labels = [];

    public override void _Ready()
    {
        Layer = CanvasLayerIndex;
        Visible = false;

        var host = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);

        var dim = new ColorRect
        {
            Color = new Color(UiTheme.Ink.R, UiTheme.Ink.G, UiTheme.Ink.B, DimAlpha),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(dim);

        _band = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        host.AddChild(_band);

        var pad = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        pad.AddThemeConstantOverride("margin_left", TextPadPx);
        pad.AddThemeConstantOverride("margin_top", TextPadPx);
        pad.AddThemeConstantOverride("margin_right", TextPadPx);
        pad.AddThemeConstantOverride("margin_bottom", TextPadPx);
        _band.AddChild(pad);

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
    }

    public void LayoutBand(Rect2 band)
    {
        if (_band == null || band.Size.X < 8f || band.Size.Y < 8f)
            return;
        _band.Position = band.Position;
        _band.Size = band.Size;
        _band.CustomMinimumSize = band.Size;
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
