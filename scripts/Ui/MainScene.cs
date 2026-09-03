using CrystalBall.Ai;
using CrystalBall.AI;
using CrystalBall.App;
using CrystalBall.Context;
using CrystalBall.Profile;
using CrystalBall.Vision;
using Godot;

namespace CrystalBall.Ui;

public partial class MainScene : Control
{
    /// <summary>
    /// SUR photo warp behind the ball. Not in in-app settings — toggle here or in DevToggles.
    /// </summary>
    [Export]
    public bool WarpBackgroundBehindBall { get; set; } = DevToggles.BackgroundWarpBehindBall;

    private TextureRect _background = null!;
    private TextureRect _ball = null!;
    private BallSparks _sparks = null!;
    private VortexField _vortex = null!;
    private BackgroundWarp _warp = null!;
    private MarginContainer _uiPad = null!;
    private Button _ask = null!;
    private Button _otherApps = null!;
    private SettingsGearButton _gear = null!;
    private ProfileModal _modal = null!;
    private AdOverlay _ads = null!;
    private InterpretationSheet _sheet = null!;
    private ContextManager _context = null!;
    private readonly BackgroundController _backgrounds = new();
    private OracleResult? _lastResult;
    private OracleStep _step = OracleStep.Ask;

    private enum OracleStep
    {
        Ask,
        Busy,
        Answer,
    }
    private Tween? _introTween;
    private bool _introStarted;
    private bool _introFinished;
    private Vector2 _ballTarget;
    private float _sparkPad;
    private ulong _introStartMsec;
    private const float IntroSeconds = 2.4f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddToGroup(SafeAreaHelper.HostGroup);
        BuildTree();
        ApplyBackground();
        _modal.Saved += _ => ApplyBackground();
        GetViewport().SizeChanged += ApplySafeArea;
        if (ProfileStore.Current is not { IsComplete: true })
            _modal.Present(editMode: false);
        CallDeferred(MethodName.ApplySafeArea);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            ApplySafeArea();
    }

    public void ApplySafeArea()
    {
        if (_uiPad == null)
            return;
        SafeAreaHelper.Apply(_uiPad, this);
        _sheet?.ApplySafeArea();
        _modal?.ApplySafeArea();
        _ads?.ApplySafeArea();
        LayoutBall();
    }

    private void BuildTree()
    {
        _background = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        _warp = new BackgroundWarp();
        AddChild(_warp);

        _ball = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = MakeWhiteTexture(),
            Visible = false,
        };
        _sparks = new BallSparks { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        var shader = LoadBallShader();
        if (shader != null)
        {
            var material = new ShaderMaterial { Shader = shader };
            RandomizeBallSession(material);
            _ball.Material = material;
        }
        else
            GD.PushError("[MainScene] Не загрузился res://scripts/crystal_ball.gdshader");
        AddChild(_ball);
        AddChild(_sparks);
        _warp.Bind(_background, _ball);
        _warp.SetEnabled(WarpBackgroundBehindBall);

        _vortex = new VortexField();
        AddChild(_vortex);

        var bottom = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        bottom.SetAnchorsPreset(LayoutPreset.FullRect);
        bottom.OffsetTop = 0;
        bottom.AddThemeConstantOverride("separation", 16);
        AddChild(bottom);

        bottom.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });

        _ask = UiTheme.MakeButton("Спросить", 24);
        _ask.CustomMinimumSize = new Vector2(0, 64);
        _ask.Disabled = true;
        _ask.Pressed += OnActionPressed;
        bottom.AddChild(_ask);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 12);
        _gear = new SettingsGearButton();
        _gear.Pressed += () => _modal.Present(editMode: true);
        footer.AddChild(_gear);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _otherApps = UiTheme.MakeQuietButton("Другие приложения");
        _otherApps.Pressed += OnOtherApps;
        footer.AddChild(_otherApps);
        bottom.AddChild(footer);

        var pad = new MarginContainer();
        pad.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        RemoveChild(bottom);
        pad.AddChild(bottom);
        AddChild(pad);
        _uiPad = pad;

        CyberFrameBorder.AttachTo(this);

        _context = new ContextManager();
        AddChild(_context);
        _modal = new ProfileModal { Visible = false };
        AddChild(_modal);
        _ads = new AdOverlay();
        AddChild(_ads);
        _sheet = new InterpretationSheet();
        _sheet.Closed += OnReadingClosed;
        AddChild(_sheet);
    }

    private void LayoutBall()
    {
        var safe = SafeAreaHelper.GetSafeRect(this);
        if (safe.Size.X < 8f || safe.Size.Y < 8f)
        {
            var fallback = GetViewportRect().Size;
            if (fallback == Vector2.Zero)
                fallback = Size;
            safe = new Rect2(Vector2.Zero, fallback);
        }

        var ballSize = Mathf.Min(safe.Size.X * 0.88f, safe.Size.Y * 0.56f);
        var left = safe.Position.X + (safe.Size.X - ballSize) * 0.5f;
        var top = safe.Position.Y + safe.Size.Y * 0.02f;
        _ball.Size = new Vector2(ballSize, ballSize);
        _sparkPad = ballSize * 0.18f;
        _sparks.Size = new Vector2(ballSize + _sparkPad * 2f, ballSize + _sparkPad * 2f);
        _sparks.SetBallRadius(ballSize * 0.5f);
        _vortex.SyncSize(GetViewportRect().Size);
        _warp?.SetEnabled(WarpBackgroundBehindBall);

        _ballTarget = new Vector2(left, top);
        if (ballSize < 8f)
            return;

        if (!_introStarted)
        {
            StartBallIntro();
            return;
        }

        if (!_introFinished)
        {
            var elapsed = (Time.GetTicksMsec() - _introStartMsec) / 1000f;
            TweenBallToTarget(Mathf.Max(0.45f, IntroSeconds - elapsed));
            return;
        }

        PlaceBallCluster(_ballTarget);
    }

    private void StartBallIntro()
    {
        _introStarted = true;
        _introStartMsec = Time.GetTicksMsec();
        var vp = GetViewportRect().Size;
        if (vp.X < 8f || vp.Y < 8f)
            vp = Size;

        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var start = OffscreenStart(_ballTarget, _ball.Size, vp, rng.RandiRange(0, 3));
        PlaceBallCluster(start);
        _ball.Visible = true;
        _sparks.Visible = true;
        TweenBallToTarget(IntroSeconds);
    }

    private void TweenBallToTarget(float seconds)
    {
        if (seconds <= 0.08f)
        {
            PlaceBallCluster(_ballTarget);
            OnIntroFinished();
            return;
        }

        _introTween?.Kill();
        _introTween = CreateTween();
        _introTween.SetParallel(true);
        _introTween.SetEase(Tween.EaseType.Out);
        _introTween.SetTrans(Tween.TransitionType.Cubic);
        _introTween.TweenProperty(_ball, "position", _ballTarget, seconds);
        _introTween.TweenProperty(_sparks, "position", SparkPosFor(_ballTarget), seconds);
        _introTween.Finished += OnIntroFinished;
    }

    private void OnIntroFinished()
    {
        if (_introFinished)
            return;
        _introFinished = true;
        RefreshActionButton();
    }

    private void RefreshActionButton()
    {
        if (_ask == null)
            return;

        _ask.Text = _step == OracleStep.Answer ? "Ответ" : "Спросить";
        _ask.Visible = _introFinished && _step != OracleStep.Busy;
        _ask.Disabled = !_introFinished || _step == OracleStep.Busy;
    }

    private void PlaceBallCluster(Vector2 ballPos)
    {
        _ball.Position = ballPos;
        _sparks.Position = SparkPosFor(ballPos);
    }

    private Vector2 SparkPosFor(Vector2 ballPos) => ballPos - new Vector2(_sparkPad, _sparkPad);

    private static Vector2 OffscreenStart(Vector2 target, Vector2 ballSize, Vector2 viewport, int side)
    {
        const float gap = 48f;
        return side switch
        {
            0 => new Vector2(target.X, -ballSize.Y - gap),
            1 => new Vector2(target.X, viewport.Y + gap),
            2 => new Vector2(-ballSize.X - gap, target.Y),
            _ => new Vector2(viewport.X + gap, target.Y),
        };
    }

    private void ApplyBackground()
    {
        _background.Texture = _backgrounds.Resolve(AppSettingsStore.Current.BackgroundPreset);
    }

    private static Texture2D MakeWhiteTexture()
    {
        var image = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    private void RandomizeBallSession(ShaderMaterial material)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        material.SetShaderParameter("session_seed", rng.RandfRange(0.4f, 97f));
        var spinSign = rng.Randf() < 0.5f ? -1f : 1f;
        var fogSpeed = rng.RandfRange(0.32f, 0.88f);
        material.SetShaderParameter("spin_sign", spinSign);
        material.SetShaderParameter("fog_speed", fogSpeed);
        material.SetShaderParameter("fog_density", rng.RandfRange(2.5f, 4.1f));
        material.SetShaderParameter("tumble", rng.RandfRange(0.18f, 0.55f));
        material.SetShaderParameter("light_drift", rng.RandfRange(0.22f, 0.55f));
        material.SetShaderParameter("glass_reflection", rng.RandfRange(0.42f, 0.68f));
        var palette = BallPaletteCatalog.Pick(rng);
        material.SetShaderParameter("fog_color_1", palette.FogA);
        material.SetShaderParameter("fog_color_2", palette.FogB);
        _sparks?.Configure(spinSign, fogSpeed, palette.FogA, palette.FogB);
    }

    private static Shader? LoadBallShader()
    {
        const string path = "res://scripts/crystal_ball.gdshader";
        if (!FileAccess.FileExists(path))
            return GD.Load<Shader>(path);

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return GD.Load<Shader>(path);

        var shader = new Shader { Code = file.GetAsText() };
        return string.IsNullOrWhiteSpace(shader.Code) ? null : shader;
    }

    private async void OnActionPressed()
    {
        if (!_introFinished || _step == OracleStep.Busy)
            return;

        if (_step == OracleStep.Answer)
        {
            if (_lastResult != null)
                _sheet.Present(_lastResult);
            return;
        }

        if (ProfileStore.Current is not { IsComplete: true } profile)
        {
            _modal.Present(editMode: false);
            return;
        }

        _step = OracleStep.Busy;
        RefreshActionButton();
        _lastResult = null;

        var request = RunOracleAsync(profile);
        try
        {
            await _vortex.PlayAsync(request);
            _lastResult = await request;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[MainScene] {ex.Message}");
            _lastResult = new OracleResult
            {
                Interpretation = "Шар замер в тумане. Повтори вопрос чуть позже.",
                Summary = "Тишина тоже ответ.",
                OsirisPresent = false,
                Source = "synthesized",
                FallbackUsed = true,
                FallbackReason = ex.Message,
            };
        }

        _step = OracleStep.Answer;
        RefreshActionButton();
    }

    private void OnReadingClosed()
    {
        _step = OracleStep.Ask;
        RefreshActionButton();
    }

    private async Task<OracleResult> RunOracleAsync(UserProfile profile)
    {
        var photo = await AnalyzePhotoAsync();
        var context = await _context.AssembleAsync(profile, photo);
        var gateway = GameRoot.Instance?.Gateway ?? new AiGateway(AppConfig.Current);
        return await gateway.InterpretAsync(context);
    }

    private async Task<PhotoAnalysis> AnalyzePhotoAsync()
    {
        var preprocessor = new ImagePreprocessor();
        var image = preprocessor.LoadSourceImage();
        var tensor = preprocessor.ToNchwTensor(image);
        var engine = OnnxInferenceEngine.Instance;
        if (engine == null)
        {
            return preprocessor.Describe(image, "unknown object", MysticTagConverter.Convert("unknown object"));
        }

        if (!engine.IsInitialized)
            await engine.InitializeEngineAsync();

        var worker = new InferenceWorker(engine);
        var outcome = await worker.RunDetailedAsync(tensor);
        return preprocessor.Describe(image, outcome.EnglishTag, outcome.MysticTag);
    }

    private void OnOtherApps()
    {
        var url = AppConfig.Current.OtherAppsUrl;
        if (!string.IsNullOrWhiteSpace(url))
            OS.ShellOpen(url);
    }
}
