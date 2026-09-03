namespace CrystalBall.Context;

/// <summary>Оттенок шара текущей сессии — модуляция тона ответа ИИ.</summary>
public static class SessionBallTint
{
    public static string Name { get; private set; } = "";
    public static string Meaning { get; private set; } = "";

    public static string Modifier =>
        string.IsNullOrEmpty(Name) ? "" : $"{Name} — {Meaning}";

    public static void Set(string name, string meaning)
    {
        Name = name ?? "";
        Meaning = meaning ?? "";
    }
}
