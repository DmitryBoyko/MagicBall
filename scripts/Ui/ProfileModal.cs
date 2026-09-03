using CrystalBall.App;
using CrystalBall.Profile;
using Godot;

namespace CrystalBall.Ui;

public partial class ProfileModal : CanvasLayer
{
    private const int MinBirthYear = 1930;
    private const int RecentYearsToSkip = 6;

    private static readonly string[] MonthsShort =
        ["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];

    public event Action<UserProfile>? Saved;

    private enum DatePart
    {
        None,
        Day,
        Month,
        Year,
    }

    private LineEdit _name = null!;
    private Button _dayBtn = null!;
    private Button _monthBtn = null!;
    private Button _yearBtn = null!;
    private int _birthDay = 25;
    private int _birthMonth = 11;
    private int _birthYear = 1995;
    private DatePart _picking;
    private TilePickerModal _picker = null!;
    private CyberpunkLabeledSwitch? _music;
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

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        box.AddThemeConstantOverride("separation", 12);
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        pad.AddChild(box);

        box.AddChild(UiTheme.MakeLabel("Настройки", UiTheme.FontModalTitle, UiTheme.Gold));
        box.AddChild(UiTheme.MakeLabel(
            "Имя и дата рождения сохраняются только локально на этом устройстве.",
            UiTheme.FontModalBody, UiTheme.Cream));

        box.AddChild(UiTheme.MakeLabel("Имя", UiTheme.FontModalBody, UiTheme.Cyan, HorizontalAlignment.Left));
        _name = new LineEdit { PlaceholderText = "Как к тебе обращаться" };
        _name.AddThemeFontSizeOverride("font_size", UiTheme.FontModalInput);
        _name.CustomMinimumSize = new Vector2(0, 56);
        box.AddChild(_name);

        box.AddChild(UiTheme.MakeLabel("Дата рождения", UiTheme.FontModalBody, UiTheme.Cyan, HorizontalAlignment.Left));
        var dates = new HBoxContainer();
        dates.AddThemeConstantOverride("separation", 8);
        _dayBtn = UiTheme.MakeDateField("25");
        _monthBtn = UiTheme.MakeDateField("ноя");
        _yearBtn = UiTheme.MakeDateField("1995");
        _dayBtn.Pressed += OpenDayPicker;
        _monthBtn.Pressed += OpenMonthPicker;
        _yearBtn.Pressed += OpenYearPicker;
        dates.AddChild(MakeDateColumn("день", _dayBtn));
        dates.AddChild(MakeDateColumn("месяц", _monthBtn));
        dates.AddChild(MakeDateColumn("год", _yearBtn));
        box.AddChild(dates);

        if (_editMode)
        {
            _music = CyberpunkLabeledSwitch.Create("Фоновая музыка", UiTheme.Magenta, UiTheme.FontModalButton);
            _music.Toggled += OnMusicToggled;
            box.AddChild(_music);

            if (DevToggles.ShowBackgroundPresetInSettings)
            {
                box.AddChild(UiTheme.MakeLabel("Фон экрана", UiTheme.FontModalBody, UiTheme.Cyan, HorizontalAlignment.Left));
                _background = new OptionButton();
                _background.AddThemeFontSizeOverride("font_size", UiTheme.FontModalInput);
                _background.AddItem("Авто (по времени суток)", 0);
                _background.AddItem("Утро", 1);
                _background.AddItem("День", 2);
                _background.AddItem("Вечер", 3);
                _background.AddItem("Ночь", 4);
                box.AddChild(_background);
            }
        }

        _save = UiTheme.MakeButton("Сохранить");
        _save.CustomMinimumSize = new Vector2(0, 64);
        _save.Pressed += OnSave;
        box.AddChild(_save);

        _close = UiTheme.MakeButton("Закрыть");
        _close.CustomMinimumSize = new Vector2(0, 64);
        _close.Pressed += HideModal;
        box.AddChild(_close);

        CyberFrameBorder.SetupModal(_panel);

        _picker = new TilePickerModal();
        _picker.Picked += OnDatePartPicked;
        AddChild(_picker);

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
        _picker?.ApplySafeArea();
    }

    private void SyncFromStore()
    {
        EnsureUi();
        var profile = ProfileStore.Current;
        _name.Text = profile?.UserName ?? string.Empty;
        var birth = DateTime.TryParse(profile?.BirthDate, out var parsed) ? parsed : new DateTime(1995, 11, 25);
        _birthDay = birth.Day;
        _birthMonth = birth.Month;
        _birthYear = birth.Year;
        ClampBirthDate();
        RefreshDateButtons();

        _music?.SetPressedNoSignal(AppSettingsStore.Current.MusicEnabled);
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

        ClampBirthDate();
        DateTime birth;
        try
        {
            birth = new DateTime(_birthYear, _birthMonth, _birthDay);
        }
        catch
        {
            return;
        }

        if (birth.Year > MaxBirthYear() || birth < new DateTime(MinBirthYear, 1, 1))
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

    private void OpenDayPicker()
    {
        _picking = DatePart.Day;
        var max = MaxDayFor(_birthYear, _birthMonth);
        var items = new List<TilePickItem>(max);
        for (var d = 1; d <= max; d++)
            items.Add(new TilePickItem(d, d.ToString()));
        _picker.Present("День", items, _birthDay, 4);
    }

    private void OpenMonthPicker()
    {
        _picking = DatePart.Month;
        const int maxMonth = 12;
        var items = new List<TilePickItem>(maxMonth);
        for (var m = 1; m <= maxMonth; m++)
            items.Add(new TilePickItem(m, MonthsShort[m - 1]));
        _picker.Present("Месяц", items, _birthMonth, 3);
    }

    private void OpenYearPicker()
    {
        _picking = DatePart.Year;
        var maxYear = MaxBirthYear();
        var items = new List<TilePickItem>(maxYear - MinBirthYear + 1);
        for (var y = maxYear; y >= MinBirthYear; y--)
            items.Add(new TilePickItem(y, y.ToString()));
        _picker.Present("Год", items, _birthYear, 4);
    }

    private void OnDatePartPicked(int id)
    {
        switch (_picking)
        {
            case DatePart.Day:
                _birthDay = id;
                break;
            case DatePart.Month:
                _birthMonth = id;
                break;
            case DatePart.Year:
                _birthYear = id;
                break;
        }

        _picking = DatePart.None;
        ClampBirthDate();
        RefreshDateButtons();
    }

    private void ClampBirthDate()
    {
        _birthYear = Mathf.Clamp(_birthYear, MinBirthYear, MaxBirthYear());
        _birthMonth = Mathf.Clamp(_birthMonth, 1, 12);
        _birthDay = Mathf.Clamp(_birthDay, 1, MaxDayFor(_birthYear, _birthMonth));
    }

    private static int MaxBirthYear() => DateTime.Today.Year - RecentYearsToSkip;

    private static int MaxDayFor(int year, int month) => DateTime.DaysInMonth(year, month);

    private void RefreshDateButtons()
    {
        _dayBtn.Text = _birthDay.ToString("00");
        _monthBtn.Text = MonthsShort[_birthMonth - 1];
        _yearBtn.Text = _birthYear.ToString();
    }

    private static Control MakeDateColumn(string caption, Button field)
    {
        var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 4);
        col.AddChild(UiTheme.MakeLabel(caption, UiTheme.FontModalCaption, UiTheme.Cyan));
        col.AddChild(field);
        return col;
    }
}
