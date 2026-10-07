namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Термин игры Го из словаря обучения.</summary>
/// <remarks>
/// Словарь объясняет игроку слова, которыми говорят об игре: русское название, японский или
/// китайский оригинал и короткое объяснение на русском. Ссылки на уроки показывают, где термин
/// разбирается, — словарь растёт вместе с курсом (замечание пользователя 2026-10-07).
/// </remarks>
public sealed class GoTerm
{
    /// <summary>Создаёт термин словаря.</summary>
    /// <param name="name">Название термина по-русски.</param>
    /// <param name="original">Оригинал: японское или китайское слово; пусто — оригинала нет.</param>
    /// <param name="text">Объяснение термина по-русски.</param>
    /// <param name="lessons">Идентификаторы уроков, в которых термин разбирается.</param>
    /// <exception cref="DomainException">Нарушен инвариант термина.</exception>
    public GoTerm(string name, string original, string text, IReadOnlyList<string> lessons)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(lessons);

        Name = name;
        Original = original ?? string.Empty;
        Text = text;
        Lessons = lessons;
    }

    /// <summary>Название термина по-русски.</summary>
    public string Name { get; }

    /// <summary>Оригинал термина: японское или китайское слово.</summary>
    public string Original { get; }

    /// <summary>Объяснение термина по-русски.</summary>
    public string Text { get; }

    /// <summary>Уроки, в которых термин разбирается.</summary>
    public IReadOnlyList<string> Lessons { get; }
}
