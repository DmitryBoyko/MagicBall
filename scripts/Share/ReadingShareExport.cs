using Godot;

namespace CrystalBall.Share;

/// <summary>
/// PNG для шаринга: скрин игрового экрана (без модалки) → текст ответа → «ВОЛШЕБНЫЙ ШАР».
/// </summary>
public static class ReadingShareExport
{
    public const string Brand = "ВОЛШЕБНЫЙ ШАР";
    private const int Width = 1080;
    private const int Pad = 48;
    private static readonly Color Bg = new(0.04f, 0.03f, 0.11f, 1f);
    private static readonly Color Cream = new(0.918f, 0.890f, 0.824f);
    private static readonly Color Gold = new(0.902f, 0.761f, 0.290f);

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

        var art = new TextureRect
        {
            Texture = shotTex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(innerW, shotH),
        };
        col.AddChild(art);

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
        brand.AddThemeFontSizeOverride("font_size", 34);
        brand.AddThemeColorOverride("font_color", Gold);
        col.AddChild(brand);

        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        var contentH = Mathf.Max(shotH + 280, Mathf.CeilToInt(col.GetCombinedMinimumSize().Y) + Pad * 2 + 8);
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
