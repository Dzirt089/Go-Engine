namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Перебор легальных ходов позиции.</summary>
/// <remarks>
/// Один общий перебор для всех селекторов: правила хода проверяет <see cref="Board.IsLegal"/>,
/// поэтому самоубийство и позиционное суперко учитываются автоматически.
/// Пас в список не входит: его селектор добавляет сам, если ходить некуда
/// (<c>GO_RULES.md</c>, п. 3).
/// </remarks>
public static class LegalMoves
{
    /// <summary>Собирает все легальные ходы цвета в позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="color">Цвет, который ходит.</param>
    /// <returns>Легальные ходы в порядке обхода доски; пустой список, если ходить некуда.</returns>
    public static IReadOnlyList<Move> For(Board board, StoneColor color)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(color);

        List<Move> moves = [];

        foreach (var point in board.EmptyPoints())
        {
            var move = Move.Play(point, color);

            if (board.IsLegal(move).IsSuccess)
            {
                moves.Add(move);
            }
        }

        return moves.AsReadOnly();
    }
}
