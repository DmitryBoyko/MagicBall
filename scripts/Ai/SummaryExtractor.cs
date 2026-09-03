using System.Text.RegularExpressions;

namespace CrystalBall.Ai;

/// <summary>
/// Порт extract_summary из TrueTaro: тело прозы и золотая фраза после [[ИТОГ]].
/// </summary>
public static partial class SummaryExtractor
{
    private static readonly Regex MarkerRegex = BuildMarkerRegex();
    private static readonly Regex LeadPunctRegex = new(@"^[\s:;—–\-]+", RegexOptions.Compiled);
    private static readonly Regex ExtraSpaces = new(@" {2,}", RegexOptions.Compiled);
    private static readonly Regex ExtraLines = new(@"\n{3,}", RegexOptions.Compiled);

    public static (string Interpretation, string Summary) Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, string.Empty);

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var matches = MarkerRegex.Matches(normalized);
        if (matches.Count == 0)
            return (StripMarkup(normalized), string.Empty);

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

        return (StripMarkup(body), summary);
    }

    public static string StripMarkup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = text.Replace("\r\n", "\n").Replace('\r', '\n');
        cleaned = Regex.Replace(cleaned, @"```(?:\w+)?\n?([\s\S]*?)```", "$1");
        cleaned = cleaned.Replace("```", string.Empty);
        cleaned = Regex.Replace(cleaned, "`+", string.Empty);
        cleaned = Regex.Replace(cleaned, @"(?m)^\s{0,3}#{1,6}\s*", string.Empty);
        cleaned = Regex.Replace(cleaned, @"\*{1,3}([^*]+)\*{1,3}", "$1");
        cleaned = Regex.Replace(cleaned, @"_{1,3}([^_]+)_{1,3}", "$1");
        cleaned = Regex.Replace(cleaned, @"(?m)^\s*(?:[-+•]|\d+[.)])\s+", string.Empty);
        cleaned = ExtraSpaces.Replace(cleaned, " ");
        return cleaned.Trim();
    }

    [GeneratedRegex(@"(?:\*{1,2}|_{1,2})?\[\[\s*итог\s*\]\](?:\*{1,2}|_{1,2})?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BuildMarkerRegex();
}
