using CrystalBall.Ai;
using Godot;

namespace CrystalBall.Ui;

public partial class InterpretationSheet : CanvasLayer
{
    public event Action? Closed;

    private Label _body = null!;
    private Label _summary = null!;
    private MarginContainer _margin = null!;

    public override void _Ready()
    {
        Layer = 28;
        Visible = false;

        var dim = new ColorRect { Color = UiTheme.ModalDim };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += OnDimInput;
        AddChild(dim);

        _margin = new MarginContainer();
        _margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_margin);
        ApplySafeArea();

        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _margin.AddChild(panel);

        var pad = CyberFrameBorder.CreateContentPad();
        panel.AddChild(pad);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 22);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        pad.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Ответ", UiTheme.FontReadingTitle, UiTheme.Gold));

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        var text = new VBoxContainer();
        text.AddThemeConstantOverride("separation", 20);
        text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(text);

        _body = UiTheme.MakeLabel("", UiTheme.FontReadingBody, UiTheme.Cream);
        _body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_body);
        _summary = UiTheme.MakeLabel("", UiTheme.FontReadingSummary, UiTheme.Gold);
        _summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_summary);

        var close = UiTheme.MakeButton("Закрыть", UiTheme.FontReadingButton);
        close.CustomMinimumSize = new Vector2(0, 88);
        close.Pressed += Dismiss;
        box.AddChild(close);

        CyberFrameBorder.SetupModal(panel);
    }

    public void ApplySafeArea()
    {
        if (_margin == null)
            return;
        SafeAreaHelper.Apply(_margin, this);
    }

    public void Present(OracleResult result)
    {
        ApplySafeArea();
        _body.Text = SummaryExtractor.StripMarkup(result.Interpretation);
        var gold = SummaryExtractor.StripMarkup(result.Summary);
        _summary.Text = gold;
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
