using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты мобильной раскладки: телефон против настольного окна.</summary>
/// <remarks>
/// Решение о раскладке — чистая функция (<see cref="BoardLayoutRules.DecideLayout"/>), поэтому
/// проверяется тестами, а не снимками экрана: снимок показывает, что вышло, а тест — что правило
/// именно такое. Границы взяты по требованию: ширина 760 и больше — настольный вид, уже — мобильный.
/// </remarks>
public sealed class MobileLayoutRulesTests
{
    [Fact]
    public void Телефон_В_Портрете_Даёт_Мобильную_Раскладку()
    {
        Assert.Equal(LayoutMode.Mobile, BoardLayoutRules.DecideLayout(360, 740));
    }

    [Fact]
    public void Настольное_Окно_Даёт_Прежнюю_Раскладку()
    {
        Assert.Equal(LayoutMode.Desktop, BoardLayoutRules.DecideLayout(1280, 800));
    }

    [Fact]
    public void Ширина_На_Границе_Остаётся_Настольной()
    {
        Assert.Equal(LayoutMode.Desktop, BoardLayoutRules.DecideLayout(BoardLayoutRules.NarrowWidth, 600));
    }

    [Fact]
    public void Ширина_На_Точку_Уже_Границы_Даёт_Мобильную_Раскладку()
    {
        Assert.Equal(LayoutMode.Mobile, BoardLayoutRules.DecideLayout(BoardLayoutRules.NarrowWidth - 1, 600));
    }

    [Fact]
    public void Планшет_В_Ландшафте_Остаётся_Настольным()
    {
        // Высота в решении не участвует: широкая и низкая область — это настольное окно,
        // а не телефон.
        Assert.Equal(LayoutMode.Desktop, BoardLayoutRules.DecideLayout(900, 400));
    }

    [Fact]
    public void Нулевые_Размеры_Дают_Настольную_Раскладку()
    {
        Assert.Equal(LayoutMode.Desktop, BoardLayoutRules.DecideLayout(0, 0));
    }

    [Fact]
    public void Доска_На_Телефоне_Занимает_Всю_Ширину()
    {
        Assert.Equal(352, BoardLayoutRules.MobileBoardSide(352, 520));
    }

    [Fact]
    public void Доска_На_Телефоне_Ограничена_Высотой_Когда_Та_Меньше()
    {
        Assert.Equal(300, BoardLayoutRules.MobileBoardSide(352, 300));
    }

    [Fact]
    public void Доска_Не_Выходит_За_Отведённое_Место()
    {
        // Место уже доски: сторона равна меньшей величине, вверх не увеличивается —
        // иначе на низком экране доска вылезла бы на шторку.
        Assert.Equal(200, BoardLayoutRules.MobileBoardSide(200, 300));
    }

    [Fact]
    public void Низкий_Экран_Даёт_Доску_По_Высоте_Места()
    {
        Assert.Equal(96, BoardLayoutRules.MobileBoardSide(700, 96));
    }

    [Fact]
    public void Нулевое_Место_Не_Даёт_Доски()
    {
        Assert.Equal(0, BoardLayoutRules.MobileBoardSide(0, 500));
    }

    [Fact]
    public void Панель_Действий_Телефона_Не_Уже_Доски()
    {
        // Обычный телефон: доска упирается в ширину экрана, панель по ней и выравнивается.
        Assert.Equal(352, BoardLayoutRules.MobileActionBarWidth(352, 396));
    }

    [Fact]
    public void Панель_Действий_Телефона_Не_Уже_Читаемой_На_Узком_Экране()
    {
        // 360×640: доска ограничена высотой (266 точек), и панель ровно по ней обрезала бы
        // подписи четырёх кнопок — она берёт наименьшую читаемую ширину.
        Assert.Equal(BoardLayoutRules.MinimumActionBarWidth, BoardLayoutRules.MobileActionBarWidth(266, 344));
    }

    [Fact]
    public void Панель_Действий_Телефона_Не_Шире_Экрана()
    {
        // Ландшафт: доска маленькая, но панель шире читаемого минимума не станет — только
        // если экран уже самого минимума.
        Assert.Equal(BoardLayoutRules.MinimumActionBarWidth, BoardLayoutRules.MobileActionBarWidth(174, 899));
    }

    [Fact]
    public void Очень_Узкий_Экран_Ограничивает_Панель_Действий_Собой()
    {
        Assert.Equal(300, BoardLayoutRules.MobileActionBarWidth(280, 300));
    }

    [Fact]
    public void Нулевое_Место_Не_Даёт_Панели_Действий()
    {
        Assert.Equal(0, BoardLayoutRules.MobileActionBarWidth(300, 0));
    }

    [Fact]
    public void Координаты_Помещаются_На_Доске_9_На_9_В_Телефоне()
    {
        Assert.True(BoardLayoutRules.CoordinatesFit(352, 9));
    }

    [Fact]
    public void Координаты_Не_Помещаются_На_Доске_19_На_19_В_Телефоне()
    {
        // Та же сторона 352 точки, но клеток вдвое больше: подписи стали бы серой кашей.
        Assert.False(BoardLayoutRules.CoordinatesFit(352, 19));
    }

    [Fact]
    public void Координаты_Помещаются_На_Большой_Доске_19_На_19()
    {
        Assert.True(BoardLayoutRules.CoordinatesFit(760, 19));
    }

    [Fact]
    public void Нулевая_Доска_Не_Показывает_Координаты()
    {
        Assert.False(BoardLayoutRules.CoordinatesFit(0, 9));
    }
}
