using Godot;

namespace CrystalBall.App;

/// <summary>
/// Runtime-разрешения: галерея и геолокация. Check/Request без Java SDK (только OS.*).
/// </summary>
public readonly record struct AppPermissionStatus(bool PhotosGranted, bool LocationGranted)
{
    public bool AllGranted => PhotosGranted && LocationGranted;
    public bool HasMissing => !AllGranted;
}

public static class AppPermissions
{
    public const string FineLocation = "android.permission.ACCESS_FINE_LOCATION";
    public const string CoarseLocation = "android.permission.ACCESS_COARSE_LOCATION";
    public const string ReadMediaImages = "android.permission.READ_MEDIA_IMAGES";
    public const string ReadExternalStorage = "android.permission.READ_EXTERNAL_STORAGE";
    public const string ReadMediaUserSelected = "android.permission.READ_MEDIA_VISUAL_USER_SELECTED";

    private const string SettingsScriptPath = "res://scripts/App/android_app_settings.gd";

    public static bool IsAndroid => OS.GetName() == "Android";

    public static AppPermissionStatus Check()
    {
        if (!IsAndroid)
            return new AppPermissionStatus(true, true);

        var granted = OS.GetGrantedPermissions() ?? [];
        return new AppPermissionStatus(PhotosGranted(granted), LocationGranted(granted));
    }

    public static bool RequestPhotos()
    {
        if (!IsAndroid)
            return true;
        if (Check().PhotosGranted)
            return true;

        // Оба имени: на API 33+ сработает Images, на старых — Storage. Лишний запрос система игнорирует.
        GD.Print("[AppPermissions] RequestPhotos");
        OS.RequestPermission(ReadMediaImages);
        OS.RequestPermission(ReadExternalStorage);
        return Check().PhotosGranted;
    }

    public static bool RequestLocation()
    {
        if (!IsAndroid)
            return true;
        if (Check().LocationGranted)
            return true;

        GD.Print("[AppPermissions] RequestLocation");
        OS.RequestPermission(FineLocation);
        OS.RequestPermission(CoarseLocation);
        return Check().LocationGranted;
    }

    public static bool RequestMissing()
    {
        if (!IsAndroid)
            return true;

        var status = Check();
        if (status.AllGranted)
            return true;
        if (!status.PhotosGranted)
            RequestPhotos();
        if (!status.LocationGranted)
            RequestLocation();
        return Check().AllGranted;
    }

    public static bool OpenSystemSettings()
    {
        if (!IsAndroid)
        {
            GD.PushWarning("[AppPermissions] OpenSystemSettings: not Android");
            return false;
        }

        if (!ResourceLoader.Exists(SettingsScriptPath) && !FileAccess.FileExists(SettingsScriptPath))
        {
            GD.PushWarning($"[AppPermissions] missing {SettingsScriptPath}");
            return false;
        }

        try
        {
            var script = GD.Load<GDScript>(SettingsScriptPath);
            if (script == null)
            {
                GD.PushWarning("[AppPermissions] settings script load failed");
                return false;
            }

            // Сначала static (без временного RefCounted); иначе instance.Call.
            var fromStatic = script.Call("open_details_static");
            if (fromStatic.VariantType != Variant.Type.Nil)
            {
                var okStatic = fromStatic.AsBool();
                GD.Print($"[AppPermissions] OpenSystemSettings static → {okStatic}");
                if (okStatic)
                    return true;
            }

            var instance = script.New().AsGodotObject();
            if (instance == null)
                return false;

            var ok = instance.Call("open_details").AsBool();
            GD.Print($"[AppPermissions] OpenSystemSettings → {ok}");
            return ok;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AppPermissions] OpenSystemSettings: {ex.Message}");
            return false;
        }
    }

    private static bool LocationGranted(string[] granted) =>
        Has(granted, FineLocation) || Has(granted, CoarseLocation);

    private static bool PhotosGranted(string[] granted) =>
        Has(granted, ReadMediaImages)
        || Has(granted, ReadMediaUserSelected)
        || Has(granted, ReadExternalStorage);

    private static bool Has(string[] granted, string name)
    {
        foreach (var item in granted)
        {
            if (string.Equals(item, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
