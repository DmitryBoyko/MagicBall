using Godot;

namespace CrystalBall.Ui;

public readonly record struct TilePickItem(int Id, string Label);

/// <summary>
/// Sub-modal with tappable tiles in 3–4 columns. Used for day / month / year.
/// </summary>
public partial class TilePickerModal : CanvasLayer
{
    public event Action<int>? Picked;

    private ColorRect _dim = null!;
    private MarginContainer? _safePad;
    private PanelContainer? _panel;
    private Label _title = null!;
    private ScrollContainer _scroll = null!;
    private GridContainer _grid = null!;
    private Button? _selectedTile;
    private int _selectedId;

    public override void _Ready()
    {
        Layer = 48;
        Visible = false;

        _dim = new ColorRect
        {
            Color = UiTheme.ModalDim,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _dim.GuiInput += OnDimInput;
        AddChild(_dim);

        _safePad = new MarginContainer();
        _safePad.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_safePad);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _safePad.AddChild(center);

        _panel = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        center.AddChild(_panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        _panel.AddChild(box);

        _title = UiTheme.MakeLabel("", 26, UiTheme.Gold);
        box.AddChild(_title);

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        box.AddChild(_scroll);

        _grid = new GridContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        _scroll.AddChild(_grid);

        var back = UiTheme.MakeButton("Назад");
        back.Pressed += Hide;
        box.AddChild(back);

        CyberFrameBorder.SetupModal(_panel);
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
        _panel.CustomMinimumSize = new Vector2(Mathf.Min(640f, inner), 0f);
    }

    public void Present(string title, IReadOnlyList<TilePickItem> items, int selectedId, int columns)
    {
        ApplySafeArea();
        _title.Text = title;
        _selectedId = selectedId;
        _grid.Columns = Mathf.Clamp(columns, 3, 4);
        _selectedTile = null;

        foreach (var child in _grid.GetChildren())
            child.Free();

        var gridW = (_panel?.CustomMinimumSize.X ?? 620f) - 44f;
        _grid.CustomMinimumSize = new Vector2(gridW, 0f);

        foreach (var item in items)
        {
            var selected = item.Id == selectedId;
            var tile = UiTheme.MakeTile(item.Label, selected);
            var id = item.Id;
            tile.Pressed += () => OnTilePressed(id);
            _grid.AddChild(tile);
            if (selected)
                _selectedTile = tile;
        }

        var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(720, 1600);
        var rows = (items.Count + columns - 1) / columns;
        var contentH = rows * 72f + Mathf.Max(0, rows - 1) * 8f;
        _scroll.CustomMinimumSize = new Vector2(0f, Mathf.Min(contentH, vp.Y * 0.55f));

        Visible = true;
        CallDeferred(nameof(ScrollSelectedIntoView));
    }

    private void ScrollSelectedIntoView()
    {
        if (_selectedTile != null && GodotObject.IsInstanceValid(_selectedTile))
            _scroll.EnsureControlVisible(_selectedTile);
    }

    private void OnTilePressed(int id)
    {
        _selectedId = id;
        Visible = false;
        Picked?.Invoke(id);
    }

    private void OnDimInput(InputEvent @event)
    {
        if (@event is InputEventScreenTouch touch && touch.Pressed)
        {
            _dim.AcceptEvent();
            Hide();
            return;
        }

        if (@event is InputEventMouseButton mouse
            && mouse.Pressed
            && mouse.ButtonIndex == MouseButton.Left
            && !DisplayServer.IsTouchscreenAvailable())
        {
            _dim.AcceptEvent();
            Hide();
        }
    }
}
