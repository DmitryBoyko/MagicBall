using Godot;

namespace CrystalBall.App;

public partial class AudioService : Node
{
    public const string Folder = "res://assets/audio/";
    public const string BirdsFolder = "res://assets/audio/birds/";
    public const string SfxFolder = "res://assets/audio/sfx/";
    private const double SilenceBeforeAdvance = 0.35;
    private const float MusicVolumeDb = -10f;
    private const float MusicDuckedDb = -26f;
    private const float BirdsVolumeDb = -18f;
    private const float BirdsDuckedDb = -36f;
    private const float WindVolumeDb = -8f;
    private const float CrystalBedDb = -14f;
    private const float CrystalHitDb = -10f;
    private const double BirdGapMin = 4.0;
    private const double BirdGapMax = 12.0;
    private const double BirdBurstMin = 2.4;
    private const double BirdBurstMax = 6.5;

    private static readonly string[] VortexWindPaths = [SfxFolder + "wind_whoosh.ogg"];
    private static readonly string[] VortexCrystalPaths =
    [
        SfxFolder + "quartz_bowls.ogg",
        SfxFolder + "crystal_bowl1.ogg",
    ];

    private AudioStreamPlayer? _music;
    private AudioStreamPlayer? _birds;
    private AudioStreamPlayer? _vortexWind;
    private AudioStreamPlayer? _vortexCrystal;
    private AudioStreamPlayer? _vortexHit;
    private Tween? _mixTween;
    private readonly List<string> _tracks = [];
    private readonly List<string> _session = [];
    private readonly List<string> _birdClips = [];
    private int _index = -1;
    private int _lastBird = -1;
    private double _silence;
    private double _birdGap;
    private double _birdBurstLeft;
    private bool _advancing;
    private bool _vortexActive;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _music = new AudioStreamPlayer
        {
            Name = "PlaylistPlayer",
            Bus = "Master",
            VolumeDb = MusicVolumeDb,
            ProcessMode = ProcessModeEnum.Always,
        };
        _music.Finished += OnTrackFinished;
        AddChild(_music);

        _birds = new AudioStreamPlayer
        {
            Name = "BirdsPlayer",
            Bus = "Master",
            VolumeDb = BirdsVolumeDb,
            ProcessMode = ProcessModeEnum.Always,
        };
        _birds.Finished += OnBirdFinished;
        AddChild(_birds);

        _vortexWind = MakeSfxPlayer("VortexWind");
        _vortexCrystal = MakeSfxPlayer("VortexCrystal");
        _vortexHit = MakeSfxPlayer("VortexCrystalHit");
        AddChild(_vortexWind);
        AddChild(_vortexCrystal);
        AddChild(_vortexHit);

        ScanFolder(Folder, _tracks);
        ScanFolder(BirdsFolder, _birdClips);
        ShuffleSession();
        _birdGap = 2.0 + Random.Shared.NextDouble() * 3.0;
        GD.Print($"[AudioService] {_session.Count} tracks, {_birdClips.Count} bird clips");
        Apply(AppSettingsStore.Current.MusicEnabled);
    }

    public override void _Process(double delta)
    {
        TickMusicWatchdog(delta);
        TickBirds(delta);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn)
            CallDeferred(MethodName.EnsurePlaying);
    }

    public void Apply(bool enabled)
    {
        if (_music == null)
            return;

        if (!enabled)
        {
            _music.Stop();
            _silence = 0;
            if (!_vortexActive)
                _mixTween?.Kill();
            return;
        }

        EnsurePlaying();
        ApplyAmbientVolumes(immediate: true);
        if (_birdGap <= 0)
            _birdGap = 1.5 + Random.Shared.NextDouble() * 2.5;
    }

    public void StartVortexMix()
    {
        if (_vortexActive)
            return;
        _vortexActive = true;
        PlayVortexBlock();
        TweenAmbientDuck(0.22);
    }

    public void StopVortexMix(float fadeSec = 0.95f)
    {
        if (!_vortexActive)
            return;
        _vortexActive = false;
        var fade = Mathf.Max(0.05f, fadeSec);
        TweenAmbientDuck(fade);
        FadeVortexBlock(fade);
    }

    public void SetEnabled(bool enabled)
    {
        var settings = AppSettingsStore.Current;
        settings.MusicEnabled = enabled;
        AppSettingsStore.Save(settings);
        Apply(enabled);
    }

    public void SetBirdsEnabled(bool enabled)
    {
        var settings = AppSettingsStore.Current;
        settings.BirdsEnabled = enabled;
        AppSettingsStore.Save(settings);
        ApplyBirds(enabled);
    }

    public void ApplyBirds(bool enabled)
    {
        if (_birds == null)
            return;

        if (!enabled)
        {
            _birds.Stop();
            _birdBurstLeft = 0;
            _birdGap = 2.0;
            return;
        }

        ApplyAmbientVolumes(immediate: true);
        if (_birdGap <= 0)
            _birdGap = 1.5 + Random.Shared.NextDouble() * 2.5;
    }

    private float CurrentMusicDb => _vortexActive ? MusicDuckedDb : MusicVolumeDb;
    private float CurrentBirdsDb => _vortexActive ? BirdsDuckedDb : BirdsVolumeDb;

    private static AudioStreamPlayer MakeSfxPlayer(string name)
    {
        return new AudioStreamPlayer
        {
            Name = name,
            Bus = "Master",
            VolumeDb = -80f,
            ProcessMode = ProcessModeEnum.Always,
        };
    }

    private void TweenAmbientDuck(double seconds)
    {
        _mixTween?.Kill();
        _mixTween = CreateTween();
        _mixTween.SetParallel(true);
        if (_music != null && AppSettingsStore.Current.MusicEnabled)
            _mixTween.TweenProperty(_music, "volume_db", CurrentMusicDb, seconds);
        if (_birds != null && AppSettingsStore.Current.BirdsEnabled)
            _mixTween.TweenProperty(_birds, "volume_db", CurrentBirdsDb, seconds);
    }

    private void ApplyAmbientVolumes(bool immediate)
    {
        if (immediate)
            _mixTween?.Kill();
        if (_music != null)
            _music.VolumeDb = CurrentMusicDb;
        if (_birds != null)
            _birds.VolumeDb = CurrentBirdsDb;
    }

    private void PlayVortexBlock()
    {
        EnsureChild(_vortexWind);
        EnsureChild(_vortexCrystal);
        EnsureChild(_vortexHit);

        ArmPlayer(_vortexWind, VortexWindPaths[0], loop: true);
        ArmPlayer(_vortexCrystal, VortexCrystalPaths[0], loop: true);
        ArmPlayer(_vortexHit, VortexCrystalPaths[1], loop: false);

        var fadeIn = CreateTween();
        fadeIn.SetParallel(true);
        FadePlayerIn(fadeIn, _vortexWind, WindVolumeDb, 0.22);
        FadePlayerIn(fadeIn, _vortexCrystal, CrystalBedDb, 0.28);
        FadePlayerIn(fadeIn, _vortexHit, CrystalHitDb, 0.12);

        _vortexWind?.Play();
        _vortexCrystal?.Play();
        _vortexHit?.Play();
    }

    private void FadeVortexBlock(float fadeSec)
    {
        var fade = CreateTween();
        fade.SetParallel(true);
        FadePlayerOut(fade, _vortexWind, fadeSec);
        FadePlayerOut(fade, _vortexCrystal, fadeSec);
        FadePlayerOut(fade, _vortexHit, fadeSec * 0.55f);
        fade.Chain().TweenCallback(Callable.From(StopVortexPlayers));
    }

    private void StopVortexPlayers()
    {
        _vortexWind?.Stop();
        _vortexCrystal?.Stop();
        _vortexHit?.Stop();
    }

    private void EnsureChild(AudioStreamPlayer? player)
    {
        if (player != null && player.GetParent() == null)
            AddChild(player);
    }

    private static void ArmPlayer(AudioStreamPlayer? player, string path, bool loop)
    {
        if (player == null)
            return;
        if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path))
            return;

        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
            return;

        SetLoop(stream, loop);
        player.Stream = stream;
        player.VolumeDb = -48f;
        player.PitchScale = 1f;
    }

    private static void FadePlayerIn(Tween tween, AudioStreamPlayer? player, float targetDb, double seconds)
    {
        if (player?.Stream == null)
            return;
        player.VolumeDb = -48f;
        tween.TweenProperty(player, "volume_db", targetDb, seconds);
    }

    private static void FadePlayerOut(Tween tween, AudioStreamPlayer? player, float seconds)
    {
        if (player == null || !player.Playing)
            return;
        tween.TweenProperty(player, "volume_db", -80f, seconds);
    }

    private void TickMusicWatchdog(double delta)
    {
        if (_music == null || !AppSettingsStore.Current.MusicEnabled)
        {
            _silence = 0;
            return;
        }

        if (_music.Playing)
        {
            _silence = 0;
            return;
        }

        _silence += delta;
        if (_silence >= SilenceBeforeAdvance)
        {
            _silence = 0;
            PlayNext();
        }
    }

    private void TickBirds(double delta)
    {
        if (_birds == null || !AppSettingsStore.Current.BirdsEnabled || _birdClips.Count == 0)
            return;

        if (_birds.Playing)
        {
            if (_birdBurstLeft <= 0)
                return;

            _birdBurstLeft -= delta;
            if (_birdBurstLeft <= 0)
                _birds.Stop();
            return;
        }

        _birdGap -= delta;
        if (_birdGap <= 0)
            PlayRandomBird();
    }

    private void EnsurePlaying()
    {
        if (_music == null || !AppSettingsStore.Current.MusicEnabled)
            return;

        if (_music.Playing)
        {
            _music.StreamPaused = false;
            return;
        }

        PlayNext();
    }

    private void OnTrackFinished()
    {
        if (!AppSettingsStore.Current.MusicEnabled)
            return;
        CallDeferred(MethodName.PlayNext);
    }

    private void OnBirdFinished()
    {
        _birdBurstLeft = 0;
        _birdGap = NextBirdGap();
    }

    private void PlayNext()
    {
        if (_advancing || _music == null || !AppSettingsStore.Current.MusicEnabled)
            return;
        if (_session.Count == 0)
        {
            ScanFolder(Folder, _tracks);
            ShuffleSession();
        }

        if (_session.Count == 0)
            return;

        _advancing = true;
        try
        {
            var attempts = _session.Count;
            while (attempts-- > 0)
            {
                _index = (_index + 1) % _session.Count;
                var path = _session[_index];
                if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path))
                    continue;

                var stream = GD.Load<AudioStream>(path);
                if (stream == null)
                    continue;

                DisableLoop(stream);
                _music.Stream = stream;
                _music.VolumeDb = CurrentMusicDb;
                _music.Play();
                _silence = 0;
                return;
            }
        }
        finally
        {
            _advancing = false;
        }
    }

    private void PlayRandomBird()
    {
        if (_birds == null || _birdClips.Count == 0)
            return;

        var attempts = _birdClips.Count;
        while (attempts-- > 0)
        {
            var i = Random.Shared.Next(_birdClips.Count);
            if (_birdClips.Count > 1 && i == _lastBird)
                i = (i + 1) % _birdClips.Count;

            var path = _birdClips[i];
            if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path))
                continue;

            var stream = GD.Load<AudioStream>(path);
            if (stream == null)
                continue;

            DisableLoop(stream);
            _lastBird = i;
            _birds.Stream = stream;
            _birds.PitchScale = (float)(0.94 + Random.Shared.NextDouble() * 0.12);
            _birds.VolumeDb = CurrentBirdsDb + (float)(Random.Shared.NextDouble() * 4.0 - 2.0);

            var length = stream.GetLength();
            var from = 0.0;
            _birdBurstLeft = 0;
            if (length > BirdBurstMax)
            {
                var burst = BirdBurstMin + Random.Shared.NextDouble() * (BirdBurstMax - BirdBurstMin);
                from = Random.Shared.NextDouble() * Math.Max(0.0, length - burst);
                _birdBurstLeft = burst;
            }

            _birds.Play((float)from);
            return;
        }

        _birdGap = NextBirdGap();
    }

    private static double NextBirdGap() =>
        BirdGapMin + Random.Shared.NextDouble() * (BirdGapMax - BirdGapMin);

    private void ShuffleSession()
    {
        _session.Clear();
        _session.AddRange(_tracks);
        for (var i = _session.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (_session[i], _session[j]) = (_session[j], _session[i]);
        }

        _index = -1;
    }

    private static void ScanFolder(string folder, List<string> into)
    {
        into.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var name in ResourceLoader.ListDirectory(folder))
                AddAudio(folder, name, seen, into);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AudioService] ListDirectory {folder}: {ex.Message}");
        }

        if (into.Count > 0)
            return;

        using var dir = DirAccess.Open(folder);
        if (dir == null)
            return;

        foreach (var name in dir.GetFiles())
            AddAudio(folder, name, seen, into);
    }

    private static void AddAudio(string folder, string name, HashSet<string> seen, List<string> into)
    {
        if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
            return;

        var file = StripImportSuffix(name.GetFile());
        if (!IsAudio(file) || !seen.Add(file))
            return;

        var path = folder.TrimEnd('/') + "/" + file;
        if (ResourceLoader.Exists(path) || FileAccess.FileExists(path))
            into.Add(path);
    }

    private static string StripImportSuffix(string file)
    {
        const string import = ".import";
        const string remap = ".remap";
        if (file.EndsWith(import, StringComparison.OrdinalIgnoreCase))
            return file[..^import.Length];
        if (file.EndsWith(remap, StringComparison.OrdinalIgnoreCase))
            return file[..^remap.Length];
        return file;
    }

    private static bool IsAudio(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".mp3") || lower.EndsWith(".ogg") || lower.EndsWith(".wav");
    }

    private static void SetLoop(AudioStream stream, bool loop)
    {
        switch (stream)
        {
            case AudioStreamMP3 mp3:
                mp3.Loop = loop;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = loop;
                break;
            case AudioStreamWav wav:
                wav.LoopMode = loop
                    ? AudioStreamWav.LoopModeEnum.Forward
                    : AudioStreamWav.LoopModeEnum.Disabled;
                break;
        }
    }

    private static void DisableLoop(AudioStream stream) => SetLoop(stream, false);
}
