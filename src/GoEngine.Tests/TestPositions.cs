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

    /// <summary>Строит позицию, в которой у чёрных нет ни одного легального хода.</summary>
    /// <returns>Доска 9×9, полностью занятая белыми камнями, кроме (4,4) и (4,6).</returns>
    /// <remarks>
    /// Две пустые точки не соседние, поэтому белая группа сохраняет второе дамэ: чёрный ход
    /// в любую из них не снимает белых и оставляет чёрный камень без дамэ — самоубийство.
    /// </remarks>
    public static Board BoardWithoutLegalMoves()
    {
        var board = new Board(BoardSize.Size9);
        var first = new Point(4, 4);
        var second = new Point(4, 6);

        foreach (var point in board.AllPoints())
        {
            if (point != first && point != second)
            {
                board = board.ApplyMove(Move.Play(point, StoneColor.White));
            }
        }

        return board;
    }

    /// <summary>Строит цумэго: белый блок 3×3 в атари, у чёрных есть единственный выигрывающий ход.</summary>
    /// <returns>Доска 9×9: белый блок (3,3)–(5,5) окружён чёрным кольцом с единственным дамэ (2,4).</returns>
    /// <remarks>
    /// Ход чёрных в (2,4) снимает девять белых камней — на 9×9 это решает партию. Любой другой ход
    /// позволяет белым спастись, заняв то же дамэ.
    /// </remarks>
    public static Board BlockInAtari()
    {
        var board = new Board(BoardSize.Size9);
        var liberty = new Point(2, 4);

        for (var x = 3; x <= 5; x++)
        {
            for (var y = 3; y <= 5; y++)
            {
                board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), StoneColor.White));
            }
        }

        foreach (var point in RingAroundBlock())
        {
            if (point != liberty)
            {
                board = board.ApplyMove(Move.Play(point, StoneColor.Black));
            }
        }

        return board;
    }

    /// <summary>Строит гонку захвата: у чёрных и белых одно общее дамэ (4,4).</summary>
    /// <returns>Доска 9×9, где чёрные занимают x = 0…4, белые x = 5…8, а пуста только (4,4).</returns>
    /// <remarks>
    /// Единственная пустая точка — единственное дамэ обеих групп: кто ходит первым, тот снимает
    /// 36 камней противника. Для чёрных это единственный легальный ход, и он решает партию.
    /// </remarks>
    public static Board CapturingRace()
    {
        var board = new Board(BoardSize.Size9);
        var liberty = new Point(4, 4);
        var middle = 4;

        for (var x = 0; x <= middle; x++)
        {
            for (var y = 0; y < BoardSize.Size9.Value; y++)
            {
                var point = new Point((byte)x, (byte)y);

                if (point != liberty)
                {
                    board = board.ApplyMove(Move.Play(point, StoneColor.Black));
                }
            }
        }

        for (var x = middle + 1; x < BoardSize.Size9.Value; x++)
        {
            for (var y = 0; y < BoardSize.Size9.Value; y++)
            {
                board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), StoneColor.White));
            }
        }

        return board;
    }

    /// <summary>Перечисляет точки, окружающие белый блок (3,3)–(5,5).</summary>
    /// <returns>Двенадцать дамэ блока.</returns>
    private static IEnumerable<Point> RingAroundBlock()
    {
        for (var x = 2; x <= 6; x++)
        {
            yield return new Point((byte)x, 2);
            yield return new Point((byte)x, 6);
        }

        for (var y = 3; y <= 5; y++)
        {
            yield return new Point(2, (byte)y);
            yield return new Point(6, (byte)y);
        }
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
