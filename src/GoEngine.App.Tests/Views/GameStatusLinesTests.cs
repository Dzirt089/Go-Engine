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
    public void Строка_Оконченной_Партии_Называет_Признак_Конца_И_Победителя()
    {
        // Дефект 2 QA 2026-10-17: после диалога итога шапка пустела, и результат было
        // не посмотреть. Признак конца и победитель обязаны оставаться на экране.
        var line = GameStatusLines.FinishedLine(hasOutcome: true, "Победили белые");

        Assert.Equal("Партия окончена · Победили белые", line);
        Assert.Contains(GameStatusLines.FinishedMark, line, StringComparison.Ordinal);
    }

    [Fact]
    public void Строка_Оконченной_Партии_Пуста_Пока_Итога_Нет()
    {
        Assert.Empty(GameStatusLines.FinishedLine(hasOutcome: false, "Победили белые"));
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

        Assert.Equal("Счёт пока примерный: Чёрные 0 : 5.5 Белые", score);
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
    public void Итог_Сдачи_Не_Показывает_Перевес_По_Доске()
    {
        // Сдача решает партию независимо от счёта доски: цифра перевеса здесь только
        // запутывала игрока (победитель по сдаче мог видеть отрицательный перевес).
        var headline = GameStatusLines.ResultHeadline(true, "Победили чёрные", string.Empty, GameTone.Win);

        Assert.Equal("Вы победили", headline);
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

    [Fact]
    public void Способ_Завершения_Сдачи_Называется_Одним_Словом()
    {
        Assert.Equal("сдача", GameStatusLines.FinishReason("Завершена сдачей"));
    }

    [Fact]
    public void Способ_Завершения_Пасов_Называется_Коротко()
    {
        Assert.Equal("два паса", GameStatusLines.FinishReason("Завершена двумя пасами"));
    }

    [Fact]
    public void Способ_Завершения_По_Лимиту_Ходов_Называется_Коротко()
    {
        Assert.Equal("лимит ходов", GameStatusLines.FinishReason("Завершена по лимиту ходов"));
    }

    [Fact]
    public void Способ_Завершения_По_Согласию_Называется_Коротко()
    {
        Assert.Equal("по согласию", GameStatusLines.FinishReason("Завершена по согласию сторон"));
    }

    [Fact]
    public void Способ_Завершения_Не_Называется_Пока_Партия_Идёт()
    {
        Assert.Empty(GameStatusLines.FinishReason("Идёт"));
    }

    [Fact]
    public void Короткий_Итог_Сдачи_Называет_Победителя_И_Способ()
    {
        // Жалоба пользователя 2026-11-09: после сдачи на экране стояли «Партия окончена»,
        // «Завершена сдачей», счёт и ещё три строки. Ребёнку нужна одна фраза.
        var line = GameStatusLines.FinalLine(
            hasOutcome: true,
            needsScoreConfirmation: false,
            "Победили белые",
            "Завершена сдачей");

        Assert.Equal("Победили белые · сдача", line);
        Assert.True(line.Length < 40, "итог не помещается в короткую строку телефона");
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Короткий_Итог_Пуст_Пока_Партия_Идёт()
    {
        Assert.Empty(GameStatusLines.FinalLine(false, false, "Победили белые", "Идёт"));
    }

    [Fact]
    public void Короткий_Итог_Недоигранной_Партии_Не_Называет_Победителя()
    {
        var line = GameStatusLines.FinalLine(
            hasOutcome: true,
            needsScoreConfirmation: true,
            "Счёт не принят",
            "Завершена двумя пасами");

        Assert.Equal("Счёт не принят", line);
    }

    [Fact]
    public void Шапка_Телефона_После_Итога_Короткая()
    {
        // Итог одной фразой стоит в свёрнутой шторке; шапка не повторяет его и не обрезается.
        var headline = GameStatusLines.MobileHeadline(
            hasOutcome: true,
            needsScoreConfirmation: false,
            network: true,
            "Партия окончена");

        Assert.Equal(GameStatusLines.FinishedMark, headline);
        Assert.DoesNotContain("Победили", headline);
    }

    [Fact]
    public void Шапка_Телефона_В_Идущей_Партии_Называет_Тип_И_Ход()
    {
        Assert.Equal(
            "С другом · Ход Лиса",
            GameStatusLines.MobileHeadline(false, false, network: true, "Ход Лиса"));
        Assert.Equal(
            "С ИИ · Ход: Чёрные",
            GameStatusLines.MobileHeadline(false, false, network: false, "Ход: Чёрные"));
    }

    [Fact]
    public void Шапка_Телефона_Недоигранной_Партии_Не_Говорит_О_Победителе()
    {
        Assert.Equal(
            GameStatusLines.UnfinishedMark,
            GameStatusLines.MobileHeadline(true, needsScoreConfirmation: true, network: false, "Идёт"));
    }
}
