namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Уровень сложности AI: от 30 кю до 5 кю.</summary>
/// <remarks>
/// Уровень задаёт вид селектора и его настройки: долю случайности для эвристик, бюджет
/// playout'ов и коэффициент исследования для MCTS. <see cref="Id"/> уровня равен его рангу
/// в кю, поэтому <c>Enumeration.FromId&lt;DifficultyLevel&gt;(15)</c> даёт 15 кю.
/// Чем больше кю, тем слабее игра.
/// </remarks>
public sealed class DifficultyLevel : Enumeration
{
    /// <summary>Доля случайности, при которой селектор ходит полностью наугад.</summary>
    private const int FullRandomness = 100;

    private DifficultyLevel(
        int id,
        string name,
        int rankKyu,
        SelectorKind kind,
        int playoutBudget,
        double ucb1C,
        int randomnessPercent,
        PlayoutConfig playoutConfig)
        : base(id, name)
    {
        RankKyu = rankKyu;
        Kind = kind;
        PlayoutBudget = playoutBudget;
        Ucb1C = ucb1C;
        RandomnessPercent = randomnessPercent;
        PlayoutConfig = playoutConfig;
    }

    /// <summary>Ранг в кю: больше — слабее.</summary>
    public int RankKyu { get; }

    /// <summary>Вид селектора уровня.</summary>
    public SelectorKind Kind { get; }

    /// <summary>Бюджет playout'ов MCTS; 0 — уровень играет без поиска.</summary>
    public int PlayoutBudget { get; }

    /// <summary>Коэффициент исследования UCB1 для уровня.</summary>
    public double Ucb1C { get; }

    /// <summary>Доля случайных ходов в процентах.</summary>
    public int RandomnessPercent { get; }

    /// <summary>Настройки playout'ов MCTS.</summary>
    public PlayoutConfig PlayoutConfig { get; }

    /// <summary>30 кю: случайные ходы без эвристик.</summary>
    public static DifficultyLevel Kyu30 { get; } =
        new(30, nameof(Kyu30), 30, SelectorKind.Random, 0, Mcts.MctsConfig.DefaultUcb1C, FullRandomness, PlayoutConfig.Default);

    /// <summary>25 кю: эвристики, но больше половины ходов — наугад.</summary>
    public static DifficultyLevel Kyu25 { get; } =
        new(25, nameof(Kyu25), 25, SelectorKind.Heuristic, 0, Mcts.MctsConfig.DefaultUcb1C, 60, PlayoutConfig.Default);

    /// <summary>20 кю: эвристики с небольшим случайным запасом.</summary>
    public static DifficultyLevel Kyu20 { get; } =
        new(20, nameof(Kyu20), 20, SelectorKind.Heuristic, 0, Mcts.MctsConfig.DefaultUcb1C, 15, PlayoutConfig.Default);

    /// <summary>15 кю: короткий поиск MCTS поверх эвристик.</summary>
    public static DifficultyLevel Kyu15 { get; } =
        new(15, nameof(Kyu15), 15, SelectorKind.Mcts, 4, Mcts.MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>10 кю: целевой уровень первой версии, поиск средней длины.</summary>
    public static DifficultyLevel Kyu10 { get; } =
        new(10, nameof(Kyu10), 10, SelectorKind.Mcts, 12, Mcts.MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>8 кю: более длинный поиск.</summary>
    public static DifficultyLevel Kyu8 { get; } =
        new(8, nameof(Kyu8), 8, SelectorKind.Mcts, 24, Mcts.MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>5 кю: самый сильный уровень первой версии.</summary>
    public static DifficultyLevel Kyu5 { get; } =
        new(5, nameof(Kyu5), 5, SelectorKind.Mcts, 48, Mcts.MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>Создаёт селектор этого уровня.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <remarks>
    /// Единственная точка создания селектора — <see cref="AiFactory.Create"/>; уровень лишь
    /// передаёт ему свои настройки.
    /// </remarks>
    public IMoveSelector CreateSelector(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return AiFactory.Create(this, random);
    }
}
