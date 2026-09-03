using System.Text.Json;
using Godot;

namespace CrystalBall.Profile;

public static class ProfileStore
{
    public const string Path = "user://user_profile.json";

    public static UserProfile? Current { get; private set; }

    public static bool Exists => FileAccess.FileExists(Path);

    public static UserProfile? Load()
    {
        if (!FileAccess.FileExists(Path))
        {
            Current = null;
            return null;
        }

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            Current = null;
            return null;
        }

        try
        {
            Current = JsonSerializer.Deserialize<UserProfile>(file.GetAsText());
            if (Current != null && DateTime.TryParse(Current.BirthDate, out var birth))
                AstroCalculator.Populate(Current, birth);
            return Current;
        }
        catch (Exception ex)
        {
            GD.PushError($"[ProfileStore] Повреждён профиль: {ex.Message}");
            Current = null;
            return null;
        }
    }

    public static void Save(UserProfile profile)
    {
        if (DateTime.TryParse(profile.BirthDate, out var birth))
            AstroCalculator.Populate(profile, birth);

        Current = profile;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError("[ProfileStore] Не удалось записать user_profile.json");
            return;
        }

        file.StoreString(JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            WriteIndented = true,
        }));
    }
}
