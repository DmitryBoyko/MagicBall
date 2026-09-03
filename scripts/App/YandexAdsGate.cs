using Godot;

namespace CrystalBall.App;

public static class YandexAdsGate
{
    public const string NodePath = "/root/YandexAdsManager";

    public static async Task<bool> ShowRequiredRewardedAsync(Node host, Ui.AdOverlay overlay)
    {
        if (OS.GetName() == "Android")
        {
            var ads = host.GetNodeOrNull(NodePath);
            if (ads == null)
                return false;

            ads.Call("show_rewarded_for_ai");
            var args = await host.ToSignal(ads, "rewarded_flow_finished");
            return args.Length > 0 && args[0].AsBool();
        }

        return await overlay.ShowRewardedAsync();
    }
}
