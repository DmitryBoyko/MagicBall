namespace CrystalBall.Profile;

public static class AstroCalculator
{
    public static void Populate(UserProfile profile, DateTime birth)
    {
        var sign = ResolveZodiac(birth.Month, birth.Day);
        profile.ZodiacSign = sign.Name;
        profile.AstrologicalElement = sign.Element;
        profile.RulingPlanet = sign.Planet;
        profile.DestinyNumber = DestinyNumber(birth);
        profile.ChineseTotem = ChineseTotem(birth.Year);
        profile.AgeGroup = AgeGroup(birth, DateTime.Now);
    }

    public static (string Name, string Element, string Planet) ResolveZodiac(int month, int day)
    {
        return (month, day) switch
        {
            (3, >= 21) or (4, <= 19) => ("Овен", "Стихия Огня", "Марс"),
            (4, >= 20) or (5, <= 20) => ("Телец", "Стихия Земли", "Венера"),
            (5, >= 21) or (6, <= 20) => ("Близнецы", "Стихия Воздуха", "Меркурий"),
            (6, >= 21) or (7, <= 22) => ("Рак", "Стихия Воды", "Луна"),
            (7, >= 23) or (8, <= 22) => ("Лев", "Стихия Огня", "Солнце"),
            (8, >= 23) or (9, <= 22) => ("Дева", "Стихия Земли", "Меркурий"),
            (9, >= 23) or (10, <= 22) => ("Весы", "Стихия Воздуха", "Венера"),
            (10, >= 23) or (11, <= 21) => ("Скорпион", "Стихия Воды", "Плутон"),
            (11, >= 22) or (12, <= 21) => ("Стрелец", "Стихия Огня", "Юпитер"),
            (12, >= 22) or (1, <= 19) => ("Козерог", "Стихия Земли", "Сатурн"),
            (1, >= 20) or (2, <= 18) => ("Водолей", "Стихия Воздуха", "Уран"),
            _ => ("Рыбы", "Стихия Воды", "Нептун"),
        };
    }

    public static int DestinyNumber(DateTime birth)
    {
        var digits = birth.ToString("ddMMyyyy").Sum(ch => ch - '0');
        while (digits > 9)
            digits = digits.ToString().Sum(ch => ch - '0');
        return digits;
    }

    public static string ChineseTotem(int year)
    {
        string[] animals =
        [
            "Год Крысы", "Год Быка", "Год Тигра", "Год Кролика",
            "Год Дракона", "Год Змеи", "Год Лошади", "Год Козы",
            "Год Обезьяны", "Год Петуха", "Год Собаки", "Год Кабана",
        ];
        var index = ((year - 4) % 12 + 12) % 12;
        return animals[index];
    }

    public static string AgeGroup(DateTime birth, DateTime now)
    {
        var age = now.Year - birth.Year;
        if (now.Month < birth.Month || (now.Month == birth.Month && now.Day < birth.Day))
            age--;

        if (age < 18)
            return "Подростковый тон общения";
        if (age < 30)
            return "Молодой, амбициозный тон общения";
        return "Зрелый, глубокий, психологический тон общения";
    }
}
