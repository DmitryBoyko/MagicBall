using System.Text.Json;
using CrystalBall.App;
using Godot;

namespace CrystalBall.Ai;

public sealed class SemanticCacheEntry
{
    public string Question { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
}

/// <summary>
/// Локальный кэш с косинусным порогом 0.82. Эмбеддинг — хеш n-грамм (без внешней модели).
/// </summary>
public sealed class SemanticCache
{
    public const string Path = "user://semantic_cache.json";
    private readonly List<SemanticCacheEntry> _entries = [];
    private readonly float _threshold;

    public SemanticCache(float threshold = AppConfig.SemanticThreshold)
    {
        _threshold = threshold;
        Load();
    }

    public bool TryFind(string question, out OracleResult result, out float score)
    {
        result = new OracleResult();
        score = 0f;
        if (string.IsNullOrWhiteSpace(question) || _entries.Count == 0)
            return false;

        var query = Embed(question);
        SemanticCacheEntry? best = null;
        foreach (var entry in _entries)
        {
            var similarity = Cosine(query, entry.Embedding);
            if (similarity > score)
            {
                score = similarity;
                best = entry;
            }
        }

        if (best == null || score < _threshold)
            return false;

        var extracted = SummaryExtractor.Extract(best.Interpretation);
        result = new OracleResult
        {
            Interpretation = extracted.Interpretation,
            Summary = extracted.Summary,
            OsirisPresent = false,
            Source = "semantic",
            FallbackUsed = true,
            FallbackReason = $"semantic hit {score:0.000} ~ {best.Question}",
            Similarity = score,
        };
        return true;
    }

    public void Remember(string question, string rawAnswer)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(rawAnswer))
            return;

        var existing = _entries.Find(item => item.Question == question);
        if (existing != null)
        {
            existing.Interpretation = rawAnswer;
            existing.Embedding = Embed(question);
        }
        else
        {
            _entries.Add(new SemanticCacheEntry
            {
                Question = question,
                Interpretation = rawAnswer,
                Embedding = Embed(question),
            });
        }

        Save();
    }

    private void Load()
    {
        _entries.Clear();
        if (!FileAccess.FileExists(Path))
            return;

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file == null)
            return;

        try
        {
            var loaded = JsonSerializer.Deserialize<List<SemanticCacheEntry>>(file.GetAsText());
            if (loaded != null)
                _entries.AddRange(loaded);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[SemanticCache] {ex.Message}");
        }
    }

    private void Save()
    {
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = false }));
    }

    public static float[] Embed(string text)
    {
        const int dim = 64;
        var vector = new float[dim];
        var norm = text.Trim().ToLowerInvariant();
        if (norm.Length < 3)
            return vector;

        for (var i = 0; i < norm.Length - 2; i++)
        {
            var gram = (norm[i] * 73856093) ^ (norm[i + 1] * 19349663) ^ (norm[i + 2] * 83492791);
            var slot = Math.Abs(gram) % dim;
            vector[slot] += 1f;
        }

        var length = MathF.Sqrt(vector.Sum(v => v * v));
        if (length <= 0f)
            return vector;
        for (var i = 0; i < dim; i++)
            vector[i] /= length;
        return vector;
    }

    public static float Cosine(float[] left, float[] right)
    {
        if (left.Length == 0 || right.Length == 0 || left.Length != right.Length)
            return 0f;
        var dot = 0f;
        for (var i = 0; i < left.Length; i++)
            dot += left[i] * right[i];
        return dot;
    }
}
