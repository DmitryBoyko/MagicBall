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
    private SunSparks _sun = null!;
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
    private CastingLogSheet _casting = null!;
    private ContextManager _context = null!;
    private readonly BackgroundController _backgrounds = new();
    private OracleResult? _lastResult;
    private OracleStep _step = OracleStep.Ask;
    private bool _askSettling;
    private Tween? _askSettleTween;
    private float _askBottomY;
    private const float SunFadeSeconds = 1.15f;

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
    /// <summary>Как фото игрушек на меню epkids: лёгкий sway + bob вокруг точки покоя.</summary>
    private const float IdleSwayRad = 0.035f;
    private const float IdleBobPx = 4f;
    private const float IdleBobRefHeight = 140f;
    private float _idleTime;
    private float _idlePhase;
    private const float BallIdleOpacity = 0.42f;
    private const float BallAskOpacity = 0.28f;
    private const float BallIdleLift = 0.10f;
    private const float BallAskLift = 0.38f;

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

    public override void _Process(double delta)
    {
        if (!_introFinished)
            return;
        _idleTime += (float)delta;
        ApplyIdlePose();
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
        _modal?.ApplySafeArea();
        _ads?.ApplySafeArea();
        LayoutBall();
        LayoutReadingSheet();
        LayoutCastingSheet();
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

        _sun = new SunSparks { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        AddChild(_sun);

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
        _sheet.CaptureChrome += OnShareCaptureChrome;
        AddChild(_sheet);
        _casting = new CastingLogSheet();
        AddChild(_casting);
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
        _ball.PivotOffset = _ball.Size * 0.5f;
        _sparkPad = ballSize * 0.18f;
        _sparks.Size = new Vector2(ballSize + _sparkPad * 2f, ballSize + _sparkPad * 2f);
        _sparks.SetBallRadius(ballSize * 0.5f);
        var sunPad = ballSize * 0.22f;
        _sun.Size = new Vector2(ballSize + sunPad * 2f, ballSize + sunPad * 2f);
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

        ApplyIdlePose();
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
        _introTween.TweenProperty(_sun, "position", _ballTarget - _ball.Size * 0.22f, seconds);
        _introTween.Finished += OnIntroFinished;
    }

    private void OnIntroFinished()
    {
        if (_introFinished)
            return;
        _introFinished = true;
        ApplyIdlePose();
        RefreshActionButton();
    }

    private void ApplyIdlePose()
    {
        var fade = Mathf.Clamp(_idleTime / 0.55f, 0f, 1f);
        var bobAmp = Mathf.Max(IdleBobPx, _ball.Size.Y * (IdleBobPx / IdleBobRefHeight));
        var sway = Mathf.Sin(_idleTime * 1.05f + _idlePhase) * IdleSwayRad * fade;
        var bob = Mathf.Sin(_idleTime * 1.45f + _idlePhase * 1.7f) * bobAmp * fade;
        _ball.PivotOffset = _ball.Size * 0.5f;
        _ball.Rotation = sway;
        PlaceBallCluster(_ballTarget + new Vector2(0f, bob));
    }

    private void RefreshActionButton()
    {
        if (_ask == null)
            return;

        _ask.Text = "Спросить";
        var sheetOpen = _sheet is { Visible: true } || _casting is { Visible: true };
        _ask.Visible = _introFinished && !sheetOpen;
        _ask.Disabled = !_introFinished || _step == OracleStep.Busy || _askSettling || sheetOpen;
        CacheAskBottom();
    }

    private void CacheAskBottom()
    {
        if (_ask == null || !_ask.IsVisibleInTree() || _ask.Size.Y < 8f)
            return;
        _askBottomY = _ask.GetGlobalRect().End.Y;
    }

    private void LayoutReadingSheet()
    {
        if (_sheet is { Visible: true })
        {
            CacheAskBottom();
            _sheet.LayoutReadingBand(ComputeReadingBand());
        }

        LayoutCastingSheet();
    }

    private void LayoutCastingSheet()
    {
        if (_casting is not { Visible: true })
            return;
        CacheAskBottom();
        _casting.LayoutBand(ComputeReadingBand());
    }

    private Rect2 ComputeReadingBand()
    {
        const float gapToBall = 50f;
        const float widthFrac = 0.92f;
        var ballBottom = _ballTarget.Y + _ball.Size.Y;
        var top = ballBottom + gapToBall;
        var bottom = _askBottomY;
        if (bottom < 8f && _ask != null && _ask.IsVisibleInTree())
            bottom = _ask.GetGlobalRect().End.Y;
        if (bottom < 8f)
        {
            var vp = GetViewportRect().Size.Y;
            bottom = vp - 80f;
        }

        var height = Mathf.Max(160f, bottom - top);
        var safe = SafeAreaHelper.GetSafeRect(this);
        var width = Mathf.Max(280f, safe.Size.X * widthFrac);
        var left = safe.Position.X + (safe.Size.X - width) * 0.5f;
        return new Rect2(left, top, width, height);
    }

    private void PlaceBallCluster(Vector2 ballPos)
    {
        _ball.Position = ballPos;
        _sparks.Position = SparkPosFor(ballPos);
        var sunPad = _ball.Size * 0.22f;
        _sun.Position = ballPos - sunPad;
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
        _idlePhase = rng.RandfRange(0f, Mathf.Tau);
        material.SetShaderParameter("session_seed", rng.RandfRange(0.4f, 97f));
        var spinSign = rng.Randf() < 0.5f ? -1f : 1f;
        var fogSpeed = rng.RandfRange(0.32f, 0.88f);
        material.SetShaderParameter("spin_sign", spinSign);
        material.SetShaderParameter("fog_speed", fogSpeed);
        material.SetShaderParameter("fog_density", rng.RandfRange(2.5f, 4.1f));
        material.SetShaderParameter("tumble", rng.RandfRange(0.18f, 0.55f));
        material.SetShaderParameter("light_drift", rng.RandfRange(0.22f, 0.55f));
        material.SetShaderParameter("glass_reflection", rng.RandfRange(0.42f, 0.68f));
        material.SetShaderParameter("opacity", BallIdleOpacity);
        material.SetShaderParameter("lift", BallIdleLift);
        var palette = BallPaletteCatalog.Pick(rng);
        material.SetShaderParameter("fog_color_1", palette.FogA);
        material.SetShaderParameter("fog_color_2", palette.FogB);
        SessionBallTint.Set(palette.Name, palette.Meaning);
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
        if (!_introFinished || _step == OracleStep.Busy || _askSettling)
            return;

        if (ProfileStore.Current is not { IsComplete: true } profile)
        {
            _modal.Present(editMode: false);
            return;
        }

        _step = OracleStep.Busy;
        RefreshActionButton();
        _lastResult = null;
        SetSunActive(false);
        SetBallLook(BallLook.Asking);
        _sparks.SetAsking(true);
        BeginCastingRitual();

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
                Interpretation = string.Empty,
                Summary = string.Empty,
                OsirisPresent = false,
                Source = "synthesized",
                FallbackUsed = true,
                FallbackReason = ex.Message,
            };
        }

        EndCastingRitual();
        ApplyOracleOutcome(_lastResult);
    }

    private void BeginCastingRitual()
    {
        CacheAskBottom();
        _casting.Begin();
        LayoutCastingSheet();
        RefreshActionButton();
        CallDeferred(MethodName.LayoutCastingSheet);
    }

    private void EndCastingRitual()
    {
        _casting.Dismiss();
        RefreshActionButton();
    }

    private async void ApplyOracleOutcome(OracleResult? result)
    {
        if (result is not { HasLlmAnswer: true })
        {
            ShowFog();
            return;
        }

        var granted = await YandexAdsGate.ShowRequiredRewardedAsync(this, _ads);
        if (!granted)
        {
            _lastResult = null;
            ShowFog();
            return;
        }

        SetBallLook(BallLook.Idle);
        SetSunActive(true);
        CacheAskBottom();
        _sheet.Present(result);
        _step = OracleStep.Answer;
        LayoutReadingSheet();
        RefreshActionButton();
        CallDeferred(MethodName.LayoutReadingSheet);
    }

    private void ShowFog()
    {
        SetSunActive(false);
        SetBallLook(BallLook.Fog);
        _sparks.SetAsking(false);
        CacheAskBottom();
        _sheet.PresentFog();
        _step = OracleStep.Ask;
        LayoutReadingSheet();
        RefreshActionButton();
        CallDeferred(MethodName.LayoutReadingSheet);
    }

    private void SetSunActive(bool on) => _sun?.SetActive(on);

    private enum BallLook
    {
        Idle,
        Asking,
        Fog,
    }

    private void SetBallLook(BallLook look)
    {
        float opacity;
        float lift;
        Color ball;
        Color sparks;
        switch (look)
        {
            case BallLook.Asking:
                opacity = BallAskOpacity;
                lift = BallAskLift;
                ball = Colors.White;
                sparks = Colors.White;
                break;
            case BallLook.Fog:
                opacity = BallIdleOpacity;
                lift = 0f;
                ball = new Color(0.40f, 0.38f, 0.50f, 1f);
                sparks = new Color(0.32f, 0.34f, 0.48f, 0.45f);
                break;
            default:
                opacity = BallIdleOpacity;
                lift = BallIdleLift;
                ball = Colors.White;
                sparks = Colors.White;
                break;
        }

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(_ball, "modulate", ball, 0.45);
        tween.TweenProperty(_sparks, "modulate", sparks, 0.45);
        if (_ball.Material is ShaderMaterial mat)
        {
            tween.TweenProperty(mat, "shader_parameter/opacity", opacity, 0.45);
            tween.TweenProperty(mat, "shader_parameter/lift", lift, 0.45);
        }
    }

    private void OnReadingClosed()
    {
        SetBallLook(BallLook.Idle);
        _sparks.SetAsking(false);
        _step = OracleStep.Ask;
        _askSettling = true;
        _askSettleTween?.Kill();
        RefreshActionButton();
        if (_ask != null)
            _ask.Modulate = new Color(1f, 1f, 1f, 0.32f);

        _askSettleTween = CreateTween();
        _askSettleTween.SetEase(Tween.EaseType.Out);
        _askSettleTween.SetTrans(Tween.TransitionType.Sine);
        if (_ask != null)
            _askSettleTween.TweenProperty(_ask, "modulate", Colors.White, SunFadeSeconds);

        _sun?.FadeOut(SunFadeSeconds, OnSunFadeFinished);
    }

    private void OnSunFadeFinished()
    {
        if (!_askSettling)
            return;
        _askSettling = false;
        if (_ask != null)
            _ask.Modulate = Colors.White;
        RefreshActionButton();
    }

    private void OnShareCaptureChrome(bool capturing)
    {
        if (_uiPad != null)
            _uiPad.Visible = !capturing;
        if (_casting != null && capturing)
            _casting.Visible = false;
        if (!capturing)
        {
            RefreshActionButton();
            LayoutReadingSheet();
        }
    }

    private async Task<OracleResult> RunOracleAsync(UserProfile profile)
    {
        var casting = new CastingProgress(_casting, GetTree());

        await casting.ReportAsync(CastingStage.Name);
        await casting.ReportAsync(CastingStage.Zodiac);
        await casting.ReportAsync(CastingStage.Destiny);
        await casting.ReportAsync(CastingStage.Time);

        var geoTask = GeoLocationService.ResolveAsync();
        var weatherTask = WeatherService.ResolveAsync();

        await casting.ReportAsync(CastingStage.Geo);
        var geo = await geoTask;

        await casting.ReportAsync(CastingStage.Weather);
        var weather = await weatherTask;

        await casting.ReportAsync(CastingStage.Battery);
        await casting.ReportAsync(CastingStage.Power);
        await casting.ReportAsync(CastingStage.InquiryPulse);

        var photo = await PhotoSampler.AnalyzeRecentAsync(casting);

        var context = _context.Assemble(profile, photo);
        context.DynamicSnapshot.GeoLocationType = string.IsNullOrWhiteSpace(geo) ? null : geo;
        context.DynamicSnapshot.WeatherState = string.IsNullOrWhiteSpace(weather) ? null : weather;

        await casting.ReportAsync(CastingStage.Entropy);
        await casting.ReportAsync(CastingStage.BallMood);
        await casting.ReportAsync(CastingStage.BallTint);
        await casting.ReportAsync(CastingStage.WorldPressure);

        var gateway = GameRoot.Instance?.Gateway ?? new AiGateway(AppConfig.Current);
        return await gateway.InterpretAsync(context, casting);
    }

    private void OnOtherApps()
    {
        var url = AppConfig.Current.OtherAppsUrl;
        if (!string.IsNullOrWhiteSpace(url))
            OS.ShellOpen(url);
    }
}
