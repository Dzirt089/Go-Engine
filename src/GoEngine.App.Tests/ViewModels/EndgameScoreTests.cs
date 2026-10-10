using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Конец партии в модели представления: мёртвые группы, пленные, итог.</summary>
/// <remarks>
/// Жалоба пользователя 1: пленные не учитывались ни в очках, ни в территории. Здесь проверяется
/// весь путь от двух пасов до подтверждённого итога: перебор предлагает мёртвые группы, клик
/// помечает и снимает пометку, счёт и разметка считаются по доске без мёртвых камней, а
/// подтверждение фиксирует итог. Основная система — японская (территория плюс пленные).
/// </remarks>
public sealed class EndgameScoreTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void После_Двух_Пасов_Итог_Назван_Сразу()
    {
        // Шага подтверждения нет (D-070): партия кончилась — итог уже назван программой.
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.True(model.HasOutcome);
    }

    [Fact]
    public void Статус_Остаётся_Завершением_Двумя_Пасами()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.Equal("Завершена двумя пасами", model.Status);
    }

    [Fact]
    public void Программа_Находит_Доказанные_Мёртвые_Группы()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.Contains(new Point(0, 0), model.DeadPoints);
    }

    [Fact]
    public void Живые_Камни_Мёртвыми_Не_Объявляются()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.DoesNotContain(new Point(8, 8), model.DeadPoints);
    }

    [Fact]
    public void Приёмка_Нажатия_По_Доске_Не_Меняют_Пометки()
    {
        // Регрессия жалобы 2026-10-10: игрок нажимал по группам после конца партии, и они
        // становились мёртвыми — «нажмёшь на свои — свои помечаются, чужие — чужие, хоть все
        // можно пометить… это подтасовка фактов». Пометки ставит только программа.
        var model = CreateCountingModel();
        var dead = model.DeadPoints;
        var revision = model.DeadRevision;

        // Модель не даёт пометкам меняться: у неё нет ни одного способа их поправить, и после
        // любого чтения они равны перебору ядра. Нажатие по доске проверяется видом
        // (<c>BoardViewTests.Нажатие_После_Конца_Партии_Не_Меняет_Пометки</c>).
        Assert.Equal(Endgame.ProposeDead(model.Board), dead);
        Assert.Equal(revision, model.DeadRevision);
        Assert.Equal(dead, model.DeadPoints);
    }

    [Fact]
    public void Приёмка_В_Идущей_Партии_Пометки_Тоже_Программные()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerInProgressGame());

        Assert.Equal(Endgame.ProposeDead(model.Board), model.DeadPoints);
    }

    [Fact]
    public void Программные_Пометки_Полностью_Совпадают_С_Перебором()
    {
        var model = CreateCountingModel();

        Assert.Equal(Endgame.ProposeDead(model.Board), model.DeadPoints);
    }

    [Fact]
    public void Территория_Считается_По_Доске_Без_Мёртвых_Камней()
    {
        // Точка снятого белого камня (0,0) становится территорией чёрных: её область окружена
        // только чёрными камнями (2,0), (2,1), (0,2), (1,2). Разметка — по кнопке (D-071).
        var model = CreateCountingModel();
        model.ShowTerritory = true;

        Assert.Equal(StoneColor.Black, model.Territory![0]);
    }

    [Fact]
    public void Найденная_Программой_Мёртвая_Группа_Отдаёт_Свой_Угол_Чёрным()
    {
        // Мёртвые камни в углу уходят из доски подсчёта, и их точки становятся территорией чёрных:
        // четыре камня и две точки. Раньше этого можно было добиться только нажатием по доске —
        // теперь так считает программа (решение 2026-10-10, D-084).
        var model = CreateCountingModel();
        model.ShowTerritory = true;

        Assert.Equal(8, model.Territory!.Count(owner => owner == StoneColor.Black));
    }

    [Fact]
    public void Предварительный_Счёт_Идёт_По_Японской_Системе()
    {
        // Площадь чёрных в этой позиции 8, а по территории с пленными — 6.
        var model = CreateCountingModel();

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Предварительный_Счёт_Считается_По_Пометкам_Программы()
    {
        // Счёт считается по пометкам программы: нажимать по доске нечего и не нужно.
        // Чтение свойств и пульс часов счёт не меняют — позиция та же.
        var model = CreateCountingModel();
        var before = model.Score;

        _ = model.DeadPoints;
        _ = model.Territory;
        model.TickClock();

        Assert.Equal(before, model.Score);
        Assert.Equal("Чёрные 6 : 5.5 Белые", before);
    }

    [Fact]
    public void Разбор_Счёта_В_Идущей_Партии_Предварительный()
    {
        // Пока партия идёт, разбор прямо об этом пишет: позиция недоиграна, и числа изменятся
        // (жалоба 2026-10-02: счёт середины партии выглядел окончательным).
        var model = Create();
        _ = model.LoadGame(DeadCornerInProgressGame());

        Assert.StartsWith("Предварительный счёт", model.ScoreDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void После_Двух_Пасов_Подпись_Баннера_Называет_Исход()
    {
        // Баннер оживает сразу: подпись — исход словами, без счёта.
        var model = CreateCountingModel();

        Assert.Equal(model.Outcome, model.ResultDetail);
    }

    [Fact]
    public void После_Подтверждения_Подпись_Баннера_Повторяет_Исход()
    {
        // Счёта в подписи нет: он назван строкой счёта рядом (замечание 3 пользователя 2026-10-02).
        var model = CreateCountingModel();
        Assert.Equal(model.Outcome, model.ResultDetail);
    }

    [Fact]
    public void Пленные_За_Партию_Видны_Отдельной_Строкой()
    {
        // Позиция канона п. 13.9: белые снимают три чёрных камня ходом. Строка называет именно
        // снятые по ходам камни: снятые как мёртвые счётчик партии не знает, и они называются
        // отдельно в PrisonersLine.
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());

        Assert.Equal("чёрные 0 : 3 белые", model.Captures);
    }

    [Fact]
    public void Строка_О_Пленных_Называет_Одно_Число_И_Разложение()
    {
        // Жалоба 2026-09-30: «пленные 0» рядом со счётом, в котором мёртвые камни уже учтены.
        // Жалоба 2026-10-02: прежняя приписка «они тоже в пленные и в очки» читалась как оговорка.
        // Теперь строка называет одно честное число на сторону и разложение в скобках.
        var model = CreateCountingModel();
        var dead = model.DeadPoints.Count;

        Assert.Equal($"Пленные: чёрные {dead} : 0 белые (в партии 0 : 0 · мертвыми {dead} : 0)", model.PrisonersLine);
    }

    [Fact]
    public void Разбор_Называет_Слагаемые_Японской_Системы()
    {
        // Территория 4 плюс пленные 2 дают 6: без слагаемых число не сходится с разбором площади.
        var model = CreateCountingModel();

        Assert.Contains(
            "Японская: чёрные 6 (территория 4 + пленные 2) : 5.5 белые (территория 0 + пленные 0 + коми 5.5)",
            model.ScoreDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Называет_Слагаемые_Китайской_Системы()
    {
        // Площадь: 4 камня чёрных плюс 4 точки территории — 8; у белых 2 камня и коми 5.5.
        var model = CreateCountingModel();

        Assert.Contains(
            "Китайская: чёрные 8 (камни 4 + территория 4) : 7.5 белые (камни 2 + территория 0 + коми 5.5)",
            model.ScoreDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Называет_Мёртвые_Группы_И_Нейтральные_Точки()
    {
        // Мёртвые — те, что уходят в пленные снявшего; нейтральные — те, что не засчитаны никому.
        var model = CreateCountingModel();

        Assert.Contains(
            "Мёртвые группы: белые 2 (в пленные чёрным) · чёрные 0 · нейтральных точек 71",
            model.ScoreDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Итог_Показывается_Словами_Игрока()
    {
        var model = CreateCountingModel();

        // Итог называется глазами игрока: цвет победителя сам по себе ничего ему не говорит.
        Assert.NotEqual(GameTone.None, model.ResultTone);
        Assert.Contains("Вы ", model.ResultHeadline, StringComparison.Ordinal);
    }

    [Fact]
    public void В_Идущей_Партии_Счёт_Сразу_Учитывает_Мёртвых()
    {
        // Игрок ничего не нажимает: программа сама помечает мёртвую группу, её камни уходят
        // в пленные чёрных, а точки становятся их территорией — 4 точки и 2 пленных (D-070).
        var model = Create();
        _ = model.LoadGame(DeadCornerInProgressGame());

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void В_Идущей_Партии_Пометки_Есть_Без_Разметки_На_Экране()
    {
        // Скрытая разметка — только показ: мёртвых программа считает всегда.
        var model = Create();
        _ = model.LoadGame(DeadCornerInProgressGame());

        model.ShowTerritory = false;

        Assert.Contains(new Point(0, 0), model.DeadPoints);
    }

    [Fact]
    public void В_Идущей_Партии_Пленные_Сразу_В_Строке()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerInProgressGame());

        Assert.Contains("мертвыми 2 : 0", model.PrisonersLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Снятые_Мёртвые_Камни_Приносят_Очки()
    {
        // Правила вида спорта «го», пп. 1.10 и 1.12: снятые как мёртвые камни добавляются
        // к пленникам и приносят очки. Регрессия жалобы 2026-09-30: счёт включал их, а строка
        // «Пленные» показывала только камни, снятые ходами.
        var model = CreateCountingModel();
        var dead = model.DeadPoints.Count;
        var before = model.Margin;

        Assert.True(dead > 0);
        Assert.Equal(before, model.Margin);
        Assert.Contains("мертвыми", model.PrisonersLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Итог_Партии_Не_Меняется_От_Нажатий()
    {
        // Итог называет программа: после конца партии он не зависит ни от чего, кроме позиции.
        var model = CreateCountingModel();
        var score = model.Score;
        var outcome = model.Outcome;

        model.TickClock();

        Assert.Equal(score, model.Score);
        Assert.Equal(outcome, model.Outcome);
    }

    [Fact]
    public void Смена_Основной_Системы_Меняет_Строку_Счёта()
    {
        // По площади у чёрных 8, по территории с пленными — 6.
        var model = CreateCountingModel();
        model.ScoringRule = ScoringRule.Chinese;

        Assert.Equal("Чёрные 8 : 7.5 Белые", model.Score);
    }

    [Fact]
    public void Японская_Система_Объявлена_Основной()
    {
        var model = Create();

        Assert.Equal(ScoringRule.Japanese, model.ScoringRule);
    }

    [Fact]
    public void Подтверждение_Без_Согласования_Ничего_Не_Делает()
    {
        var model = Create();
        var score = model.Score;

        Assert.Equal(score, model.Score);
    }

    [Fact]
    public void Отмена_Возвращает_Партию()
    {
        var model = CreateCountingModel();

        _ = model.Undo();

        // Отмена возвращает партию в игру: названного итога больше нет.
        Assert.False(model.HasOutcome);
    }

    [Fact]
    public void Приёмка_Снятая_Группа_Даёт_Японскому_Счёту_Три_Очка()
    {
        // GO_RULES.md, п. 13.9: чёрные снимают группу из трёх белых камней, затем два паса.
        // Территория чёрных — 3 точки, пленные — 3 камня: 3 + 3 = 6 против коми 5.5.
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Приёмка_Без_Пленных_Счёт_Меньше_Ровно_На_Число_Снятых()
    {
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());
        var with = Endgame.Finalize(
            model.Board,
            model.DeadPoints,
            Komi.For9x9,
            model.CapturedWhite,
            model.CapturedBlack,
            ScoringRule.Japanese);

        var without = Endgame.Finalize(model.Board, model.DeadPoints, Komi.For9x9, 0, 0, ScoringRule.Japanese);

        Assert.Equal(3, with.BlackTerritoryPoints - without.BlackTerritoryPoints);
    }

    [Fact]
    public void Приёмка_Перебор_Ничего_Не_Помечает_В_Этой_Партии()
    {
        // Пометок мёртвых нет: счёт партии не подкручен, три очка дал именно захват.
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());

        Assert.Empty(model.DeadPoints);
    }

    [Fact]
    public void Приёмка_Китайский_Счёт_Пленных_Не_Считает()
    {
        // Площадь: 4 камня чёрных + 3 точки территории = 7, у белых 1 камень + коми 5.5 = 6.5.
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());
        model.ScoringRule = ScoringRule.Chinese;

        Assert.Equal("Чёрные 7 : 6.5 Белые", model.Score);
    }

    [Fact]
    public void Пас_Завершающий_Партию_Не_Ждёт_Подтверждения()
    {
        // Партия уже приняла один пас белых: пас чёрных становится вторым подряд и завершает партию.
        // Путь живой — ход идёт через TryPlay, а не через загрузку готовой партии.
        var model = Create();
        _ = model.LoadGame(new SgfGame(
            Size,
            Komi.For9x9,
            [
                Move.Play(new Point(4, 4), StoneColor.Black),
                Move.Play(new Point(5, 5), StoneColor.White),
                Move.Play(new Point(0, 0), StoneColor.Black),
                Move.Pass(StoneColor.White)
            ],
            null));

        _ = model.Pass();

        Assert.True(model.HasOutcome);
    }

    [Fact]
    public void Загрузка_Позиции_Сообщает_О_Новых_Пометках()
    {
        // Пометки меняются вместе с позицией — и только так: вид узнаёт о них от модели.
        var model = Create();
        HashSet<string> changed = [];
        _ = model.LoadGame(DeadCornerInProgressGame());
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        _ = model.LoadGame(DeadCornerGame());

        Assert.Contains(nameof(MainViewModel.DeadPoints), changed);
        Assert.Contains(nameof(MainViewModel.ScoreDetail), changed);
    }

    [Fact]
    public void Загрузка_Завершённой_Партии_Сообщает_Об_Итоге()
    {
        var model = Create();
        HashSet<string> changed = [];
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        _ = model.LoadGame(DeadCornerGame());

        Assert.Contains(nameof(MainViewModel.HasOutcome), changed);
    }

    [Fact]
    public void Справка_Называет_Системы_И_Коми_Турниров()
    {
        var model = Create();

        Assert.Contains("6,5", model.ScoringHint, StringComparison.Ordinal);
    }

    [Fact]
    public void Приёмка_Итог_Показан_Сразу_После_Двух_Пасов()
    {
        // Жалоба 2026-10-02: до нажатия «Посчитать» подсчёт выглядел неверным. Нажатия больше нет
        // (D-070): после двух пасов всё посчитано. Жалоба 2026-10-03: рисуется это только по кнопке
        // (D-071), а числа и пометки готовы сразу.
        var model = CreateCountingModel();

        Assert.False(model.HasTerritory);
        Assert.NotEmpty(model.DeadPoints);
        Assert.Equal(Endgame.ProposeDead(model.Board), model.DeadPoints);
    }

    [Fact]
    public void Приёмка_Помеченная_Группа_Идёт_В_Пленные_Победителю()
    {
        // Требование пользователя 2026-10-02: «мёртвые группы должны считаться пленными,
        // а территория засчитываться победителю». Помеченные белые камни (0,0) и (1,0) уходят
        // в пленные чёрных: в строке пленных это «мертвыми 2 : 0», в счёте они прибавляются
        // к территории чёрных.
        var model = CreateCountingModel();

        Assert.Contains("мертвыми 2 : 0", model.PrisonersLine, StringComparison.Ordinal);

        var score = Endgame.Finalize(
            model.Board,
            model.DeadPoints,
            Komi.For9x9,
            model.CapturedWhite,
            model.CapturedBlack,
            ScoringRule.Japanese);

        Assert.Equal(2, score.BlackPrisoners);
        Assert.Equal(4, score.BlackTerritory);
        Assert.Equal(0, score.WhiteTerritory);
    }

    [Fact]
    public void Приёмка_Точки_Мёртвой_Группы_Принадлежат_Победителю()
    {
        // Разметка территории — та же величина, что входит в счёт: точки снятой группы
        // окрашены цветом победителя, а не остались нейтральными.
        var model = CreateCountingModel();
        model.ShowTerritory = true;

        var territory = model.Territory!;

        Assert.Equal(StoneColor.Black, territory[(0 * Size.Value) + 0]);
        Assert.Equal(StoneColor.Black, territory[(0 * Size.Value) + 1]);
    }

    [Fact]
    public void Приёмка_Счёт_Считается_Только_Программой()
    {
        // Приёмка решения 2026-10-10 (D-084): итог партии определяется позицией и перебором,
        // и ничем больше. Своего счёта у игрока нет — значит, и подтасовать его нечем.
        var model = CreateCountingModel();

        var fromProposal = Endgame.Finalize(
            model.Board, Endgame.ProposeDead(model.Board), Komi.For9x9, model.CapturedWhite, model.CapturedBlack, ScoringRule.Japanese);

        Assert.Equal(fromProposal.ToString(), model.Score);
        Assert.Equal(Endgame.ProposeDead(model.Board), model.DeadPoints);
    }

    [Fact]
    public void Приёмка_Территория_Считается_Без_Найденных_Мёртвых_Камней()
    {
        // Точки снятой белой группы становятся территорией чёрных: 4 камня + 4 точки,
        // у белых остаются только два камня вдали.
        var model = CreateCountingModel();
        model.ShowTerritory = true;

        var territory = model.Territory!;

        Assert.Equal(8, territory.Count(owner => owner == StoneColor.Black));
        Assert.Equal(2, territory.Count(owner => owner == StoneColor.White));
    }

    [Fact]
    public void Приёмка_Японская_Величина_Это_Территория_Плюс_Пленные_Плюс_Коми()
    {
        // Территория чёрных 4, пленные 2; у белых 0 и коми 5.5: 4 + 2 против 0 + 5.5.
        // Слагаемые названы в строке прямо: без них соседний разбор площади («камни 4 + территория 4»)
        // не сходился со счётом и выглядел ошибкой подсчёта (жалоба пользователя 2026-10-02).
        var model = CreateCountingModel();

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
        Assert.Contains(
            "Японская: чёрные 6 (территория 4 + пленные 2) : 5.5 белые (территория 0 + пленные 0 + коми 5.5)",
            model.ScoreDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Приёмка_Китайская_Величина_Считает_Камни_И_Территорию()
    {
        // Площадь: 4 камня чёрных + 4 точки территории = 8, у белых 2 камня + коми 5.5 = 7.5.
        var model = CreateCountingModel();

        Assert.Contains(
            "Китайская: чёрные 8 (камни 4 + территория 4) : 7.5 белые (камни 2 + территория 0 + коми 5.5)",
            model.ScoreDetail,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Приёмка_Разбор_Согласования_Считается_По_Доске_Без_Мёртвых_Камней()
    {
        var model = CreateCountingModel();

        // Территория и камни названы по сторонам, а нейтральные точки — отдельно: сумма
        // «территория + камни + нейтрально» равна площади доски, и по строке это видно.
        Assert.Contains("Территория — чёрные 4 · белые 0 · нейтрально 71", model.ScoreBreakdown, StringComparison.Ordinal);
        Assert.Contains("Камни — чёрные 4 · белые 2", model.ScoreBreakdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Приёмка_Победитель_Назван_Сразу_После_Двух_Пасов()
    {
        var model = CreateCountingModel();

        // Пока партия идёт, итог не показывается; после двух пасов он назван без единого нажатия.
        Assert.True(GameStatusLines.ShowOutcome(model.HasOutcome));
        Assert.Contains("Вы ", model.ResultHeadline, StringComparison.Ordinal);
    }

    [Fact]
    public void Предложение_Мёртвых_Считается_Один_Раз_На_Позицию()
    {
        // Регрессия жалобы 2026-10-02: Endgame.ProposeDead перебирает до 20 000 позиций на группу,
        // и он не должен запускаться на каждое чтение свойства — только на первую просьбу
        // об оценке для новой позиции. Партия завершена, поэтому оценка запрошена сама собой.
        var model = CreateCountingModel();
        _ = model.Score;
        var proposals = model.DeadProposalCount;

        Assert.Equal(1, proposals);

        for (var index = 0; index < 50; index++)
        {
            _ = model.Score;
            _ = model.ScoreDetail;
            _ = model.ScoreBreakdown;
            _ = model.Territory;
            _ = model.PrisonersLine;
            _ = model.Outcome;
            _ = model.Margin;
            _ = model.DeadPoints;
        }

        Assert.Equal(proposals, model.DeadProposalCount);
    }

    [Fact]
    public void Чтение_Счёта_Не_Запускает_Перебор_Заново()
    {
        // Позиция не меняется — перебор предложения не повторяется: счёт читается по готовому списку.
        var model = CreateCountingModel();
        _ = model.Score;
        var proposals = model.DeadProposalCount;

        _ = model.Score;
        _ = model.DeadPoints;
        _ = model.Territory;

        Assert.Equal(proposals, model.DeadProposalCount);
    }

    /// <summary>Создаёт модель представления со случайным соперником: партия в тесте идёт быстро.</summary>
    /// <param name="color">Цвет игрока.</param>
    /// <returns>Модель представления партии 9×9; построение — общее для тестов.</returns>
    private static MainViewModel Create(StoneColor? color = null) =>
        TestViewModel.Create(color, DifficultyLevel.Kyu30, size: Size);

    /// <summary>Создаёт модель, в которой партия уже завершена двумя пасами.</summary>
    /// <returns>Модель с найденной программой мёртвой группой: белая группа в углу доказанно мёртвая.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// </remarks>
    private static MainViewModel CreateCountingModel()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerGame());

        return model;
    }

    /// <summary>Строит партию, в которой три камня снимаются ходом, и завершает её двумя пасами.</summary>
    /// <returns>Прочитанная партия для <see cref="MainViewModel.LoadGame"/>.</returns>
    /// <remarks>
    /// Позиция из <c>GO_RULES.md</c>, п. 13.9: группа (0,0), (1,0), (0,1) получает единственное
    /// дамэ (0,2), и ход в него снимает все три камня. Дальше обе стороны играют по камню вдалеке
    /// и пасуют — партия завершается двумя пасами, пленных трое. Цвет снявшего проверяют
    /// приёмочные тесты счёта: по японской системе снятые камни приносят ему три очка.
    /// </remarks>
    private static SgfGame CapturedThreeGame() => new(
        Size,
        Komi.For9x9,
        [
            Move.Play(new Point(1, 1), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(5, 5), StoneColor.Black),
            Move.Play(new Point(0, 1), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Pass(StoneColor.Black),
            Move.Pass(StoneColor.White)
        ],
        null);

    /// <summary>Строит идущую партию 9×9 с доказанно мёртвой белой группой в углу.</summary>
    /// <returns>Прочитанная партия без двух пасов: статус «идёт», разметка ещё не запрошена.</returns>
    private static SgfGame DeadCornerInProgressGame() => new(
        Size,
        Komi.For9x9,
        [
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White)
        ],
        null);

    /// <summary>Строит партию 9×9 с мёртвой белой группой в углу, завершённую двумя пасами.</summary>
    /// <returns>Прочитанная партия для <see cref="MainViewModel.LoadGame"/>.</returns>
    private static SgfGame DeadCornerGame() => new(
        Size,
        Komi.For9x9,
        [
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White),
            Move.Pass(StoneColor.Black),
            Move.Pass(StoneColor.White)
        ],
        null);
}
