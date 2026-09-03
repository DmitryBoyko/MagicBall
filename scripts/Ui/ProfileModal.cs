using CrystalBall.App;
using CrystalBall.Profile;
using Godot;

namespace CrystalBall.Ui;

public partial class ProfileModal : CanvasLayer
{
    public event Action<UserProfile>? Saved;

    private LineEdit _name = null!;
    private OptionButton _day = null!;
    private OptionButton _month = null!;
    private OptionButton _year = null!;
    private CheckButton? _music;
    private OptionButton? _background;
    private Button _save = null!;
    private Button? _close;
    private MarginContainer? _safePad;
    private PanelContainer? _panel;
    private bool _editMode;

    public void Present(bool editMode)
    {
        _editMode = editMode;
        if (GetChildCount() > 0)
        {
            foreach (var child in GetChildren())
                child.Free();
        }

        EnsureUi();
        SyncFromStore();
        Visible = true;
    }

    public void HideModal()
    {
        if (!_editMode)
            return;
        Visible = false;
    }

    private void EnsureUi()
    {
        if (GetChildCount() > 0)
            return;

        Layer = 32;
        var dim = new ColorRect
        {
            Color = UiTheme.Dim,
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
        _panel.AddThemeStyleboxOverride("panel", UiTheme.Panel());
        center.AddChild(_panel);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        box.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Профиль Оракула", 28, UiTheme.Gold));
        box.AddChild(UiTheme.MakeLabel(
            "Имя и дата рождения сохраняются только локально на этом устройстве (user_profile.json). Их можно изменить позже в настройках.",
            16, UiTheme.Cream));

        box.AddChild(UiTheme.MakeLabel("Имя", 16, UiTheme.Cyan, HorizontalAlignment.Left));
        _name = new LineEdit { PlaceholderText = "Как к тебе обращаться" };
        _name.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_name);

        box.AddChild(UiTheme.MakeLabel("Дата рождения", 16, UiTheme.Cyan, HorizontalAlignment.Left));
        var dates = new HBoxContainer();
        dates.AddThemeConstantOverride("separation", 8);
        _day = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _month = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _year = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        for (var d = 1; d <= 31; d++)
            _day.AddItem(d.ToString("00"), d);
        string[] months = ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
        for (var m = 1; m <= 12; m++)
            _month.AddItem(months[m - 1], m);
        var nowYear = DateTime.Now.Year;
        for (var y = nowYear - 12; y >= 1935; y--)
            _year.AddItem(y.ToString(), y);
        dates.AddChild(_day);
        dates.AddChild(_month);
        dates.AddChild(_year);
        box.AddChild(dates);

        if (_editMode)
        {
            _music = new CheckButton { Text = "Фоновая музыка" };
            _music.AddThemeFontSizeOverride("font_size", 18);
            _music.Toggled += OnMusicToggled;
            box.AddChild(_music);

            box.AddChild(UiTheme.MakeLabel("Фон экрана", 16, UiTheme.Cyan, HorizontalAlignment.Left));
            _background = new OptionButton();
            _background.AddItem("Авто (по времени суток)", 0);
            _background.AddItem("Утро", 1);
            _background.AddItem("День", 2);
            _background.AddItem("Вечер", 3);
            _background.AddItem("Ночь", 4);
            box.AddChild(_background);
        }

        _save = UiTheme.MakeButton("Сохранить");
        _save.Pressed += OnSave;
        box.AddChild(_save);

        _close = UiTheme.MakeButton("Закрыть");
        _close.Pressed += HideModal;
        box.AddChild(_close);

        ApplySafeArea();
    }

    public void ApplySafeArea()
    {
        if (_safePad == null)
            return;
        var insets = SafeAreaHelper.Apply(_safePad, this);
        if (_panel == null)
            return;
        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(720, 1600);
        var inner = Mathf.Max(280f, vp.X - insets.Left - insets.Right - 8f);
        _panel.CustomMinimumSize = new Vector2(Mathf.Min(620f, inner), 0f);
    }

    private void SyncFromStore()
    {
        EnsureUi();
        var profile = ProfileStore.Current;
        _name.Text = profile?.UserName ?? string.Empty;
        var birth = DateTime.TryParse(profile?.BirthDate, out var parsed) ? parsed : new DateTime(1995, 11, 25);
        _day.Select(_day.GetItemIndex(birth.Day));
        _month.Select(_month.GetItemIndex(birth.Month));
        var yearIndex = _year.GetItemIndex(birth.Year);
        _year.Select(yearIndex >= 0 ? yearIndex : 0);

        if (_music != null)
            _music.SetPressedNoSignal(AppSettingsStore.Current.MusicEnabled);
        if (_background != null)
        {
            _background.Selected = AppSettingsStore.Current.BackgroundPreset switch
            {
                "morning" => 1,
                "day" => 2,
                "evening" => 3,
                "night" => 4,
                _ => 0,
            };
        }

        if (_close != null)
            _close.Visible = _editMode;
    }

    private void OnSave()
    {
        var name = _name.Text.Trim();
        if (name.Length < 2)
        {
            GD.Print("Введите имя.");
            return;
        }

        var day = _day.GetSelectedId();
        var month = _month.GetSelectedId();
        var year = _year.GetSelectedId();
        DateTime birth;
        try
        {
            birth = new DateTime(year, month, day);
        }
        catch
        {
            return;
        }

        if (birth > DateTime.Today || birth < new DateTime(1935, 1, 1))
            return;

        var profile = new UserProfile
        {
            UserName = name,
            BirthDate = birth.ToString("yyyy-MM-dd"),
        };
        ProfileStore.Save(profile);

        if (_music != null || _background != null)
        {
            var settings = AppSettingsStore.Current;
            if (_music != null)
                settings.MusicEnabled = _music.ButtonPressed;
            if (_background != null)
            {
                settings.BackgroundPreset = _background.Selected switch
                {
                    1 => "morning",
                    2 => "day",
                    3 => "evening",
                    4 => "night",
                    _ => "auto",
                };
            }

            AppSettingsStore.Save(settings);
            GetNodeOrNull<AudioService>("/root/AudioService")?.SetEnabled(settings.MusicEnabled);
        }

        Visible = false;
        Saved?.Invoke(profile);
    }

    private void OnMusicToggled(bool enabled)
    {
        GetNodeOrNull<AudioService>("/root/AudioService")?.SetEnabled(enabled);
    }
}
