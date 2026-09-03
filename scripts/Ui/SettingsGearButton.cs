using Godot;

namespace CrystalBall.Ui;

public partial class SettingsGearButton : Button
{
    private static readonly Color IconColor = new(1f, 0.82f, 0.28f);
    private static readonly Color IconRim = new(1f, 0.97f, 0.91f, 0.85f);
    private static readonly Color ShadowColor = new(0.05f, 0.02f, 0f, 0.38f);

    public override void _Ready()
    {
        ClipContents = false;
        Text = string.Empty;
        Flat = true;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(72, 72);
        var empty = new StyleBoxEmpty();
        AddThemeStyleboxOverride("normal", empty);
        AddThemeStyleboxOverride("hover", empty);
        AddThemeStyleboxOverride("pressed", empty);
        AddThemeStyleboxOverride("disabled", empty);
        Resized += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        var disc = Mathf.Min(Size.X, Size.Y) * 0.88f;
        var offset = IsPressed() ? new Vector2(1.2f, 1.6f) : new Vector2(2.4f, 3.2f);
        DrawGear(center + offset, disc, ShadowColor, true);
        DrawGear(center, disc, IconColor, false);
    }

    private void DrawGear(Vector2 center, float disc, Color col, bool isShadow)
    {
        const int teeth = 8;
        var rOut = disc * 0.28f;
        var rIn = disc * 0.11f;
        var stroke = Mathf.Max(disc * 0.035f, 1.2f);
        var pts = new Vector2[teeth * 2];
        for (var i = 0; i < teeth * 2; i++)
        {
            var a = -Mathf.Pi * 0.5f + Mathf.Tau * i / (teeth * 2);
            var rr = i % 2 == 0 ? rOut : rOut * 0.72f;
            pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
        }

        DrawColoredPolygon(pts, col);
        if (isShadow)
        {
            DrawCircle(center, rIn, new Color(ShadowColor, ShadowColor.A * 0.85f));
            return;
        }

        DrawCircle(center, rIn, col.Darkened(0.12f));
        DrawArc(center, rIn + stroke, 0f, Mathf.Tau, 24, IconRim, stroke, true);
    }
}
