namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Урок: последовательность шагов от объяснения к заданию.</summary>
/// <remarks>
/// <para>
/// Урок не зависит от интерфейса: он знает доску, шаги и принимаемые ходы. Тем же свойством
/// обладают задачи (<see cref="Problem"/>), и по той же причине: материал обязан проверяться
/// тестами без окна и работать на телефоне без ввода-вывода.
/// </para>
/// <para>
/// <see cref="Source"/> называет источник, по которому собран урок: тексты уроков пересказывают
/// методику обучающих видео, и ссылка на источник — часть материала, а не служебная пометка.
/// </para>
/// </remarks>
public sealed class Lesson
{
    /// <summary>Создаёт урок.</summary>
    /// <param name="id">Идентификатор урока.</param>
    /// <param name="title">Название урока.</param>
    /// <param name="summary">Короткое описание: о чём урок.</param>
    /// <param name="source">Источник материала.</param>
    /// <param name="steps">Шаги урока в порядке прохождения.</param>
    /// <exception cref="DomainException">Нарушен инвариант урока.</exception>
    public Lesson(string id, string title, string summary, string source, IReadOnlyList<LessonStep> steps)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            throw new DomainException($"Урок {id}: в нём нет ни одного шага.");
        }

        Id = id;
        Title = title;
        Summary = summary;
        Source = source ?? string.Empty;
        Steps = steps;
    }

    /// <summary>Идентификатор урока.</summary>
    public string Id { get; }

    /// <summary>Название урока для игрока.</summary>
    public string Title { get; }

    /// <summary>Короткое описание урока.</summary>
    public string Summary { get; }

    /// <summary>Источник, по которому собран урок.</summary>
    public string Source { get; }

    /// <summary>Шаги урока.</summary>
    public IReadOnlyList<LessonStep> Steps { get; }

    /// <summary>Число шагов.</summary>
    public int StepCount => Steps.Count;

    /// <summary>Сколько шагов в уроке — заданий.</summary>
    public int TaskCount => Steps.Count(static step => step.IsTask);

    /// <summary>Подпись урока в списке: номер и название.</summary>
    /// <remarks>
    /// Тем же правилом живёт список задач («ts-001 · Спасти пять камней»): игрок ищет урок
    /// по названию, а идентификатор нужен, чтобы сослаться на него в документации.
    /// </remarks>
    public string Label => $"{Id} · {Title}";
}
