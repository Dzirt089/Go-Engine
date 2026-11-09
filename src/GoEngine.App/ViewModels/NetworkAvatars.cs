namespace GoEngine.App.ViewModels;

/// <summary>Восемь зверей-аватаров сетевого лобби.</summary>
/// <remarks>
/// Имя и значок — только для интерфейса; в сеть уходит номер аватара. Ребёнок выбирает
/// картинку, а не печатает имя, поэтому персональных данных в объявлении комнаты нет.
/// </remarks>
public static class NetworkAvatars
{
    /// <summary>Все доступные аватары.</summary>
    public static IReadOnlyList<AvatarChoice> All { get; } =
    [
        new(0, "Лиса", "🦊"),
        new(1, "Панда", "🐼"),
        new(2, "Кот", "🐱"),
        new(3, "Сова", "🦉"),
        new(4, "Лягушка", "🐸"),
        new(5, "Медведь", "🐻"),
        new(6, "Тигр", "🐯"),
        new(7, "Заяц", "🐰")
    ];

    /// <summary>Имя аватара по номеру; неизвестный номер — первый аватар.</summary>
    /// <param name="id">Номер аватара.</param>
    /// <returns>Короткое имя, например «Лиса».</returns>
    public static string NameOf(int id) => id is >= 0 and < 8 ? All[id].Name : All[0].Name;

    /// <summary>Значок аватара по номеру; неизвестный номер — первый аватар.</summary>
    /// <param name="id">Номер аватара.</param>
    /// <returns>Крупный значок.</returns>
    public static string GlyphOf(int id) => id is >= 0 and < 8 ? All[id].Glyph : All[0].Glyph;
}
