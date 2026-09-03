using Godot;

namespace CrystalBall.App;

public partial class AudioService : Node
{
    public const string Folder = "res://assets/audio/";
    private const double SilenceBeforeAdvance = 0.35;

    private AudioStreamPlayer? _music;
    private readonly List<string> _tracks = [];
    private readonly List<string> _session = [];
    private int _index = -1;
    private double _silence;
    private bool _advancing;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _music = new AudioStreamPlayer
        {
            Name = "PlaylistPlayer",
            Bus = "Master",
            VolumeDb = -10f,
            ProcessMode = ProcessModeEnum.Always,
        };
        _music.Finished += OnTrackFinished;
        AddChild(_music);
        ScanTracks();
        ShuffleSession();
        GD.Print($"[AudioService] {_session.Count} tracks in session playlist");
        Apply(AppSettingsStore.Current.MusicEnabled);
    }

    public override void _Process(double delta)
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
            return;
        }

        EnsurePlaying();
    }

    public void SetEnabled(bool enabled)
    {
        var settings = AppSettingsStore.Current;
        settings.MusicEnabled = enabled;
        AppSettingsStore.Save(settings);
        Apply(enabled);
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

    private void PlayNext()
    {
        if (_advancing || _music == null || !AppSettingsStore.Current.MusicEnabled)
            return;
        if (_session.Count == 0)
        {
            ScanTracks();
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

    private void ScanTracks()
    {
        _tracks.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var name in ResourceLoader.ListDirectory(Folder))
                AddTrack(name, seen);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AudioService] ListDirectory: {ex.Message}");
        }

        if (_tracks.Count > 0)
            return;

        using var dir = DirAccess.Open(Folder);
        if (dir == null)
            return;

        foreach (var name in dir.GetFiles())
            AddTrack(name, seen);
    }

    private void AddTrack(string name, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
            return;

        var file = StripImportSuffix(name.GetFile());
        if (!IsAudio(file) || !seen.Add(file))
            return;

        var path = Folder + file;
        if (ResourceLoader.Exists(path) || FileAccess.FileExists(path))
            _tracks.Add(path);
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

    private static void DisableLoop(AudioStream stream)
    {
        switch (stream)
        {
            case AudioStreamMP3 mp3:
                mp3.Loop = false;
                break;
            case AudioStreamOggVorbis ogg:
                ogg.Loop = false;
                break;
            case AudioStreamWav wav:
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
                break;
        }
    }
}
