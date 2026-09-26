using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты подсчёта по китайским правилам — <c>GO_RULES.md</c>, п. 9–10 и 13.7–13.8.</summary>
public sealed class ScorerTests
{
    [Fact]
    public void Score_Пустая_Доска_9x9_Все_Белые()
    {
        // Ни камней, ни территории: у белых только коми.
        var score = Scorer.Calculate(new Board(BoardSize.Size9), Komi.For9x9);

        Assert.Equal(new Score(0, 5.5), score);
    }

    [Fact]
    public void Score_Все_Чёрные_9x9_Победа_Чёрных()
    {
        // Белые не поставили ни камня: вся доска — территория чёрных, включая два их камня.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(BoardSize.Size9.Area, 5.5), score);
    }

    [Fact]
    public void Score_Территория_В_Углу()
    {
        // Угловое дамэ (0,0) окружено только чёрными; остальная доска нейтральна: там есть и белые камни.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(3, score.Black);
    }

    [Fact]
    public void Score_Территория_В_Центре()
    {
        // Центральное дамэ (4,4) окружено четырьмя чёрными камнями.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 2), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(5, score.Black);
    }

    [Fact]
    public void Score_Смешанная_Позиция()
    {
        // У чёрных угол (0,0) и три камня, у белых угол (8,0) и два камня, остальное нейтрально.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(7, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(8, 1), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(4, 8.5), score);
    }

    [Fact]
    public void Score_Сэки_Нейтральные_Не_Считаются()
    {
        // GO_RULES.md, 13.7: между стенами остаются общие дамэ — они не идут никому.
        var score = Scorer.Calculate(TestPositions.Seki(), Komi.For9x9);

        Assert.Equal(new Score(4, 9.5), score);
    }

    [Fact]
    public void Score_Мёртвые_Камни_Не_Снимаются()
    {
        // Белый камень внутри владения чёрных не снимается: точки вокруг него уже не территория.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 3), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 4), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(5, 6.5), score);
    }

    [Fact]
    public void Score_Камни_Считаются_Без_Территории()
    {
        // Два одиноких камня в открытой позиции: территории нет ни у кого.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(1, 6.5), score);
    }

    [Fact]
    public void Score_Пустая_Доска_19x19_Коми_7_5()
    {
        var score = Scorer.Calculate(new Board(BoardSize.Size19), Komi.For19x19);

        Assert.Equal(new Score(0, 7.5), score);
    }

    [Fact]
    public void Score_Победитель_По_Очкам()
    {
        var score = new Score(12, 7.5);

        Assert.Equal(StoneColor.Black, score.Winner);
    }

    [Fact]
    public void Score_Победитель_Белые_По_Коми()
    {
        var score = new Score(0, 5.5);

        Assert.Equal(StoneColor.White, score.Winner);
    }

    [Fact]
    public void Score_Ничья_Пустой_Цвет()
    {
        var score = new Score(7.5, 7.5);

        Assert.Equal(StoneColor.Empty, score.Winner);
    }

    [Fact]
    public void Score_Margin_Без_Знака()
    {
        Assert.Equal(4.5, new Score(12, 7.5).Margin);
    }

    [Fact]
    public void Score_Коми_Принадлежит_Белым()
    {
        var score = Scorer.Calculate(new Board(BoardSize.Size9), Komi.For9x9);

        Assert.Equal(5.5, score.White);
    }

    [Fact]
    public void Komi_Валидация()
    {
        Assert.Throws<DomainException>(() => new Komi(-1));
    }

    [Fact]
    public void Komi_Валидация_Не_Число()
    {
        Assert.Throws<DomainException>(() => new Komi(double.NaN));
    }

    [Fact]
    public void Komi_Стандартные_Значения()
    {
        Assert.Equal([7.5, 5.5, 5.5], new[] { Komi.For19x19.Value, Komi.For13x13.Value, Komi.For9x9.Value });
    }

    [Fact]
    public void Komi_По_Размеру_Доски()
    {
        Assert.Equal(Komi.For13x13, Komi.For(BoardSize.Size13));
    }

    [Fact]
    public void Komi_Ноль_Допустим()
    {
        Assert.Equal(0, new Komi(0).Value);
    }
}
