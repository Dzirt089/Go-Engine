using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты выбора раскладки — телефон против настольного окна.</summary>
/// <remarks>
/// Логика вынесена из вида в чистую функцию, поэтому проверяется тестами, а не снимками экрана:
/// снимок показывает, что вышло, а тест — что правило именно такое.
/// </remarks>
public sealed class BoardLayoutRulesTests
{
    [Fact]
    public void Настольное_Окно_Держит_Доску_Слева()
    {
        Assert.False(BoardLayoutRules.IsNarrow(920, 720));
    }

    [Fact]
    public void Телефон_В_Портрете_Ставит_Управление_Под_Доску()
    {
        Assert.True(BoardLayoutRules.IsNarrow(420, 800));
    }

    [Fact]
    public void Телефон_В_Ландшафте_Оставляет_Панель_Справа()
    {
        Assert.False(BoardLayoutRules.IsNarrow(800, 420));
    }

    [Fact]
    public void Узкое_Окно_Переключается_На_Вертикальную_Раскладку()
    {
        Assert.True(BoardLayoutRules.IsNarrow(BoardLayoutRules.NarrowWidth - 1, 600));
    }

    [Fact]
    public void Ширина_На_Границе_Остаётся_Горизонтальной()
    {
        Assert.False(BoardLayoutRules.IsNarrow(BoardLayoutRules.NarrowWidth, 600));
    }

    [Fact]
    public void Доска_В_Портрете_Квадратная_И_Не_Выше_Экрана()
    {
        var height = BoardLayoutRules.BoardHeight(420, 800);

        Assert.Equal(420, height);
    }

    [Fact]
    public void Доска_На_Узком_Экране_Не_Занимает_Всю_Высоту()
    {
        var height = BoardLayoutRules.BoardHeight(360, 500);

        Assert.True(height < 500);
    }

    [Fact]
    public void Доска_Не_Становится_Меньше_Разумного_Минимума()
    {
        var height = BoardLayoutRules.BoardHeight(200, 300);

        Assert.Equal(BoardLayoutRules.MinimumBoardHeight, height);
    }

    [Fact]
    public void Нулевые_Размеры_Не_Ломают_Правила()
    {
        Assert.False(BoardLayoutRules.IsNarrow(0, 0));
        Assert.Equal(0, BoardLayoutRules.BoardHeight(0, 0));
    }
}
