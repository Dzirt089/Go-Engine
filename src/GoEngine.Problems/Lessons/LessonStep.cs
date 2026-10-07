namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Шаг урока: объяснение или задание с проверяемым ходом.</summary>
/// <remarks>
/// Принимаемые ходы у задания перечисляет автор урока, а не перебор: урок объясняет приём,
/// и «правильных» ходов у него ровно столько, сколько задумано. Неверный ход позицию не меняет —
/// так же ведёт себя задача (<see cref="ProblemVerdict.Wrong"/>), и игрок может пробовать снова.
/// </remarks>
public sealed class LessonStep
{
    /// <summary>Создаёт шаг урока.</summary>
    /// <param name="kind">Вид шага: объяснение или задание.</param>
    /// <param name="title">Короткий заголовок шага.</param>
    /// <param name="text">Текст шага для игрока.</param>
    /// <param name="position">Позиция на доске.</param>
    /// <param name="answer">Принимаемые ходы задания; у объяснения пусто.</param>
    /// <param name="hint">Подсказка задания; у объяснения — <c>null</c>.</param>
    /// <param name="success">Что сказать игроку после верного хода.</param>
    /// <param name="failure">Что сказать игроку после неверного хода.</param>
    /// <exception cref="DomainException">Нарушен инвариант шага.</exception>
    public LessonStep(
        LessonStepKind kind,
        string title,
        string text,
        LessonPosition position,
        IReadOnlyList<Move> answer,
        Move? hint,
        string success,
        string failure)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(answer);

        if (kind == LessonStepKind.Task && answer.Count == 0)
        {
            throw new DomainException($"Шаг «{title}»: у задания нет ни одного принимаемого хода.");
        }

        if (kind == LessonStepKind.Explain && answer.Count > 0)
        {
            throw new DomainException($"Шаг «{title}»: у объяснения не бывает принимаемых ходов.");
        }

        foreach (var move in answer)
        {
            if (!move.Point.IsOnBoard(position.Size))
            {
                throw new DomainException($"Шаг «{title}»: принимаемый ход {move.Point} вне доски {position.Size}.");
            }

            if (move.Color != position.ToMove)
            {
                throw new DomainException($"Шаг «{title}»: принимаемый ход {move.Point} не того цвета, чей ход в позиции.");
            }
        }

        if (hint is { } hintMove && !answer.Contains(hintMove))
        {
            throw new DomainException($"Шаг «{title}»: подсказка {hintMove.Point} не входит в принимаемые ходы.");
        }

        Kind = kind;
        Title = title;
        Text = text;
        Position = position;
        Answer = answer;
        Hint = hint;
        Success = success;
        Failure = failure;
    }

    /// <summary>Вид шага.</summary>
    public LessonStepKind Kind { get; }

    /// <summary>Короткий заголовок шага.</summary>
    public string Title { get; }

    /// <summary>Текст шага для игрока.</summary>
    public string Text { get; }

    /// <summary>Позиция на доске.</summary>
    public LessonPosition Position { get; }

    /// <summary>Принимаемые ходы задания.</summary>
    public IReadOnlyList<Move> Answer { get; }

    /// <summary>Подсказка задания.</summary>
    public Move? Hint { get; }

    /// <summary>Что сказать игроку после верного хода.</summary>
    public string Success { get; }

    /// <summary>Что сказать игроку после неверного хода.</summary>
    public string Failure { get; }

    /// <summary>Задание: игрок должен сделать ход.</summary>
    public bool IsTask => Kind == LessonStepKind.Task;
}
