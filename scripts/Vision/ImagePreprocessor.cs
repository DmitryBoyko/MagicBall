using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Vision;

/// <summary>
/// Готовит кадр для MobileNetV2: 224×224, RGB8, нормализация ImageNet, тензор NCHW.
/// Godot Image — только с главного потока; байтовый тензор/палитра можно считать в фоне.
/// </summary>
public sealed class ImagePreprocessor
{
    public const string TestTexturePath = "res://assets/test/sample_photo.png";

    public readonly record struct PreparedFrame(byte[] Rgb224, int Width, int Height);

    private static readonly float[] ImageNetMean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] ImageNetStd = [0.229f, 0.224f, 0.225f];

    public Image LoadFallbackImage() =>
        TryLoadTestTexture() ?? CreateFallbackImage();

    public Image LoadSourceImage()
    {
        var paths = ListLatestGalleryPaths(1);
        if (paths.Count > 0)
        {
            var loaded = TryLoadFile(paths[0]);
            if (loaded != null)
                return loaded;
        }

        return LoadFallbackImage();
    }

    public Image? TryLoadFile(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path))
                return null;
            var image = new Image();
            return image.Load(path) == Error.Ok ? image : null;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ImagePreprocessor] {path}: {ex.Message}");
            return null;
        }
    }

    public PreparedFrame? TryPrepareFrame(string path)
    {
        var image = TryLoadFile(path);
        if (image == null)
            return null;
        try
        {
            return PrepareRgb224(image);
        }
        finally
        {
            image.Dispose();
        }
    }

    public PreparedFrame PrepareFallbackFrame()
    {
        var image = LoadFallbackImage();
        try
        {
            return PrepareRgb224(image);
        }
        finally
        {
            image.Dispose();
        }
    }

    public List<string> ListLatestGalleryPaths(int take)
    {
        take = Math.Clamp(take, 1, AppConfig.MaxPhotoLookback);
        var found = new List<(string Path, ulong Time)>();
        CollectImages(OS.GetSystemDir(OS.SystemDir.Dcim), 0, found, stopAfter: 48);
        CollectImages(OS.GetSystemDir(OS.SystemDir.Pictures), 0, found, stopAfter: 48);
        return found
            .DistinctBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(row => row.Time)
            .Take(take)
            .Select(row => row.Path)
            .ToList();
    }

    /// <summary>Тот же скан, но с yield каждые N файлов — без фриза UI.</summary>
    public async Task<List<string>> ListLatestGalleryPathsAsync(int take)
    {
        take = Math.Clamp(take, 1, AppConfig.MaxPhotoLookback);
        if (OS.GetName() == "Android")
        {
            var media = AndroidMediaStoreBridge.ListRecentPaths(take);
            if (media.Count > 0)
                return media;
        }

        var found = new List<(string Path, ulong Time)>();
        await CollectImagesAsync(OS.GetSystemDir(OS.SystemDir.Dcim), 0, found, stopAfter: 48).ConfigureAwait(true);
        await CollectImagesAsync(OS.GetSystemDir(OS.SystemDir.Pictures), 0, found, stopAfter: 48).ConfigureAwait(true);
        return found
            .DistinctBy(row => row.Path, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(row => row.Time)
            .Take(take)
            .Select(row => row.Path)
            .ToList();
    }

    public float[] ToNchwTensor(Image source)
    {
        var prepared = PrepareRgb224(source);
        return ToNchwTensorFromRgb(prepared.Rgb224, prepared.Width, prepared.Height);
    }

    public static float[] ToNchwTensorFromRgb(byte[] pixels, int width, int height)
    {
        if (pixels == null)
            throw new ArgumentNullException(nameof(pixels));
        if (width != AppConfig.ImageSize || height != AppConfig.ImageSize)
            throw new ArgumentException($"Ожидался RGB {AppConfig.ImageSize}×{AppConfig.ImageSize}.");
        if (pixels.Length < width * height * 3)
            throw new ArgumentException("Недостаточно байт RGB.", nameof(pixels));

        var tensor = new float[AppConfig.TensorLength];
        const int size = AppConfig.ImageSize;
        const int plane = size * size;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var src = (y * size + x) * 3;
                var spatial = y * size + x;
                for (var c = 0; c < 3; c++)
                {
                    var normalized = (pixels[src + c] / 255f - ImageNetMean[c]) / ImageNetStd[c];
                    tensor[c * plane + spatial] = normalized;
                }
            }
        }

        return tensor;
    }

    public PhotoAnalysis Describe(Image source, string rawTag, string mysticTag)
    {
        var (palette, luminance) = SampleVisuals(source);
        return new PhotoAnalysis
        {
            RawTag = rawTag,
            MysticTag = mysticTag,
            ColorPalette = FormatPalette(palette),
            LuminanceVibe = FormatLuminance(luminance),
        };
    }

    public (Dictionary<string, int> Palette, double Luminance) SampleVisuals(Image source)
    {
        using var work = new Image();
        work.CopyFrom(source);
        work.Resize(32, 32, Image.Interpolation.Nearest);
        if (work.GetFormat() != Image.Format.Rgb8)
            work.Convert(Image.Format.Rgb8);
        return SampleVisualsFromRgb(work.GetData(), work.GetWidth(), work.GetHeight());
    }

    public static (Dictionary<string, int> Palette, double Luminance) SampleVisualsFromRgb(
        byte[] pixels, int width, int height)
    {
        var buckets = new Dictionary<string, int>();
        double sum = 0;
        var count = Math.Max(1, width * height);
        // Даунсэмпл до ~32×32 без Godot Image — безопасно в фоне.
        var stepX = Math.Max(1, width / 32);
        var stepY = Math.Max(1, height / 32);
        var samples = 0;
        for (var y = 0; y < height; y += stepY)
        {
            for (var x = 0; x < width; x += stepX)
            {
                var src = (y * width + x) * 3;
                var r = pixels[src] / 255f;
                var g = pixels[src + 1] / 255f;
                var b = pixels[src + 2] / 255f;
                var color = new Color(r, g, b);
                var name = NameColor(color);
                buckets[name] = buckets.GetValueOrDefault(name) + 1;
                sum += color.Luminance;
                samples++;
            }
        }

        return (buckets, sum / Math.Max(samples, 1));
    }

    public static PhotoAnalysis Merge(IReadOnlyList<PhotoFrame> frames, bool fromGallery = false)
    {
        if (frames.Count == 0)
            return new PhotoAnalysis
            {
                RawTag = "unknown object",
                MysticTag = MysticTagConverter.UnknownArchetype,
                ColorPalette = "глубокий черный",
                LuminanceVibe = FormatLuminance(0),
            };

        var palette = new Dictionary<string, int>();
        double lum = 0;
        foreach (var frame in frames)
        {
            lum += frame.Luminance;
            foreach (var (name, votes) in frame.Palette)
                palette[name] = palette.GetValueOrDefault(name) + votes;
        }

        return new PhotoAnalysis
        {
            RawTag = VoteNewestWins(frames.Select(f => f.RawTag)),
            MysticTag = VoteNewestWins(frames.Select(f => f.MysticTag)),
            ColorPalette = FormatPalette(palette),
            LuminanceVibe = FormatLuminance(lum / frames.Count),
            FromGallery = fromGallery,
        };
    }

    public Image? TryLoadLatestGalleryImage()
    {
        var paths = ListLatestGalleryPaths(1);
        return paths.Count == 0 ? null : TryLoadFile(paths[0]);
    }

    private static PreparedFrame PrepareRgb224(Image source)
    {
        using var work = new Image();
        work.CopyFrom(source);
        if (work.GetFormat() != Image.Format.Rgb8)
            work.Convert(Image.Format.Rgb8);

        // Bilinear быстрее Lanczos — меньше фриз на main thread.
        work.Resize(AppConfig.ImageSize, AppConfig.ImageSize, Image.Interpolation.Bilinear);
        if (work.GetFormat() != Image.Format.Rgb8)
            work.Convert(Image.Format.Rgb8);

        return new PreparedFrame(work.GetData(), work.GetWidth(), work.GetHeight());
    }

    private static void CollectImages(string directory, int depth, List<(string Path, ulong Time)> into, int stopAfter = 64)
    {
        const int maxDepth = 2;
        if (into.Count >= stopAfter)
            return;
        if (string.IsNullOrEmpty(directory) || depth > maxDepth || !DirAccess.DirExistsAbsolute(directory))
            return;

        using var dir = DirAccess.Open(directory);
        if (dir == null)
            return;

        dir.ListDirBegin();
        while (into.Count < stopAfter)
        {
            var name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
                break;
            if (name is "." or "..")
                continue;

            var full = directory.TrimEnd('/', '\\') + "/" + name;
            if (dir.CurrentIsDir())
            {
                if (IsSkippedDir(name))
                    continue;
                CollectImages(full, depth + 1, into, stopAfter);
                continue;
            }

            if (!IsImageName(name))
                continue;
            into.Add((full, FileAccess.GetModifiedTime(full)));
        }

        dir.ListDirEnd();
    }

    private static async Task CollectImagesAsync(
        string directory, int depth, List<(string Path, ulong Time)> into, int stopAfter = 64)
    {
        const int maxDepth = 2;
        if (into.Count >= stopAfter)
            return;
        if (string.IsNullOrEmpty(directory) || depth > maxDepth || !DirAccess.DirExistsAbsolute(directory))
            return;

        using var dir = DirAccess.Open(directory);
        if (dir == null)
            return;

        dir.ListDirBegin();
        var steps = 0;
        while (into.Count < stopAfter)
        {
            var name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
                break;
            if (name is "." or "..")
                continue;

            var full = directory.TrimEnd('/', '\\') + "/" + name;
            if (dir.CurrentIsDir())
            {
                if (IsSkippedDir(name))
                    continue;
                await CollectImagesAsync(full, depth + 1, into, stopAfter).ConfigureAwait(true);
                continue;
            }

            if (!IsImageName(name))
                continue;
            into.Add((full, FileAccess.GetModifiedTime(full)));
            if (++steps % 24 == 0 && Engine.GetMainLoop() is SceneTree tree)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        dir.ListDirEnd();
    }

    private static bool IsSkippedDir(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower is ".thumbnails" or "thumbnails" or ".trashed" or "cache" or "android";
    }

    private static bool IsImageName(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png") || lower.EndsWith(".webp");
    }

    private static Image? TryLoadTestTexture()
    {
        if (!ResourceLoader.Exists(TestTexturePath))
            return null;
        var texture = GD.Load<Texture2D>(TestTexturePath);
        return texture?.GetImage();
    }

    private static Image CreateFallbackImage()
    {
        var image = Image.CreateEmpty(AppConfig.ImageSize, AppConfig.ImageSize, false, Image.Format.Rgb8);
        for (var y = 0; y < AppConfig.ImageSize; y++)
        {
            for (var x = 0; x < AppConfig.ImageSize; x++)
            {
                var t = x / (float)AppConfig.ImageSize;
                var s = y / (float)AppConfig.ImageSize;
                image.SetPixel(x, y, new Color(0.15f + t * 0.4f, 0.05f + s * 0.25f, 0.45f + (1f - t) * 0.4f));
            }
        }

        return image;
    }

    private static string FormatPalette(Dictionary<string, int> buckets)
    {
        var top = buckets.OrderByDescending(pair => pair.Value).Take(3).Select(pair => pair.Key);
        return string.Join(", ", top);
    }

    public static string FormatLuminance(double mean) =>
        mean >= 0.45
            ? "Яркий свет (ясность мотивов)"
            : "Глубокий сумрак (скрытые тайны)";

    private static string VoteNewestWins(IEnumerable<string> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (list.Count == 0)
            return string.Empty;

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var value in list)
            counts[value] = counts.GetValueOrDefault(value) + 1;

        var best = 0;
        var winner = list[0];
        foreach (var value in list)
        {
            var n = counts[value];
            if (n <= best)
                continue;
            best = n;
            winner = value;
        }

        return winner;
    }

    private static string NameColor(Color color)
    {
        if (color.Luminance < 0.12f)
            return "глубокий черный";
        if (color.Luminance > 0.88f)
            return "холодный белый";

        var h = color.H;
        if (h < 0.05f || h >= 0.95f)
            return "неоновый алый";
        if (h < 0.12f)
            return "малиновый";
        if (h < 0.18f)
            return "янтарное золото";
        if (h < 0.35f)
            return "изумрудный";
        if (h < 0.55f)
            return "глубокий бирюзовый";
        if (h < 0.75f)
            return "фиолетовый";
        return "пурпурный";
    }
}
