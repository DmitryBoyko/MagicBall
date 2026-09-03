using CrystalBall.Ai;
using CrystalBall.Share;
using Godot;

namespace CrystalBall.Ui;

public partial class InterpretationSheet : CanvasLayer
{
    public event Action? Closed;
    public event Action<bool>? CaptureChrome;

    private const float PanelWidthFrac = 0.92f;
    private const float PanelHeightFrac = 0.5f;
    private const float PanelBgAlpha = 0.78f;

    private Label _body = null!;
    private Label _summary = null!;
    private Label _shareHint = null!;
    private MarginContainer _margin = null!;
    private PanelContainer _panel = null!;
    private ScrollContainer _scroll = null!;
    private Button _share = null!;
    private Button _close = null!;
    private bool _sharing;

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

        _shareHint = UiTheme.MakeLabel("", UiTheme.FontModalCaption, UiTheme.Cream);
        _shareHint.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        box.AddChild(_shareHint);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
        };
        buttons.AddThemeConstantOverride("separation", 16);
        box.AddChild(buttons);

        _share = UiTheme.MakeButton("Поделиться", UiTheme.FontReadingButton);
        _share.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _share.CustomMinimumSize = new Vector2(0, 64);
        _share.Pressed += OnSharePressed;
        buttons.AddChild(_share);

        _close = UiTheme.MakeButton("Закрыть", UiTheme.FontReadingButton);
        _close.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _close.CustomMinimumSize = new Vector2(0, 64);
        _close.Pressed += Dismiss;
        buttons.AddChild(_close);

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
        _shareHint.Text = "";
        _sharing = false;
        SetShareBusy(false);
        _share.Visible = true;
        Visible = true;
    }

    public void PresentFog()
    {
        ApplySafeArea();
        _body.Text = "Туман судьбы неразличим.";
        _summary.Text = string.Empty;
        _scroll.ScrollVertical = 0;
        _shareHint.Text = "";
        _sharing = false;
        SetShareBusy(false);
        _share.Visible = false;
        Visible = true;
    }

    private async void OnSharePressed()
    {
        if (_sharing || !Visible)
            return;

        _sharing = true;
        SetShareBusy(true);
        _shareHint.Text = "Готовим изображение…";

        CaptureChrome?.Invoke(true);
        Visible = false;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        var shot = GetViewport()?.GetTexture()?.GetImage();
        Visible = true;
        CaptureChrome?.Invoke(false);

        if (shot == null || shot.IsEmpty())
        {
            FinishShare("", "Не удалось сохранить.");
            return;
        }

        var path = await ReadingShareExport.ExportAsync(this, shot, _body.Text, _summary.Text);
        if (string.IsNullOrEmpty(path))
        {
            FinishShare("", "Не удалось сохранить.");
            return;
        }

        if (OS.GetName() == "Android" || OS.HasFeature("Android"))
        {
            GalleryShare.Launch(this, path, "Поделиться предсказанием");
            return;
        }

        if (OS.GetName() is "Windows" or "Linux" or "macOS")
            OS.ShellOpen(path.GetBaseDir());
        FinishShare("", "");
    }

    public void _on_gallery_android_result(bool ok, string errorText)
    {
        if (ok)
            FinishShare("ok", "");
        else
            FinishShare("", string.IsNullOrEmpty(errorText) ? "Не удалось поделиться." : errorText);
    }

    private void FinishShare(string path, string error)
    {
        _sharing = false;
        SetShareBusy(false);
        _shareHint.Text = error ?? "";
    }

    private void SetShareBusy(bool busy)
    {
        _share.Disabled = busy;
        _close.Disabled = busy;
    }

    private void Dismiss()
    {
        if (!Visible || _sharing)
            return;
        Visible = false;
        Closed?.Invoke();
    }

    private void OnDimInput(InputEvent @event)
    {
        if (_sharing)
            return;

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
