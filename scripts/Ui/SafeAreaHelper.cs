using Godot;

namespace CrystalBall.Ui;

public readonly record struct SafeInsets(int Left, int Top, int Right, int Bottom);

/// <summary>
/// Notch / status / 3-button nav / gesture home-indicator → viewport margins.
/// Background stays full-bleed; tappable chrome sits inside GetSafeRect.
/// </summary>
public static class SafeAreaHelper
{
    public const string HostGroup = "safe_area_host";
    public const string ApplyMethod = "ApplySafeArea";
    public const float DesignWidth = 720f;

    private const float FloorL = 16f;
    private const float FloorT = 36f;
    private const float FloorR = 16f;
    private const float FloorB = 48f;
    private const float Comfort = 10f;

    private static int _androidBottomWinPx = -1;

    public static void RefreshInsets()
    {
        if (OS.GetName() != "Android")
        {
            _androidBottomWinPx = 0;
            return;
        }

        _androidBottomWinPx = ProbeAndroidBottomInsetWinPx();
        GD.Print($"[SafeArea] bottom_win_px={_androidBottomWinPx} safe={DisplayServer.GetDisplaySafeArea()}");
    }

    public static void RelayoutTree(SceneTree? tree)
    {
        RefreshInsets();
        tree?.CallGroup(HostGroup, ApplyMethod);
    }

    public static SafeInsets GetInsets(Node host, SafeInsets? extra = null)
    {
        var vp = ViewportSize(host);
        var sys = SystemMargins(vp);
        var extraInsets = extra ?? default;
        return new SafeInsets(
            Mathf.Max(DesignPx(FloorL, vp.X), sys.Left) + DesignPx(Comfort, vp.X) + extraInsets.Left,
            Mathf.Max(DesignPx(FloorT, vp.X), sys.Top) + DesignPx(Comfort, vp.X) + extraInsets.Top,
            Mathf.Max(DesignPx(FloorR, vp.X), sys.Right) + DesignPx(Comfort, vp.X) + extraInsets.Right,
            Mathf.Max(DesignPx(FloorB, vp.X), sys.Bottom) + DesignPx(Comfort, vp.X) + extraInsets.Bottom
        );
    }

    public static Rect2 GetSafeRect(Node host, SafeInsets? extra = null)
    {
        var vp = ViewportSize(host);
        var m = GetInsets(host, extra);
        var x = m.Left;
        var y = m.Top;
        var w = Mathf.Max(1f, vp.X - m.Left - m.Right);
        var h = Mathf.Max(1f, vp.Y - m.Top - m.Bottom);
        return new Rect2(x, y, w, h);
    }

    public static SafeInsets Apply(MarginContainer container, Node host, SafeInsets? extra = null)
    {
        var m = GetInsets(host, extra);
        if (container == null)
            return m;

        container.AddThemeConstantOverride("margin_left", m.Left);
        container.AddThemeConstantOverride("margin_top", m.Top);
        container.AddThemeConstantOverride("margin_right", m.Right);
        container.AddThemeConstantOverride("margin_bottom", m.Bottom);
        return m;
    }

    private static Vector2 ViewportSize(Node host)
    {
        if (host is CanvasItem item)
        {
            var rect = item.GetViewportRect();
            if (rect.Size.X > 1f && rect.Size.Y > 1f)
                return rect.Size;
        }

        var vp = host.GetViewport();
        if (vp != null)
        {
            var visible = vp.GetVisibleRect().Size;
            if (visible.X > 1f && visible.Y > 1f)
                return visible;
        }

        return new Vector2(720f, 1600f);
    }

    private static SafeInsets SystemMargins(Vector2 vpSize)
    {
        var win = (Vector2)DisplayServer.WindowGetSize();
        if (win.X < 1f || win.Y < 1f)
            win = vpSize;

        var safe = DisplayServer.GetDisplaySafeArea();
        int left = 0, top = 0, right = 0, bottom = 0;
        if (safe.Size.X >= 1 && safe.Size.Y >= 1)
        {
            var sx = vpSize.X / win.X;
            var sy = vpSize.Y / win.Y;
            left = Mathf.Max(0, Mathf.RoundToInt(safe.Position.X * sx));
            top = Mathf.Max(0, Mathf.RoundToInt(safe.Position.Y * sy));
            right = Mathf.Max(0, Mathf.RoundToInt((win.X - (safe.Position.X + safe.Size.X)) * sx));
            bottom = Mathf.Max(0, Mathf.RoundToInt((win.Y - (safe.Position.Y + safe.Size.Y)) * sy));
        }

        if (OS.GetName() == "Android")
        {
            if (_androidBottomWinPx < 0)
                _androidBottomWinPx = ProbeAndroidBottomInsetWinPx();
            if (_androidBottomWinPx > 0)
            {
                var sy = vpSize.Y / Mathf.Max(1f, win.Y);
                bottom = Mathf.Max(bottom, Mathf.RoundToInt(_androidBottomWinPx * sy));
            }
        }

        return new SafeInsets(left, top, right, bottom);
    }

    private static int DesignPx(float value, float vpWidth)
    {
        var w = vpWidth > 1f ? vpWidth : DesignWidth;
        return Mathf.Max(1, Mathf.RoundToInt(value * w / DesignWidth));
    }

    private static int ProbeAndroidBottomInsetWinPx()
    {
        try
        {
            if (!Engine.HasSingleton("AndroidRuntime"))
                return 0;

            var runtime = Engine.GetSingleton("AndroidRuntime");
            var activity = runtime.Call("getActivity").AsGodotObject();
            if (activity == null)
                return 0;
            var window = activity.Call("getWindow").AsGodotObject();
            if (window == null)
                return 0;
            var decor = window.Call("getDecorView").AsGodotObject();
            if (decor == null)
                return 0;
            var root = decor.Call("getRootWindowInsets").AsGodotObject();
            if (root == null)
                return 0;

            var bottom = 0;
            bottom = Math.Max(bottom, InsetBottom(root.Call("getMandatorySystemGestureInsets")));
            bottom = Math.Max(bottom, InsetBottom(root.Call("getSystemGestureInsets")));
            bottom = Math.Max(bottom, root.Call("getSystemWindowInsetBottom").AsInt32());

            if (Engine.HasSingleton("JavaClassWrapper"))
            {
                var wrapper = Engine.GetSingleton("JavaClassWrapper");
                var type = wrapper.Call("wrap", "android.view.WindowInsets$Type").AsGodotObject();
                if (type != null)
                {
                    bottom = Math.Max(bottom, InsetBottom(root.Call("getInsets", type.Call("navigationBars"))));
                    bottom = Math.Max(bottom, InsetBottom(root.Call("getInsets", type.Call("systemBars"))));
                    bottom = Math.Max(bottom, InsetBottom(root.Call("getInsets", type.Call("systemGestures"))));
                    bottom = Math.Max(bottom, InsetBottom(root.Call("getInsets", type.Call("tappableElement"))));
                }
            }

            return bottom;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SafeArea] Android insets: {ex.Message}");
            return 0;
        }
    }

    private static int InsetBottom(Variant inset)
    {
        var obj = inset.AsGodotObject();
        if (obj == null)
            return 0;
        try
        {
            return Math.Max(0, obj.Get("bottom").AsInt32());
        }
        catch
        {
            return 0;
        }
    }
}
