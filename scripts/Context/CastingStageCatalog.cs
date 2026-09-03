namespace CrystalBall.Context;

public enum CastingStage
{
    Name,
    Zodiac,
    Destiny,
    Time,
    Geo,
    Weather,
    Battery,
    Power,
    InquiryPulse,
    PhotoScan,
    PhotoMystic,
    PhotoPalette,
    PhotoLuminance,
    Entropy,
    BallMood,
    BallTint,
    WorldPressure,
    Weave,
    Await,
}

/// <summary>
/// Завуалированные фразы ритуала сборки промпта (без реальных значений).
/// </summary>
public static class CastingStageCatalog
{
    public static string Phrase(CastingStage stage) => stage switch
    {
        CastingStage.Name => "Слушаю имя, что зовёт тебя…",
        CastingStage.Zodiac => "Черчу знак по звёздам рождения…",
        CastingStage.Destiny => "Считаю нить судьбы и тотем года…",
        CastingStage.Time => "Ловлю час и дыхание дня…",
        CastingStage.Geo => "Ощущаю землю под твоими шагами…",
        CastingStage.Weather => "Читаю покров неба над тобой…",
        CastingStage.Battery => "Внимаю жару внутри сосуда…",
        CastingStage.Power => "Чувствую, откуда течёт сила…",
        CastingStage.InquiryPulse => "Слушаю ритм твоих обращений…",
        CastingStage.PhotoScan => "Перебираю отпечатки недавнего взгляда…",
        CastingStage.PhotoMystic => "Вынимаю скрытый образ из стекла…",
        CastingStage.PhotoPalette => "Собираю краски твоего мгновения…",
        CastingStage.PhotoLuminance => "Взвешиваю свет и тень кадра…",
        CastingStage.Entropy => "Бросаю якорь в поток знаков…",
        CastingStage.BallMood => "Настраиваю настроение стекла…",
        CastingStage.BallTint => "Впитываю оттенок хрусталя…",
        CastingStage.WorldPressure => "Слушаю давление внешнего круга…",
        CastingStage.Weave => "Плету нити в единый ответ…",
        CastingStage.Await => "Жду, пока шар произнесёт…",
        _ => "Шар внимает…",
    };
}
