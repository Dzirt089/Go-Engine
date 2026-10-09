namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Вопрос разбора: найдите ход, который играет сильная сторона.</summary>
/// <remarks>
/// Вопрос — это и есть практика: игрок не читает разбор, а сам ищет ход на доске. Принимаемых
/// ходов может быть несколько (равноценные продолжения), но партия после верного ответа идёт
/// своим ходом: разбор показывает, как сыграли в этой партии.
/// </remarks>
public sealed class ReviewQuiz
{
    /// <summary>Создаёт вопрос разбора.</summary>
    /// <param name="answer">Принимаемые ходы.</param>
    /// <param name="hint">Подсказка; <c>null</c> — подсказывать нечего.</param>
    /// <param name="success">Что сказать игроку после верного хода.</param>
    /// <param name="failure">Что сказать игроку после неверного хода.</param>
    /// <exception cref="DomainException">У вопроса нет принимаемых ходов.</exception>
    public ReviewQuiz(IReadOnlyList<Move> answer, Move? hint, string success, string failure)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Count == 0)
        {
            throw new DomainException("У вопроса разбора нет ни одного принимаемого хода.");
        }

        Answer = answer;
        Hint = hint;
        Success = string.IsNullOrWhiteSpace(success) ? ReviewJson.DefaultSuccess : success;
        Failure = string.IsNullOrWhiteSpace(failure) ? ReviewJson.DefaultFailure : failure;
    }

    /// <summary>Принимаемые ходы.</summary>
    public IReadOnlyList<Move> Answer { get; }

    /// <summary>Подсказка: точка, на которую стоит посмотреть.</summary>
    public Move? Hint { get; }

    /// <summary>Похвала после верного хода.</summary>
    public string Success { get; }

    /// <summary>Объяснение после неверного хода.</summary>
    public string Failure { get; }
}
