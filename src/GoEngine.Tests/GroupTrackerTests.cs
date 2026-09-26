using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты поиска групп и дамэ — <c>GO_RULES.md</c>, п. 4 и 13.1–13.2.</summary>
public sealed class GroupTrackerTests
{
    [Fact]
    public void Группа_Одиночный_Камень()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Single(GroupTracker.FindGroup(board, new Point(4, 4)).Stones);
    }

    [Fact]
    public void Группа_Два_Камня_По_Стороне()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.Black));

        Assert.Equal(2, GroupTracker.FindGroup(board, new Point(4, 4)).Stones.Count);
    }

    [Fact]
    public void Группа_Три_Камня_Углом()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(1, 2), StoneColor.Black));

        Assert.Equal(3, GroupTracker.FindGroup(board, new Point(2, 1)).Stones.Count);
    }

    [Fact]
    public void Группа_Не_Соединяется_По_Диагонали()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(5, 5), StoneColor.Black));

        Assert.Single(GroupTracker.FindGroup(board, new Point(4, 4)).Stones);
    }

    [Fact]
    public void Группа_Не_Включает_Камни_Другого_Цвета()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(4, 5), StoneColor.White));

        Assert.Single(GroupTracker.FindGroup(board, new Point(4, 4)).Stones);
    }

    [Fact]
    public void Группа_В_Атари_Одна_Дамэ()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.White));

        Assert.True(GroupTracker.FindGroup(board, new Point(1, 1)).IsInAtari);
    }

    [Fact]
    public void Группа_Дамэ_Одиночного_Камня_В_Центре()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(4, GroupTracker.FindGroup(board, new Point(4, 4)).Liberties.Count);
    }

    [Fact]
    public void Группа_Дамэ_Одиночного_Камня_В_Углу()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));

        Assert.Equal(2, GroupTracker.FindGroup(board, new Point(0, 0)).Liberties.Count);
    }

    [Fact]
    public void Группа_Общие_Дамэ_Двух_Камней_Не_Дублируются()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black));

        Assert.Equal(6, GroupTracker.FindGroup(board, new Point(1, 1)).Liberties.Count);
    }

    [Fact]
    public void Группа_Пустой_Точки_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => GroupTracker.FindGroup(board, new Point(4, 4)));
    }

    [Fact]
    public void Группа_Вне_Доски_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => GroupTracker.FindGroup(board, new Point(9, 9)));
    }

    [Fact]
    public void FindLiberties_Считает_Дамэ_По_Камням_Группы()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(0, 0), StoneColor.Black));
        var group = GroupTracker.FindGroup(board, new Point(0, 0));

        Assert.Equal(2, GroupTracker.FindLiberties(board, group).Count);
    }

    [Fact]
    public void FindLiberties_Не_Меняется_После_Обновления_Группы()
    {
        // Дамэ группы пересчитываются по доске, а не берутся из снимка, сделанного ранее.
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));
        var group = GroupTracker.FindGroup(board, new Point(4, 4));

        var next = board.ApplyMove(Move.Play(new Point(4, 5), StoneColor.White));

        Assert.Equal(3, GroupTracker.FindLiberties(next, group).Count);
    }

    [Fact]
    public void AllGroups_Корректно_Разделяет()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 7), StoneColor.Black));

        Assert.Equal(2, GroupTracker.AllGroups(board, StoneColor.Black).Count);
    }

    [Fact]
    public void AllGroups_Возвращает_Камни_Каждой_Группы()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.Black));

        Assert.All(GroupTracker.AllGroups(board, StoneColor.Black), group => Assert.NotEmpty(group.Stones));
    }

    [Fact]
    public void AllGroups_Не_Смешивает_Цвета()
    {
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(2, 1), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(6, 6), StoneColor.White))
            .ApplyMove(Move.Play(new Point(6, 7), StoneColor.White));

        Assert.Single(GroupTracker.AllGroups(board, StoneColor.Black));
    }

    [Fact]
    public void AllGroups_Без_Камней_Возвращает_Пусто()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Empty(GroupTracker.AllGroups(board, StoneColor.Black));
    }

    [Fact]
    public void AllGroups_Пустого_Цвета_Бросает()
    {
        var board = new Board(BoardSize.Size9);

        Assert.Throws<DomainException>(() => GroupTracker.AllGroups(board, StoneColor.Empty));
    }
}
