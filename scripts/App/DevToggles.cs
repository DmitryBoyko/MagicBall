namespace CrystalBall.App;

/// <summary>
/// Developer switches. Not shown on the in-app settings screen.
/// Flip values here (or the matching export on the Main node) and rerun.
/// </summary>
public static class DevToggles
{
    /// SUR-style photo warp; one emitter, pinned to the crystal ball.
    public const bool BackgroundWarpBehindBall = true;

    /// Screen-background picker in Settings. Keep the OptionButton code; hide until ready.
    public static readonly bool ShowBackgroundPresetInSettings = false;
}
