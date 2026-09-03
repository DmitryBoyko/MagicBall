using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Editor/desktop stand-in for Yandex rewarded. Early close fails the LLM reveal.
/// </summary>
public partial class AdOverlay : CanvasLayer
{
    private Label _timer = null!;
    private Button _closeEarly = null!;
    private Button _continue = null!;
    private MarginContainer? _pad;
    private float _left;
    private TaskCompletionSource<bool>? _done;

    public override void _Ready()
    {
        Layer = 40;
        Visible = false;
        var dim = new ColorRect { Color = UiTheme.ModalDim };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        _pad = new MarginContainer();
        _pad.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_pad);

        var panel = new PanelContainer();
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pad.AddChild(panel);

        var content = CyberFrameBorder.CreateContentPad();
        panel.AddChild(content);

        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 16);
        content.AddChild(box);
        ApplySafeArea();

        box.AddChild(UiTheme.MakeLabel("Реклама", UiTheme.FontModalTitle, UiTheme.Gold));
        box.AddChild(UiTheme.MakeLabel(
            "Досмотрите ролик до конца — иначе шар затянет туманом.",
            UiTheme.FontModalBody,
            UiTheme.Cream));
        _timer = UiTheme.MakeLabel("", UiTheme.FontModalBody, UiTheme.Cyan);
        box.AddChild(_timer);

        _closeEarly = UiTheme.MakeQuietButton("Закрыть");
        _closeEarly.CustomMinimumSize = new Vector2(0, 56);
        _closeEarly.Pressed += () => Complete(false);
        box.AddChild(_closeEarly);

        _continue = UiTheme.MakeButton("Продолжить");
        _continue.CustomMinimumSize = new Vector2(0, 64);
        _continue.Visible = false;
        _continue.Pressed += () => Complete(true);
        box.AddChild(_continue);

        CyberFrameBorder.SetupModal(panel);
    }

    public void ApplySafeArea()
    {
        if (_pad == null)
            return;
        SafeAreaHelper.Apply(_pad, this);
    }

    public Task<bool> ShowRewardedAsync(float seconds = 5f)
    {
        _done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _left = Mathf.Max(1f, seconds);
        Visible = true;
        _closeEarly.Visible = true;
        _continue.Visible = false;
        ApplySafeArea();
        SetProcess(true);
        return _done.Task;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        _left -= (float)delta;
        if (_left > 0)
        {
            _timer.Text = $"Осталось {Mathf.CeilToInt(_left)} с";
            return;
        }

        _timer.Text = "Ролик досмотрен";
        _closeEarly.Visible = false;
        _continue.Visible = true;
        SetProcess(false);
    }

    private void Complete(bool granted)
    {
        Visible = false;
        SetProcess(false);
        _done?.TrySetResult(granted);
    }
}
