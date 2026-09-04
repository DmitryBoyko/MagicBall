using System.Text.RegularExpressions;

namespace CrystalBall.Ai;

/// <summary>
/// Тело прозы и золотая фраза после маркера ИТОГ. На экране слово «ИТОГ» не должно появляться.
/// </summary>
public static partial class SummaryExtractor
{
    private static readonly Regex MarkerRegex = BuildMarkerRegex();
    private static readonly Regex PlainItogHeader = BuildPlainItogHeader();
    private static readonly Regex ItogToken = BuildItogToken();
    private static readonly Regex LeadPunctRegex = new(@"^[\s:;—–\-]+", RegexOptions.Compiled);
    private static readonly Regex ExtraSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex ExtraLines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex TrailBrackets = new(@"\[\[\s*([^\[\]]+?)\s*\]\]\s*$", RegexOptions.Compiled);
    private static readonly Regex DoubleBrackets = new(@"\[\[\s*(.*?)\s*\]\]", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex SingleBrackets = new(@"\[([^\[\]\n]{1,80})\]", RegexOptions.Compiled);
    private static readonly Regex VisibleOnly = new(@"[^\w\s.,!?:;…\-—–()«»""'“”‘’‚„]", RegexOptions.Compiled);

    public static (string Interpretation, string Summary) Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, string.Empty);

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        normalized = NormalizePlainItogHeaders(normalized);

        var matches = MarkerRegex.Matches(normalized);
        if (matches.Count == 0)
        {
            var stripped = normalized.Trim();
            var trail = TrailBrackets.Match(stripped);
            if (trail.Success)
            {
                var inner = trail.Groups[1].Value.Trim();
                if (inner.Length is > 0 and <= 80 && !IsItogOnly(inner))
                    return (StripMarkup(stripped[..trail.Index]), StripMarkup(inner));
            }

            return (StripMarkup(normalized), string.Empty);
        }

        var match = matches[^1];
        var before = normalized[..match.Index];
        var after = LeadPunctRegex.Replace(normalized[(match.Index + match.Length)..], string.Empty);

        var summary = string.Empty;
        var leftover = after;
        var lines = after.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var piece = lines[i].Trim().Trim('«', '»', '"', '\'');
            if (string.IsNullOrWhiteSpace(piece))
            {
                leftover = string.Join('\n', lines.Skip(i + 1));
                continue;
            }

            summary = MarkerRegex.Replace(piece, string.Empty).Trim().Trim('«', '»', '"', '\'', ':');
            leftover = string.Join('\n', lines.Skip(i + 1));
            break;
        }

        var body = before.Trim();
        leftover = leftover.Trim();
        if (!string.IsNullOrEmpty(leftover))
            body = string.IsNullOrEmpty(body) ? leftover : $"{body}\n{leftover}";

        body = MarkerRegex.Replace(body, string.Empty);
        body = ExtraLines.Replace(body.Replace(" \n", "\n"), "\n\n").Trim();
        summary = StripMarkup(summary);
        if (summary.Length > 300)
            summary = summary[..300].Trim();
        if (IsItogOnly(summary) || StartsWithItog(summary))
        {
            summary = StripItogLabel(summary);
            if (IsItogOnly(summary))
                summary = string.Empty;
        }

        return (StripMarkup(body), summary);
    }

    public static string StripMarkup(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = text.Replace("\r\n", "\n").Replace('\r', '\n');
        cleaned = NormalizePlainItogHeaders(cleaned);
        cleaned = MarkerRegex.Replace(cleaned, string.Empty);
        cleaned = Regex.Replace(cleaned, @"```(?:\w+)?\n?([\s\S]*?)```", "$1");
        cleaned = cleaned.Replace("```", string.Empty);
        cleaned = Regex.Replace(cleaned, "`+", string.Empty);
        cleaned = Regex.Replace(cleaned, @"(?m)^\s{0,3}#{1,6}\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"(?m)^\s{0,3}>\s?", string.Empty);
        cleaned = Regex.Replace(cleaned, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        cleaned = Regex.Replace(cleaned, @"\[([^\]]+)\]\([^)]+\)", "$1");
        cleaned = UnwrapBrackets(cleaned);
        cleaned = Regex.Replace(cleaned, @"\*{1,3}([^*]+)\*{1,3}", "$1");
        cleaned = Regex.Replace(cleaned, @"_{1,3}([^_]+)_{1,3}", "$1");
        cleaned = Regex.Replace(cleaned, @"[*_`#~|\\]+", string.Empty);
        cleaned = Regex.Replace(cleaned, @"(?m)^\s*(?:[-+•]|\d+[.)])\s+", string.Empty);
        cleaned = VisibleOnly.Replace(cleaned, string.Empty);
        cleaned = cleaned.Replace("_", string.Empty);
        cleaned = StripItogLabel(cleaned);
        cleaned = ExtraLines.Replace(cleaned.Replace(" \n", "\n"), "\n\n");
        cleaned = ExtraSpaces.Replace(cleaned, " ");
        return cleaned.Trim();
    }

    /// <summary>«ИТОГ фраза» / «ИТОГ: фраза» / «**ИТОГ**» → единый маркер для Extract.</summary>
    private static string NormalizePlainItogHeaders(string text)
    {
        var cleaned = MarkerRegex.Replace(text, "[[ИТОГ]]");
        cleaned = PlainItogHeader.Replace(cleaned, "$1[[ИТОГ]] ");
        return cleaned;
    }

    private static string StripItogLabel(string text)
    {
        var cleaned = MarkerRegex.Replace(text, string.Empty);
        cleaned = PlainItogHeader.Replace(cleaned, "$1");
        cleaned = ItogToken.Replace(cleaned, " ");
        cleaned = ExtraSpaces.Replace(cleaned, " ");
        return cleaned;
    }

    private static bool IsItogOnly(string text)
    {
        var t = text.Trim().Trim('«', '»', '"', '\'', ':', '.', '—', '–', '-');
        return t.Equals("итог", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithItog(string text) =>
        PlainItogHeader.IsMatch(text) || MarkerRegex.IsMatch(text);

    private static string UnwrapBrackets(string text)
    {
        var prev = string.Empty;
        var cleaned = text;
        while (cleaned != prev)
        {
            prev = cleaned;
            cleaned = DoubleBrackets.Replace(cleaned, "$1");
        }

        return SingleBrackets.Replace(cleaned, "$1");
    }

    // [[ИТОГ]], *[[ИТОГ]]*, строка только «ИТОГ»
    [GeneratedRegex(
        @"(?:\*{1,2}|_{1,2}|\[)?\s*\[\[\s*итог\s*\]\]\s*(?:\*{1,2}|_{1,2}|\])?|(?m)^\s*(?:\*{1,2}|_{1,2})?\s*итог\s*(?:\*{1,2}|_{1,2})?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex BuildMarkerRegex();

    // Начало строки / куска: «ИТОГ» + опц. пунктуация + хвост фразы
    [GeneratedRegex(
        @"(?m)^(\s*)(?:\*{1,2}|_{1,2}|\[)?\s*итог\s*(?:\*{1,2}|_{1,2}|\])?\s*[:.—–\-]?\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex BuildPlainItogHeader();

    // Любой оставшийся токен «итог» как отдельное слово (не «в итоге»)
    [GeneratedRegex(
        @"(?<![\p{L}\p{N}])итог(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuildItogToken();
}
