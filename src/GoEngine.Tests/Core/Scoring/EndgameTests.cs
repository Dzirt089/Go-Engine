using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Конец партии: снятие мёртвых камней, пленные и подсчёт по очищенной доске.</summary>
/// <remarks>
/// Жалоба пользователя 1: мёртвые камни не снимались и приносили очки своему цвету, а пленные
/// не участвовали в итоге. Здесь проверяется <see cref="Endgame.ClearedBoard"/> и
/// <see cref="Endgame.Finalize"/>: территория и владение считаются по доске без мёртвых,
/// снятые камни уходят в пленные соперника, а обе величины итога считаются из одного разбора.
/// </remarks>
public sealed class EndgameTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    /// <summary>Занумерованная точка (0,0): белый камень мёртвой группы в углу.</summary>
    private static readonly Point DeadCorner = new(0, 0);

    [Fact]
    public void Очистка_Без_Мёртвых_Оставляет_Позицию_Как_Есть()
    {
        var board = CornerDeadWhite();

        var cleared = Endgame.ClearedBoard(board, []);

        Assert.Equal(Stones(board), Stones(cleared));
    }

    [Fact]
    public void Очистка_По_Одной_Точке_Снимает_Всю_Группу()
    {
        // Камень живёт и умирает вместе с группой: клик по (0,0) снимает и (1,0).
        var cleared = Endgame.ClearedBoard(CornerDeadWhite(), [DeadCorner]);

        Assert.Equal(StoneColor.Empty, cleared.At(new Point(1, 0)));
    }

    [Fact]
    public void Очистка_Не_Меняет_Исходную_Доску()
    {
        var board = CornerDeadWhite();

        _ = Endgame.ClearedBoard(board, [DeadCorner]);

        Assert.Equal(StoneColor.White, board.At(DeadCorner));
    }

    [Fact]
    public void Очистка_Пустой_Точки_Отказ()
    {
        var board = CornerDeadWhite();

        Assert.Throws<DomainException>(() => Endgame.ClearedBoard(board, [new Point(8, 8)]));
    }

    [Fact]
    public void Очистка_Точки_Вне_Доски_Отказ()
    {
        var board = CornerDeadWhite();

        Assert.Throws<DomainException>(() => Endgame.ClearedBoard(board, [new Point(9, 9)]));
    }

    [Fact]
    public void Очистка_Без_Доски_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Endgame.ClearedBoard(null!, []));
    }

    [Fact]
    public void Очистка_Без_Списка_Мёртвых_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Endgame.ClearedBoard(CornerDeadWhite(), null!));
    }

    [Fact]
    public void Владение_Снятой_Точки_Достаётся_Захватившему()
    {
        // Точка снятого белого камня становится окружённой только чёрными — территорией чёрных.
        var ownership = Scorer.Ownership(Endgame.ClearedBoard(CornerDeadWhite(), [DeadCorner]));

        Assert.Equal(StoneColor.Black, ownership[0]);
    }

    [Fact]
    public void Итог_Мёртвая_Группа_Становится_Пленными()
    {
        var final = Endgame.Finalize(CornerDeadWhite(), [DeadCorner], new Komi(0), 0, 0);

        Assert.Equal(2, final.BlackPrisoners);
    }

    [Fact]
    public void Итог_Мёртвая_Группа_Не_Приносит_Очков_Своему_Цвету()
    {
        var final = Endgame.Finalize(CornerDeadWhite(), [DeadCorner], new Komi(0), 0, 0);

        Assert.Equal(0, final.WhiteArea);
    }

    [Fact]
    public void Итог_Пленные_За_Партию_Прибавляются_К_Снятым()
    {
        var final = Endgame.Finalize(CornerDeadWhite(), [DeadCorner], new Komi(0), 3, 0);

        Assert.Equal(5, final.BlackPrisoners);
    }

    [Fact]
    public void Итог_Повторный_Вызов_Не_Двоит_Пленных()
    {
        var board = CornerDeadWhite();

        var first = Endgame.Finalize(board, [DeadCorner], new Komi(0), 1, 0);
        var second = Endgame.Finalize(board, [DeadCorner], new Komi(0), 1, 0);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Итог_Без_Мёртвых_Совпадает_С_Разбором_Площади()
    {
        var board = CornerDeadWhite();
        var area = Scorer.Breakdown(board);

        var final = Endgame.Finalize(board, [], new Komi(0), 0, 0);

        Assert.Equal((area.BlackStones, area.WhiteStones, area.Neutral), (final.BlackStones, final.WhiteStones, final.Neutral));
    }

    [Fact]
    public void Итог_Считает_Территорию_По_Очищенной_Доске()
    {
        // После снятия белой группы вся доска окружена только чёрными: 7 камней и 74 точки.
        var final = Endgame.Finalize(CornerDeadWhite(), [DeadCorner], new Komi(0), 0, 0);

        Assert.Equal(74, final.BlackTerritory);
    }

    [Fact]
    public void Итог_Стандартное_Коми_Берётся_По_Размеру_Доски()
    {
        var final = Endgame.Finalize(CornerDeadWhite(), []);

        Assert.Equal(Komi.For9x9, final.Komi);
    }

    [Fact]
    public void Итог_Коми_Партии_Сохраняется()
    {
        var final = Endgame.Finalize(CornerDeadWhite(), [], new Komi(7.5));

        Assert.Equal(7.5, final.Komi.Value);
    }

    [Fact]
    public void Итог_Отрицательные_Пленные_Отказ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Endgame.Finalize(CornerDeadWhite(), [], new Komi(0), -1, 0));
    }

    [Fact]
    public void Итог_На_Пустой_Доске_Всё_Нейтрально()
    {
        var final = Endgame.Finalize(new Board(Size), [], new Komi(0), 0, 0);

        Assert.Equal(Size.Area, final.Neutral);
    }

    [Fact]
    public void Итог_Разница_По_Площади_Равна_Разнице_По_Территории()
    {
        // Доигранная позиция: дамэ заполнены, мёртвых нет, камней поставлено поровну.
        // Тождество: площадь(ч) − площадь(б) = территория(ч) − территория(б).
        // Оно и есть доказательство, что пленные не потеряны и не посчитаны дважды.
        var final = BalancedGame().FinalScore([]);

        Assert.Equal(final.BlackArea - final.WhiteArea, final.BlackTerritoryPoints - final.WhiteTerritoryPoints);
    }

    [Fact]
    public void Итог_Обе_Системы_Называют_Одного_Победителя()
    {
        var final = BalancedGame().FinalScore([]);

        Assert.Equal(final.AreaWinner, final.TerritoryWinner);
    }

    /// <summary>Строит позицию: белая группа в углу в атари, остальные камни — чёрная стена.</summary>
    /// <returns>Доска 9×9 с двумя белыми камнями (0,0) и (1,0) без спасения.</returns>
    /// <remarks>
    /// Белая группа в углу имеет одно дамэ (2,0), и ход в него — самоубийство: остальные соседи
    /// закрыты чёрными. Чёрная стена держит семь дамэ, поэтому перебор её мёртвой не назовёт.
    /// </remarks>
    private static Board CornerDeadWhite()
    {
        var board = new Board(Size);

        foreach (var point in new[] { (0, 0), (1, 0) })
        {
            board = board.ApplyMove(Move.Play(new Point((byte)point.Item1, (byte)point.Item2), StoneColor.White));
        }

        foreach (var point in new[] { (0, 1), (1, 1), (2, 1), (3, 1), (4, 1), (3, 0), (4, 0) })
        {
            board = board.ApplyMove(Move.Play(new Point((byte)point.Item1, (byte)point.Item2), StoneColor.Black));
        }

        return board;
    }

    /// <summary>Строит доигранную партию с захватом: камней поставлено поровну, мёртвых нет.</summary>
    /// <returns>Партия, завершённая двумя пасами: у чёрных один пленный, территории по одной точке.</returns>
    /// <remarks>
    /// Порядок ходов важен: чёрные снимают белый камень в углу (это пленный чёрных), затем обе
    /// стороны ставят по два камня вдалеке и пасуют. Камней поставлено по три — без этого равенства
    /// тождество «площадь минус площадь равна территории минус территория» не выполняется, и тест
    /// проверял бы не то.
    /// </remarks>
    private static GameState BalancedGame()
    {
        var game = GameState.NewGame(Size, new Komi(0));
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(8, 8), StoneColor.White));
        _ = game.Play(Move.Play(new Point(7, 8), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(8, 7), StoneColor.White));
        _ = game.Play(Move.Pass(StoneColor.Black));
        _ = game.Play(Move.Pass(StoneColor.White));

        return game;
    }

    /// <summary>Снимает с доски расположение камней: список занятых точек с цветами.</summary>
    /// <param name="board">Позиция.</param>
    /// <returns>Точки с камнями в порядке обхода доски.</returns>
    private static List<(Point Point, StoneColor Color)> Stones(Board board)
    {
        List<(Point, StoneColor)> stones = [];

        foreach (var point in board.AllPoints())
        {
            var color = board.At(point);

            if (color != StoneColor.Empty)
            {
                stones.Add((point, color));
            }
        }

        return stones;
    }
}
