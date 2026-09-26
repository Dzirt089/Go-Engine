using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Позиции из <c>GO_RULES.md</c>, п. 13, общие для нескольких наборов тестов.</summary>
/// <remarks>
/// Вынесены в одно место, чтобы канонические позиции не описывались заново в каждом файле
/// и не расходились между наборами тестов.
/// </remarks>
internal static class TestPositions
{
    /// <summary>Строит позицию, где чёрный ход в (0,0) — самоубийство.</summary>
    /// <returns>Доска 9×9: белые камни закрывают все дамэ будущей чёрной группы в углу.</returns>
    public static Board SuicideAtCorner() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

    /// <summary>Строит позицию, где чёрный ход в (0,0) снимает белую группу, хотя своя остаётся без дамэ.</summary>
    /// <returns>Доска 9×9: у белой группы в углу единственное дамэ — (0,0).</returns>
    public static Board SuicideWithCapture() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.Black));

    /// <summary>Строит классическую позицию ко на 9×9.</summary>
    /// <param name="history">История партии или <c>null</c>, если история не ведётся.</param>
    /// <returns>Доска, где белый ход в (2,1) снимает чёрный камень в (2,2).</returns>
    /// <remarks>
    /// Чёрный камень (2,2) в атари: его единственное дамэ — (2,1). Белые камни (1,2), (3,2), (2,3)
    /// закрывают остальные дамэ, а чёрные (1,1), (3,1), (2,0) оставляют будущему белому камню (2,1)
    /// ровно одно дамэ — (2,2). Обратный захват повторяет позицию, поэтому запрещён суперко.
    /// </remarks>
    public static Board KoShape(PositionHistory? history)
    {
        var board = new Board(BoardSize.Size9);

        if (history is not null)
        {
            board = board.WithHistory(history);
        }

        return board
            .ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 3), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.Black));
    }

    /// <summary>Строит позицию сэки: две стены и общие дамэ между ними.</summary>
    /// <returns>Доска 9×9 с чёрной стеной (1,1)–(1,4) и белой (3,1)–(3,4).</returns>
    /// <remarks>
    /// Между стенами остаётся колонка (2,1)–(2,4): каждая её точка — дамэ обеих групп,
    /// то есть нейтральная точка. Ни одна из групп не имеет двух глаз, но и не снимается:
    /// это сэки в смысле <c>GO_RULES.md</c>, п. 9 и глоссария.
    /// </remarks>
    public static Board Seki() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 2), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 3), StoneColor.White))
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.White));
}
