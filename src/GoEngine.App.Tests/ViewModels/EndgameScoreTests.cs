using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Конец партии в модели представления: согласование мёртвых, пленные, итог.</summary>
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
    public void После_Двух_Пасов_Идёт_Согласование_Мёртвых()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.True(model.IsCounting);
    }

    [Fact]
    public void Согласование_Можно_Подтвердить()
    {
        var model = Create();
        _ = model.LoadGame(DeadCornerGame());

        Assert.True(model.CanConfirmScore);
    }

    [Fact]
    public void Статус_Остаётся_Завершением_Двумя_Пасами()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.Equal("Завершена двумя пасами", model.Status);
    }

    [Fact]
    public void Согласование_Предлагает_Доказанные_Мёртвые_Группы()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.Contains(new Point(0, 0), model.DeadPoints);
    }

    [Fact]
    public void Согласование_Не_Предлагает_Живые_Камни()
    {
        var model = Create();

        _ = model.LoadGame(DeadCornerGame());

        Assert.DoesNotContain(new Point(8, 8), model.DeadPoints);
    }

    [Fact]
    public void Клик_Помечает_Всю_Группу()
    {
        // Группа из двух камней: клик по одному помечает и второй.
        var model = CreateCountingModel();
        _ = model.ToggleDeadAt(new Point(8, 8));

        var marked = model.DeadPoints;

        Assert.Contains(new Point(8, 7), marked);
    }

    [Fact]
    public void Повторный_Клик_Снимает_Пометку()
    {
        // Живая группа белых в дальнем углу: перебор её не предлагал, значит оба клика — наши.
        var model = CreateCountingModel();
        _ = model.ToggleDeadAt(new Point(8, 8));

        _ = model.ToggleDeadAt(new Point(8, 8));

        Assert.DoesNotContain(new Point(8, 8), model.DeadPoints);
    }

    [Fact]
    public void Клик_По_Пустой_Точке_Ничего_Не_Меняет()
    {
        var model = CreateCountingModel();

        Assert.False(model.ToggleDeadAt(new Point(4, 4)));
    }

    [Fact]
    public void Клик_Пока_Партия_Идёт_Ничего_Не_Меняет()
    {
        var model = Create();

        Assert.False(model.ToggleDeadAt(new Point(4, 4)));
    }

    [Fact]
    public void Клик_Увеличивает_Счётчик_Пометок()
    {
        var model = CreateCountingModel();
        var before = model.DeadRevision;

        _ = model.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(before + 1, model.DeadRevision);
    }

    [Fact]
    public void Территория_Считается_По_Доске_Без_Мёртвых_Камней()
    {
        // Точка снятого белого камня (0,0) становится территорией чёрных: её область окружена
        // только чёрными камнями (2,0), (2,1), (0,2), (1,2).
        var model = CreateCountingModel();

        Assert.Equal(StoneColor.Black, model.Territory![0]);
    }

    [Fact]
    public void Без_Пометки_Угол_Не_Считается_Территорией_Чёрных()
    {
        // Живые камни в углу делят область с чёрными, и территории не достаётся никому:
        // чёрными остаются только четыре их камня. Именно поэтому мёртвую группу помечают.
        var model = CreateCountingModel();
        _ = model.ToggleDeadAt(new Point(0, 0));

        Assert.Equal(4, model.Territory!.Count(owner => owner == StoneColor.Black));
    }

    [Fact]
    public void Предварительный_Счёт_Идёт_По_Японской_Системе()
    {
        // Площадь чёрных в этой позиции 8, а по территории с пленными — 6.
        var model = CreateCountingModel();

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Снятие_Пометки_Меняет_Предварительный_Счёт()
    {
        var model = CreateCountingModel();
        _ = model.ToggleDeadAt(new Point(0, 0));

        Assert.Equal("Чёрные 0 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Разбор_Говорит_О_Предварительном_Подсчёте()
    {
        // Пока подсчёт не подтверждён, баннер прямо об этом пишет: игрок должен видеть,
        // что счёт ещё изменится (жалоба 2026-09-30 — прежние четыре строки разбора путали).
        var model = CreateCountingModel();

        Assert.Contains("предварительно", model.ResultDetail, StringComparison.Ordinal);
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
    public void Строка_О_Пленных_Называет_И_Снятых_Мёртвыми()
    {
        // Жалоба 2026-09-30: «пленные 0» рядом со счётом, в котором мёртвые камни уже учтены.
        // Строка обязана называть обе величины.
        var model = CreateCountingModel();
        var dead = model.DeadPoints.Count;

        Assert.Contains($"снято мёртвыми: {dead}", model.PrisonersLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Называет_Обе_Системы_Одной_Строкой()
    {
        var model = CreateCountingModel();

        // Первой идёт основная система (японская), второй — китайская; слагаемые в подписи
        // не расписываются: их видно в расшифровке площади.
        Assert.Contains("Японская:", model.ScoreDetail, StringComparison.Ordinal);
        Assert.Contains("Китайская:", model.ScoreDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Подтверждение_Заканчивает_Согласование()
    {
        var model = CreateCountingModel();

        model.ConfirmScore();

        Assert.False(model.IsCounting);
    }

    [Fact]
    public void Подтверждение_Показывает_Итог_Словами_Игрока()
    {
        var model = CreateCountingModel();

        model.ConfirmScore();

        // Итог называется глазами игрока: цвет победителя сам по себе ничего ему не говорит.
        Assert.NotEqual(GameTone.None, model.ResultTone);
        Assert.Contains("Вы ", model.ResultHeadline, StringComparison.Ordinal);
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

        model.ConfirmScore();

        Assert.True(dead > 0);
        Assert.Equal(before, model.Margin);
        Assert.Contains("снято мёртвыми", model.PrisonersLine, StringComparison.Ordinal);
    }

    [Fact]
    public void После_Подтверждения_Пометки_Не_Меняют_Итог()
    {
        var model = CreateCountingModel();
        model.ConfirmScore();
        var score = model.Score;

        _ = model.ToggleDeadAt(new Point(0, 0));

        Assert.Equal(score, model.Score);
    }

    [Fact]
    public void Смена_Основной_Системы_Меняет_Строку_Счёта()
    {
        // По площади у чёрных 8, по территории с пленными — 6.
        var model = CreateCountingModel();
        model.ConfirmScore();

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

        model.ConfirmScore();

        Assert.Equal(score, model.Score);
    }

    [Fact]
    public void Отмена_После_Подтверждения_Возвращает_Партию()
    {
        var model = CreateCountingModel();
        model.ConfirmScore();

        _ = model.Undo();

        Assert.False(model.IsCounting);
    }

    [Fact]
    public void Приёмка_Снятая_Группа_Даёт_Японскому_Счёту_Три_Очка()
    {
        // GO_RULES.md, п. 13.9: чёрные снимают группу из трёх белых камней, затем два паса.
        // Территория чёрных — 3 точки, пленные — 3 камня: 3 + 3 = 6 против коми 5.5.
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());

        model.ConfirmScore();

        Assert.Equal("Чёрные 6 : 5.5 Белые", model.Score);
    }

    [Fact]
    public void Приёмка_Без_Пленных_Счёт_Меньше_Ровно_На_Число_Снятых()
    {
        var model = Create();
        _ = model.LoadGame(CapturedThreeGame());
        model.ConfirmScore();

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
        // Пометок нет: счёт партии не подкручен согласованием, три очка дал именно захват.
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
        model.ConfirmScore();

        model.ScoringRule = ScoringRule.Chinese;

        Assert.Equal("Чёрные 7 : 6.5 Белые", model.Score);
    }

    [Fact]
    public void Пас_Завершающий_Партию_Включает_Согласование()
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

        Assert.True(model.IsCounting);
    }

    [Fact]
    public void Уведомления_Согласования_Содержат_Новые_Свойства()
    {
        var model = CreateCountingModel();
        HashSet<string> changed = [];
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        model.ConfirmScore();

        Assert.Contains(nameof(MainViewModel.ScoreDetail), changed);
    }

    [Fact]
    public void Уведомления_Пометки_Содержат_Список_И_Счётчик()
    {
        var model = CreateCountingModel();
        HashSet<string> changed = [];
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        _ = model.ToggleDeadAt(new Point(8, 8));

        Assert.Contains(nameof(MainViewModel.DeadPoints), changed);
    }

    [Fact]
    public void Загрузка_Завершённой_Партии_Сообщает_О_Согласовании()
    {
        var model = Create();
        HashSet<string> changed = [];
        model.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        _ = model.LoadGame(DeadCornerGame());

        Assert.Contains(nameof(MainViewModel.IsCounting), changed);
    }

    [Fact]
    public void Справка_Называет_Системы_И_Коми_Турниров()
    {
        var model = Create();

        Assert.Contains("6,5", model.ScoringHint, StringComparison.Ordinal);
    }

    /// <summary>Создаёт модель представления со случайным соперником: партия в тесте идёт быстро.</summary>
    /// <param name="color">Цвет игрока.</param>
    /// <returns>Модель представления партии 9×9; построение — общее для тестов.</returns>
    private static MainViewModel Create(StoneColor? color = null) =>
        TestViewModel.Create(color, DifficultyLevel.Kyu30, size: Size);

    /// <summary>Создаёт модель, в которой партия уже завершена двумя пасами.</summary>
    /// <returns>Модель с согласованием мёртвых групп: белая группа в углу доказанно мёртвая.</returns>
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
