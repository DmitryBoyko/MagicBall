using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ui;

public sealed class BackgroundController
{
    public const string Root = "res://assets/backgrounds/";

    public Texture2D Resolve(string preset)
    {
        var part = TimeOfDayCatalog.ParsePreset(preset, DateTime.Now);
        var folder = Root + TimeOfDayCatalog.FolderName(part) + "/";
        var fromDisk = LoadRandomFrom(folder);
        return fromDisk ?? CreateGradient(part);
    }

    private static Texture2D? LoadRandomFrom(string folder)
    {
        var names = new List<string>();
        try
        {
            foreach (var name in ResourceLoader.ListDirectory(folder))
            {
                if (IsTexture(name))
                    names.Add(folder + name);
            }
        }
        catch (Exception)
        {
            // APK / editor fallback ниже
        }

        if (names.Count == 0)
            return null;

        var path = names[Random.Shared.Next(names.Count)];
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static bool IsTexture(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".webp");
    }

    private static Texture2D CreateGradient(DayPart part)
    {
        var (top, bottom) = part switch
        {
            DayPart.Morning => (new Color(0.98f, 0.72f, 0.46f), new Color(0.42f, 0.28f, 0.55f)),
            DayPart.Day => (new Color(0.45f, 0.75f, 0.95f), new Color(0.18f, 0.38f, 0.62f)),
            DayPart.Evening => (new Color(0.92f, 0.38f, 0.28f), new Color(0.18f, 0.06f, 0.28f)),
            _ => (new Color(0.08f, 0.05f, 0.22f), new Color(0.01f, 0.01f, 0.06f)),
        };

        var gradient = new Gradient();
        gradient.SetColor(0, top);
        gradient.SetColor(1, bottom);
        return new GradientTexture2D
        {
            Gradient = gradient,
            Width = 720,
            Height = 1600,
            Fill = GradientTexture2D.FillEnum.Linear,
            FillFrom = new Vector2(0.5f, 0f),
            FillTo = new Vector2(0.5f, 1f),
        };
    }
}
