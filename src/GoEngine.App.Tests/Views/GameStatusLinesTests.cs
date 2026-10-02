using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты строк состояния партии: что показывается до и после её конца.</summary>
/// <remarks>
/// Тексты вынесены в чистые функции <see cref="GameStatusLines"/>, поэтому проверяются тестами,
/// а не снимком экрана. Шага согласования, во время которого победитель молчал, больше нет
/// (D-070): пока партия идёт, показываются очередь хода и номер хода, а после конца — состояние
/// и названный итог.
/// </remarks>
public sealed class GameStatusLinesTests
{
    [Fact]
    public void В_Идущей_Партии_Шапка_Показывает_Очередь_Хода()
    {
        var headline = GameStatusLines.Headline(hasOutcome: false, "Идёт", "Чёрные");

        Assert.Equal("Ход: Чёрные", headline);
    }

    [Fact]
    public void В_Идущей_Партии_Вторая_Строка_Показывает_Номер_Хода()
    {
        var detail = GameStatusLines.Detail(hasOutcome: false, string.Empty, "7");

        Assert.Equal("Ход №7", detail);
    }

    [Fact]
    public void После_Конца_Партии_Шапка_Показывает_Состояние()
    {
        var headline = GameStatusLines.Headline(hasOutcome: true, "Завершена двумя пасами", "Белые");

        Assert.Equal("Завершена двумя пасами", headline);
    }

    [Fact]
    public void После_Конца_Партии_Вторая_Строка_Показывает_Итог()
    {
        var detail = GameStatusLines.Detail(hasOutcome: true, "Победили белые", "12");

        Assert.Equal("Победили белые", detail);
    }

    [Fact]
    public void Идущая_Партия_Считает_Счёт_Предварительным()
    {
        // Регрессия жалобы 2026-10-02: партия идёт, мёртвые камни не сняты и дамэ не заполнены,
        // а панель называла счёт недоигранной позиции окончательным.
        var score = GameStatusLines.ScoreLine(provisional: true, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Предварительный счёт: Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void Завершённая_Партия_Показывает_Счёт_Без_Пометки()
    {
        var score = GameStatusLines.ScoreLine(provisional: false, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Счёт: Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void Камень_Очереди_Показывается_В_Идущей_Партии()
    {
        Assert.True(GameStatusLines.ShowTurnStone(hasOutcome: false));
    }

    [Fact]
    public void Камень_Очереди_Не_Показывается_После_Партии()
    {
        // После конца партии хода нет: камень очереди не показывается.
        Assert.False(GameStatusLines.ShowTurnStone(hasOutcome: true));
    }

    [Fact]
    public void Итог_Показывается_Когда_Он_Есть()
    {
        // Итог называется сразу после конца партии: подтверждать его не нужно (D-070).
        Assert.True(GameStatusLines.ShowOutcome(hasOutcome: true));
    }

    [Fact]
    public void Без_Итога_Показывать_Нечего()
    {
        Assert.False(GameStatusLines.ShowOutcome(hasOutcome: false));
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
    public void Подпись_Баннера_Называет_Исход_Словами()
    {
        // Счёта в подписи нет: он назван отдельной строкой рядом, и его повтор читался как два
        // разных числа (замечание 3 пользователя 2026-10-02).
        var detail = GameStatusLines.ResultDetail("Победили белые");

        Assert.Equal("Победили белые", detail);
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
