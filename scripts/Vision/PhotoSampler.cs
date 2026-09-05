using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Vision;

/// <summary>
/// Один разобранный кадр до сводки в промпт. Порядок списка — от новых к старым.
/// </summary>
public readonly record struct PhotoFrame(
    string RawTag,
    string MysticTag,
    Dictionary<string, int> Palette,
    double Luminance);

/// <summary>
/// Скан галереи с кэшем: 1 кадр при старте, остальное — по «Спросить».
/// </summary>
public static class PhotoSampler
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, PhotoFrame> FrameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> CacheOrder = [];
    private static Task? _warmupTask;
    private static int _warmupGeneration;

    public static async Task<PhotoAnalysis> AnalyzeRecentAsync(CastingProgress? casting = null)
    {
        var analysis = await AnalyzeRecentCoreAsync().ConfigureAwait(true);

        if (casting != null)
        {
            await casting.ReportAsync(CastingStage.PhotoScan, analysis.FromGallery).ConfigureAwait(true);
            var mysticOk = !string.IsNullOrWhiteSpace(analysis.MysticTag)
                && !string.Equals(analysis.MysticTag, MysticTagConverter.UnknownArchetype, StringComparison.Ordinal);
            await casting.ReportAsync(CastingStage.PhotoMystic, mysticOk).ConfigureAwait(true);
            await casting.ReportAsync(CastingStage.PhotoPalette, inPrompt: false).ConfigureAwait(true);
            await casting.ReportAsync(CastingStage.PhotoLuminance, !string.IsNullOrWhiteSpace(analysis.LuminanceVibe))
                .ConfigureAwait(true);
        }

        return analysis;
    }

    /// <summary>
    /// Прогрев одного свежего кадра после старта / возврата в приложение.
    /// </summary>
    public static Task WarmupAsync(int count = 1)
    {
        count = Math.Clamp(count, 1, AppConfig.MaxPhotoLookback);
        var generation = Interlocked.Increment(ref _warmupGeneration);
        var task = WarmupCoreAsync(count, generation);
        lock (Gate)
            _warmupTask = task;
        return task;
    }

    /// <summary>
    /// Полная сводка: кэш + недостающие кадры до photo_lookback_count.
    /// </summary>
    public static async Task<PhotoAnalysis> AnalyzeRecentCoreAsync()
    {
        await YieldFrameAsync().ConfigureAwait(true);

        Task? warmup;
        lock (Gate)
            warmup = _warmupTask;

        // Ждём прогрев только если галерея уже доступна; иначе не блокируем ритуал.
        if (warmup is { IsCompleted: false }
            && (!AppPermissions.IsAndroid || AppPermissions.Check().PhotosGranted))
        {
            var finished = await Task.WhenAny(warmup, DelaySecondsAsync(0.35)).ConfigureAwait(true);
            if (finished == warmup)
            {
                try
                {
                    await warmup.ConfigureAwait(true);
                }
                catch
                {
                    // прогрев упал — ниже полный скан
                }
            }
        }

        var take = AppConfig.Current.PhotoLookbackCount;
        return await AnalyzePathsAsync(take).ConfigureAwait(true);
    }

    private static async Task WarmupCoreAsync(int count, int generation)
    {
        await YieldFrameAsync().ConfigureAwait(true);

        if (generation != _warmupGeneration)
            return;

        // Не крутим секунды в ожидании разрешения — иначе «Спросить» ждёт этот прогрев.
        if (AppPermissions.IsAndroid && !AppPermissions.Check().PhotosGranted)
            return;

        try
        {
            await AnalyzePathsAsync(count).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[PhotoSampler] warmup: {ex.Message}");
        }
    }

    private static async Task<PhotoAnalysis> AnalyzePathsAsync(int take)
    {
        var preprocessor = new ImagePreprocessor();
        var paths = await preprocessor.ListLatestGalleryPathsAsync(take).ConfigureAwait(true);
        await YieldFrameAsync().ConfigureAwait(true);

        var engine = OnnxInferenceEngine.Instance;
        if (engine is { IsInitialized: false })
            await engine.InitializeEngineAsync().ConfigureAwait(true);

        InferenceWorker? worker = engine is { IsAvailable: true } ? new InferenceWorker(engine) : null;
        var frames = new List<PhotoFrame>(paths.Count);

        foreach (var path in paths)
        {
            await YieldFrameAsync().ConfigureAwait(true);

            if (TryGetCached(path, out var cached) && !IsUnknownFrame(cached))
            {
                frames.Add(cached);
                continue;
            }

            // Decode/resize — после yield, чтобы фразы ритуала не залипали.
            await YieldFrameAsync().ConfigureAwait(true);
            var prepared = preprocessor.TryPrepareFrame(path);
            if (prepared == null)
                continue;

            var frame = await AnalyzePreparedAsync(worker, engine, prepared.Value).ConfigureAwait(true);
            if (!IsUnknownFrame(frame))
                Remember(path, frame);
            frames.Add(frame);
            await YieldFrameAsync().ConfigureAwait(true);
        }

        if (frames.Count > 0)
            return ImagePreprocessor.Merge(frames, fromGallery: true);

        await YieldFrameAsync().ConfigureAwait(true);
        const string fallbackKey = "__fallback__";
        if (TryGetCached(fallbackKey, out var fallbackCached) && !IsUnknownFrame(fallbackCached))
            return ImagePreprocessor.Merge([fallbackCached], fromGallery: false);

        var fallback = preprocessor.PrepareFallbackFrame();
        var fallbackFrame = await AnalyzePreparedAsync(worker, engine, fallback).ConfigureAwait(true);
        Remember(fallbackKey, fallbackFrame);
        return ImagePreprocessor.Merge([fallbackFrame], fromGallery: false);
    }

    private static bool IsUnknownFrame(PhotoFrame frame) =>
        string.Equals(frame.RawTag, "unknown object", StringComparison.Ordinal)
        || string.Equals(frame.MysticTag, MysticTagConverter.UnknownArchetype, StringComparison.Ordinal);

    private static string AuraMystic(double luminance) =>
        luminance >= 0.45
            ? "Сияющий Лик Стекла"
            : "Теневой Лик Стекла";

    private static bool TryGetCached(string path, out PhotoFrame frame)
    {
        lock (Gate)
            return FrameCache.TryGetValue(path, out frame);
    }

    private static void Remember(string path, PhotoFrame frame)
    {
        lock (Gate)
        {
            if (!FrameCache.ContainsKey(path))
                CacheOrder.Add(path);
            FrameCache[path] = frame;

            var keep = Math.Max(AppConfig.MaxPhotoLookback * 2, 8);
            while (CacheOrder.Count > keep)
            {
                var old = CacheOrder[0];
                CacheOrder.RemoveAt(0);
                FrameCache.Remove(old);
            }
        }
    }

    private static async Task<PhotoFrame> AnalyzePreparedAsync(
        InferenceWorker? worker,
        OnnxInferenceEngine? engine,
        ImagePreprocessor.PreparedFrame prepared)
    {
        var visualsTask = Task.Run(() =>
        {
            var (palette, luminance) = ImagePreprocessor.SampleVisualsFromRgb(
                prepared.Rgb224, prepared.Width, prepared.Height);
            var tensor = ImagePreprocessor.ToNchwTensorFromRgb(prepared.Rgb224, prepared.Width, prepared.Height);
            return (palette, luminance, tensor);
        });

        var (palette, luminance, tensor) = await visualsTask.ConfigureAwait(true);

        if (worker == null || engine is not { IsAvailable: true })
        {
            var aura = AuraMystic(luminance);
            return new PhotoFrame("luminance aura", aura, palette, luminance);
        }

        var outcome = await worker.RunDetailedAsync(tensor).ConfigureAwait(true);
        return new PhotoFrame(outcome.EnglishTag, outcome.MysticTag, palette, luminance);
    }

    private static async Task YieldFrameAsync()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static async Task DelaySecondsAsync(double seconds)
    {
        if (Engine.GetMainLoop() is SceneTree tree)
        {
            var end = Time.GetTicksMsec() + (ulong)(seconds * 1000.0);
            while (Time.GetTicksMsec() < end)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(seconds)).ConfigureAwait(true);
    }
}
