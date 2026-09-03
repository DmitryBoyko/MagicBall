using Godot;

namespace CrystalBall.Ui;

public static class UiTheme
{
    public static readonly Color Gold = new(1f, 0.82f, 0.28f);
    public static readonly Color Purple = new(0.48f, 0.18f, 1f);
    public static readonly Color Crimson = new(1f, 0.18f, 0.42f);
    public static readonly Color Cyan = new(0.05f, 0.96f, 1f);
    public static readonly Color Cream = new(0.96f, 0.90f, 0.80f);
    public static readonly Color Ink = new(0.07f, 0.03f, 0.14f, 0.96f);
    public static readonly Color Dim = new(0.02f, 0.01f, 0.06f, 0.72f);

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

    public static Button MakeButton(string text, int fontSize = 22)
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

    public static StyleBoxFlat Panel()
    {
        return new StyleBoxFlat
        {
            BgColor = Ink,
            BorderColor = new Color(1f, 0.38f, 0.88f, 0.85f),
            BorderWidthTop = 3,
            BorderWidthBottom = 3,
            BorderWidthLeft = 3,
            BorderWidthRight = 3,
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomLeft = 24,
            CornerRadiusBottomRight = 24,
            ShadowColor = new Color(0.72f, 0.15f, 1f, 0.35f),
            ShadowSize = 12,
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
        };
    }
}
