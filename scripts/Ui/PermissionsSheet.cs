using CrystalBall.App;
using Godot;

namespace CrystalBall.Ui;

/// <summary>
/// Субмодалка настроек: переключатели разрешений (выданные — включены и заблокированы).
/// </summary>
public partial class PermissionsSheet : CanvasLayer
{
    public event Action? Closed;

    private MarginContainer? _safePad;
    private PanelContainer? _panel;
    private CyberpunkLabeledSwitch _photos = null!;
    private CyberpunkLabeledSwitch _location = null!;
    private bool _built;
    private bool _syncing;

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
            Refresh();
    }

    public void Present()
    {
        EnsureUi();
        Refresh();
        Visible = true;
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
        Layer = 36;
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
        box.AddThemeConstantOverride("separation", 14);
        pad.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Разрешения", UiTheme.FontModalTitle, UiTheme.Gold));
        box.AddChild(UiTheme.MakeLabel(
            "Включите доступ. Уже выданные разрешения нельзя отключить здесь — только в системных настройках.",
            UiTheme.FontModalCaption, UiTheme.Cream));

        _photos = CyberpunkLabeledSwitch.Create("Галерея", UiTheme.Cyan, UiTheme.FontModalButton);
        _photos.Toggled += on => OnTogglePhotos(on);
        box.AddChild(_photos);

        _location = CyberpunkLabeledSwitch.Create("Местоположение", UiTheme.Magenta, UiTheme.FontModalButton);
        _location.Toggled += on => OnToggleLocation(on);
        box.AddChild(_location);

        var system = UiTheme.MakeButton("Системные настройки приложения", UiTheme.FontModalCaption);
        system.CustomMinimumSize = new Vector2(0, 56);
        system.Pressed += OnOpenSystemSettings;
        box.AddChild(system);

        var close = UiTheme.MakeButton("Закрыть");
        close.CustomMinimumSize = new Vector2(0, 64);
        close.Pressed += Dismiss;
        box.AddChild(close);

        CyberFrameBorder.SetupModal(_panel);
        ApplySafeArea();
    }

    private void Refresh()
    {
        if (_photos == null || _location == null)
            return;

        _syncing = true;
        var status = AppPermissions.Check();

        _photos.SetPressedNoSignal(status.PhotosGranted);
        _photos.SetLocked(status.PhotosGranted);

        _location.SetPressedNoSignal(status.LocationGranted);
        _location.SetLocked(status.LocationGranted);
        _syncing = false;
    }

    private void OnTogglePhotos(bool on)
    {
        if (_syncing)
            return;
        if (!on)
        {
            Refresh();
            return;
        }

        AppPermissions.RequestPhotos();
        Refresh();
    }

    private void OnToggleLocation(bool on)
    {
        if (_syncing)
            return;
        if (!on)
        {
            Refresh();
            return;
        }

        AppPermissions.RequestLocation();
        Refresh();
    }

    private void OnOsPermissionResult(string _permission, bool _granted) => Refresh();

    private void OnOpenSystemSettings()
    {
        // После отпускания кнопки — чтобы жест UI не перехватывал переход в Settings.
        CallDeferred(MethodName.OpenSystemSettingsDeferred);
    }

    private void OpenSystemSettingsDeferred()
    {
        if (!AppPermissions.OpenSystemSettings(this))
            GD.PushWarning("[PermissionsSheet] не удалось открыть системные настройки");
    }

    public void _on_android_settings_result(bool ok)
    {
        if (!ok)
            GD.PushWarning("[PermissionsSheet] системные настройки не открылись");
    }

    private void Dismiss()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
