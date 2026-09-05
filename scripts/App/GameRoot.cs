using CrystalBall.Ai;
using CrystalBall.Context;
using CrystalBall.Profile;
using CrystalBall.Ui;
using CrystalBall.Vision;
using Godot;

namespace CrystalBall.App;

public partial class GameRoot : Node
{
    public static GameRoot? Instance { get; private set; }

    public AppConfig Config { get; private set; } = new();
    public AiGateway? Gateway { get; private set; }
    public bool EngineReady { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Config = AppConfig.Load();
        AppSettingsStore.Load();
        ProfileStore.Load();
        Gateway = new AiGateway(Config);
        CallDeferred(MethodName.WarmupDeferred);
        CallDeferred(MethodName.ProbeSafeArea);
        CallDeferred(MethodName.EnsureLocationHost);
        CallDeferred(MethodName.WarmupLocation);
        if (OS.GetName() == "Android")
        {
            ScheduleInsetProbe(0.35);
            ScheduleInsetProbe(0.9);
            ScheduleInsetProbe(2.0);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
        {
            ProbeSafeArea();
            EnsureLocationHost();
            GetNodeOrNull(LocationHostName)?.Call("kick");
            GeoLocationService.Warmup();
            WeatherService.Warmup();
            // Обновить самый свежий кадр, если пользователь вернулся из галереи.
            _ = PhotoSampler.WarmupAsync(1);
        }
    }

    private void ScheduleInsetProbe(double delay)
    {
        GetTree().CreateTimer(delay).Timeout += ProbeSafeArea;
    }

    private void ProbeSafeArea()
    {
        SafeAreaHelper.RelayoutTree(GetTree());
    }

    public const string LocationHostName = "AndroidLocationHost";
    public const string LocationScriptPath = "res://scripts/Context/android_location.gd";

    private void EnsureLocationHost()
    {
        if (OS.GetName() != "Android")
            return;
        if (GetNodeOrNull(LocationHostName) != null)
            return;
        if (!ResourceLoader.Exists(LocationScriptPath) && !FileAccess.FileExists(LocationScriptPath))
            return;

        var script = GD.Load<GDScript>(LocationScriptPath);
        if (script == null)
            return;

        var host = new Node { Name = LocationHostName };
        host.SetScript(script);
        AddChild(host);
        host.Call("kick");
    }

    private void WarmupLocation()
    {
        EnsureLocationHost();
        GetNodeOrNull(LocationHostName)?.Call("kick");
        GeoLocationService.Warmup();
        WeatherService.Warmup();
    }

    private async void WarmupDeferred()
    {
        // Дать MainScene отрисовать 2 кадра до тяжёлого ONNX (иначе «чёрный экран»).
        var tree = GetTree();
        if (tree != null)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        var engine = OnnxInferenceEngine.Instance;
        if (engine != null)
            await engine.InitializeEngineAsync();
        EngineReady = true;

        await PhotoSampler.WarmupAsync(1);
    }

    public override void _ExitTree()
    {
        Gateway?.Dispose();
        if (Instance == this)
            Instance = null;
    }
}
