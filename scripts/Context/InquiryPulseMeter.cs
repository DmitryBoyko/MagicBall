namespace CrystalBall.Context;

/// <summary>
/// Плотность обращений к шару → скрытый «пульс вопрошания».
/// Считаем не lifetime-клики, а окно последних N суток: интервалы, вспышки, ночные тапы.
/// </summary>
public static class InquiryPulseMeter
{
    public const int DefaultWindowDays = 3;
    public const int MinWindowDays = 1;
    public const int MaxWindowDays = 14;

    public const int CalmTapsPerDay = 2;
    public const double BurstGapMinutes = 10;
    public const double AcuteGapMinutes = 2;
    public const double SessionGapMinutes = 45;
    public const double LingerGapHours = 3;

    public static int ClampWindowDays(int days) =>
        Math.Clamp(days <= 0 ? DefaultWindowDays : days, MinWindowDays, MaxWindowDays);

    public static DateTime WindowStart(DateTime nowLocal, int windowDays)
    {
        var days = ClampWindowDays(windowDays);
        return nowLocal.Date.AddDays(-(days - 1));
    }

    public static InquiryPulseReading Evaluate(IReadOnlyList<DateTime> stampsLocal, DateTime nowLocal, int windowDays)
    {
        var start = WindowStart(nowLocal, windowDays);
        var ordered = stampsLocal
            .Where(stamp => stamp >= start && stamp <= nowLocal)
            .OrderBy(stamp => stamp)
            .ToList();

        if (ordered.Count == 0)
            ordered.Add(nowLocal);

        var count = ordered.Count;
        var density = DensityScore(count, windowDays);
        var burst = BurstScore(ordered);
        var recency = RecencyScore(ordered, nowLocal);
        var night = NightBoost(ordered);
        var score = Math.Clamp(0.42 * density + 0.33 * burst + 0.25 * recency + night, 0.0, 1.0);
        return Map(score);
    }

    private static double DensityScore(int count, int windowDays)
    {
        var days = ClampWindowDays(windowDays);
        var high = Math.Max(CalmTapsPerDay * days * 2, 4);
        return Math.Clamp((count - 1) / (double)(high - 1), 0.0, 1.0);
    }

    private static double BurstScore(IReadOnlyList<DateTime> ordered)
    {
        if (ordered.Count < 2)
            return 0.0;

        var bursts = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i] - ordered[i - 1]).TotalMinutes < BurstGapMinutes)
                bursts++;
        }

        return bursts / (double)(ordered.Count - 1);
    }

    private static double RecencyScore(IReadOnlyList<DateTime> ordered, DateTime nowLocal)
    {
        if (ordered.Count < 2)
            return 0.0;

        var previous = ordered[^2];
        var gap = nowLocal - previous;
        if (gap.TotalMinutes < AcuteGapMinutes)
            return 1.0;
        if (gap.TotalMinutes < BurstGapMinutes)
            return 0.72;
        if (gap.TotalMinutes < SessionGapMinutes)
            return 0.4;
        if (gap.TotalHours < LingerGapHours)
            return 0.18;
        return 0.0;
    }

    private static double NightBoost(IReadOnlyList<DateTime> ordered)
    {
        if (ordered.Count < 3)
            return 0.0;

        var night = ordered.Count(stamp => TimeOfDayCatalog.FromHour(stamp.Hour) == DayPart.Night);
        return 0.12 * (night / (double)ordered.Count);
    }

    private static InquiryPulseReading Map(double score)
    {
        if (score < 0.30)
        {
            return new InquiryPulseReading(
                score,
                "Ровный / Созерцательный",
                "Ровный / Созерцательный. Редкие обращения; человек не требует немедленной опоры. " +
                "Тон загадочный, философский, без нарочитого успокоения.");
        }

        if (score < 0.62)
        {
            return new InquiryPulseReading(
                score,
                "Настороженный / Ищущий опору",
                "Настороженный / Ищущий опору. Возвращается к шару чаще обычного. " +
                "Тон яснее и теплее; меньше тумана, больше опоры в формулировке.");
        }

        return new InquiryPulseReading(
            score,
            "Тревожный / Частый зов",
            "Тревожный / Частый зов. Плотность вопросов высокая, паузы короткие. " +
            "Тон мягче и прямее; дай ясную опору «здесь и сейчас», без загадок ради загадок. " +
            "Не упоминай частоту обращений, не читай нотаций и не называй этот параметр.");
    }
}

public readonly record struct InquiryPulseReading(double Score, string Category, string Aura);
