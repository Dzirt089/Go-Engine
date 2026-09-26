using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты хода и его фабрик.</summary>
public sealed class MoveTests
{
    [Fact]
    public void Move_Play_Создаёт_корректно()
    {
        var move = Move.Play(new Point(3, 4), StoneColor.White);

        Assert.Equal((MoveType.Play, new Point(3, 4), StoneColor.White), (move.Type, move.Point, move.Color));
    }

    [Fact]
    public void Move_Pass_Создаёт_корректно()
    {
        var move = Move.Pass(StoneColor.Black);

        Assert.Equal((MoveType.Pass, default(Point), StoneColor.Black), (move.Type, move.Point, move.Color));
    }

    [Fact]
    public void Move_Resign_Создаёт_корректно()
    {
        var move = Move.Resign(StoneColor.White);

        Assert.Equal((MoveType.Resign, default(Point), StoneColor.White), (move.Type, move.Point, move.Color));
    }

    [Fact]
    public void Move_Play_С_Пустым_Цветом_Бросает()
    {
        Assert.Throws<DomainException>(() => Move.Play(new Point(0, 0), StoneColor.Empty));
    }

    [Fact]
    public void Move_Pass_С_Пустым_Цветом_Бросает()
    {
        Assert.Throws<DomainException>(() => Move.Pass(StoneColor.Empty));
    }

    [Fact]
    public void Move_None_Обозначает_Отсутствие_Хода()
    {
        Assert.True(Move.None.IsNone);
    }

    [Fact]
    public void Move_Настоящий_Ход_Не_Считается_Отсутствующим()
    {
        Assert.False(Move.Pass(StoneColor.Black).IsNone);
    }

    [Fact]
    public void Move_Одинаковые_Ходы_Равны()
    {
        Assert.Equal(Move.Play(new Point(2, 2), StoneColor.Black), Move.Play(new Point(2, 2), StoneColor.Black));
    }
}
