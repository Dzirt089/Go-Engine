namespace GoEngine.App.Services;

using GoEngine.Core;

/// <summary>Выбор оформления: светлая тема, тёмная или как в системе.</summary>
/// <remarks>
/// Хранится именем, а не числом: файл настроек читается человеком, и перестановка значений
/// не должна менять смысл уже сохранённого выбора (то же правило, что у системы подсчёта).
/// </remarks>
public sealed class ThemeChoice : Enumeration
{
    /// <summary>Как в системе: тему выбирает система, приложение следует за ней.</summary>
    public static ThemeChoice System { get; } = new(1, nameof(System), "как в системе");

    /// <summary>Светлая тема.</summary>
    public static ThemeChoice Light { get; } = new(2, nameof(Light), "светлая");

    /// <summary>Тёмная тема.</summary>
    public static ThemeChoice Dark { get; } = new(3, nameof(Dark), "тёмная");

    /// <summary>Все варианты в порядке показа: системная, светлая, тёмная.</summary>
    public static IReadOnlyList<ThemeChoice> All { get; } = [System, Light, Dark];

    private ThemeChoice(int id, string name, string descriptions)
        : base(id, name, descriptions)
    {
    }
}
