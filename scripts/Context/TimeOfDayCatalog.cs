namespace CrystalBall.Context;

public enum DayPart
{
    Morning,
    Day,
    Evening,
    Night,
}

public static class TimeOfDayCatalog
{
    public static DayPart FromHour(int hour)
    {
        if (hour >= 4 && hour < 12)
            return DayPart.Morning;
        if (hour >= 12 && hour < 17)
            return DayPart.Day;
        if (hour >= 17 && hour < 22)
            return DayPart.Evening;
        return DayPart.Night;
    }

    public static string Atmosphere(DayPart part) => part switch
    {
        DayPart.Morning => "Рассветные сумерки и утро",
        DayPart.Day => "Разгар дня и социальная активность",
        DayPart.Evening => "Преддверие заката и вечерняя тишина",
        _ => "Глубокая ночь и час ведьм",
    };

    public static string FolderName(DayPart part) => part switch
    {
        DayPart.Morning => "morning",
        DayPart.Day => "day",
        DayPart.Evening => "evening",
        _ => "night",
    };

    public static string Season(DateTime now) => now.Month switch
    {
        12 or 1 or 2 => "Глубокая зима",
        3 or 4 or 5 => "Пробуждение весны",
        6 or 7 or 8 => "Позднее лето",
        _ => "Ранняя осень",
    };

    public static DayPart ParsePreset(string preset, DateTime now)
    {
        return preset.Trim().ToLowerInvariant() switch
        {
            "morning" or "утро" => DayPart.Morning,
            "day" or "день" => DayPart.Day,
            "evening" or "вечер" => DayPart.Evening,
            "night" or "ночь" => DayPart.Night,
            _ => FromHour(now.Hour),
        };
    }
}
