namespace GoEngine.AI.Mcts;

/// <summary>Настройки MCTS-селектора.</summary>
/// <param name="PlayoutBudget">Сколько playout'ов делает селектор на один ход.</param>
/// <param name="Ucb1C">Коэффициент исследования UCB1: больше — шире поиск, меньше — глубже разработка лучшего хода.</param>
/// <remarks>
/// Бюджет — параметр, а не константа в коде (<c>AGENTS_GO.md</c>, п. 7): уровень сложности задаёт
/// его значением, а тесты — уменьшают, чтобы прогон оставался быстрым.
/// </remarks>
public readonly record struct MctsConfig(int PlayoutBudget, double Ucb1C)
{
    /// <summary>Бюджет playout'ов по умолчанию.</summary>
    public const int DefaultPlayoutBudget = 5000;

    /// <summary>Коэффициент исследования UCB1 по умолчанию.</summary>
    public const double DefaultUcb1C = 1.41;

    /// <summary>Настройки по умолчанию: 5000 playout'ов, UCB1 = 1.41.</summary>
    public static MctsConfig Default => new(DefaultPlayoutBudget, DefaultUcb1C);
}
