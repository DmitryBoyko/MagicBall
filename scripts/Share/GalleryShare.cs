using Godot;

namespace CrystalBall.Share;

public static class GalleryShare
{
    private const string ScriptPath = "res://scripts/Share/android_gallery_share.gd";

    public static void Launch(Node host, string absoluteImagePath, string chooserTitle, string shareText = "")
    {
        var script = GD.Load<GDScript>(ScriptPath);
        if (script == null)
        {
            GD.PushWarning("GalleryShare: android_gallery_share.gd не загрузился");
            host.CallDeferred("_on_gallery_android_result", false, "share script missing");
            return;
        }

        var instance = script.New().AsGodotObject();
        instance.Call("launch", host, absoluteImagePath, chooserTitle, shareText ?? "");
    }
}
