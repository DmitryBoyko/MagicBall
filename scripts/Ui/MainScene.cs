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
    private Label _askHint = null!;
    private Control _askHintGap = null!;
    private Button _otherApps = null!;
    private SettingsGearButton _gear = null!;
    private ProfileModal _modal = null!;
    private PermissionsOnboardingModal _permissionsOnboarding = null!;
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
    private readonly BallRipples _ballRipples = new();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddToGroup(SafeAreaHelper.HostGroup);
        BuildTree();
        ApplyBackground();
        _modal.Saved += _ => ApplyBackground();
        GetViewport().SizeChanged += ApplySafeArea;
        CallDeferred(MethodName.ApplySafeArea);
        CallDeferred(MethodName.PresentStartupModals);
    }

    private void PresentStartupModals()
    {
        var needProfile = ProfileStore.Current is not { IsComplete: true };
        var shownPerms = false;
        try
        {
            shownPerms = _permissionsOnboarding.TryPresent();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[MainScene] permissions modal: {ex.Message}");
        }

        if (shownPerms)
        {
            void OnPermsClosed()
            {
                _permissionsOnboarding.Closed -= OnPermsClosed;
                if (needProfile)
                    CallDeferred(MethodName.PresentProfileIfNeeded);
            }

            _permissionsOnboarding.Closed += OnPermsClosed;
            return;
        }

        if (needProfile)
            PresentProfileIfNeeded();
    }

    private void PresentProfileIfNeeded()
    {
        if (ProfileStore.Current is not { IsComplete: true })
            _modal.Present(editMode: false);
    }

    public override void _Process(double delta)
    {
        if (_ball.Material is ShaderMaterial rippleMat)
            _ballRipples.Tick(delta, rippleMat);

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
        _permissionsOnboarding?.ApplySafeArea();
        _ads?.ApplySafeArea();
        LayoutBall();
        LayoutReadingSheet();
        LayoutCastingSheet();
    }

    private void BuildTree()
    {
        // Пока Resolve() не поставил фон суток — тёмная подложка под цвет splash.
        var baseFill = new ColorRect
        {
            Color = new Color(0.08f, 0.04f, 0.16f, 1f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        baseFill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(baseFill);

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
            MouseFilter = MouseFilterEnum.Stop,
            Texture = MakeWhiteTexture(),
            Visible = false,
        };
        _ball.GuiInput += OnBallGuiInput;
        _sparks = new BallSparks { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        var shader = LoadBallShader();
        if (shader != null)
        {
            var material = new ShaderMaterial { Shader = shader };
            RandomizeBallSession(material);
            _ballRipples.Reset();
            _ballRipples.Tick(0, material);
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
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bottom.SetAnchorsPreset(LayoutPreset.FullRect);
        bottom.OffsetTop = 0;
        bottom.AddThemeConstantOverride("separation", 16);
        AddChild(bottom);

        bottom.AddChild(new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });

        _askHint = UiTheme.MakeLabel("", UiTheme.FontAskHint, new Color(UiTheme.Cream.R, UiTheme.Cream.G, UiTheme.Cream.B, 0.78f));
        _askHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _askHint.MouseFilter = MouseFilterEnum.Ignore;
        _askHint.MaxLinesVisible = 2;
        _askHint.Visible = false;
        var hintRng = new RandomNumberGenerator();
        hintRng.Randomize();
        _askHint.Text = AskFocusCatalog.Pick(hintRng);

        _ask = UiTheme.MakeButton("Спросить", 24);
        _ask.CustomMinimumSize = new Vector2(0, 64);
        _ask.Disabled = true;
        _ask.Pressed += OnActionPressed;

        var askBlock = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        askBlock.AddThemeConstantOverride("separation", 0);
        _askHintGap = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(0, 200),
            Visible = false,
        };
        askBlock.AddChild(_askHint);
        askBlock.AddChild(_askHintGap);
        askBlock.AddChild(_ask);
        bottom.AddChild(askBlock);

        var footer = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        footer.AddThemeConstantOverride("separation", 12);
        _gear = new SettingsGearButton();
        _gear.Pressed += () => _modal.Present(editMode: true);
        footer.AddChild(_gear);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _otherApps = UiTheme.MakeQuietButton("Другие приложения", 22);
        _otherApps.CustomMinimumSize = new Vector2(0, 56);
        _otherApps.Pressed += OnOtherApps;
        footer.AddChild(_otherApps);
        bottom.AddChild(footer);

        var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
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
        _permissionsOnboarding = new PermissionsOnboardingModal { Visible = false };
        AddChild(_permissionsOnboarding);
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
        GetNodeOrNull<AudioService>("/root/AudioService")?.NotifyUiReady();
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
        CallDeferred(MethodName.PrewarmVortex);
    }

    private void OnBallGuiInput(InputEvent @event)
    {
        if (!_introFinished || !_ball.Visible)
            return;

        var localEvent = _ball.MakeInputLocal(@event);
        Vector2 local;
        if (localEvent is InputEventScreenTouch { Pressed: true } touch)
            local = touch.Position;
        else if (localEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
            local = mouse.Position;
        else
            return;

        var size = _ball.Size;
        if (size.X < 4f || size.Y < 4f)
            return;

        var uv = new Vector2(local.X / size.X, local.Y / size.Y);
        if (!_ballRipples.TryAddAtUv(uv))
            return;

        if (_ball.Material is ShaderMaterial mat)
            _ballRipples.Tick(0, mat);
        _ball.AcceptEvent();
    }

    private void PrewarmVortex() => _vortex?.Prewarm();

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
        if (_askHint != null)
            _askHint.Visible = _ask.Visible && _step == OracleStep.Ask;
        if (_askHintGap != null)
            _askHintGap.Visible = _ask.Visible;
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
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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
        var faded = new Color(1f, 1f, 1f, 0.32f);
        if (_ask != null)
            _ask.Modulate = faded;
        if (_askHint != null)
            _askHint.Modulate = faded;

        _askSettleTween = CreateTween();
        _askSettleTween.SetEase(Tween.EaseType.Out);
        _askSettleTween.SetTrans(Tween.TransitionType.Sine);
        if (_ask != null)
            _askSettleTween.TweenProperty(_ask, "modulate", Colors.White, SunFadeSeconds);
        if (_askHint != null)
            _askSettleTween.TweenProperty(_askHint, "modulate", Colors.White, SunFadeSeconds);

        _sun?.FadeOut(SunFadeSeconds, OnSunFadeFinished);
    }

    private void OnSunFadeFinished()
    {
        if (!_askSettling)
            return;
        _askSettling = false;
        if (_ask != null)
            _ask.Modulate = Colors.White;
        if (_askHint != null)
            _askHint.Modulate = Colors.White;
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
        var geoTask = GeoLocationService.ResolveForAskAsync();
        var weatherTask = WeatherService.ResolveForAskAsync();

        static bool Ok(string? value) => !string.IsNullOrWhiteSpace(value);

        await casting.ReportAsync(CastingStage.Name, Ok(profile.UserName));
        await casting.ReportAsync(CastingStage.Zodiac, Ok(profile.ZodiacSign) || Ok(profile.BirthDate));
        await casting.ReportAsync(CastingStage.Destiny, profile.DestinyNumber != 0 || Ok(profile.BirthDate));
        await casting.ReportAsync(CastingStage.Time, true);

        var photoTask = PhotoSampler.AnalyzeRecentCoreAsync();

        var geo = await geoTask;
        await casting.ReportAsync(CastingStage.Geo, Ok(geo));

        var weather = await weatherTask;
        await casting.ReportAsync(CastingStage.Weather, Ok(weather));

        // Батарея / питание / пульс всегда кладутся в snapshot при Assemble.
        await casting.ReportAsync(CastingStage.Battery, true);
        await casting.ReportAsync(CastingStage.Power, true);
        await casting.ReportAsync(CastingStage.InquiryPulse, true);

        var photo = await photoTask;
        var mysticOk = Ok(photo.MysticTag)
            && !string.Equals(photo.MysticTag, MysticTagConverter.UnknownArchetype, StringComparison.Ordinal);
        await casting.ReportAsync(CastingStage.PhotoScan, photo.FromGallery);
        await casting.ReportAsync(CastingStage.PhotoMystic, mysticOk);
        // Палитра в промпт больше не уходит.
        await casting.ReportAsync(CastingStage.PhotoPalette, false);
        await casting.ReportAsync(CastingStage.PhotoLuminance, Ok(photo.LuminanceVibe));

        var context = _context.Assemble(profile, photo);
        context.DynamicSnapshot.GeoLocationType = Ok(geo) ? geo : null;
        context.DynamicSnapshot.WeatherState = Ok(weather) ? weather : null;
        var snap = context.DynamicSnapshot;

        await casting.ReportAsync(CastingStage.Entropy, Ok(snap.EntropyWordAnchor));
        await casting.ReportAsync(CastingStage.BallMood, Ok(snap.BallMoodModifier));
        await casting.ReportAsync(CastingStage.BallTint, Ok(snap.BallTintModifier));
        await casting.ReportAsync(CastingStage.WorldPressure, Ok(snap.WorldPressureModifier));

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
