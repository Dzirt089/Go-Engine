using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты строк состояния партии: победитель не называется до подтверждения подсчёта.</summary>
/// <remarks>
/// Жалоба: в шапке стояло «Победили белые», пока мёртвые группы ещё не согласованы, — игрок видел
/// приговор раньше проверки. Правило вынесено в чистые функции <see cref="GameStatusLines"/>,
/// поэтому проверяется тестами, а не снимком экрана.
/// </remarks>
public sealed class GameStatusLinesTests
{
    [Fact]
    public void В_Подсчёте_Шапка_Не_Называет_Победителя()
    {
        var headline = GameStatusLines.Headline(isCounting: true, hasOutcome: true, "Завершена двумя пасами", "Белые");

        Assert.Equal("Подсчёт не подтверждён", headline);
    }

    [Fact]
    public void В_Подсчёте_Вторая_Строка_Просит_Проверить_Группы()
    {
        var detail = GameStatusLines.Detail(isCounting: true, hasOutcome: true, "Победили белые", "12");

        Assert.Equal("Проверьте мёртвые группы", detail);
    }

    [Fact]
    public void После_Подтверждения_Шапка_Показывает_Состояние_Партии()
    {
        var headline = GameStatusLines.Headline(isCounting: false, hasOutcome: true, "Завершена двумя пасами", "Белые");

        Assert.Equal("Завершена двумя пасами", headline);
    }

    [Fact]
    public void После_Подтверждения_Вторая_Строка_Показывает_Итог()
    {
        var detail = GameStatusLines.Detail(isCounting: false, hasOutcome: true, "Победили белые", "12");

        Assert.Equal("Победили белые", detail);
    }

    [Fact]
    public void В_Идущей_Партии_Шапка_Показывает_Очередь_Хода()
    {
        var headline = GameStatusLines.Headline(isCounting: false, hasOutcome: false, "Идёт", "Чёрные");

        Assert.Equal("Ход: Чёрные", headline);
    }

    [Fact]
    public void В_Идущей_Партии_Вторая_Строка_Показывает_Номер_Хода()
    {
        var detail = GameStatusLines.Detail(isCounting: false, hasOutcome: false, string.Empty, "7");

        Assert.Equal("Ход №7", detail);
    }

    [Fact]
    public void В_Подсчёте_Счёт_Называется_Предварительным()
    {
        var score = GameStatusLines.ScoreLine(isCounting: true, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Счёт (предварительный): Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void После_Подтверждения_Счёт_Показывается_Без_Пометки()
    {
        var score = GameStatusLines.ScoreLine(isCounting: false, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Счёт: Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void Камень_Очереди_Не_Показывается_В_Подсчёте_И_После_Партии()
    {
        Assert.False(GameStatusLines.ShowTurnStone(isCounting: true, hasOutcome: true));
        Assert.False(GameStatusLines.ShowTurnStone(isCounting: false, hasOutcome: true));
        Assert.True(GameStatusLines.ShowTurnStone(isCounting: false, hasOutcome: false));
    }

    [Fact]
    public void Итог_Не_Показывается_До_Подтверждения_Подсчёта()
    {
        Assert.False(GameStatusLines.ShowOutcome(isCounting: true, hasOutcome: true));
        Assert.True(GameStatusLines.ShowOutcome(isCounting: false, hasOutcome: true));
    }
}
