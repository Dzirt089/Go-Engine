using GoEngine.Problems;

namespace GoEngine.App.ViewModels;

/// <summary>Строка словаря терминов: название, оригинал, объяснение и где разбирается.</summary>
/// <param name="Name">Название термина по-русски.</param>
/// <param name="Original">Оригинал: японское или китайское слово; пусто — оригинала нет.</param>
/// <param name="Text">Объяснение по-русски.</param>
/// <param name="Lessons">Где термин разбирается: «Уроки: …» или пусто.</param>
public readonly record struct GlossaryLine(string Name, string Original, string Text, string Lessons);

/// <summary>Модель представления словаря терминов Го.</summary>
/// <remarks>
/// Словарь — это данные (<see cref="GlossaryLibrary"/>), а здесь только то, что нужно виду:
/// строки списка на русском и ссылки на уроки, где термин встречается. Обучение ведётся
/// по-русски, поэтому объяснение стоит первым, а оригинал — справкой рядом
/// (замечание пользователя 2026-10-07).
/// </remarks>
public sealed class GlossaryViewModel
{
    /// <summary>Создаёт словарь с терминами библиотеки.</summary>
    public GlossaryViewModel()
        : this(GlossaryLibrary.All)
    {
    }

    /// <summary>Создаёт словарь с готовым набором терминов.</summary>
    /// <param name="terms">Термины словаря.</param>
    public GlossaryViewModel(IReadOnlyList<GoTerm> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        Terms = terms.Select(ToLine).ToList().AsReadOnly();
    }

    /// <summary>Строки словаря в порядке названий.</summary>
    public IReadOnlyList<GlossaryLine> Terms { get; }

    /// <summary>Число терминов.</summary>
    public int Count => Terms.Count;

    /// <summary>Превращает термин в строку списка.</summary>
    /// <param name="term">Термин словаря.</param>
    /// <returns>Строка для вида.</returns>
    private static GlossaryLine ToLine(GoTerm term)
    {
        var titles = term.Lessons
            .Select(id => LessonLibrary.ById(id)?.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .ToArray();

        var where = titles.Length > 0
            ? "Уроки: " + string.Join(", ", titles)
            : string.Empty;

        return new GlossaryLine(term.Name, term.Original, term.Text, where);
    }
}
