using Godot;

namespace CrystalBall.App;

public partial class AudioService : Node
{
    public const string Folder = "res://assets/audio/";

    private AudioStreamPlayer? _music;
    private readonly List<string> _tracks = [];
    private readonly List<string> _queue = [];
    private string? _lastPath;

    public override void _Ready()
    {
        _music = new AudioStreamPlayer
        {
            Name = "PlaylistPlayer",
            Bus = "Master",
            VolumeDb = -10f,
        };
        _music.Finished += OnTrackFinished;
        AddChild(_music);
        ScanTracks();
        Apply(AppSettingsStore.Current.MusicEnabled);
    }

    public void Apply(bool enabled)
    {
        if (_music == null)
            return;

        if (!enabled)
        {
            _music.Stop();
            return;
        }

        if (_music.Playing)
            return;

        PlayNext();
    }

    public void SetEnabled(bool enabled)
    {
        var settings = AppSettingsStore.Current;
        settings.MusicEnabled = enabled;
        AppSettingsStore.Save(settings);
        Apply(enabled);
    }

    private void OnTrackFinished()
    {
        if (!AppSettingsStore.Current.MusicEnabled)
            return;
        PlayNext();
    }

    private void PlayNext(int skips = 0)
    {
        if (_music == null || _tracks.Count == 0 || skips > _tracks.Count)
            return;

        if (_queue.Count == 0)
            RefillQueue();

        var path = _queue[0];
        _queue.RemoveAt(0);
        if (!ResourceLoader.Exists(path))
        {
            PlayNext(skips + 1);
            return;
        }

        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
        {
            PlayNext(skips + 1);
            return;
        }

        DisableLoop(stream);
        _lastPath = path;
        _music.Stream = stream;
        _music.Play();
    }

    private void RefillQueue()
    {
        _queue.Clear();
        _queue.AddRange(_tracks);
        for (var i = _queue.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
        }

        if (_lastPath != null && _queue.Count > 1 && _queue[0] == _lastPath)
            (_queue[0], _queue[1]) = (_queue[1], _queue[0]);
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

        dir.ListDirBegin();
        while (true)
        {
            var name = dir.GetNext();
            if (string.IsNullOrEmpty(name))
                break;
            if (dir.CurrentIsDir())
                continue;
            AddTrack(name, seen);
        }

        dir.ListDirEnd();
    }

    private void AddTrack(string name, HashSet<string> seen)
    {
        if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
            return;

        var file = name.GetFile();
        if (!IsAudio(file) || !seen.Add(file))
            return;

        var path = Folder + file;
        if (ResourceLoader.Exists(path) || FileAccess.FileExists(path))
            _tracks.Add(path);
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
