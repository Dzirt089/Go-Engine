using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты точки доски: границы, соседи по стороне, координатный формат.</summary>
public sealed class PointTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(4, 4)]
    public void Point_IsOnBoard_Возвращает_корректно(int x, int y)
    {
        Assert.True(new Point((byte)x, (byte)y).IsOnBoard(BoardSize.Size9));
    }

    [Theory]
    [InlineData(9, 0)]
    [InlineData(0, 9)]
    [InlineData(9, 9)]
    public void Point_IsOnBoard_За_Доской_Возвращает_Ложь(int x, int y)
    {
        Assert.False(new Point((byte)x, (byte)y).IsOnBoard(BoardSize.Size9));
    }

    [Fact]
    public void Point_Neighbors_Угол_Возвращает_двух_соседей()
    {
        var neighbors = new Point(0, 0).Neighbors(BoardSize.Size9);

        Assert.Equal(new Point[] { new(1, 0), new(0, 1) }, neighbors);
    }

    [Fact]
    public void Point_Neighbors_Край_Возвращает_трёх_соседей()
    {
        var neighbors = new Point(0, 4).Neighbors(BoardSize.Size9);

        Assert.Equal(new Point[] { new(1, 4), new(0, 3), new(0, 5) }, neighbors);
    }

    [Fact]
    public void Point_Neighbors_Центр_Возвращает_четырёх_соседей()
    {
        var neighbors = new Point(4, 4).Neighbors(BoardSize.Size9);

        Assert.Equal(new Point[] { new(3, 4), new(5, 4), new(4, 3), new(4, 5) }, neighbors);
    }

    [Fact]
    public void Point_Neighbors_Не_Содержат_Диагональных_Точек()
    {
        var neighbors = new Point(4, 4).Neighbors(BoardSize.Size9);

        // Регрессия GO_RULES п. 4: группа соединяется по стороне, а не по диагонали.
        Assert.DoesNotContain(new Point(3, 3), neighbors);
    }

    [Fact]
    public void Point_ToString_19x19_Формат_A1()
    {
        Assert.Equal("A1", new Point(0, 0).ToString());
    }

    [Fact]
    public void Point_ToString_19x19_Пропускает_букву_I()
    {
        Assert.Equal("J4", new Point(8, 3).ToString());
    }

    [Fact]
    public void Point_ToString_19x19_Последний_Столбец_Обозначен_Буквой_T()
    {
        Assert.Equal("T19", new Point(18, 18).ToString());
    }

    [Fact]
    public void Point_ToString_За_Пределами_19x19_Не_Бросает()
    {
        Assert.Equal("(20,3)", new Point(20, 3).ToString());
    }
}
