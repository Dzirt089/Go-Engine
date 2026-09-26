using GoEngine.Core;

namespace GoEngine.AI;

/// <summary>Случайный выбор хода: уровень 30 кю.</summary>
/// <remarks>
/// Селектор собирает все легальные ходы и выбирает один из них. Случайность берётся только
/// из инжектированного <see cref="Random"/> — своего генератора класс не создаёт
/// (<c>AGENTS_GO.md</c>, п. 7). При фиксированном seed выбор детерминирован.
/// </remarks>
public sealed class RandomMoveSelector : IMoveSelector
{
    private readonly Random _random;

    /// <summary>Создаёт селектор.</summary>
    /// <param name="random">Источник случайности; в тестах — с фиксированным seed.</param>
    public RandomMoveSelector(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
    }

    /// <inheritdoc />
    public string Name => "Случайный ход (30 кю)";

    /// <inheritdoc />
    public Move SelectMove(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return SelectMove(state.Board, state.ToMove);
    }

    /// <summary>Выбирает ход по позиции и цвету, минуя партию.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальный ход или пас, если легальных ходов нет.</returns>
    /// <remarks>
    /// Перегрузка нужна анализу и MCTS: там позиция есть, а партии может и не быть.
    /// </remarks>
    public Move SelectMove(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        var legalMoves = FindLegalMoves(board, color);

        // Пас разрешён всегда (GO_RULES.md, п. 3): если ходить некуда, игрок пропускает ход.
        return legalMoves.Count == 0
            ? Move.Pass(color)
            : legalMoves[_random.Next(legalMoves.Count)];
    }

    /// <summary>Собирает все легальные ходы цвета в позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальные ходы; пустой список, если ходить некуда.</returns>
    private static List<Move> FindLegalMoves(Board board, StoneColor color)
    {
        List<Move> moves = [];

        foreach (var point in board.EmptyPoints())
        {
            var move = Move.Play(point, color);

            if (board.IsLegal(move).IsSuccess)
            {
                moves.Add(move);
            }
        }

        return moves;
    }
}
