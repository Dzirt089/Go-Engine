using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты цвета камня.</summary>
public sealed class StoneColorTests
{
    [Fact]
    public void StoneColor_Opponent_Возвращает_противоположный()
    {
        Assert.Equal(StoneColor.White, StoneColor.Black.Opponent());
    }

    [Fact]
    public void StoneColor_Opponent_Белого_Возвращает_Чёрный()
    {
        Assert.Equal(StoneColor.Black, StoneColor.White.Opponent());
    }

    [Fact]
    public void StoneColor_Opponent_Для_Empty_Бросает()
    {
        // Регрессия GO_RULES п. 2: противоположный цвет определён только для Black и White.
        Assert.Throws<DomainException>(() => StoneColor.Empty.Opponent());
    }

    [Fact]
    public void StoneColor_Элементы_Имеют_Разные_Идентификаторы()
    {
        var ids = new[] { StoneColor.Empty, StoneColor.Black, StoneColor.White }.Select(static color => color.Id);

        Assert.Equal(new[] { 0, 1, 2 }, ids);
    }

    [Fact]
    public void StoneColor_Найденный_По_Имени_Равен_Статическому_Элементу()
    {
        Assert.Equal(StoneColor.Black, Enumeration.FromName<StoneColor>("Black"));
    }
}
