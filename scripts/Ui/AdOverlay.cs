using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Полноэкранная симуляция AdMob/AppLovin. Пока она висит, крутится ONNX-воркер.
/// </summary>
public partial class AdOverlay : CanvasLayer
{
    private Label _timer = null!;
    private Button _skip = null!;
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

        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        box.AddThemeConstantOverride("separation", 16);
        panel.AddChild(box);
        ApplySafeArea();

        box.AddChild(UiTheme.MakeLabel("Реклама", 34, UiTheme.Gold));
        box.AddChild(UiTheme.MakeLabel("Шар читает слепок кадра, пока идёт показ.", 18, UiTheme.Cream));
        _timer = UiTheme.MakeLabel("", 20, UiTheme.Cyan);
        box.AddChild(_timer);
        _skip = UiTheme.MakeButton("Пропустить");
        _skip.Visible = false;
        _skip.Pressed += Finish;
        box.AddChild(_skip);

        CyberFrameBorder.SetupModal(panel);
    }

    public void ApplySafeArea()
    {
        if (_pad == null)
            return;
        SafeAreaHelper.Apply(_pad, this);
    }

    public Task ShowAsync(float seconds = 5f)
    {
        _done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _left = seconds;
        Visible = true;
        _skip.Visible = false;
        ApplySafeArea();
        SetProcess(true);
        return _done.Task;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        _left -= (float)delta;
        _timer.Text = _left > 0 ? $"Осталось {Mathf.CeilToInt(_left)} с" : "Можно закрыть";
        if (_left <= 0)
            _skip.Visible = true;
    }

    private void Finish()
    {
        Visible = false;
        SetProcess(false);
        _done?.TrySetResult(true);
    }
}
