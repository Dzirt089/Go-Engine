using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>
/// Позиции 1–8 из <c>GO_RULES.md</c>, раздел 13. Каждый тест проверяет один конкретный исход
/// на детерминированной позиции.
/// </summary>
/// <remarks>
/// Позиции 7 (сэки) и 8 (подсчёт) проверяются и по камням, и по точному счёту
/// (<see cref="Scorer"/>, китайские правила).
/// </remarks>
public sealed class RulesIntegrationTests
{
    [Fact]
    public void Позиция_1_Одиночный_Захват()
    {
        // GO_RULES.md, 13.1: чёрный камень в углу, белый ставит второй камень — чёрный снимается.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White));

        Assert.Equal(StoneColor.Empty, board.At(new Point(0, 0)));
    }

    [Fact]
    public void Позиция_2_Группа_Из_Двух_Камней_Снимается_Целиком()
    {
        // GO_RULES.md, 13.2: группа из двух камней у края снимается целиком.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.White));

        Point[] capturedGroup = [new(0, 0), new(1, 0)];

        Assert.All(capturedGroup, point => Assert.Equal(StoneColor.Empty, board.At(point)));
    }

    [Fact]
    public void Позиция_3_Самоубийство_Отклонено()
    {
        // GO_RULES.md, 13.3: у хода одна дамэ, захвата чёрных нет — ход отклонён.
        var board = TestPositions.SuicideAtCorner();

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black)));
    }

    [Fact]
    public void Позиция_4_Захват_С_Самоубийством_Разрешён()
    {
        // GO_RULES.md, 13.4: ход снимает чёрную группу, хотя своя группа осталась без дамэ.
        var board = TestPositions.SuicideWithCapture().ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.Equal(StoneColor.Black, board.At(new Point(0, 0)));
    }

    [Fact]
    public void Позиция_5_Простое_Ко_Запрещено()
    {
        // GO_RULES.md, 13.5: классическое ко, немедленный обратный захват запрещён.
        // Простое ко — частный случай позиционного суперко (GO_RULES.md, п. 6).
        var board = TestPositions.KoShape(new PositionHistory())
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.Throws<DomainException>(() => board.ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black)));
    }

    [Fact]
    public void Позиция_6_Суперко_Повторение_Через_Два_Хода()
    {
        // GO_RULES.md, 13.6: позиция после обратного захвата — не предыдущая, а позиция
        // двухходовой давности; запрещает её именно история партии, то есть суперко.
        var history = new PositionHistory();
        var beforeCapture = TestPositions.KoShape(history);
        var afterCapture = beforeCapture.ApplyMove(Move.Play(new Point(2, 1), StoneColor.White));

        Assert.NotEqual(PositionHash.From(beforeCapture), PositionHash.From(afterCapture));
        Assert.Throws<DomainException>(() => afterCapture.ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black)));
    }

    [Fact]
    public void Позиция_7_Сэки_Общие_Дамэ_Нейтральны()
    {
        // GO_RULES.md, 13.7 и п. 9: в сэки камни остаются на доске, а общие дамэ — нейтральные
        // точки, которые не считаются ни за кого. Счёт проверяется в T-007.
        var board = TestPositions.Seki();
        var black = GroupTracker.FindGroup(board, new Point(1, 1));
        var white = GroupTracker.FindGroup(board, new Point(3, 1));
        var sharedDame = new Point(2, 1);

        Assert.Contains(sharedDame, black.Liberties);
        Assert.Contains(sharedDame, white.Liberties);
    }

    [Fact]
    public void Позиция_7_Сэки_Счёт_Без_Территории()
    {
        // GO_RULES.md, 13.7 и п. 9: у обеих сторон только камни, общие дамэ не считаются ни за кого.
        var score = Scorer.Calculate(TestPositions.Seki(), Komi.For9x9);

        Assert.Equal(new Score(4, 9.5), score);
    }

    [Fact]
    public void Позиция_8_Подсчёт_Территории_Точный_Счёт()
    {
        // GO_RULES.md, 13.8: четыре камня в открытой позиции, территории нет ни у кого,
        // у белых только коми 5.5.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(2, 2), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 6), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 2), StoneColor.White));

        var score = Scorer.Calculate(board, Komi.For9x9);

        Assert.Equal(new Score(2, 7.5), score);
    }
}
