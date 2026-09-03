using CrystalBall.Ai;
using Godot;

namespace CrystalBall.Ui;

public partial class InterpretationSheet : CanvasLayer
{
    public event Action? Closed;

    private const float PanelWidthFrac = 0.92f;
    private const float PanelHeightFrac = 0.5f;
    private const float PanelBgAlpha = 0.78f;

    private Label _body = null!;
    private Label _summary = null!;
    private MarginContainer _margin = null!;
    private PanelContainer _panel = null!;
    private ScrollContainer _scroll = null!;

    public override void _Ready()
    {
        Layer = 28;
        Visible = false;

        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += OnDimInput;
        AddChild(dim);

        _margin = new MarginContainer();
        _margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_margin);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _margin.AddChild(center);

        _panel = new PanelContainer();
        center.AddChild(_panel);

        var pad = CyberFrameBorder.CreateContentPad();
        _panel.AddChild(pad);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        pad.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Ответ", UiTheme.FontReadingTitle, UiTheme.Gold));

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        box.AddChild(_scroll);

        var text = new VBoxContainer();
        text.AddThemeConstantOverride("separation", 16);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _scroll.AddChild(text);

        _body = UiTheme.MakeLabel("", UiTheme.FontReadingBody, UiTheme.Cream);
        _body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_body);
        _summary = UiTheme.MakeLabel("", UiTheme.FontReadingSummary, UiTheme.Gold);
        _summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_summary);

        var close = UiTheme.MakeButton("Закрыть", UiTheme.FontReadingButton);
        close.CustomMinimumSize = new Vector2(0, 64);
        close.Pressed += Dismiss;
        box.AddChild(close);

        CyberFrameBorder.SetupModal(_panel, PanelBgAlpha);
        ApplySafeArea();
    }

    public void ApplySafeArea()
    {
        if (_margin == null || _panel == null)
            return;
        SafeAreaHelper.Apply(_margin, this);
        var safe = SafeAreaHelper.GetSafeRect(this);
        _panel.CustomMinimumSize = new Vector2(
            Mathf.Max(280f, safe.Size.X * PanelWidthFrac),
            Mathf.Max(240f, safe.Size.Y * PanelHeightFrac));
    }

    public void Present(OracleResult result)
    {
        ApplySafeArea();
        _body.Text = SummaryExtractor.StripMarkup(result.Interpretation);
        _summary.Text = SummaryExtractor.StripMarkup(result.Summary);
        _scroll.ScrollVertical = 0;
        Visible = true;
    }

    public void PresentFog()
    {
        ApplySafeArea();
        _body.Text = "Туман судьбы неразличим.";
        _summary.Text = string.Empty;
        _scroll.ScrollVertical = 0;
        Visible = true;
    }

    private void Dismiss()
    {
        if (!Visible)
            return;
        Visible = false;
        Closed?.Invoke();
    }

    private void OnDimInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            Dismiss();
            return;
        }

        if (@event is InputEventMouseButton mouse
            && mouse.Pressed
            && mouse.ButtonIndex == MouseButton.Left
            && !DisplayServer.IsTouchscreenAvailable())
        {
            Dismiss();
        }
    }
}
