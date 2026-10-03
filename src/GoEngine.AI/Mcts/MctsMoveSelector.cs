namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Выбор хода методом Монте-Карло (MCTS) — <c>DECISIONS.md</c>, D-003.</summary>
/// <remarks>
/// Селектор связывает три части: поиск по дереву (<see cref="MctsSearch"/>), оценку позиции
/// (<see cref="PlayoutEvaluation"/> или <see cref="NeuralEvaluation"/>) и отбор допустимых ходов
/// (<see cref="IMoveFilter"/>). Один ход — это <see cref="MctsConfig.PlayoutBudget"/> итераций
/// «спуск → расширение → оценка → передача результата»; ход выбирается по числу посещений: чем чаще
/// вариант приводил к победе, тем больше он исследован.
/// Отбор идёт в три правила: тактический предохранитель не даёт подставлять свои группы,
/// <see cref="EndgamePolicy"/> не даёт доедать свою территорию, а <see cref="PrisonerPolicy"/> —
/// кормить пленных у своих доказанно мёртвых групп. Если играть нечего, селектор пасует,
/// и партия заканчивается двумя пасами (жалоба 2026-10-03).
/// Селектор детерминирован при фиксированном <see cref="Random"/> и своего генератора
/// не создаёт (<c>AGENTS_GO.md</c>, п. 7). Логов нет: слой AI не логирует.
/// </remarks>
public sealed class MctsMoveSelector : IMoveSelector
{
    /// <summary>Шкала процентов: доля случайности задаётся в процентах.</summary>
    private const int PercentScale = 100;

    private readonly Random _random;
    private readonly MctsSearch _search;
    private readonly IMoveFilter _filter;
    private readonly int _randomnessPercent;
    private readonly bool _usesNetwork;

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="playoutPolicy">Политика игры в playout'ах.</param>
    /// <param name="config">Настройки поиска.</param>
    /// <param name="randomnessPercent">Доля случайных ходов в процентах: слабые уровни иногда
    /// ходят наугад вместо поиска — это и делает их слабее.</param>
    /// <param name="timeProvider">Источник времени; в тестах — <c>FakeTimeProvider</c>.</param>
    /// <param name="evaluator">Оценка позиции сетью: если задана, playout'ы заменяются оценкой
    /// сети, а приоритеты ходов берутся из её политики (T-033).</param>
    /// <exception cref="ArgumentOutOfRangeException">Доля случайности вне диапазона 0…100,
    /// коэффициент UCB1 отрицательный, бюджет не положительный.</exception>
    public MctsMoveSelector(
        Random random,
        PlayoutPolicy playoutPolicy,
        MctsConfig config,
        int randomnessPercent = 0,
        TimeProvider? timeProvider = null,
        IPositionEvaluator? evaluator = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(playoutPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(config.Ucb1C);
        ArgumentOutOfRangeException.ThrowIfNegative(randomnessPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(randomnessPercent, PercentScale);

        _random = random;
        _randomnessPercent = randomnessPercent;
        _usesNetwork = evaluator is not null;
        _filter = MoveFilters.Guarded(config.TacticalGuard);
        _search = new MctsSearch(
            config,
            evaluator is null ? new PlayoutEvaluation(playoutPolicy, random) : new NeuralEvaluation(evaluator),
            timeProvider);
    }

    /// <summary>Создаёт селектор с коэффициентом исследования по умолчанию.</summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="playoutPolicy">Политика игры в playout'ах.</param>
    /// <param name="playoutBudget">Бюджет playout'ов на ход.</param>
    public MctsMoveSelector(Random random, PlayoutPolicy playoutPolicy, int playoutBudget)
        : this(random, playoutPolicy, new MctsConfig(playoutBudget, MctsConfig.DefaultUcb1C), 0, null)
    {
    }

    /// <inheritdoc />
    public string Name
    {
        get
        {
            var budget = _search.TimeBudget is { } time
                ? $"{time.TotalMilliseconds:F0} мс на ход"
                : _usesNetwork
                    ? $"{_search.PlayoutBudget} итераций с сетью"
                    : $"{_search.PlayoutBudget} playout'ов";
            var source = _usesNetwork ? "нейросеть" : "playout'ы";

            return $"MCTS ({budget}, {source}, случайность {_randomnessPercent}%)";
        }
    }

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove, state.Komi, state.Moves);
    }

    /// <summary>Выбирает ход по позиции, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми, по которому считаются playout'ы.</param>
    /// <param name="history">Ходы партии до этой позиции: нужны сети для плоскостей истории.</param>
    /// <returns>Легальный ход или пас, если легальных ходов нет.</returns>
    public Move SelectMove(Board board, StoneColor color, Komi komi, IReadOnlyList<Move>? history = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        // Пас соперника меняет правило отбора: играем только то, что приносит очки.
        var opponentPassed = EndgamePolicy.OpponentPassed(history);

        if (Blunders())
        {
            return RandomMove(board, color, opponentPassed);
        }

        var root = Search(board, color, komi, history);
        List<MctsNode> candidates = [];

        foreach (var child in root.Children)
        {
            // Поиск идёт по доске без истории, поэтому суперко проверяется здесь, на настоящей доске:
            // ход, повторяющий позицию партии, играть нельзя (GO_RULES.md, п. 6).
            if (board.IsLegal(child.Move).IsSuccess)
            {
                candidates.Add(child);
            }
        }

        candidates = Allowed(candidates, board);
        candidates = Policies(candidates, board, color, opponentPassed);

        MctsNode? best = null;

        foreach (var child in candidates)
        {
            if (best is null || IsBetter(child, best))
            {
                best = child;
            }
        }

        return best is null ? Move.Pass(color) : best.Move;
    }

    /// <summary>Строит дерево поиска для позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми playout'ов.</param>
    /// <param name="history">Ходы партии до этой позиции: нужны сети для плоскостей истории.</param>
    /// <returns>Корень дерева с разобранными вариантами.</returns>
    /// <remarks>Открыт для тестов и разбора: по дереву видно, сколько итераций получил каждый ход.</remarks>
    public MctsNode Search(Board board, StoneColor color, Komi komi, IReadOnlyList<Move>? history = null) =>
        _search.Search(board, color, komi, history);

    /// <summary>Оставляет ходы, допустимые фильтром.</summary>
    /// <param name="candidates">Разобранные ходы поиска.</param>
    /// <param name="board">Позиция до хода.</param>
    /// <returns>Допустимые ходы; если фильтр не сузил список — все кандидаты.</returns>
    /// <remarks>
    /// Тактический предохранитель отсекает подстановки: если безопасных ходов не осталось,
    /// фильтр разрешает все — любой ход лучше вынужденного паса.
    /// </remarks>
    private List<MctsNode> Allowed(List<MctsNode> candidates, Board board)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var allowed = _filter.Apply(board, [.. candidates.Select(child => child.Move)]);

        return allowed.EverythingAllowed
            ? candidates
            : [.. candidates.Where(child => allowed.Moves.Contains(child.Move))];
    }

    /// <summary>Оставляет ходы, которые разрешают правила движка.</summary>
    /// <param name="candidates">Разобранные ходы поиска.</param>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <returns>Разрешённые ходы; пустой список означает пас.</returns>
    /// <remarks>
    /// Убыточные ходы отсекаются у корня, а не внутри поиска: доигрывание оценивает «своё» очко
    /// и потерю камня так же, как полезный ход, поэтому отсекать их надо до выбора. Правил два —
    /// конец партии и пленные (<see cref="MovePolicy"/>), и оба могут отсечь всё: тогда селектор пасует.
    /// </remarks>
    private static List<MctsNode> Policies(List<MctsNode> candidates, Board board, StoneColor color, bool opponentPassed)
    {
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var allowed = PolicyPool(board, color, [.. candidates.Select(child => child.Move)], opponentPassed);

        return allowed.Count == candidates.Count
            ? candidates
            : [.. candidates.Where(child => allowed.Contains(child.Move))];
    }

    /// <summary>Применяет правила движка к списку ходов.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="pool">Допустимые ходы после предохранителя.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <returns>Ходы, которые правила разрешают; пустой список означает пас.</returns>
    private static IReadOnlyList<Move> PolicyPool(Board board, StoneColor color, IReadOnlyList<Move> pool, bool opponentPassed)
    {
        if (pool.Count == 0)
        {
            return pool;
        }

        var allowed = MovePolicy.Apply(board, color, pool, opponentPassed);

        return allowed.EverythingAllowed ? pool : allowed.Moves;
    }

    /// <summary>Проверяет, ходит ли селектор на этот раз наугад.</summary>
    /// <returns><c>true</c>, если ход выбирается без поиска.</returns>
    private bool Blunders() => _random.Next(PercentScale) < _randomnessPercent;

    /// <summary>Выбирает случайный допустимый ход: слабые уровни иногда ходят наугад.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="opponentPassed">Соперник только что пропустил ход.</param>
    /// <returns>Легальный ход или пас, если играть нечего.</returns>
    /// <remarks>
    /// Случайный ход — тоже ход: при включённом предохранителе он выбирается из тактически
    /// безопасных, а правила движка не дают «наугад» доедать свою территорию и кормить пленных
    /// у своих мёртвых групп. Иначе уровни кю подставляли свои группы под захват именно
    /// на «наугад» (T-037, жалоба 2026-10-03).
    /// </remarks>
    private Move RandomMove(Board board, StoneColor color, bool opponentPassed)
    {
        var legalMoves = LegalMoves.For(board, color);

        if (legalMoves.Count == 0)
        {
            return Move.Pass(color);
        }

        var allowed = _filter.Apply(board, legalMoves);
        var safe = allowed.EverythingAllowed ? legalMoves : allowed.Moves;
        var pool = PolicyPool(board, color, safe, opponentPassed);

        return pool.Count == 0 ? Move.Pass(color) : pool[_random.Next(pool.Count)];
    }

    /// <summary>Сравнивает ходы: сначала по числу посещений, при равенстве — по доле побед.</summary>
    /// <param name="candidate">Проверяемый ход.</param>
    /// <param name="current">Текущий лучший ход.</param>
    /// <returns><c>true</c>, если проверяемый ход лучше.</returns>
    /// <remarks>
    /// При малом бюджете почти все ходы получают по одному посещению, и выбор «первого»
    /// из них был бы случаен. Доля побед различает такие ходы: ход, выигравший свои playout'ы,
    /// лучше хода, их проигравшего. Если и она совпала, решает вероятность хода по мнению
    /// сети — так сеть влияет на выбор даже при бюджете в несколько итераций (T-033).
    /// </remarks>
    private static bool IsBetter(MctsNode candidate, MctsNode current)
    {
        if (candidate.Visits != current.Visits)
        {
            return candidate.Visits > current.Visits;
        }

        if (WinRate(candidate) != WinRate(current))
        {
            return WinRate(candidate) > WinRate(current);
        }

        // При равной статистике решает мнение сети: у поиска без сети приоритеты нулевые.
        return candidate.Prior > current.Prior;
    }

    /// <summary>Считает долю выигранных playout'ов.</summary>
    /// <param name="node">Узел дерева.</param>
    /// <returns>Отношение побед к посещениям; 0 у непосещённого узла.</returns>
    private static double WinRate(MctsNode node) => node.Visits == 0 ? 0 : node.Wins / node.Visits;
}
