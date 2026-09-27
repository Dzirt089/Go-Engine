namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Уровень сложности AI: от 30 кю до 5 кю и уровни Дан с нейросетью.</summary>
/// <remarks>
/// Уровень задаёт вид селектора и его настройки: долю случайности, бюджет playout'ов без сети
/// и бюджет итераций с сетью. <see cref="Id"/> уровня равен его рангу в кю, поэтому
/// <c>Enumeration.FromId&lt;DifficultyLevel&gt;(15)</c> даёт 15 кю. Чем больше кю, тем слабее игра.
/// Все уровни кю играют поиском (MCTS): эвристики и случайные ходы уступали поиску настолько,
/// что слабые ступени выглядели «тупыми» рядом с соседними (жалоба пользователя после живой игры).
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
        PlayoutConfig playoutConfig,
        NeuralBudget? neuralBudget = null)
        : base(id, name)
    {
        RankKyu = rankKyu;
        Kind = kind;
        PlayoutBudget = playoutBudget;
        TimeBudget = timeBudget;
        NeuralBudget = neuralBudget;
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

    /// <summary>Бюджет playout'ов MCTS; 0 — уровень думает по времени или играет только сетью.</summary>
    public int PlayoutBudget { get; }

    /// <summary>Бюджет времени на ход у уровней, которые думают по времени без сети, или <c>null</c>.</summary>
    public TimeSpan? TimeBudget { get; }

    /// <summary>Бюджет итераций на ход, если уровень играет с сетью; иначе <c>null</c>.</summary>
    /// <remarks>
    /// Уровни кю играют сетью только тогда, когда для размера доски есть модель; без модели
    /// они играют обычным MCTS по <see cref="PlayoutBudget"/> или <see cref="TimeBudget"/>.
    /// Бюджет задан в итерациях, поэтому время хода зависит от доски, а сила — нет (D-054).
    /// </remarks>
    public NeuralBudget? NeuralBudget { get; }

    /// <summary>Коэффициент исследования UCB1 для уровня.</summary>
    public double Ucb1C { get; }

    /// <summary>Доля случайных ходов в процентах.</summary>
    public int RandomnessPercent { get; }

    /// <summary>Настройки playout'ов MCTS.</summary>
    public PlayoutConfig PlayoutConfig { get; }

    /// <summary>30 кю: две итерации поиска без сети, одна с сетью; больше половины ходов — наугад.</summary>
    /// <remarks>
    /// Случайный ход здесь тоже проходит через тактический предохранитель и правила конца партии:
    /// уровень ошибается, но не подставляет группы и не доедает свою территорию.
    /// </remarks>
    public static DifficultyLevel Kyu30 { get; } = new(
        30, nameof(Kyu30), 30, SelectorKind.Mcts, 2, null, MctsConfig.DefaultUcb1C, 60, PlayoutConfig.Default,
        new NeuralBudget(1, 1, 1));

    /// <summary>25 кю: поиск чуть глубже, случайность заметно ниже.</summary>
    public static DifficultyLevel Kyu25 { get; } = new(
        25, nameof(Kyu25), 25, SelectorKind.Mcts, 8, null, MctsConfig.DefaultUcb1C, 35, PlayoutConfig.Default,
        new NeuralBudget(2, 2, 1));

    /// <summary>20 кю: поиск видит ближайшую тактику, случайность небольшая.</summary>
    public static DifficultyLevel Kyu20 { get; } = new(
        20, nameof(Kyu20), 20, SelectorKind.Mcts, 24, null, MctsConfig.DefaultUcb1C, 15, PlayoutConfig.Default,
        new NeuralBudget(3, 3, 2));

    /// <summary>15 кю: короткий, но настоящий поиск с сетью; бюджет — в итерациях, поэтому замеры
    /// на этом уровне воспроизводимы.</summary>
    public static DifficultyLevel Kyu15 { get; } = new(
        15, nameof(Kyu15), 15, SelectorKind.Mcts, 48, null, MctsConfig.DefaultUcb1C, 8, PlayoutConfig.Default,
        new NeuralBudget(5, 4, 3));

    /// <summary>10 кю: целевой уровень первой версии — секунда на ход без сети.</summary>
    public static DifficultyLevel Kyu10 { get; } = new(
        10, nameof(Kyu10), 10, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(1000), MctsConfig.DefaultUcb1C, 8,
        PlayoutConfig.Default, new NeuralBudget(9, 7, 5));

    /// <summary>8 кю: две секунды на ход без сети.</summary>
    public static DifficultyLevel Kyu8 { get; } = new(
        8, nameof(Kyu8), 8, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(2000), MctsConfig.DefaultUcb1C, 3,
        PlayoutConfig.Default, new NeuralBudget(14, 11, 8));

    /// <summary>5 кю: сильнейший уровень кю — три секунды на ход без сети, без случайности.</summary>
    public static DifficultyLevel Kyu5 { get; } = new(
        5, nameof(Kyu5), 5, SelectorKind.Mcts, 0, TimeSpan.FromMilliseconds(3000), MctsConfig.DefaultUcb1C, 0,
        PlayoutConfig.Default, new NeuralBudget(20, 16, 12));

    /// <summary>Ступени с сетью: уровень играет только с моделью под размер доски.</summary>
    /// <remarks>
    /// Ранги номинальные: силу этих уровней внешним соперником не мерили, поэтому в интерфейсе
    /// рядом с рангом всегда стоят движок и бюджет. Ступени различаются числом итераций поиска —
    /// сеть одна, а число просмотренных вариантов растёт с бюджетом. Играют только там, где есть
    /// модель под размер доски (D-038, D-039).
    /// </remarks>
    public static DifficultyLevel Kyu1 { get; } = new(
        1, nameof(Kyu1), 1, SelectorKind.Neural, 0, null, MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default,
        new NeuralBudget(26, 21, 15));

    /// <summary>1 дан: средняя ступень лестницы с нейросетью.</summary>
    public static DifficultyLevel Dan1 { get; } = new(
        -1, nameof(Dan1), -1, SelectorKind.Neural, 0, null, MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default,
        new NeuralBudget(34, 27, 20));

    /// <summary>5 дан: сильнейшая ступень — самый большой бюджет итераций с сетью.</summary>
    public static DifficultyLevel Dan5 { get; } = new(
        -5, nameof(Dan5), -5, SelectorKind.Neural, 0, null, MctsConfig.DefaultUcb1C, 0, PlayoutConfig.Default,
        new NeuralBudget(44, 35, 25));

    /// <summary>Создаёт селектор этого уровня.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="boardSize">Размер доски партии: от него зависит бюджет итераций с сетью.</param>
    /// <param name="evaluator">Оценка позиции нейросетью.</param>
    /// <param name="modelsDirectory">Каталог файлов моделей.</param>
    /// <returns>Селектор, играющий на уровне.</returns>
    /// <remarks>
    /// Единственная точка создания селектора — <see cref="AiFactory.Create"/>; уровень лишь
    /// передаёт ему свои настройки.
    /// </remarks>
    public IMoveSelector CreateSelector(
        Random random,
        BoardSize? boardSize = null,
        IPositionEvaluator? evaluator = null,
        string? modelsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        return AiFactory.Create(this, random, boardSize, evaluator, modelsDirectory);
    }
}
