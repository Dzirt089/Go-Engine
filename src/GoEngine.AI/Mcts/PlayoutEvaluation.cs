namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Оценка позиции случайным доигрыванием: одна симуляция партии до двух пасов.</summary>
/// <remarks>
/// Оценка узла — результат партии, доигранной случайной политикой; ходы симуляции уходят
/// в статистику RAVE. Позиция копируется один раз и дальше меняется на месте: <see cref="Board.ApplyMove"/>
/// создавал бы новую доску на каждом ходу playout'а, а это самый горячий цикл движка.
/// </remarks>
public sealed class PlayoutEvaluation : IMctsEvaluation
{
    /// <summary>Сколько пасов подряд завершают playout.</summary>
    private const int PassesToFinish = 2;

    private readonly PlayoutPolicy _playoutPolicy;
    private readonly Random _random;

    /// <summary>Создаёт оценку доигрываниями.</summary>
    /// <param name="playoutPolicy">Политика игры в доигрываниях.</param>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    /// <exception cref="ArgumentNullException">Политика или источник случайности не заданы.</exception>
    public PlayoutEvaluation(PlayoutPolicy playoutPolicy, Random random)
    {
        ArgumentNullException.ThrowIfNull(playoutPolicy);
        ArgumentNullException.ThrowIfNull(random);

        _playoutPolicy = playoutPolicy;
        _random = random;
    }

    /// <inheritdoc />
    public bool GivesPriors => false;

    /// <inheritdoc />
    public void Evaluate(MctsTree tree, MctsNode node, IReadOnlyList<Move> history, Komi komi)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(node);

        // Сначала расширяем узел, потом играем из нового ребёнка: так каждый playout проверяет
        // ещё не разобранный ход, а не повторяет уже проверенные.
        var leaf = node.IsFullyExpanded ? node : tree.Expand(node);
        var (winner, played) = RunPlayout(leaf, komi);

        tree.Backpropagate(leaf, winner, played);
    }

    /// <summary>Играет случайную партию из узла до конца.</summary>
    /// <param name="node">Узел, с которого начинается playout.</param>
    /// <param name="komi">Коми партии.</param>
    /// <returns>Победитель партии и все сыгранные в ней ходы: ходы нужны статистике RAVE.</returns>
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
