using CrystalBall.App;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Онбординг при старте: разрешения по одному (галерея → местоположение).
/// </summary>
public partial class PermissionsOnboardingModal : CanvasLayer
{
    public event Action? Closed;

    private enum Step
    {
        Photos,
        Location,
        Done,
    }

    private MarginContainer? _safePad;
    private PanelContainer? _panel;
    private Label _title = null!;
    private Label _body = null!;
    private Button _allow = null!;
    private Button _skip = null!;
    private Step _step = Step.Photos;
    private bool _built;

    public override void _EnterTree()
    {
        base._EnterTree();
        var tree = GetTree();
        if (tree != null)
            tree.OnRequestPermissionsResult += OnOsPermissionResult;
    }

    public override void _ExitTree()
    {
        var tree = GetTree();
        if (tree != null)
            tree.OnRequestPermissionsResult -= OnOsPermissionResult;
        base._ExitTree();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn && Visible)
            SyncStepFromOs();
    }

    public bool TryPresent()
    {
        if (!AppPermissions.IsAndroid || AppPermissions.Check().AllGranted)
            return false;

        EnsureUi();
        SyncStepFromOs();
        if (_step == Step.Done && AppPermissions.Check().AllGranted)
            return false;

        Visible = true;
        return true;
    }

    public void ApplySafeArea()
    {
        if (_safePad == null)
            return;
        SafeAreaHelper.Apply(_safePad, this);
    }

    private void EnsureUi()
    {
        if (_built)
            return;
        _built = true;
        Layer = 40;
        Visible = false;

        var dim = new ColorRect
        {
            Color = UiTheme.ModalDim,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        _safePad = new MarginContainer();
        _safePad.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_safePad);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _safePad.AddChild(center);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        center.AddChild(_panel);

        var pad = CyberFrameBorder.CreateContentPad();
        _panel.AddChild(pad);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        pad.AddChild(box);

        _title = UiTheme.MakeLabel("", UiTheme.FontModalTitle, UiTheme.Gold);
        box.AddChild(_title);

        _body = UiTheme.MakeLabel("", UiTheme.FontModalBody, UiTheme.Cream);
        box.AddChild(_body);

        _allow = UiTheme.MakeButton("Разрешить");
        _allow.CustomMinimumSize = new Vector2(0, 64);
        _allow.Pressed += OnAllow;
        box.AddChild(_allow);

        _skip = UiTheme.MakeQuietButton("Позже", UiTheme.FontModalButton);
        _skip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _skip.CustomMinimumSize = new Vector2(0, 52);
        _skip.Pressed += OnSkipOrClose;
        box.AddChild(_skip);

        CyberFrameBorder.SetupModal(_panel);
        ApplySafeArea();
    }

    private void SyncStepFromOs()
    {
        var status = AppPermissions.Check();
        if (status.AllGranted)
            _step = Step.Done;
        else if (!status.PhotosGranted)
            _step = Step.Photos;
        else
            _step = Step.Location;
        RefreshStep();
    }

    private void RefreshStep()
    {
        switch (_step)
        {
            case Step.Photos:
                _title.Text = "Доступ к галерее";
                _body.Text =
                    "Шар смотрит на недавние снимки, чтобы точнее уловить образ. " +
                    "Разрешите доступ к фото — по одному запросу за раз.";
                _allow.Text = "Разрешить галерею";
                _allow.Visible = true;
                _skip.Text = "Позже";
                break;
            case Step.Location:
                _title.Text = "Местоположение";
                _body.Text =
                    "Город и погода входят в ответ шара. " +
                    "Разрешите геолокацию — только с устройства, без приближения по IP.";
                _allow.Text = "Разрешить местоположение";
                _allow.Visible = true;
                _skip.Text = "Позже";
                break;
            default:
                _title.Text = AppPermissions.Check().AllGranted ? "Готово" : "Можно продолжить";
                _body.Text = AppPermissions.Check().AllGranted
                    ? "Доступы выданы. Изменить их позже — в настройках → «Разрешения»."
                    : "Часть доступов не выдана. Их можно включить позже в настройках → «Разрешения».";
                _allow.Visible = false;
                _skip.Text = "Закрыть";
                break;
        }
    }

    private void OnAllow()
    {
        if (_step == Step.Photos)
            AppPermissions.RequestPhotos();
        else if (_step == Step.Location)
            AppPermissions.RequestLocation();
    }

    private void OnOsPermissionResult(string _permission, bool _granted) => SyncStepFromOs();

    private void OnSkipOrClose()
    {
        if (_step == Step.Photos)
        {
            _step = AppPermissions.Check().LocationGranted ? Step.Done : Step.Location;
            RefreshStep();
            return;
        }

        if (_step == Step.Location)
        {
            _step = Step.Done;
            RefreshStep();
            return;
        }

        Visible = false;
        Closed?.Invoke();
    }
}
