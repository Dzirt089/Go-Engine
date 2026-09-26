namespace GoEngine.AI.Mcts;

using GoEngine.Core;

/// <summary>Настройки MCTS-селектора: сколько искать и как считать оценку варианта.</summary>
/// <remarks>
/// Бюджет задаётся ровно одним способом: либо числом playout'ов (детерминированные тесты),
/// либо временем на ход (игра — сила не зависит от скорости машины). Оба сразу или ни одного —
/// нарушение инварианта настроек. Позиционная запись не может валидировать аргументы, поэтому
/// конструктор объявлен явно, а свойства доступны только для чтения.
/// </remarks>
public readonly record struct MctsConfig
{
    /// <summary>Бюджет playout'ов по умолчанию: детерминированный режим.</summary>
    public const int DefaultPlayoutBudget = 5000;

    /// <summary>Коэффициент исследования UCB1 по умолчанию.</summary>
    public const double DefaultUcb1C = 1.41;

    /// <summary>Создаёт настройки с бюджетом по числу playout'ов.</summary>
    /// <param name="playoutBudget">Сколько playout'ов делает селектор на ход; должно быть положительным.</param>
    /// <param name="ucb1C">Коэффициент исследования UCB1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Бюджет не положительный или коэффициент отрицательный.</exception>
    public MctsConfig(int playoutBudget, double ucb1C = DefaultUcb1C) : this(playoutBudget, null, ucb1C)
    {
    }

    /// <summary>Создаёт настройки с бюджетом по времени на ход.</summary>
    /// <param name="timeBudget">Сколько времени селектор думает над ходом; должно быть положительным.</param>
    /// <param name="ucb1C">Коэффициент исследования UCB1.</param>
    /// <exception cref="ArgumentOutOfRangeException">Время не положительное или коэффициент отрицательный.</exception>
    public MctsConfig(TimeSpan timeBudget, double ucb1C = DefaultUcb1C) : this(null, timeBudget, ucb1C)
    {
    }

    /// <summary>Создаёт настройки с явным указанием обоих бюджетов.</summary>
    /// <param name="playoutBudget">Бюджет playout'ов или <c>null</c>.</param>
    /// <param name="timeBudget">Бюджет времени или <c>null</c>.</param>
    /// <param name="ucb1C">Коэффициент исследования UCB1.</param>
    /// <exception cref="DomainException">Заданы оба бюджета или ни одного.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Заданный бюджет не положительный.</exception>
    public MctsConfig(int? playoutBudget, TimeSpan? timeBudget, double ucb1C = DefaultUcb1C)
    {
        var byPlayouts = playoutBudget is not null;
        var byTime = timeBudget is not null;

        if (byPlayouts == byTime)
        {
            throw new DomainException("У MCTS задаётся ровно один бюджет: число playout'ов или время на ход.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ucb1C);

        if (playoutBudget is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playoutBudget), playoutBudget, "Бюджет playout'ов должен быть положительным.");
        }

        if (timeBudget is not null && timeBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeBudget), timeBudget, "Бюджет времени должен быть положительным.");
        }

        PlayoutBudget = playoutBudget;
        TimeBudget = timeBudget;
        Ucb1C = ucb1C;
    }

    /// <summary>Бюджет по числу playout'ов или <c>null</c>, если бюджет задан временем.</summary>
    public int? PlayoutBudget { get; }

    /// <summary>Бюджет по времени на ход или <c>null</c>, если бюджет задан числом playout'ов.</summary>
    public TimeSpan? TimeBudget { get; }

    /// <summary>Коэффициент исследования UCB1: больше — шире поиск.</summary>
    public double Ucb1C { get; }

    /// <summary>Настройки по умолчанию: 5000 playout'ов, UCB1 = 1.41.</summary>
    public static MctsConfig Default => new(DefaultPlayoutBudget, DefaultUcb1C);

    /// <summary>Бюджет времени по умолчанию для размера доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>2000 мс для 9×9, 4000 мс для 13×13, 6000 мс для 19×19.</returns>
    /// <remarks>Чем больше доска, тем больше вариантов — и тем дольше разумный поиск.</remarks>
    public static MctsConfig DefaultForSize(BoardSize size) => size.Value switch
    {
        var value when value == BoardSize.Size13.Value => new(TimeSpan.FromMilliseconds(MediumBoardMilliseconds)),
        var value when value == BoardSize.Size19.Value => new(TimeSpan.FromMilliseconds(LargeBoardMilliseconds)),
        _ => new(TimeSpan.FromMilliseconds(SmallBoardMilliseconds))
    };

    /// <summary>Время на ход для доски 9×9 в миллисекундах.</summary>
    private const int SmallBoardMilliseconds = 2000;

    /// <summary>Время на ход для доски 13×13 в миллисекундах.</summary>
    private const int MediumBoardMilliseconds = 4000;

    /// <summary>Время на ход для доски 19×19 в миллисекундах.</summary>
    private const int LargeBoardMilliseconds = 6000;
}
