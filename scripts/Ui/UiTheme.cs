using Godot;

namespace CrystalBall.Ui;

public static class UiTheme
{
    public static readonly Color Gold = new(1f, 0.82f, 0.28f);
    public static readonly Color Purple = new(0.48f, 0.18f, 1f);
    public static readonly Color Crimson = new(1f, 0.18f, 0.42f);
    public static readonly Color Cyan = new(0.05f, 0.96f, 1f);
    public static readonly Color Magenta = new(1f, 0.38f, 0.88f);
    public static readonly Color Cream = new(0.96f, 0.90f, 0.80f);
    public static readonly Color Ink = new(0.07f, 0.03f, 0.14f, 1f);
    public static readonly Color Dim = new(0.02f, 0.01f, 0.06f, 0.72f);
    public static readonly Color ModalDim = new(0f, 0f, 0f, 0.9f);

    public const int FontModalTitle = 32;
    public const int FontModalBody = 20;
    public const int FontModalCaption = 18;
    public const int FontModalInput = 24;
    public const int FontModalButton = 24;
    public const int FontModalTile = 22;
    public const int FontReadingTitle = 36;
    public const int FontReadingBody = 32;
    public const int FontReadingSummary = 34;
    public const int FontReadingButton = 28;

    public static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static Button MakeButton(string text, int fontSize = FontModalButton)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", Cream);
        button.AddThemeStyleboxOverride("normal", Pill(new Color(0.16f, 0.08f, 0.24f, 0.96f), Gold));
        button.AddThemeStyleboxOverride("hover", Pill(new Color(0.24f, 0.12f, 0.32f, 0.98f), Gold.Lightened(0.15f)));
        button.AddThemeStyleboxOverride("pressed", Pill(new Color(0.32f, 0.14f, 0.38f, 1f), Cyan));
        button.AddThemeStyleboxOverride("disabled", Pill(new Color(0.12f, 0.08f, 0.16f, 0.7f), new Color(0.4f, 0.35f, 0.3f)));
        return button;
    }

    public static Button MakeQuietButton(string text, int fontSize = 13)
    {
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", new Color(Cream.R, Cream.G, Cream.B, 0.72f));
        button.AddThemeStyleboxOverride("normal", QuietPill(new Color(0.06f, 0.03f, 0.12f, 0.38f), new Color(1f, 0.82f, 0.28f, 0.22f)));
        button.AddThemeStyleboxOverride("hover", QuietPill(new Color(0.10f, 0.05f, 0.18f, 0.52f), new Color(1f, 0.82f, 0.28f, 0.4f)));
        button.AddThemeStyleboxOverride("pressed", QuietPill(new Color(0.14f, 0.06f, 0.22f, 0.62f), new Color(Cyan.R, Cyan.G, Cyan.B, 0.45f)));
        button.AddThemeStyleboxOverride("disabled", QuietPill(new Color(0.06f, 0.04f, 0.10f, 0.28f), new Color(0.4f, 0.35f, 0.3f, 0.2f)));
        return button;
    }

    public static Button MakeDateField(string text)
    {
        var button = MakeButton(text, FontModalTile);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(0, 64);
        var normal = TileBox(new Color(0.16f, 0.08f, 0.24f, 0.96f), Gold);
        var hover = TileBox(new Color(0.24f, 0.12f, 0.32f, 0.98f), Gold.Lightened(0.15f));
        var pressed = TileBox(new Color(0.32f, 0.14f, 0.38f, 1f), Cyan);
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        return button;
    }

    public static Button MakeTile(string text, bool selected)
    {
        var accent = selected ? Gold : Cyan;
        var bg = selected
            ? new Color(0.32f, 0.18f, 0.08f, 1f)
            : new Color(0.14f, 0.07f, 0.22f, 1f);
        var button = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 72),
        };
        button.AddThemeFontSizeOverride("font_size", FontModalTile);
        button.AddThemeColorOverride("font_color", selected ? Gold.Lightened(0.12f) : Cream);
        button.AddThemeStyleboxOverride("normal", TileBox(bg, accent));
        button.AddThemeStyleboxOverride("hover", TileBox(bg.Lightened(0.08f), accent.Lightened(0.12f)));
        button.AddThemeStyleboxOverride("pressed", TileBox(bg.Lightened(0.12f), Gold));
        return button;
    }

    public static StyleBoxFlat TileBox(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 14,
            CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14,
            CornerRadiusBottomRight = 14,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
    }

    public static StyleBoxFlat Pill(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        };
    }

    public static StyleBoxFlat QuietPill(Color bg, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 5,
            ContentMarginBottom = 5,
        };
    }

    public static StyleBoxFlat Panel() => ModalShell(24);

    /// <summary>
    /// Modal fill. Neon edge comes from <see cref="CyberFrameBorder"/>.
    /// </summary>
    public static StyleBoxFlat ModalShell(int cornerRadius = 12, float alpha = 1f)
    {
        var r = Mathf.Max(12, cornerRadius);
        return new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.03f, 0.11f, Mathf.Clamp(alpha, 0.2f, 1f)),
            CornerRadiusTopLeft = r,
            CornerRadiusTopRight = r,
            CornerRadiusBottomLeft = r,
            CornerRadiusBottomRight = r,
        };
    }
}
