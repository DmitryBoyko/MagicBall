using CrystalBall.App;
using Godot;

namespace CrystalBall.Share;

/// <summary>
/// PNG для шаринга: скрин с оверлеем «Волшебный шар» → текст ответа → бренд + RuStore.
/// </summary>
public static class ReadingShareExport
{
    public const string Brand = "Волшебный шар";
    public const string DefaultStoreUrl = "https://www.rustore.ru/catalog/developer/9cf9ks";
    private const string TitleFontPath = "res://fonts/Philosopher-Bold.ttf";
    private const int Width = 1080;
    private const int Pad = 48;
    private static readonly Color Bg = new(0.04f, 0.03f, 0.11f, 1f);
    private static readonly Color Cream = new(0.918f, 0.890f, 0.824f);
    private static readonly Color Gold = new(0.902f, 0.761f, 0.290f);
    private static readonly Color TitleShadow = new(0.06f, 0.03f, 0.14f, 0.92f);

    public static string StoreUrl =>
        string.IsNullOrWhiteSpace(AppConfig.Current?.OtherAppsUrl)
            ? DefaultStoreUrl
            : AppConfig.Current.OtherAppsUrl.Trim();

    public static string ShareCaption(string summary = "")
    {
        var line = string.IsNullOrWhiteSpace(summary) ? Brand : summary.Trim();
        return $"{line}\n\n{Brand}\n{StoreUrl}";
    }

    public static async Task<string> ExportAsync(Node host, Image screenshot, string body, string summary)
    {
        if (host == null || !GodotObject.IsInstanceValid(host) || screenshot == null || screenshot.IsEmpty())
            return "";

        var tree = host.GetTree();
        if (tree?.Root == null)
            return "";

        var innerW = Width - Pad * 2;
        var shotH = Mathf.Max(240, Mathf.RoundToInt(innerW * screenshot.GetHeight() / (float)Mathf.Max(1, screenshot.GetWidth())));
        if (screenshot.GetFormat() != Image.Format.Rgba8)
            screenshot.Convert(Image.Format.Rgba8);
        var shotTex = ImageTexture.CreateFromImage(screenshot);
        var titleFont = LoadTitleFont();
        var shareBody = CombineText(body, summary);

        var vp = new SubViewport
        {
            TransparentBg = false,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(Width, 1600),
        };
        tree.Root.AddChild(vp);

        var root = new ColorRect { Color = Bg, Size = new Vector2(Width, 1600) };
        vp.AddChild(root);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", Pad);
        margin.AddThemeConstantOverride("margin_right", Pad);
        margin.AddThemeConstantOverride("margin_top", Pad);
        margin.AddThemeConstantOverride("margin_bottom", Pad);
        root.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 28);
        margin.AddChild(col);

        col.AddChild(BuildShotWithTitle(shotTex, titleFont, innerW, shotH));

        if (!string.IsNullOrWhiteSpace(shareBody))
        {
            var text = new Label
            {
                Text = shareBody.Trim(),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(innerW, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            text.AddThemeFontSizeOverride("font_size", 42);
            text.AddThemeColorOverride("font_color", Cream);
            col.AddChild(text);
        }

        var brand = new Label
        {
            Text = Brand,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        if (titleFont != null)
            brand.AddThemeFontOverride("font", titleFont);
        brand.AddThemeFontSizeOverride("font_size", 48);
        brand.AddThemeColorOverride("font_color", Gold);
        col.AddChild(brand);

        var store = new Label
        {
            Text = StoreUrl,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(innerW, 0),
        };
        store.AddThemeFontSizeOverride("font_size", 28);
        store.AddThemeColorOverride("font_color", new Color(Gold.R, Gold.G, Gold.B, 0.85f));
        col.AddChild(store);

        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var contentH = Mathf.Max(shotH + 360, Mathf.CeilToInt(col.GetCombinedMinimumSize().Y) + Pad * 2 + 8);
        vp.Size = new Vector2I(Width, contentH);
        root.Size = new Vector2(Width, contentH);
        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

        Image? img = null;
        var texOut = vp.GetTexture();
        if (texOut != null)
            img = texOut.GetImage();
        vp.QueueFree();
        if (img == null || img.IsEmpty())
        {
            GD.PushWarning("ReadingShareExport: empty capture");
            return "";
        }

        if (img.GetFormat() != Image.Format.Rgba8)
            img.Convert(Image.Format.Rgba8);

        var fileName = $"magicalball_share_{(long)Time.GetUnixTimeFromSystem()}.png";
        var userPath = $"user://{fileName}";
        var err = img.SavePng(userPath);
        if (err != Error.Ok)
        {
            GD.PushWarning($"ReadingShareExport: save_png failed ({err})");
            return "";
        }

        if (!FileAccess.FileExists(userPath))
            return "";

        if (OS.HasFeature("Android"))
            return OS.GetUserDataDir().PathJoin(fileName);
        return ProjectSettings.GlobalizePath(userPath);
    }

    private static Control BuildShotWithTitle(Texture2D shotTex, Font? titleFont, int innerW, int shotH)
    {
        var frame = new Control
        {
            CustomMinimumSize = new Vector2(innerW, shotH),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };

        var art = new TextureRect
        {
            Texture = shotTex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        art.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddChild(art);

        var veil = new ColorRect
        {
            Color = new Color(0.04f, 0.02f, 0.10f, 0.28f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddChild(veil);

        var title = new Label
        {
            Text = Brand,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        title.OffsetTop = shotH * 0.08f;
        title.OffsetBottom = -shotH * 0.55f;
        if (titleFont != null)
            title.AddThemeFontOverride("font", titleFont);
        title.AddThemeFontSizeOverride("font_size", 96);
        title.AddThemeColorOverride("font_color", Gold);
        title.AddThemeColorOverride("font_outline_color", TitleShadow);
        title.AddThemeConstantOverride("outline_size", 14);
        title.AddThemeColorOverride("font_shadow_color", TitleShadow);
        title.AddThemeConstantOverride("shadow_offset_x", 0);
        title.AddThemeConstantOverride("shadow_offset_y", 6);
        frame.AddChild(title);

        return frame;
    }

    private static FontFile? LoadTitleFont()
    {
        if (!ResourceLoader.Exists(TitleFontPath) && !FileAccess.FileExists(TitleFontPath))
            return null;

        var loaded = GD.Load<FontFile>(TitleFontPath);
        if (loaded != null)
            return loaded;

        var font = new FontFile();
        var err = font.LoadDynamicFont(TitleFontPath);
        return err == Error.Ok ? font : null;
    }

    private static string CombineText(string body, string summary)
    {
        body = (body ?? "").Trim();
        summary = (summary ?? "").Trim();
        if (string.IsNullOrEmpty(summary))
            return body;
        if (string.IsNullOrEmpty(body))
            return summary;
        return $"{summary}\n\n{body}";
    }
}
