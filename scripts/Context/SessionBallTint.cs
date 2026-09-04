namespace CrystalBall.Context;

/// <summary>Оттенок шара текущей сессии — модуляция тона ответа ИИ.</summary>
public static class SessionBallTint
{
    public static string Name { get; private set; } = "";
    public static string Meaning { get; private set; } = "";

    /// <summary>В промпт — только смысл тона, без имени-краски («Лавандовый…»).</summary>
    public static string Modifier => Meaning ?? "";

    public static void Set(string name, string meaning)
    {
        Name = name ?? "";
        Meaning = meaning ?? "";
    }
}
