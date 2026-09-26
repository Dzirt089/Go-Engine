using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты размера доски: допустимы только 9×9, 13×13 и 19×19.</summary>
public sealed class BoardSizeTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(19)]
    public void BoardSize_Создаётся_с_валидным_размером(int value)
    {
        var size = new BoardSize((byte)value);

        Assert.Equal((byte)value, size.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(18)]
    [InlineData(20)]
    [InlineData(255)]
    public void BoardSize_Бросает_при_невалидном_размере(int value)
    {
        // Регрессия GO_RULES п. 1: «Другие размеры запрещены».
        Assert.Throws<DomainException>(() => new BoardSize((byte)value));
    }

    [Theory]
    [InlineData(9, 81)]
    [InlineData(13, 169)]
    [InlineData(19, 361)]
    public void BoardSize_Area_Равен_Квадрату_Стороны(int value, int expectedArea)
    {
        Assert.Equal(expectedArea, new BoardSize((byte)value).Area);
    }
}
