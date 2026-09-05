using Godot;

namespace CrystalBall.Vision;

/// <summary>
/// C# → android_media_store.gd: свежие пути из MediaStore.
/// </summary>
public static class AndroidMediaStoreBridge
{
    private const string ScriptPath = "res://scripts/Vision/android_media_store.gd";

    public static List<string> ListRecentPaths(int take)
    {
        var list = new List<string>();
        if (OS.GetName() != "Android" || take <= 0)
            return list;

        try
        {
            if (!ResourceLoader.Exists(ScriptPath) && !FileAccess.FileExists(ScriptPath))
                return list;

            var script = GD.Load<GDScript>(ScriptPath);
            if (script == null)
                return list;

            var instance = script.New().AsGodotObject();
            if (instance == null)
                return list;

            var variant = instance.Call("list_recent_paths", take);
            if (variant.VariantType == Variant.Type.PackedStringArray)
            {
                foreach (var path in variant.AsStringArray())
                {
                    if (!string.IsNullOrWhiteSpace(path))
                        list.Add(path);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AndroidMediaStore] {ex.Message}");
        }

        return list;
    }
}
