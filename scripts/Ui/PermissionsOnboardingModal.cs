using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Онбординг при старте: разрешения по одному. «Позже» сразу закрывает модалку.
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
    private Button? _openSettings;
    private Label? _hint;
    private Step _step = Step.Photos;
    private bool _built;
    private bool _awaitingOs;
    private bool _closing;
    private SceneTreeTimer? _watchdog;

    public override void _EnterTree()
    {
        base._EnterTree();
        var tree = GetTree();
        if (tree != null)
            tree.OnRequestPermissionsResult += OnOsPermissionResult;
    }

    public override void _ExitTree()
    {
        DisconnectWatchdog();
        var tree = GetTree();
        if (tree != null)
            tree.OnRequestPermissionsResult -= OnOsPermissionResult;
        base._ExitTree();
    }

    public override void _Notification(int what)
    {
        // Только после системного диалога, не на каждый focus (избегаем лишней работы).
        if (what == NotificationApplicationFocusIn && Visible && _awaitingOs)
            CallDeferred(MethodName.SyncStepFromOs);
    }

    public bool TryPresent()
    {
        if (!AppPermissions.IsAndroid)
            return false;

        AppPermissionStatus status;
        try
        {
            status = AppPermissions.Check();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PermissionsOnboarding] Check failed: {ex.Message}");
            return false;
        }

        if (status.AllGranted)
            return false;

        EnsureUi();
        _closing = false;
        _awaitingOs = false;
        _step = !status.PhotosGranted ? Step.Photos : Step.Location;
        RefreshStep();
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

        _hint = UiTheme.MakeLabel("", UiTheme.FontModalCaption, UiTheme.Gold);
        _hint.Visible = false;
        box.AddChild(_hint);

        _openSettings = UiTheme.MakeButton("Открыть системные настройки", UiTheme.FontModalCaption);
        _openSettings.CustomMinimumSize = new Vector2(0, 52);
        _openSettings.Visible = false;
        _openSettings.Pressed += () => CallDeferred(MethodName.OpenSystemSettingsDeferred);
        box.AddChild(_openSettings);

        // Обычная кнопка — QuietButton на части устройств плохо ловит Pressed.
        _skip = UiTheme.MakeButton("Позже", UiTheme.FontModalButton);
        _skip.CustomMinimumSize = new Vector2(0, 56);
        _skip.Pressed += OnSkipOrClose;
        box.AddChild(_skip);

        CyberFrameBorder.SetupModal(_panel);
        ApplySafeArea();
    }

    private void SyncStepFromOs()
    {
        if (_closing || !Visible)
            return;

        AppPermissionStatus status;
        try
        {
            status = AppPermissions.Check();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PermissionsOnboarding] Sync Check: {ex.Message}");
            return;
        }

        if (status.AllGranted)
        {
            _step = Step.Done;
            _awaitingOs = false;
            RefreshStep();
            return;
        }

        if (_step == Step.Photos && status.PhotosGranted)
        {
            _step = Step.Location;
            _awaitingOs = false;
            RefreshStep();
            return;
        }

        if (_step == Step.Location && status.LocationGranted)
        {
            _step = Step.Done;
            _awaitingOs = false;
            RefreshStep();
        }
    }

    private void RefreshStep()
    {
        if (_hint != null)
            _hint.Visible = false;
        if (_openSettings != null)
            _openSettings.Visible = false;

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
                _title.Text = "Можно продолжить";
                _body.Text = "Доступы можно выдать позже в настройках → «Разрешения».";
                try
                {
                    if (AppPermissions.Check().AllGranted)
                    {
                        _title.Text = "Готово";
                        _body.Text = "Доступы выданы. Изменить их позже — в настройках → «Разрешения».";
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                _allow.Visible = false;
                _skip.Text = "Закрыть";
                break;
        }
    }

    private void OnAllow()
    {
        if (_closing)
            return;

        _awaitingOs = true;
        if (_hint != null)
        {
            _hint.Text = "Ждём ответ системы…";
            _hint.Visible = true;
        }

        if (_step == Step.Photos)
            CallDeferred(MethodName.DeferredRequestPhotos);
        else if (_step == Step.Location)
            CallDeferred(MethodName.DeferredRequestLocation);

        DisconnectWatchdog();
        var tree = GetTree();
        if (tree != null)
        {
            _watchdog = tree.CreateTimer(1.8);
            _watchdog.Timeout += OnRequestWatchdog;
        }
    }

    private void DeferredRequestPhotos()
    {
        if (_closing)
            return;
        try
        {
            AppPermissions.RequestPhotos();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PermissionsOnboarding] RequestPhotos: {ex.Message}");
            ShowSettingsFallback();
        }
    }

    private void DeferredRequestLocation()
    {
        if (_closing)
            return;
        try
        {
            AppPermissions.RequestLocation();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PermissionsOnboarding] RequestLocation: {ex.Message}");
            ShowSettingsFallback();
        }
    }

    private void OnRequestWatchdog()
    {
        DisconnectWatchdog();
        if (!Visible || !_awaitingOs || _closing)
            return;

        try
        {
            var status = AppPermissions.Check();
            var ok = _step == Step.Photos ? status.PhotosGranted : status.LocationGranted;
            if (ok)
            {
                _awaitingOs = false;
                SyncStepFromOs();
                return;
            }
        }
        catch (Exception)
        {
            // fall through to settings hint
        }

        ShowSettingsFallback();
    }

    private void ShowSettingsFallback()
    {
        if (_hint != null)
        {
            _hint.Text =
                "Если окно Android не появилось, откройте системные настройки и включите доступ вручную.";
            _hint.Visible = true;
        }

        if (_openSettings != null)
            _openSettings.Visible = true;
    }

    private void OnOsPermissionResult(string permission, bool granted)
    {
        GD.Print($"[PermissionsOnboarding] result {permission}={granted}");
        _awaitingOs = false;
        if (granted)
            GeoLocationService.Warmup();
        CallDeferred(MethodName.SyncStepFromOs);
    }

    private void OnSkipOrClose()
    {
        // «Позже» / «Закрыть» — сразу выйти, без Java и без следующего шага.
        Dismiss();
    }

    private void OpenSystemSettingsDeferred()
    {
        if (!AppPermissions.OpenSystemSettings(this))
            GD.PushWarning("[PermissionsOnboarding] не удалось открыть системные настройки");
    }

    public void _on_android_settings_result(bool ok)
    {
        if (!ok)
            GD.PushWarning("[PermissionsOnboarding] системные настройки не открылись");
    }

    private void Dismiss()
    {
        if (_closing)
            return;
        _closing = true;
        _awaitingOs = false;
        DisconnectWatchdog();
        Visible = false;
        CallDeferred(MethodName.EmitClosedDeferred);
    }

    private void EmitClosedDeferred()
    {
        try
        {
            Closed?.Invoke();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PermissionsOnboarding] Closed handler: {ex.Message}");
        }
    }

    private void DisconnectWatchdog()
    {
        if (_watchdog == null)
            return;
        try
        {
            _watchdog.Timeout -= OnRequestWatchdog;
        }
        catch (Exception)
        {
            // timer may already be freed
        }

        _watchdog = null;
    }
}
