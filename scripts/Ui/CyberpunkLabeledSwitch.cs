using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Label + neon switch; the whole row toggles. Port of MagicCrystalClash cyberpunk_labeled_switch.gd.
/// </summary>
public partial class CyberpunkLabeledSwitch : PanelContainer
{
    public event Action<bool>? Toggled;

    private Label _label = null!;
    private CyberpunkSwitch _switch = null!;

    public bool ButtonPressed => _switch.ButtonPressed;

    public static CyberpunkLabeledSwitch Create(
        string text,
        Color accent,
        int fontSize = 24,
        float minHeight = 72f,
        float switchScale = 2f)
    {
        var row = new CyberpunkLabeledSwitch();
        row.Build(text, accent, fontSize, minHeight, switchScale);
        return row;
    }

    public void SetPressedNoSignal(bool on) => _switch.SetPressedNoSignal(on);

    private void Build(string text, Color accent, int fontSize, float minHeight, float switchScale)
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0f, minHeight);
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var hbox = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        hbox.AddThemeConstantOverride("separation", 16);
        AddChild(hbox);

        _label = new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _label.AddThemeFontSizeOverride("font_size", fontSize);
        _label.AddThemeColorOverride("font_color", accent.Lightened(0.15f));
        hbox.AddChild(_label);

        _switch = new CyberpunkSwitch
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _switch.Configure(accent, switchScale);
        _switch.Toggled += on => Toggled?.Invoke(on);
        hbox.AddChild(_switch);

        GuiInput += OnRowInput;
    }

    private void OnRowInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton && DisplayServer.IsTouchscreenAvailable())
        {
            AcceptEvent();
            return;
        }

        if (!IsPress(@event))
            return;
        _switch.Toggle();
        AcceptEvent();
    }

    private static bool IsPress(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch)
            return touch.Pressed;
        if (@event is InputEventMouseButton mouse)
        {
            if (DisplayServer.IsTouchscreenAvailable())
                return false;
            return mouse.Pressed && mouse.ButtonIndex == MouseButton.Left;
        }

        return false;
    }
}
