using CrystalBall.Ai;
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
            ProbeSafeArea();
    }

    private void ScheduleInsetProbe(double delay)
    {
        GetTree().CreateTimer(delay).Timeout += ProbeSafeArea;
    }

    private void ProbeSafeArea()
    {
        SafeAreaHelper.RelayoutTree(GetTree());
    }

    private async void WarmupDeferred()
    {
        var engine = OnnxInferenceEngine.Instance;
        if (engine != null)
            await engine.InitializeEngineAsync();
        EngineReady = true;
    }

    public override void _ExitTree()
    {
        Gateway?.Dispose();
        if (Instance == this)
            Instance = null;
    }
}
