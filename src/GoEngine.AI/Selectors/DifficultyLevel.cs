namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Уровень сложности AI: от 30 кю до 5 кю и уровни Дан с нейросетью.</summary>
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
        TimeSpan? timeBudget,
        double ucb1C,
        int randomnessPercent,
        PlayoutConfig playoutConfig)
        : base(id, name)
    {
        RankKyu = rankKyu;
        Kind = kind;
        PlayoutBudget = playoutBudget;
        TimeBudget = timeBudget;
        Ucb1C = ucb1C;
        RandomnessPercent = randomnessPercent;
        PlayoutConfig = playoutConfig;
    }

    /// <summary>Ранг в кю: больше — слабее. У уровней Дан ранг отрицательный.</summary>
    public int RankKyu { get; }

    /// <summary>Нужна ли уровню оценка нейросети.</summary>
    /// <remarks>
    /// Уровни Дан играют только с сетью и только на досках 19×19 и 13×13: на 9×9 её оценка
    /// неправдоподобна, поэтому запрос такого уровня для 9×9 — отказ (<c>DECISIONS.md</c>, D-038).
    /// </remarks>
    public bool NeedsNetwork => Kind == SelectorKind.Neural;

    /// <summary>Вид селектора уровня.</summary>
    public SelectorKind Kind { get; }

    /// <summary>Бюджет playout'ов MCTS; 0 — уровень играет без поиска или думает по времени.</summary>
    public int PlayoutBudget { get; }

    /// <summary>Бюджет времени на ход у уровней, которые думают по времени, или <c>null</c>.</summary>
    public TimeSpan? TimeBudget { get; }

    /// <summary>Коэффициент исследования UCB1 для уровня.</summary>
    public double Ucb1C { get; }

    /// <summary>Доля случайных ходов в процентах.</summary>
    public int RandomnessPercent { get; }

    /// <summary>Настройки playout'ов MCTS.</summary>
    public PlayoutConfig PlayoutConfig { get; }

    /// <summary>30 кю: случайные ходы без эвристик.</summary>
    public static DifficultyLevel Kyu30 { get; } =
        new(30, nameof(Kyu30), 30, SelectorKind.Random, 0, null, MctsConfig.DefaultUcb1C, FullRandomness, PlayoutConfig.Default);

    /// <summary>25 кю: эвристики, но больше половины ходов — наугад.</summary>
    public static DifficultyLevel Kyu25 { get; } =
        new(25, nameof(Kyu25), 25, SelectorKind.Heuristic, 0, null, MctsConfig.DefaultUcb1C, 60, PlayoutConfig.Default);

    /// <summary>20 кю: эвристики с небольшим случайным запасом.</summary>
    public static DifficultyLevel Kyu20 { get; } =
        new(20, nameof(Kyu20), 20, SelectorKind.Heuristic, 0, null, MctsConfig.DefaultUcb1C, 15, PlayoutConfig.Default);

    /// <summary>15 кю: короткий поиск с заметной долей случайных ходов; бюджет — по playout'ам,
    /// поэтому замеры на этом уровне воспроизводимы.</summary>
    public static DifficultyLevel Kyu15 { get; } =
        new(15, nameof(Kyu15), 15, SelectorKind.Mcts, 6, null, MctsConfig.DefaultUcb1C, 15, PlayoutConfig.Default);

    /// <summary>10 кю: целевой уровень первой версии — секунда на ход.</summary>
    public static DifficultyLevel Kyu10 { get; } =
        new(10, nameof(Kyu10), 10, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(1000), MctsConfig.DefaultUcb1C, 8, PlayoutConfig.Default);

    /// <summary>8 кю: две секунды на ход.</summary>
    public static DifficultyLevel Kyu8 { get; } =
        new(8, nameof(Kyu8), 8, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(2000), MctsConfig.DefaultUcb1C, 3, PlayoutConfig.Default);

    /// <summary>5 кю: самый сильный уровень первой версии — три секунды на ход без случайности.</summary>
    public static DifficultyLevel Kyu5 { get; } =
        new(5, nameof(Kyu5), 5, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(3000), MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>5 дан: сильнейший уровень — поиск с оценкой нейросети, две секунды на ход.</summary>
    /// <remarks>
    /// Ранг отрицательный, потому что Дан выше любого кю. Играет только на 19×19 и 13×13
    /// и только с загруженной моделью (D-038).
    /// </remarks>
    public static DifficultyLevel Dan5 { get; } =
        new(-5, nameof(Dan5), -5, SelectorKind.Neural, 0, TimeSpan.FromMilliseconds(2000), MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default);

    /// <summary>Создаёт селектор этого уровня.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <remarks>
    /// Единственная точка создания селектора — <see cref="AiFactory.Create"/>; уровень лишь
    /// передаёт ему свои настройки.
    /// </remarks>
    public IMoveSelector CreateSelector(Random random, BoardSize? boardSize = null, IPositionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        return AiFactory.Create(this, random, boardSize, evaluator);
    }
}