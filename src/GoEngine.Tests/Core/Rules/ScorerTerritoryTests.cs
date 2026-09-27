using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты территории: кому принадлежит каждая точка доски — <c>GO_RULES.md</c>, п. 9 и 13.7–13.8.</summary>
/// <remarks>
/// Проверяются <see cref="Scorer.Ownership"/> и <see cref="Scorer.Breakdown"/>. Территория бывает
/// за чёрными, за белыми или нейтральной: последняя — область, граничащая с обоими цветами, и вся
/// пустая доска. Очки по этим же данным проверяет <c>ScorerTests</c>.
/// </remarks>
public sealed class ScorerTerritoryTests
{
    [Fact]
    public void Ownership_Пустая_Доска_Вся_Нейтральна()
    {
        // Ни одного камня: у пустых точек нет границы, территории нет ни у кого.
        var ownership = Scorer.Ownership(new Board(BoardSize.Size9));

        Assert.All(ownership, owner => Assert.Equal(StoneColor.Empty, owner));
    }

    [Fact]
    public void Breakdown_Пустая_Доска_Всё_Нейтрально()
    {
        var area = Scorer.Breakdown(new Board(BoardSize.Size9));

        Assert.Equal(0, area.Black);
        Assert.Equal(0, area.White);
        Assert.Equal(BoardSize.Size9.Area, area.Neutral);
        Assert.Equal(BoardSize.Size9.Area, area.Total);
    }

    [Fact]
    public void Ownership_Один_Камень_Вся_Доска_Принадлежит_Его_Цвету()
    {
        // Единственный камень — вся граница пустой области, поэтому вся доска уходит его цвету.
        // Так и считает китайский подсчёт: точка принадлежит цвету, если область, где она лежит,
        // граничит только с его камнями (п. 69–70). Край доски не принадлежит никому и границей
        // не считается; формулировка п. 71 про «пути до края» — пояснение, буквально она
        // отрицала бы и угловую территорию (см. тесты угла ниже).
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(BoardSize.Size9.Area, ownership.Count(owner => owner == StoneColor.Black));
    }

    [Fact]
    public void Score_Один_Камень_Вся_Доска_Считается_Чёрными()
    {
        // Продолжение предыдущего теста на уровне очков: 1 камень + 80 точек территории = 81.
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(BoardSize.Size9.Area, Komi.For9x9.Value), score);
    }

    [Fact]
    public void Ownership_Два_Камня_Разных_Цветов_Пустые_Точки_Нейтральны()
    {
        // Область граничит и с чёрным, и с белым — территории не достаётся никому (п. 9).
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.White));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(BoardSize.Size9.Area - 2, ownership.Count(owner => owner == StoneColor.Empty));
    }

    [Fact]
    public void Ownership_Индекс_Точки_Это_Строка_Умножить_На_Размер_Плюс_Столбец()
    {
        // Порядок важен тому, кто рисует владение по индексу: точка (x, y) лежит по адресу y * size + x.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Black, ownership[1]);
        Assert.Equal(StoneColor.White, ownership[BoardSize.Size9.Value]);
    }

    [Fact]
    public void Ownership_Угол_Прижат_К_Краю_Территория_Чёрных()
    {
        // Точка (0,0) окружена камнями (1,0) и (0,1): край доски территории не мешает.
        // Белый камень вдали нужен, чтобы остальная доска стала нейтральной: без него она
        // граничила бы только с чёрными и тоже ушла бы чёрным (см. тест одного камня).
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(8, 8), StoneColor.White));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Black, ownership[0]);
        Assert.Equal(3, ownership.Count(owner => owner == StoneColor.Black));
    }

    [Fact]
    public void Ownership_Угол_С_Камнем_Соперника_На_Границе_Нейтрален()
    {
        // На границе угловой области и чёрный, и белый: область смешанная, территории ни у кого.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Equal(StoneColor.Empty, Scorer.Ownership(board)[0]);
    }

    [Fact]
    public void Ownership_Угол_13x13_За_Белыми()
    {
        // Тот же угол на 13×13 и за белых: у (0,0) граница только из белых камней,
        // а чёрный камень вдали делает остальную доску нейтральной.
        var board = new Board(BoardSize.Size13)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(12, 12), StoneColor.Black));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.White, ownership[0]);
        Assert.Equal(4, ownership.Count(owner => owner == StoneColor.White));
    }

    [Fact]
    public void Ownership_Угол_19x19_За_Чёрными_Остальное_Нейтрально()
    {
        var board = new Board(BoardSize.Size19)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Black, ownership[0]);
        Assert.Equal(3, ownership.Count(owner => owner == StoneColor.Black));
        Assert.Equal(StoneColor.Empty, ownership[^1]);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(19)]
    public void Ownership_Любой_Размер_Угол_Территория_Дальний_Угол_Нейтрален(int side)
    {
        // Один и тот же рисунок на всех трёх досках: территория угла и нейтральная остальная часть.
        var size = new BoardSize((byte)side);
        var board = new Board(size)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White));

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Black, ownership[0]);
        Assert.Equal(StoneColor.Empty, ownership[^1]);
    }

    [Fact]
    public void Ownership_Сэки_Общие_Дамэ_Нейтральны()
    {
        // GO_RULES.md, 13.7: коридор между стенами — общие дамэ обеих групп, территории не даёт.
        var ownership = Scorer.Ownership(TestPositions.Seki());

        for (byte y = 1; y <= 4; y++)
        {
            Assert.Equal(StoneColor.Empty, ownership[(y * BoardSize.Size9.Value) + 2]);
        }
    }

    [Fact]
    public void Ownership_Сэки_Камни_Стен_Остаются_За_Своими_Цветами()
    {
        // В сэки камни не снимаются и не переходят сопернику: они просто стоят на доске.
        var ownership = Scorer.Ownership(TestPositions.Seki());

        Assert.Equal(StoneColor.Black, ownership[BoardSize.Size9.Value + 1]);
        Assert.Equal(StoneColor.White, ownership[BoardSize.Size9.Value + 3]);
    }

    [Fact]
    public void Ownership_Сэки_Ход_В_Общее_Дамэ_Не_Снимает_Стену()
    {
        // Коридор нейтрален именно потому, что это общие дамэ, а не территория: ход белых
        // в (2,1) легален и стены не снимает — у чёрных остаются свои дамэ слева и сверху.
        var board = TestPositions.Seki();
        var after = board.ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.Equal(4, after.OccupiedPoints(StoneColor.Black).Count());
    }

    [Fact]
    public void Ownership_Глаз_Чёрной_Группы_Территория_Чёрных()
    {
        // Четыре чёрных камня вокруг (4,4): глаз граничит только с чёрными.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.White));

        Assert.Equal(StoneColor.Black, Scorer.Ownership(board)[(4 * BoardSize.Size9.Value) + 4]);
    }

    [Fact]
    public void Ownership_Глаз_Белой_Группы_Территория_Белых()
    {
        // Тот же рисунок за белых на 13×13: глаз (7,7) граничит только с белыми камнями.
        var board = new Board(BoardSize.Size13)
            .ApplyMove(Move.Play(new Point(6, 7), StoneColor.White))
            .ApplyMove(Move.Play(new Point(8, 7), StoneColor.White))
            .ApplyMove(Move.Play(new Point(7, 6), StoneColor.White))
            .ApplyMove(Move.Play(new Point(7, 8), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.Equal(StoneColor.White, Scorer.Ownership(board)[(7 * BoardSize.Size13.Value) + 7]);
    }

    [Fact]
    public void Ownership_Область_Заперта_Стенами_Территория_Чёрных()
    {
        // Стена по четвёртой вертикали и четвёртой горизонтали запирает угол: 16 пустых точек
        // граничат только с чёрными камнями. Стены касаются друг друга по диагонали — по стороне
        // утечки нет, поэтому область замкнута. Белый камень в дальнем углу оставляет внешней
        // части доски смешанную границу, то есть нейтральность.
        var board = TsumegoBuilder.Build(
            "....X....",
            "....X....",
            "....X....",
            "....X....",
            "XXXX.....",
            ".........",
            ".........",
            ".........",
            "........O");

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Black, ownership[0]);
        Assert.Equal(24, ownership.Count(owner => owner == StoneColor.Black));
    }

    [Fact]
    public void Ownership_Белый_Камень_Внутри_Области_Отменяет_Территорию_Чёрных()
    {
        // Мёртвые камни не снимаются (п. 73): белый камень внутри области сам становится её
        // границей, область начинает граничить с двумя цветами и делается нейтральной.
        var board = TsumegoBuilder.Build(
            "....X....",
            "....X....",
            "..O.X....",
            "....X....",
            "XXXX.....",
            ".........",
            ".........",
            ".........",
            ".........");

        var ownership = Scorer.Ownership(board);

        Assert.Equal(StoneColor.Empty, ownership[0]);

        // Нейтральны ровно 15 точек: вся внутренность области, кроме занятой белым камнем.
        // Внешняя часть доски остаётся за чёрными — её граница это только стена: белый камень
        // внутри к ней не примыкает.
        Assert.Equal(15, ownership.Count(owner => owner == StoneColor.Empty));
    }

    [Fact]
    public void Breakdown_Считает_Камни_Территорию_И_Нейтральные_Точки()
    {
        // Камни: 2 чёрных и 1 белый. Территория: угол (0,0) за чёрными. Остальное нейтрально.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White));

        var area = Scorer.Breakdown(board);

        Assert.Equal(2, area.BlackStones);
        Assert.Equal(1, area.BlackTerritory);
        Assert.Equal(1, area.WhiteStones);
        Assert.Equal(0, area.WhiteTerritory);
        Assert.Equal(BoardSize.Size9.Area - 4, area.Neutral);
        Assert.Equal(BoardSize.Size9.Area, area.Total);
    }

    [Fact]
    public void Breakdown_Совпадает_С_Подсчётом_Очков()
    {
        // Панель показывает разбор, счёт считается по нему же: расходиться они не имеют права.
        var board = TestPositions.Seki();

        var area = Scorer.Breakdown(board);
        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal((double)area.Black, score.Black);
        Assert.Equal(area.White + Komi.For9x9.Value, score.White);
    }

    [Fact]
    public void Breakdown_Точный_Подсчёт_Позиции_9x9()
    {
        // GO_RULES.md, 13.8: «пустая доска 9×9 после нескольких ходов — точный подсчёт».
        // Чёрные заперли угол (x ≤ 2, y ≤ 2) — 9 точек территории, белые заперли угол
        // (x ≥ 6, y ≥ 6) — тоже 9 точек, середина граничит с обеими стенами и нейтральна.
        var area = Scorer.Breakdown(HandCountedPosition());

        Assert.Equal(6, area.BlackStones);
        Assert.Equal(9, area.BlackTerritory);
        Assert.Equal(6, area.WhiteStones);
        Assert.Equal(9, area.WhiteTerritory);
        Assert.Equal(51, area.Neutral);
    }

    [Fact]
    public void Score_Точный_Подсчёт_Позиции_9x9()
    {
        // Чёрные: 6 камней + 9 территории = 15. Белые: 6 камней + 9 территории + 5,5 коми = 20,5.
        var score = Scorer.Calculate(HandCountedPosition(), Komi.For9x9);

        Assert.Equal(new Score(15, 20.5), score);
    }

    /// <summary>Позиция для точного подсчёта вручную: два запертых угла и нейтральная середина.</summary>
    /// <returns>Доска 9×9: чёрная стена запирает левый верхний угол, белая — правый нижний.</returns>
    private static Board HandCountedPosition() =>
        TsumegoBuilder.Build(
            "...X.....",
            "...X.....",
            "...X.....",
            "XXX......",
            ".........",
            "......OOO",
            ".....O...",
            ".....O...",
            ".....O...");

    [Fact]
    public void Ownership_Без_Доски_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Scorer.Ownership(null!));
    }

    [Fact]
    public void Breakdown_Без_Доски_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Scorer.Breakdown(null!));
    }

    [Fact]
    public void Calculate_Без_Доски_Отказ()
    {
        Assert.Throws<ArgumentNullException>(() => Scorer.Calculate(null!, Komi.For9x9));
    }
}
