using CrystalBall.Ai;
using Godot;

namespace CrystalBall.Ui;

public partial class InterpretationSheet : CanvasLayer
{
    private Label _body = null!;
    private Label _summary = null!;
    private MarginContainer _margin = null!;

    public override void _Ready()
    {
        Layer = 28;
        Visible = false;

        var dim = new ColorRect { Color = UiTheme.Dim };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += _ => Hide();
        AddChild(dim);

        _margin = new MarginContainer();
        _margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_margin);
        ApplySafeArea();

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _margin.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 22);
        panel.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Толкование шара", 52, UiTheme.Gold));

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

        _body = UiTheme.MakeLabel("", 36, UiTheme.Cream);
        _body.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_body);
        _summary = UiTheme.MakeLabel("", 40, UiTheme.Gold);
        _summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        text.AddChild(_summary);

        var close = UiTheme.MakeButton("Закрыть", 44);
        close.CustomMinimumSize = new Vector2(0, 88);
        close.Pressed += Hide;
        box.AddChild(close);
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
        _body.Text = result.Interpretation;
        _summary.Text = string.IsNullOrWhiteSpace(result.Summary) ? string.Empty : result.Summary;
        Visible = true;
    }
}
