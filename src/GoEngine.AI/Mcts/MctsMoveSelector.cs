namespace GoEngine.AI.Mcts;

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

    private readonly Random _random;
    private readonly PlayoutPolicy _playoutPolicy;
    private readonly MctsConfig _config;

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <param name="playoutPolicy">Политика игры в playout'ах.</param>
    /// <param name="config">Настройки поиска.</param>
    /// <exception cref="ArgumentOutOfRangeException">Бюджет не положительный или коэффициент UCB1 отрицательный.</exception>
    public MctsMoveSelector(Random random, PlayoutPolicy playoutPolicy, MctsConfig config)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(playoutPolicy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(config.PlayoutBudget);
        ArgumentOutOfRangeException.ThrowIfNegative(config.Ucb1C);

        _random = random;
        _playoutPolicy = playoutPolicy;
        _config = config;
    }

    /// <summary>Создаёт селектор с коэффициентом исследования по умолчанию.</summary>
    /// <param name="random">Источник случайности.</param>
    /// <param name="playoutPolicy">Политика игры в playout'ах.</param>
    /// <param name="playoutBudget">Бюджет playout'ов на ход.</param>
    public MctsMoveSelector(Random random, PlayoutPolicy playoutPolicy, int playoutBudget)
        : this(random, playoutPolicy, new MctsConfig(playoutBudget, MctsConfig.DefaultUcb1C))
    {
    }

    /// <inheritdoc />
    public string Name => $"MCTS ({_config.PlayoutBudget} playout'ов)";

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove, state.Komi);
    }

    /// <summary>Выбирает ход по позиции, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми, по которому считаются playout'ы.</param>
    /// <returns>Легальный ход или пас, если легальных ходов нет.</returns>
    public Move SelectMove(Board board, StoneColor color, Komi komi)
    {
        var root = Search(board, color, komi);

        if (root.Children.Count == 0)
        {
            return Move.Pass(color);
        }

        var best = root.Children[0];

        foreach (var child in root.Children)
        {
            if (IsBetter(child, best))
            {
                best = child;
            }
        }

        return best.Move;
    }

    /// <summary>Сравнивает ходы: сначала по числу посещений, при равенстве — по доле побед.</summary>
    /// <param name="candidate">Проверяемый ход.</param>
    /// <param name="current">Текущий лучший ход.</param>
    /// <returns><c>true</c>, если проверяемый ход лучше.</returns>
    /// <remarks>
    /// При малом бюджете почти все ходы получают по одному посещению, и выбор «первого»
    /// из них был бы случаен. Доля побед различает такие ходы: ход, выигравший свои playout'ы,
    /// лучше хода, их проигравшего.
    /// </remarks>
    private static bool IsBetter(MctsNode candidate, MctsNode current) =>
        candidate.Visits != current.Visits
            ? candidate.Visits > current.Visits
            : WinRate(candidate) > WinRate(current);

    /// <summary>Считает долю выигранных playout'ов.</summary>
    /// <param name="node">Узел дерева.</param>
    /// <returns>Отношение побед к посещениям; 0 у непосещённого узла.</returns>
    private static double WinRate(MctsNode node) => node.Visits == 0 ? 0 : node.Wins / node.Visits;

    /// <summary>Строит дерево поиска для позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <param name="komi">Коми playout'ов.</param>
    /// <returns>Корень дерева с разобранными вариантами.</returns>
    /// <remarks>Открыт для тестов и разбора: по дереву видно, сколько playout'ов получил каждый ход.</remarks>
    public MctsNode Search(Board board, StoneColor color, Komi komi)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var tree = new MctsTree(board, color, _config.Ucb1C);

        for (var playout = 0; playout < _config.PlayoutBudget; playout++)
        {
            var node = tree.Select();

            if (!node.IsFullyExpanded)
            {
                node = tree.Expand(node);
            }

            tree.Backpropagate(node, RunPlayout(node, komi));
        }

        return tree.Root;
    }

    /// <summary>Играет случайную партию из узла до конца.</summary>
    /// <param name="node">Узел, с которого начинается playout.</param>
    /// <param name="komi">Коми партии.</param>
    /// <returns>Победитель партии.</returns>
    /// <remarks>
    /// Позиция копируется один раз на playout и дальше меняется на месте: <see cref="Board.ApplyMove"/>
    /// создавал бы новую доску на каждом ходу playout'а, а это самый горячий цикл движка.
    /// </remarks>
    private StoneColor RunPlayout(MctsNode node, Komi komi)
    {
        var board = node.Board.Clone();
        var limit = board.Size.DefaultMoveLimit;
        var toMove = node.ToMove;
        var passes = 0;
        var moves = 0;

        // Два паса завершают playout так же, как завершают партию (GO_RULES.md, п. 7).
        while (passes < PassesToFinish && moves < limit)
        {
            var move = _playoutPolicy.SelectMove(board, toMove, _random);

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

        return Scorer.Calculate(board, komi).Winner;
    }
}
