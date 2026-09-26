namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Уровень сложности AI: от 30 кю до 10 кю в первой версии.</summary>
/// <remarks>
/// Уровень задаёт две вещи: каким селектором играет AI и сколько случайности в его решениях.
/// Чем больше кю, тем слабее игра. Полный набор уровней и MCTS добавляются в T-017,
/// здесь — базовые 30, 25 и 20 кю.
/// </remarks>
public sealed class DifficultyLevel : Enumeration
{
    /// <summary>Доля случайности, при которой селектор ходит полностью наугад.</summary>
    private const int FullRandomness = 100;

    private DifficultyLevel(int id, string name, int rankKyu, int playoutBudget, int randomnessPercent)
        : base(id, name)
    {
        RankKyu = rankKyu;
        PlayoutBudget = playoutBudget;
        RandomnessPercent = randomnessPercent;
    }

    /// <summary>Ранг в кю: больше — слабее.</summary>
    public int RankKyu { get; }

    /// <summary>Бюджет playout'ов MCTS; 0 — уровень играет без MCTS.</summary>
    public int PlayoutBudget { get; }

    /// <summary>Доля случайных ходов в процентах.</summary>
    public int RandomnessPercent { get; }

    /// <summary>30 кю: случайные ходы без эвристик.</summary>
    public static DifficultyLevel Kyu30 { get; } = new(30, nameof(Kyu30), 30, 0, FullRandomness);

    /// <summary>25 кю: эвристики, но больше половины ходов — наугад.</summary>
    public static DifficultyLevel Kyu25 { get; } = new(25, nameof(Kyu25), 25, 0, 60);

    /// <summary>20 кю: эвристики со случайным запасом.</summary>
    public static DifficultyLevel Kyu20 { get; } = new(20, nameof(Kyu20), 20, 0, 15);

    /// <summary>Создаёт селектор этого уровня.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <remarks>Все уровни без MCTS играют эвристиками; 30 кю эвристик не использует вовсе.</remarks>
    public IMoveSelector CreateSelector(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return RandomnessPercent >= FullRandomness
            ? new RandomMoveSelector(random)
            : new HeuristicMoveSelector(random, RandomnessPercent);
    }
}
