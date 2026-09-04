using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Ритуальный лог сборки промпта: медленный построчный fade-in и ★ статуса сигнала.
/// </summary>
public partial class CastingLogSheet : CanvasLayer
{
    private const float DimAlpha = 0.32f;
    private const float LineFadeIn = 0.7f;
    private const float TopFadeOut = 0.65f;
    private const int MaxVisibleLines = 12;
    private const int TextPadPx = 20;
    private const string StarGlyph = "★";

    public const int CanvasLayerIndex = 28;

    private static readonly Color StarOk = UiTheme.Gold;
    private static readonly Color StarMiss = UiTheme.Cyan;

    private Control _band = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _lines = null!;
    private readonly List<Control> _rows = [];

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
        _lines.AddThemeConstantOverride("separation", 12);
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

    /// <param name="inPrompt">Золотая ★ — сигнал в промпте; голубая ★ — нет.</param>
    public void AppendLine(string phrase, bool inPrompt = true)
    {
        if (!Visible || string.IsNullOrWhiteSpace(phrase))
            return;

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        row.AddThemeConstantOverride("separation", 10);

        var star = UiTheme.MakeLabel(StarGlyph, UiTheme.FontReadingBody - 2, inPrompt ? StarOk : StarMiss);
        star.MouseFilter = Control.MouseFilterEnum.Ignore;
        star.CustomMinimumSize = new Vector2(36, 0);
        row.AddChild(star);

        var label = UiTheme.MakeLabel(phrase, UiTheme.FontReadingBody - 4, UiTheme.Cream);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.HorizontalAlignment = HorizontalAlignment.Left;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(label);

        _lines.AddChild(row);
        _rows.Add(row);

        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.Out);
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(row, "modulate:a", 1f, LineFadeIn);

        TrimTopIfNeeded();
        CallDeferred(MethodName.ScrollToBottom);
    }

    private void TrimTopIfNeeded()
    {
        while (_rows.Count > MaxVisibleLines)
        {
            var oldest = _rows[0];
            _rows.RemoveAt(0);
            FadeAndFree(oldest);
        }
    }

    private void FadeAndFree(Control row)
    {
        if (!IsInstanceValid(row))
            return;
        var tween = CreateTween();
        tween.SetEase(Tween.EaseType.In);
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.TweenProperty(row, "modulate:a", 0f, TopFadeOut);
        tween.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(row))
                row.QueueFree();
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
        foreach (var row in _rows)
        {
            if (IsInstanceValid(row))
                row.QueueFree();
        }

        _rows.Clear();
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
