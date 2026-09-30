using GoEngine.App.ViewModels;
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

    [Fact]
    public void Заголовок_Баннера_Называет_Итог_Словами_Игрока()
    {
        // «Победили чёрные» игроку ничего не говорит: важно, выиграл он сам или нет
        // (жалоба 2026-09-30).
        var headline = GameStatusLines.ResultHeadline(true, "Победили чёрные", "7.5", GameTone.Win);

        Assert.Equal("Вы победили · +7.5", headline);
    }

    [Fact]
    public void Заголовок_Баннера_При_Проигрыше_Показывает_Перевес_Соперника()
    {
        var headline = GameStatusLines.ResultHeadline(true, "Победили белые", "2.5", GameTone.Loss);

        Assert.Equal("Вы проиграли · −2.5", headline);
    }

    [Fact]
    public void Заголовок_Баннера_При_Ничьей_Без_Перевеса()
    {
        var headline = GameStatusLines.ResultHeadline(true, "Ничья", "0", GameTone.Draw);

        Assert.Equal("Ничья", headline);
    }

    [Fact]
    public void Без_Итога_Баннер_Пуст()
    {
        Assert.Empty(GameStatusLines.ResultHeadline(false, string.Empty, "0", GameTone.None));
    }

    [Fact]
    public void Подпись_Баннера_Во_Время_Подсчёта_Предварительная()
    {
        var detail = GameStatusLines.ResultDetail(isCounting: true, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Счёт предварительно: Чёрные 0 : 5.5 Белые", detail);
    }

    [Fact]
    public void Строка_Сведений_Собирает_Соперника_Цвет_Ход_И_Коми()
    {
        // Четыре отдельные строки панели заменены одной: игроку нужен не столбик, а строка.
        // Слова короткие намеренно — иначе строка переносится в узкой панели.
        var stats = GameStatusLines.StatsLine("20 кю · перебор", "чёрные", "12", "5.5");

        Assert.Equal("Соперник: 20 кю · перебор · вы: чёрные · ход №12 · коми 5.5", stats);
    }
}
