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
    public void В_Подсчёте_Шапка_Называет_Шаг_Согласования()
    {
        // Регрессия жалобы 2026-10-02: «Подсчёт не подтверждён» читалось как сообщение об ошибке,
        // хотя счёт уже посчитан и показан верно. Теперь шаг называется спокойно.
        var headline = GameStatusLines.Headline(isCounting: true, hasOutcome: true, "Завершена двумя пасами", "Белые");

        Assert.Equal("Согласование подсчёта", headline);
    }

    [Fact]
    public void В_Подсчёте_Вторая_Строка_Просит_Проверить_Пометки()
    {
        var detail = GameStatusLines.Detail(isCounting: true, hasOutcome: true, "Победили белые", "12");

        Assert.Equal("Проверьте пометки мёртвых групп", detail);
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
        var score = GameStatusLines.ScoreLine(provisional: true, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Предварительный счёт: Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void После_Подтверждения_Счёт_Показывается_Без_Пометки()
    {
        var score = GameStatusLines.ScoreLine(provisional: false, "Чёрные 0 : 5.5 Белые");

        Assert.Equal("Счёт: Чёрные 0 : 5.5 Белые", score);
    }

    [Fact]
    public void В_Идущей_Партии_Счёт_Тоже_Предварительный()
    {
        // Регрессия жалобы 2026-10-02: партия идёт, мёртвые камни не сняты и дамэ не заполнены,
        // а панель называла счёт недоигранной позиции окончательным («Счёт: Чёрные 0 : 5.5 Белые»
        // на пустой доске). Предварительным он обязан называться и здесь.
        var score = GameStatusLines.ScoreLine(provisional: true, "Чёрные 0 : 5.5 Белые");

        Assert.StartsWith("Предварительный счёт", score, StringComparison.Ordinal);
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

        Assert.Equal("Предварительный счёт: Чёрные 0 : 5.5 Белые", detail);
    }

    [Fact]
    public void Регрессия_Подсказка_Согласования_Не_Обещает_Счёт_По_Кнопке()
    {
        // Жалоба 2026-10-02: подсказка панели обещала «Окончательный счёт — по кнопке» и выглядела
        // ошибкой, хотя счёт уже показан. Тексты панели живут здесь же, поэтому регрессию держит
        // тест, а не снимок экрана.
        Assert.DoesNotContain("Окончательный счёт", GameStatusLines.CountingInvitation, StringComparison.Ordinal);
        Assert.DoesNotContain("Окончательный счёт", GameStatusLines.CountingInvitationShort, StringComparison.Ordinal);
        Assert.DoesNotContain("Подсчёт не подтверждён", GameStatusLines.CountingInvitation, StringComparison.Ordinal);
    }

    [Fact]
    public void Подсказка_Согласования_Приглашает_Проверить_Пометки_И_Подтвердить()
    {
        var invitation = GameStatusLines.CountingInvitation;

        Assert.StartsWith("Проверьте пометки мёртвых групп", invitation, StringComparison.Ordinal);
        Assert.Contains("помечает или снимает пометку", invitation, StringComparison.Ordinal);
        Assert.Contains("Подтвердите подсчёт", invitation, StringComparison.Ordinal);
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
