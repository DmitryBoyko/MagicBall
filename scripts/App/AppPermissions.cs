using Godot;

namespace CrystalBall.App;

/// <summary>
/// Runtime-разрешения: галерея (фото в промпт) и геолокация (погода / город).
/// Запрос поштучно из онбординг-модалки и из настроек.
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

    public static void RequestPhotos()
    {
        if (!IsAndroid || Check().PhotosGranted)
            return;
        foreach (var name in PhotoPermissions())
            OS.RequestPermission(name);
    }

    public static void RequestLocation()
    {
        if (!IsAndroid || Check().LocationGranted)
            return;
        OS.RequestPermission(FineLocation);
        OS.RequestPermission(CoarseLocation);
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
            return false;
        if (!FileAccess.FileExists(SettingsScriptPath))
            return false;

        var script = GD.Load<GDScript>(SettingsScriptPath);
        if (script == null)
            return false;
        var instance = script.New().AsGodotObject();
        if (instance == null)
            return false;
        return instance.Call("open_details").AsBool();
    }

    public static int AndroidSdk()
    {
        if (!IsAndroid)
            return 0;
        if (!FileAccess.FileExists(SettingsScriptPath))
            return 0;
        var script = GD.Load<GDScript>(SettingsScriptPath);
        if (script == null)
            return 0;
        var instance = script.New().AsGodotObject();
        return instance?.Call("sdk_int").AsInt32() ?? 0;
    }

    public static string[] PhotoPermissions()
    {
        var sdk = AndroidSdk();
        if (sdk >= 33)
            return [ReadMediaImages, ReadMediaUserSelected];
        return [ReadExternalStorage];
    }

    private static bool LocationGranted(string[] granted) =>
        Has(granted, FineLocation) || Has(granted, CoarseLocation);

    private static bool PhotosGranted(string[] granted)
    {
        var sdk = AndroidSdk();
        if (sdk >= 33)
            return Has(granted, ReadMediaImages);
        return Has(granted, ReadExternalStorage);
    }

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
