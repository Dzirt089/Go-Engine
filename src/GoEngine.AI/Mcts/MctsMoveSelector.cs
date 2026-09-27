namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Выбор хода методом Монте-Карло (MCTS) — <c>DECISIONS.md</c>, D-003.</summary>
/// <remarks>
/// Один ход — это <see cref="MctsConfig.PlayoutBudget"/> циклов «спуск → расширение → playout →
/// передача результата». Ход выбирается по числу посещений: чем чаще вариант приводил
/// к победе в playout'ах, тем больше он исследован.
/// Селектор детерминирован при фиксированном <see cref="Random"/> и своего генератора
/// не создаёт (<c>AGENTS_GO.md</c>, п. 7). Логов нет: слой AI не логирует.
/// </remarks>
public sealed class MctsMoveSelector : IMoveSelector
{
    /// <summary>Сколько пасов подряд завершают playout.</summary>
    private const int PassesToFinish = 2;

    /// <summary>Шкала процентов: доля случайности задаётся в процентах.</summary>
    private const int PercentScale = 100;

    private readonly Random _random;
    private readonly PlayoutPolicy _playoutPolicy;
    private readonly MctsConfig _config;
    private readonly int _randomnessPercent;
    private readonly TimeProvider _timeProvider;
    private readonly IPositionEvaluator? _evaluator;

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
        _playoutPolicy = playoutPolicy;
        _config = config;
        _randomnessPercent = randomnessPercent;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _evaluator = evaluator;
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
            var budget = _config.TimeBudget is { } time
                ? $"{time.TotalMilliseconds:F0} мс на ход"
                : $"{_config.PlayoutBudget} playout'ов";
            var source = _evaluator is null ? "playout'ы" : "нейросеть";

            return $"MCTS ({budget}, {source}, случайность {_randomnessPercent}%)";
        }
    }

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove, state.Komi, state.Moves);
    }

    /// <summary>Проверяет, ходит ли селектор на этот раз наугад.</summary>
    /// <returns><c>true</c>, если ход выбирается без поиска.</returns>
    private bool Blunders() => _random.Next(PercentScale) < _randomnessPercent;

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

        if (Blunders())
        {
            var legalMoves = LegalMoves.For(board, color);

            return legalMoves.Count == 0 ? Move.Pass(color) : legalMoves[_random.Next(legalMoves.Count)];
        }

        var root = Search(board, color, komi, history);
        MctsNode? best = null;

        foreach (var child in root.Children)
        {
            // Поиск идёт по доске без истории, поэтому суперко проверяется здесь, на настоящей доске:
            // ход, повторяющий позицию партии, играть нельзя (GO_RULES.md, п. 6).
            if (!board.IsLegal(child.Move).IsSuccess)
            {
                continue;
            }

            if (best is null || IsBetter(child, best))
            {
                best = child;
            }
        }

        return best is null ? Move.Pass(color) : best.Move;
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

    /// <summary>Строит дерево поиска для позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми playout'ов.</param>
    /// <param name="history">Ходы партии до этой позиции: нужны сети для плоскостей истории.</param>
    /// <returns>Корень дерева с разобранными вариантами.</returns>
    /// <remarks>
    /// Открыт для тестов и разбора: по дереву видно, сколько playout'ов получил каждый ход.
    /// Без оценки сети играются playout'ы, с ней — каждая итерация оценивает позицию один раз
    /// и берёт из той же оценки приоритеты ходов (T-033).
    /// </remarks>
    public MctsNode Search(Board board, StoneColor color, Komi komi, IReadOnlyList<Move>? history = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var moves = history ?? [];
        var tree = new MctsTree(board, color, _config.Ucb1C, _config.RaveK, usePriors: _evaluator is not null);

        // Бюджет по времени: сила не зависит от скорости машины. Отсчёт — только через TimeProvider
        // (AGENTS.md, п. 6): DateTime.Now и Stopwatch в AI запрещены.
        var startedAt = _timeProvider.GetUtcNow();
        var playouts = 0;

        while (HasBudget(startedAt, playouts))
        {
            var node = tree.Select();

            if (_evaluator is null)
            {
                if (!node.IsFullyExpanded)
                {
                    node = tree.Expand(node);
                }

                var (winner, played) = RunPlayout(node, komi);
                tree.Backpropagate(node, winner, played);
            }
            else
            {
                EvaluateNode(tree, node, komi, moves);
            }

            playouts++;
        }

        return tree.Root;
    }

    /// <summary>Оценивает позицию узла сетью и передаёт оценку наверх.</summary>
    /// <param name="tree">Дерево поиска.</param>
    /// <param name="node">Узел, чья позиция оценивается.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="history">Ходы партии до корня дерева.</param>
    /// <remarks>
    /// Одна оценка даёт и исход позиции (её получает путь до корня), и вероятности ходов
    /// (их получает новый ребёнок): сеть вызывается один раз на итерацию.
    /// </remarks>
    private void EvaluateNode(MctsTree tree, MctsNode node, Komi komi, IReadOnlyList<Move> history)
    {
        var evaluation = _evaluator!.Evaluate(node.Board, node.ToMove, komi, PathMoves(node, history));

        if (!node.IsFullyExpanded)
        {
            // Порядок разбора задаёт сеть: первым пробуется ход, который она считает вероятным.
            var move = BestUntriedMove(node, evaluation);
            _ = tree.Expand(node, move, PriorOf(evaluation, move, node.Board.Size));
        }

        tree.Backpropagate(node, evaluation.WinProbability);
    }

    /// <summary>Выбирает неразобранный ход с наибольшей вероятностью по мнению сети.</summary>
    /// <param name="node">Узел с неразобранными ходами.</param>
    /// <param name="evaluation">Оценка позиции узла.</param>
    /// <returns>Ход, который сеть считает самым вероятным из оставшихся.</returns>
    private static Move BestUntriedMove(MctsNode node, PositionEvaluation evaluation)
    {
        var size = node.Board.Size;
        var best = node.UntriedMoves[0];
        var bestPrior = PriorOf(evaluation, best, size);

        foreach (var move in node.UntriedMoves)
        {
            var prior = PriorOf(evaluation, move, size);

            if (prior > bestPrior)
            {
                best = move;
                bestPrior = prior;
            }
        }

        return best;
    }

    /// <summary>Берёт вероятность хода из оценки сети.</summary>
    /// <param name="evaluation">Оценка позиции.</param>
    /// <param name="move">Ход.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Вероятность хода от 0 до 1.</returns>
    private static double PriorOf(PositionEvaluation evaluation, Move move, BoardSize size) =>
        move.Type == MoveType.Pass
            ? evaluation.ProbabilityOf(evaluation.PassIndex)
            : evaluation.ProbabilityOf((move.Point.Y * size.Value) + move.Point.X);

    /// <summary>Собирает ходы от начала партии до узла.</summary>
    /// <param name="node">Узел дерева.</param>
    /// <param name="history">Ходы партии до корня дерева.</param>
    /// <returns>Ходы по порядку: сначала партия, затем путь по дереву.</returns>
    private static IReadOnlyList<Move> PathMoves(MctsNode node, IReadOnlyList<Move> history)
    {
        var path = new List<Move>();

        for (var current = node; current?.Parent is not null; current = current.Parent)
        {
            path.Add(current.Move);
        }

        if (path.Count == 0)
        {
            return history;
        }

        path.Reverse();
        path.InsertRange(0, history);

        return path;
    }

    /// <summary>Проверяет, остался ли бюджет поиска.</summary>
    /// <param name="startedAt">Момент начала поиска.</param>
    /// <param name="playouts">Сколько playout'ов уже сделано.</param>
    /// <returns><c>true</c>, если можно сделать ещё один playout.</returns>
    /// <remarks>
    /// Режим задаётся настройками: либо время на ход, либо число playout'ов.
    /// Проверка стоит перед playout'ом, поэтому поиск всегда завершает начатую симуляцию.
    /// </remarks>
    private bool HasBudget(DateTimeOffset startedAt, int playouts) =>
        _config.TimeBudget is { } time
            ? _timeProvider.GetUtcNow() - startedAt < time
            : playouts < _config.PlayoutBudget!.Value;

    /// <summary>Играет случайную партию из узла до конца.</summary>
    /// <param name="node">Узел, с которого начинается playout.</param>
    /// <param name="komi">Коми партии.</param>
    /// <returns>Победитель партии и все сыгранные в ней ходы: ходы нужны статистике RAVE.</returns>
    /// <remarks>
    /// Позиция копируется один раз на playout и дальше меняется на месте: <see cref="Board.ApplyMove"/>
    /// создавал бы новую доску на каждом ходу playout'а, а это самый горячий цикл движка.
    /// </remarks>
    private (StoneColor Winner, List<Move> Played) RunPlayout(MctsNode node, Komi komi)
    {
        var board = node.Board.Clone();
        var limit = board.Size.DefaultMoveLimit;
        var toMove = node.ToMove;
        var passes = 0;
        var moves = 0;
        List<Move> played = [];

        // Два паса завершают playout так же, как завершают партию (GO_RULES.md, п. 7).
        while (passes < PassesToFinish && moves < limit)
        {
            var move = _playoutPolicy.SelectMove(board, toMove, _random);
            played.Add(move);

            if (move.Type == MoveType.Pass)
            {
                passes++;
            }
            else
            {
                passes = 0;
                board.MakeMove(move);
            }

            toMove = toMove.Opponent();
            moves++;
        }

        return (Scorer.Calculate(board, komi).Winner, played);
    }
}