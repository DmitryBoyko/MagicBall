namespace CrystalBall.Vision;

/// <summary>
/// Переводит сырой английский тег ImageNet в мистический архетип на русском.
/// Чистый C#, без Godot API — можно вызывать из фонового потока.
/// </summary>
public static class MysticTagConverter
{
    public const string UnknownArchetype = "Неявленный Знак Вещей";

    private static readonly (string[] Keys, string Archetype)[] Rules =
    [
        (["husky", "malamute", "eskimo dog", "samoyed", "retriever", "shepherd", "terrier", "hound",
            "poodle", "beagle", "boxer", "bulldog", "chihuahua", "dalmatian", "doberman", "collie",
            "corgi", "spaniel", "puppy", "dog"], "Архетип Верного Спутника"),
        (["tabby", "tiger cat", "persian cat", "siamese", "egyptian cat", "lynx", "cougar", "leopard",
            "jaguar", "lion", "tiger", "cat"], "Лунный Страж Порога"),
        (["laptop", "notebook", "desktop computer", "computer keyboard", "mouse", "monitor", "screen",
            "remote control", "cellular telephone", "iPod", "modem", "web site", "joystick"], "Цифровая Вуаль"),
        (["knife", "cleaver", "letter opener", "guillotine", "scabbard", "sword", "revolver",
            "assault rifle", "rifle", "cannon", "projectile", "hatchet", "axe"], "Стальное Отсечение"),
        (["wine bottle", "beer bottle", "beer glass", "goblet", "red wine", "cocktail shaker",
            "whiskey jug", "pitcher"], "Чаша Скрытых Возлияний"),
        (["wallet", "purse", "safe", "piggy bank", "mailbag", "backpack", "plastic bag"], "Страж Материального Круга"),
        (["mirror", "looking glass", "medicine chest"], "Отражение Истинного Лика"),
        (["pizza", "bagel", "hotdog", "cheeseburger", "hot pot", "carbonara", "burrito", "trifle",
            "ice cream", "pretzel", "guacamole"], "Пир Земных Изобилий"),
        (["candle", "oil lamp", "torch", "lighter"], "Живой Огонь Намерения"),
        (["book", "comic book", "binder", "envelope", "packet", "menu"], "Тайный Свиток Знания"),
        (["hourglass", "analog clock", "digital clock", "stopwatch", "magnetic compass"], "Песочные Часы Перелома"),
        (["mask", "gasmask", "ski mask", "face powder"], "Чужая Маска Сцены"),
        (["castle", "church", "monastery", "palace", "altar", "triumphal arch"], "Каменный Престол Власти"),
        (["ship", "wreck", "lifeboat", "canoe", "yawl", "catamaran", "speedboat"], "Переправа Через Порог"),
        (["mountain", "alp", "volcano", "cliff", "valley"], "Горный Пик Испытания"),
        (["bridge", "viaduct", "suspension"], "Разрушенный или Живой Мост"),
        (["flower", "daisy", "rose", "pot", "vase", "bouquet"], "Вектор Живого Прорастания"),
        (["spider", "tick", "scorpion", "centipede"], "Железный Капкан Тени"),
        (["snake", "viper", "cobra", "python", "thunder snake"], "Скрытый Огонь Искушения"),
        (["bird", "jay", "magpie", "raven", "kite", "vulture", "peacock", "lorikeet"], "Вестник Дальнего Края"),
        (["wolf", "coyote", "hyena", "dingo", "red wolf"], "Голодный Волк Желания"),
        (["horse", "zebra", "sorrel"], "Дикий Бег Судьбы"),
        (["car", "limousine", "sports car", "minivan", "jeep", "cab", "racer"], "Стальной Колесный Путь"),
        (["phone", "pay-phone", "dial telephone"], "Нить Далёкого Голоса"),
        (["camera", "reflex camera", "lens cap", "tripod"], "Свидетель Украденного Мгновения"),
        (["bed", "four-poster", "studio couch", "quilt"], "Порог Сна и Яви"),
        (["chair", "throne", "rocking chair"], "Место Силы и Ожидания"),
        (["bottlecap", "water bottle", "pop bottle"], "Сосуд Удержанной Влаги"),
        (["umbrella", "gown", "cloak", "fur coat", "kimono"], "Покров, Скрывающий Истинное"),
        (["lock", "padlock", "combination lock"], "Ржавый Замок Старой Обиды"),
        (["key", "keycard"], "Забытый Ключ Решения"),
        (["chain", "manacle", "handkerchief"], "Тяжёлый Якорь Привязанности"),
        (["fire", "stove", "barbecue"], "Очаг, Требующий Меры"),
        (["water", "lakeside", "seashore", "fountain", "waterfall"], "Стоячая или Живая Вода"),
        (["tree", "oak", "pine", "bonsai"], "Гнилой или Живой Корень Рода"),
        (["money", "slot", "vending machine"], "Двуликая Монета Выбора"),
        (["weapon", "shield", "armor", "breastplate", "cuirass"], "Железный Щит Обороны"),
        (["child", "teddy", "toy"], "Хрупкий Сосуд Начала"),
        (["skull", "mask"], "Чёрная Свеча Завершения"),
    ];

    public static string Convert(string? englishTag)
    {
        if (string.IsNullOrWhiteSpace(englishTag))
            return UnknownArchetype;

        var haystack = englishTag.Trim().ToLowerInvariant();
        foreach (var (keys, archetype) in Rules)
        {
            foreach (var key in keys)
            {
                if (haystack.Contains(key, StringComparison.Ordinal))
                    return archetype;
            }
        }

        return $"Архетип Сокрытой Формы ({Capitalize(englishTag)})";
    }

    private static string Capitalize(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            return trimmed;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }
}
