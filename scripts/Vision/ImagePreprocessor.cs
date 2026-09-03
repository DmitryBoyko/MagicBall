using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Vision;

/// <summary>
/// Готовит кадр для MobileNetV2: 224×224, RGB8, нормализация ImageNet, тензор NCHW.
/// Вызывать только с главного потока Godot.
/// </summary>
public sealed class ImagePreprocessor
{
    public const string TestTexturePath = "res://assets/test/sample_photo.png";

    private static readonly float[] ImageNetMean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] ImageNetStd = [0.229f, 0.224f, 0.225f];

    public Image LoadSourceImage()
    {
        return TryLoadLatestGalleryImage()
               ?? TryLoadTestTexture()
               ?? CreateFallbackImage();
    }

    public float[] ToNchwTensor(Image source)
    {
        using var work = new Image();
        work.CopyFrom(source);
        if (work.GetFormat() != Image.Format.Rgb8)
            work.Convert(Image.Format.Rgb8);

        work.Resize(AppConfig.ImageSize, AppConfig.ImageSize, Image.Interpolation.Lanczos);
        if (work.GetFormat() != Image.Format.Rgb8)
            work.Convert(Image.Format.Rgb8);

        var pixels = work.GetData();
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
        return new PhotoAnalysis
        {
            RawTag = rawTag,
            MysticTag = mysticTag,
            ColorPalette = ExtractPalette(source),
            LuminanceVibe = ExtractLuminance(source),
        };
    }

    public Image? TryLoadLatestGalleryImage()
    {
        try
        {
            var pictures = OS.GetSystemDir(OS.SystemDir.Dcim);
            var found = FindLatestImage(pictures) ?? FindLatestImage(OS.GetSystemDir(OS.SystemDir.Pictures));
            if (string.IsNullOrEmpty(found) || !FileAccess.FileExists(found))
                return null;

            var image = new Image();
            var err = image.Load(found);
            return err == Error.Ok ? image : null;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[ImagePreprocessor] Галерея недоступна: {ex.Message}");
            return null;
        }
    }

    private static string? FindLatestImage(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !DirAccess.DirExistsAbsolute(directory))
            return null;

        using var dir = DirAccess.Open(directory);
        if (dir == null)
            return null;

        string? latest = null;
        ulong latestUnix = 0;
        dir.ListDirBegin();
        while (true)
        {
            var name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
                break;
            if (dir.CurrentIsDir())
                continue;
            if (!IsImageName(name))
                continue;

            var full = directory.TrimEnd('/', '\\') + "/" + name;
            var modified = FileAccess.GetModifiedTime(full);
            if (modified >= latestUnix)
            {
                latestUnix = modified;
                latest = full;
            }
        }

        dir.ListDirEnd();
        return latest;
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

    private static string ExtractPalette(Image source)
    {
        using var work = new Image();
        work.CopyFrom(source);
        work.Resize(32, 32, Image.Interpolation.Nearest);
        var buckets = new Dictionary<string, int>();
        for (var y = 0; y < work.GetHeight(); y++)
        {
            for (var x = 0; x < work.GetWidth(); x++)
            {
                var name = NameColor(work.GetPixel(x, y));
                buckets[name] = buckets.GetValueOrDefault(name) + 1;
            }
        }

        var top = buckets.OrderByDescending(pair => pair.Value).Take(3).Select(pair => pair.Key);
        return string.Join(", ", top);
    }

    private static string ExtractLuminance(Image source)
    {
        using var work = new Image();
        work.CopyFrom(source);
        work.Resize(32, 32, Image.Interpolation.Nearest);
        double sum = 0;
        var count = work.GetWidth() * work.GetHeight();
        for (var y = 0; y < work.GetHeight(); y++)
        {
            for (var x = 0; x < work.GetWidth(); x++)
                sum += work.GetPixel(x, y).Luminance;
        }

        var mean = sum / Math.Max(count, 1);
        return mean >= 0.45
            ? "Яркий свет (ясность мотивов)"
            : "Глубокий сумрак (скрытые тайны)";
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
