namespace GoEngine.Core;

/// <summary>Позиционное суперко: запрет повторения любой уже встречавшейся позиции партии.</summary>
/// <remarks>
/// Простое ко — немедленный обратный захват — отдельно не проверяется: это частный случай суперко
/// (<c>GO_RULES.md</c>, п. 6). Позиции сравниваются только по расположению камней: очерёдность хода
/// и коми в хеш не входят. Без истории (<see cref="Board.History"/> равно <c>null</c>) запрет
/// не действует — так работают анализ и playout в MCTS.
/// </remarks>
public static class KoRule
{
    /// <summary>Проверяет, повторяет ли ход уже встречавшуюся позицию.</summary>
    /// <param name="board">Позиция до хода.</param>
    /// <param name="move">Проверяемый ход.</param>
    /// <returns><c>true</c>, если позиция после хода уже была в истории партии.</returns>
    /// <exception cref="DomainException">Ход не задан, не является <c>Play</c>, точка вне доски
    /// или занята: предпросмотр позиции невозможен.</exception>
    /// <remarks>
    /// Пас и сдачу проверять не нужно: они не меняют позицию, а повторение позиции после паса
    /// не является нарушением (<c>GO_RULES.md</c>, п. 3).
    /// </remarks>
    public static bool ViolatesSuperko(Board board, Move move)
    {
        ArgumentNullException.ThrowIfNull(board);

        // Пас и сдача позицию не меняют, повторения не создают и предпросмотра не требуют.
        if (board.History is null || move.IsNone || move.Type != MoveType.Play)
        {
            return false;
        }

        return ViolatesSuperko(board.History, board.PreviewMove(move), move);
    }

    /// <summary>Проверяет повторение по уже построенной позиции после хода.</summary>
    /// <param name="history">История партии или <c>null</c>, если история не ведётся.</param>
    /// <param name="positionAfterMove">Позиция после хода.</param>
    /// <param name="move">Сделанный ход.</param>
    /// <returns><c>true</c>, если такая позиция уже встречалась в партии.</returns>
    /// <remarks>
    /// Перегрузка нужна доске: она строит позицию после хода для проверки самоубийства и
    /// переиспользует её здесь, чтобы не копировать доску второй раз.
    /// </remarks>
    internal static bool ViolatesSuperko(PositionHistory? history, Board positionAfterMove, Move move)
    {
        // Пас и сдача позицию не меняют, поэтому повторением её не создают.
        if (history is null || move.IsNone || move.Type != MoveType.Play)
        {
            return false;
        }

        return history.Contains(positionAfterMove);
    }
}
