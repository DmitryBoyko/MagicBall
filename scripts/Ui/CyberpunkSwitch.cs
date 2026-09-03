using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Neon track + animated knob. Port of MagicCrystalClash cyberpunk_switch.gd.
/// </summary>
public partial class CyberpunkSwitch : Control
{
    private const float BaseTrackW = 56f;
    private const float BaseTrackH = 28f;
    private const float KnobMarginBase = 3f;

    public event Action<bool>? Toggled;

    private bool _buttonPressed;
    private float _scale = 2f;
    private float _knobT;
    private Tween? _animTween;

    public Color Accent { get; private set; } = new(1f, 0.38f, 0.88f);

    public bool ButtonPressed
    {
        get => _buttonPressed;
        set => SetPressed(value, emitToggled: true);
    }

    public override void _Ready()
    {
        MouseDefaultCursorShape = CursorShape.PointingHand;
        FocusMode = FocusModeEnum.All;
        ApplySize();
        _knobT = _buttonPressed ? 1f : 0f;
        QueueRedraw();
    }

    public void Configure(Color accent, float scale = 2f)
    {
        Accent = accent;
        _scale = Mathf.Max(scale, 0.75f);
        ApplySize();
        QueueRedraw();
    }

    public void Toggle() => SetPressed(!_buttonPressed, emitToggled: true);

    public void SetPressedNoSignal(bool on) => SetPressed(on, emitToggled: false);

    private void SetPressed(bool on, bool emitToggled)
    {
        if (_buttonPressed == on)
            return;
        _buttonPressed = on;
        AnimateKnob(on ? 1f : 0f);
        if (emitToggled)
            Toggled?.Invoke(on);
    }

    private void ApplySize()
    {
        CustomMinimumSize = new Vector2(BaseTrackW * _scale, BaseTrackH * _scale);
    }

    private void AnimateKnob(float target)
    {
        if (!IsInsideTree())
        {
            _knobT = Mathf.Clamp(target, 0f, 1f);
            QueueRedraw();
            return;
        }

        if (_animTween != null && _animTween.IsValid())
            _animTween.Kill();
        _animTween = CreateTween();
        _animTween.SetEase(Tween.EaseType.Out);
        _animTween.SetTrans(Tween.TransitionType.Cubic);
        _animTween.TweenMethod(Callable.From<float>(SetKnobT), _knobT, target, 0.14);
    }

    private void SetKnobT(float value)
    {
        _knobT = Mathf.Clamp(value, 0f, 1f);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton && DisplayServer.IsTouchscreenAvailable())
        {
            AcceptEvent();
            return;
        }

        if (!IsPress(@event))
            return;
        Toggle();
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

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var radius = (int)(Size.Y * 0.5f);
        var trackOn = new Color(Accent.R * 0.55f, Accent.G * 0.55f, Accent.B * 0.55f, 0.98f);
        var trackOff = new Color(0.08f, 0.07f, 0.16f, 0.98f);
        var borderOn = new Color(Accent.R, Accent.G, Accent.B, 0.95f);
        var borderOff = new Color(Accent.R, Accent.G, Accent.B, 0.45f);
        var trackCol = trackOn.Lerp(trackOff, 1f - _knobT);
        var borderCol = borderOn.Lerp(borderOff, 1f - _knobT);
        var trackStyle = new StyleBoxFlat
        {
            BgColor = trackCol,
            BorderColor = borderCol,
        };
        trackStyle.SetBorderWidthAll(Mathf.Max(1, (int)(1.5f * _scale)));
        trackStyle.SetCornerRadiusAll(radius);
        DrawStyleBox(trackStyle, rect);

        var margin = KnobMarginBase * _scale;
        var knobD = Size.Y - margin * 2f;
        var travel = Mathf.Max(0f, Size.X - margin * 2f - knobD);
        var knobX = margin + travel * _knobT;
        var knobCenter = new Vector2(knobX + knobD * 0.5f, Size.Y * 0.5f);
        var knobR = knobD * 0.5f;
        DrawCircle(knobCenter + new Vector2(1f, 2f), knobR, new Color(0f, 0f, 0f, 0.35f));
        DrawCircle(knobCenter, knobR, new Color(0.94f, 0.96f, 1f, 1f));
        DrawArc(
            knobCenter,
            knobR,
            0f,
            Mathf.Tau,
            48,
            new Color(Accent.R, Accent.G, Accent.B, 0.25f + 0.35f * _knobT),
            Mathf.Max(1f, 1.2f * _scale),
            true);
    }
}
