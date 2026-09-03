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
/// Последние N фото из галереи: грузит по одному, инференс в фоне, в промпт — одна сводка.
/// </summary>
public static class PhotoSampler
{
    public static async Task<PhotoAnalysis> AnalyzeRecentAsync(CastingProgress? casting = null)
    {
        if (casting != null)
            await casting.ReportAsync(CastingStage.PhotoScan).ConfigureAwait(true);

        var preprocessor = new ImagePreprocessor();
        var take = AppConfig.Current.PhotoLookbackCount;
        var paths = preprocessor.ListLatestGalleryPaths(take);
        var engine = OnnxInferenceEngine.Instance;
        if (engine is { IsInitialized: false })
            await engine.InitializeEngineAsync().ConfigureAwait(true);

        InferenceWorker? worker = engine != null ? new InferenceWorker(engine) : null;
        var frames = new List<PhotoFrame>(paths.Count);

        foreach (var path in paths)
        {
            var image = preprocessor.TryLoadFile(path);
            if (image == null)
                continue;
            try
            {
                frames.Add(await AnalyzeFrameAsync(preprocessor, worker, engine, image).ConfigureAwait(true));
            }
            finally
            {
                image.Dispose();
            }

            await YieldFrameAsync().ConfigureAwait(true);
        }

        PhotoAnalysis analysis;
        if (frames.Count > 0)
        {
            analysis = ImagePreprocessor.Merge(frames);
        }
        else
        {
            var fallback = preprocessor.LoadFallbackImage();
            try
            {
                analysis = ImagePreprocessor.Merge(
                    [await AnalyzeFrameAsync(preprocessor, worker, engine, fallback).ConfigureAwait(true)]);
            }
            finally
            {
                fallback.Dispose();
            }
        }

        if (casting != null)
        {
            await casting.ReportAsync(CastingStage.PhotoMystic).ConfigureAwait(true);
            await casting.ReportAsync(CastingStage.PhotoPalette).ConfigureAwait(true);
            await casting.ReportAsync(CastingStage.PhotoLuminance).ConfigureAwait(true);
        }

        return analysis;
    }

    private static async Task<PhotoFrame> AnalyzeFrameAsync(
        ImagePreprocessor preprocessor,
        InferenceWorker? worker,
        OnnxInferenceEngine? engine,
        Image image)
    {
        var (palette, luminance) = preprocessor.SampleVisuals(image);
        var tensor = preprocessor.ToNchwTensor(image);

        if (worker == null || engine is not { IsAvailable: true })
        {
            var unknown = "unknown object";
            return new PhotoFrame(unknown, MysticTagConverter.Convert(unknown), palette, luminance);
        }

        var outcome = await worker.RunDetailedAsync(tensor).ConfigureAwait(true);
        return new PhotoFrame(outcome.EnglishTag, outcome.MysticTag, palette, luminance);
    }

    private static async Task YieldFrameAsync()
    {
        if (Engine.GetMainLoop() is SceneTree tree)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
