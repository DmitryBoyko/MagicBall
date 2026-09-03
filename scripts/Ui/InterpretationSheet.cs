using CrystalBall.Ai;
using CrystalBall.Share;
using Godot;

namespace CrystalBall.Ui;

public partial class InterpretationSheet : CanvasLayer
{
    public event Action? Closed;
    public event Action<bool>? CaptureChrome;

    private const float PanelBgAlpha = 0.78f;

    private Label _body = null!;
    private Label _summary = null!;
    private Label _shareHint = null!;
    private PanelContainer _panel = null!;
    private ScrollContainer _scroll = null!;
    private Button _share = null!;
    private Button _close = null!;
    private bool _sharing;

    public override void _Ready()
    {
        Layer = 28;
        Visible = false;

        var host = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);

        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        host.AddChild(_panel);

        var pad = CyberFrameBorder.CreateContentPad();
        _panel.AddChild(pad);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
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
    }

    public void LayoutReadingBand(Rect2 band)
    {
        if (_panel == null || band.Size.X < 8f || band.Size.Y < 8f)
            return;
        _panel.Position = band.Position;
        _panel.Size = band.Size;
        _panel.CustomMinimumSize = band.Size;
    }

    public void Present(OracleResult result)
    {
        _body.Text = SummaryExtractor.StripMarkup(result.Interpretation);
        _summary.Text = SummaryExtractor.StripMarkup(result.Summary);
        ShowSheet(share: true);
    }

    public void PresentFog()
    {
        _body.Text = "Туман судьбы неразличим.";
        _summary.Text = string.Empty;
        ShowSheet(share: false);
    }

    private void ShowSheet(bool share)
    {
        _scroll.ScrollVertical = 0;
        _shareHint.Text = "";
        _sharing = false;
        SetShareBusy(false);
        _share.Visible = share;
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
}
