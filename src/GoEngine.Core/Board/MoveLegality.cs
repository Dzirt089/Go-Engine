namespace GoEngine.Core;

/// <summary>Правила постановки камня: самоубийство и позиционное суперко.</summary>
/// <remarks>
/// Вынесено из <see cref="Board"/>: доска хранит позицию и меняет её, а правила решают, можно ли
/// это делать (<c>GO_RULES.md</c>, п. 5–6). Проверки ничего не меняют — на это опирается
/// <see cref="Board.MakeMove"/>: при отказе позиция и история остаются прежними.
/// Правила читают камни через внутренний доступ доски, поэтому копию позиции не создают.
/// </remarks>
internal static class MoveLegality
{
    /// <summary>Проверяет ход постановки камня по правилам, не меняя доску.</summary>
    /// <param name="board">Позиция, в которую ставят камень.</param>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/>.</param>
    /// <returns>Успех, если ход легален; иначе причина отказа на русском языке.</returns>
    /// <remarks>
    /// Самоубийство проверяется без изменения доски: у своей будущей группы считаются дамэ,
    /// а у соседних групп противника — последнее дамэ. Копия доски нужна только тогда, когда
    /// ведётся история партии: для проверки суперко позицию после хода надо построить целиком.
    /// </remarks>
    internal static Result<Unit> CheckPlay(Board board, Move move)
    {
        if (!move.Point.IsOnBoard(board.Size))
        {
            return Result<Unit>.Fail($"Точка {move.Point} находится вне доски {board.Size}.");
        }

        if (board.StoneAt(board.IndexOf(move.Point)) != StoneColor.Empty)
        {
            return Result<Unit>.Fail($"Точка {move.Point} уже занята.");
        }

        var suicide = CheckSuicide(board, move);

        if (!suicide.IsSuccess)
        {
            return suicide;
        }

        // Без истории партии проверять суперко нечем и незачем: анализ может обойтись без копии доски.
        if (board.History is null)
        {
            return Result<Unit>.Ok(Unit.Value);
        }

        var probe = board.PreviewMove(move);

        // GO_RULES.md, п. 6: позиция после хода не должна встречаться в партии ни разу.
        return KoRule.ViolatesSuperko(board.History, probe, move)
            ? Result<Unit>.Fail($"Ход нарушает суперко: позиция после хода в точку {move.Point} уже встречалась в партии.")
            : Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Проверяет самоубийство, не меняя доску.</summary>
    /// <param name="board">Позиция, в которую ставят камень.</param>
    /// <param name="move">Ход типа <see cref="MoveType.Play"/> на пустую точку доски.</param>
    /// <returns>Успех, если после хода у своей группы останутся дамэ.</returns>
    /// <remarks>
    /// Считается без копии доски: дамэ будущей своей группы — это пустые соседи точки хода плюс
    /// дамэ соседних своих групп, кроме самой точки. Ход разрешён и тогда, когда своя группа
    /// осталась бы без дамэ, но снимается хотя бы один камень противника
    /// (<c>GO_RULES.md</c>, п. 5): группа противника снимается ровно тогда, когда её единственное
    /// дамэ — точка хода.
    /// </remarks>
    private static Result<Unit> CheckSuicide(Board board, Move move)
    {
        var color = move.Color;
        HashSet<Point> liberties = [];
        HashSet<Point> visited = [];
        var captures = false;

        foreach (var neighbor in move.Point.Neighbors(board.Size))
        {
            var neighborColor = board.StoneAt(board.IndexOf(neighbor));

            if (neighborColor == StoneColor.Empty)
            {
                liberties.Add(neighbor);
                continue;
            }

            if (!visited.Add(neighbor))
            {
                continue;
            }

            var group = GroupTracker.FindGroup(board, neighbor);
            visited.UnionWith(group.Stones);

            if (neighborColor == color)
            {
                liberties.UnionWith(group.Liberties);
                continue;
            }

            captures |= group.Liberties.Count == 1 && group.Liberties.Contains(move.Point);
        }

        // Сама точка хода перестаёт быть пустой, поэтому своим же дамэ она не считается.
        liberties.Remove(move.Point);

        return liberties.Count > 0 || captures
            ? Result<Unit>.Ok(Unit.Value)
            : Result<Unit>.Fail($"Самоубийственный ход: у своей группы в точке {move.Point} не остаётся дамэ.");
    }
}
