using System.Text.Json;
using Godot;

namespace CrystalBall.App;

public sealed class AppSettings
{
    public bool MusicEnabled { get; set; } = true;
    public string BackgroundPreset { get; set; } = "auto";
}

public static class AppSettingsStore
{
    public const string Path = "user://app_settings.json";

    public static AppSettings Current { get; private set; } = new();

    public static AppSettings Load()
    {
        if (!FileAccess.FileExists(Path))
        {
            Current = new AppSettings();
            return Current;
        }

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            Current = new AppSettings();
            return Current;
        }

        try
        {
            Current = JsonSerializer.Deserialize<AppSettings>(file.GetAsText()) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AppSettingsStore] {ex.Message}");
            Current = new AppSettings();
        }

        return Current;
    }

    public static void Save(AppSettings settings)
    {
        Current = settings;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError("[AppSettingsStore] Не удалось записать настройки.");
            return;
        }

        file.StoreString(JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}
