using System.Globalization;
using System.Text.RegularExpressions;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Разметка территории: что показывается, что считается и когда включается.</summary>
/// <remarks>
/// Заход 1: пользователь просил видеть, кому принадлежат пустые точки, и различать территорию
/// чёрных, белых и нейтральные точки. Считает её <see cref="Scorer.Ownership"/>; тесты держат
/// и состав разметки, и расшифровку в панели партии.
/// </remarks>
public sealed class TerritoryTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Территория_Показана_Сразу_После_Запуска()
    {
        // Решение D-070: игрок не должен ничего нажимать, чтобы увидеть состояние доски.
        var model = Create();

        Assert.True(model.HasTerritory);
    }

    [Fact]
    public void Территория_Скрывается_Выключенным_Переключателем()
    {
        // Переключатель остался выключателем разметки: выключили — на доске её нет.
        var model = Create();
        model.ShowTerritory = false;

        Assert.False(model.HasTerritory);
    }

    [Fact]
    public void Разметка_Покрывает_Всю_Доску()
    {
        var model = Create();

        Assert.Equal(Size.Area, model.Territory!.Count);
    }

    [Fact]
    public void Выключенная_Разметка_Не_Отдаёт_Владение()
    {
        var model = Create();
        model.ShowTerritory = false;

        Assert.Null(model.Territory);
    }

    [Fact]
    public void Территория_Показывается_Сама_После_Двух_Пасов()
    {
        var model = Create();
        _ = model.LoadGame(new SgfGame(Size, Komi.For9x9, [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)], null));

        Assert.NotEqual("Идёт", model.Status);
        Assert.True(model.HasTerritory);
        Assert.NotNull(model.Territory);
    }

    [Fact]
    public void Разметка_Отличает_Территорию_От_Нейтральных_Точек()
    {
        // Чёрные замкнули угол: точка (0,0) окружена только их камнями, остальная доска спорная.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        var territory = model.Territory!;

        Assert.Equal(StoneColor.Black, territory[0]);
        Assert.Equal(StoneColor.Empty, territory[(4 * Size.Value) + 4]);

        // Три камня чёрных и замкнутый угол — четыре точки; два белых камня — две.
        // Разметка включена, значит оценка запрошена и перебор идёт, но в этой позиции он
        // ничего не доказывает: у обеих групп по три дамэ и больше.
        Assert.Equal(4, territory.Count(owner => owner == StoneColor.Black));
        Assert.Equal(2, territory.Count(owner => owner == StoneColor.White));
        Assert.Empty(model.DeadPoints);
    }

    [Fact]
    public void Во_Время_Партии_Разбор_Называет_Камни_Территорию_И_Нейтральные_Точки()
    {
        // Разбор считается по реальной доске партии: три камня чёрных плюс точка замкнутого угла,
        // два белых камня без территории, остальные 75 точек нейтральны.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        Assert.Contains("Территория — чёрные 1 · белые 0 · нейтрально 75", model.ScoreBreakdown, StringComparison.Ordinal);
        Assert.Contains("Камни — чёрные 3 · белые 2", model.ScoreBreakdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Не_Прячется_За_Режимом_Согласования()
    {
        // Во время партии разбор показывает переключатель «Территория», после двух пасов разметка
        // включается сама: строка разбора не привязана к одному режиму (жалоба 2026-10-02).
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        Assert.NotEmpty(model.ScoreBreakdown);

        model.ShowTerritory = false;
        _ = model.LoadGame(new SgfGame(Size, Komi.For9x9, [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)], null));

        Assert.True(model.HasTerritory);
        Assert.NotEmpty(model.ScoreBreakdown);
    }

    [Fact]
    public void Расшифровка_Считает_Площадь_Сторон_И_Нейтральные_Точки()
    {
        var model = Create(StoneColor.White);
        _ = model.LoadGame(CornerGame());
        model.ShowTerritory = true;

        // Три чёрных камня и замкнутый угол — 4; два белых камня — 2; остальное нейтрально.
        // Коми в расшифровке площади нет: оно стоит в строке счёта, а площадь его не включает.
        // Территория и камни названы отдельными строками: без этого разбор не сходился со счётом.
        Assert.Contains("Территория — чёрные 1 · белые 0", model.ScoreBreakdown, StringComparison.Ordinal);
        Assert.Contains("Камни — чёрные 3 · белые 2", model.ScoreBreakdown, StringComparison.Ordinal);
        Assert.Contains("нейтрально 75", model.ScoreBreakdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Переключатель_Территории_Доступен_В_Идущей_И_В_Завершённой_Партии()
    {
        // Жалоба пользователя 2026-10-02: «по кнопке Территория я должен увидеть территорию…
        // в режиме игры». Пока партия идёт, кнопка — единственный способ запросить оценку;
        // после конца партии разметка включается сама, но выключить её тоже должно быть чем.
        var model = Create();
        _ = model.LoadGame(CornerGame());

        var inGame = model.CanToggleTerritory;

        _ = model.LoadGame(new SgfGame(Size, Komi.For9x9, [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)], null));

        Assert.True(inGame && model.CanToggleTerritory);
    }

    [Fact]
    public void Переключатель_Территории_Скрыт_До_Первого_Хода()
    {
        // На пустой доске территории не бывает: все точки нейтральны, и запрашивать нечего.
        var model = Create();

        Assert.False(model.CanToggleTerritory);
    }

    [Fact]
    public void Разметка_В_Идущей_Партии_Помечает_Мёртвую_Группу()
    {
        // Оценка запрошена переключателем — программу просят определить мёртвых, и она это делает
        // до конца партии: угловая белая группа помечена, точка стала территорией чёрных.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadCornerGame());

        model.ShowTerritory = true;

        Assert.Contains(new Point(0, 0), model.DeadPoints);
    }

    [Fact]
    public void В_Идущей_Партии_Пометки_Есть_Без_Нажатий()
    {
        // Разметка включена по умолчанию, и мёртвых определяет программа (D-070).
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadCornerGame());

        Assert.NotEmpty(model.DeadPoints);
    }

    [Fact]
    public void В_Идущей_Партии_Перебор_Считается_Один_Раз()
    {
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadCornerGame());

        _ = model.Score;
        _ = model.ScoreDetail;
        _ = model.Territory;

        Assert.Equal(1, model.DeadProposalCount);
    }

    [Fact]
    public void В_Идущей_Партии_С_Оценкой_Камни_Совпадают_С_Доской_Без_Мёртвых()
    {
        // Регрессия проверки 2026-10-02: панель показывала на один белый камень больше, чем
        // остаётся после снятия помеченной группы. Камни каждого цвета сверяются с доской,
        // собранной независимо — по позиции партии и пометкам, а не по тексту панели.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadInPlayGame());
        model.ShowTerritory = true;

        var cleared = Endgame.ClearedBoard(model.Board, model.DeadPoints);
        var panel = ReadPanelBreakdown(model.ScoreBreakdown);

        Assert.Equal(
            (cleared.OccupiedPoints(StoneColor.Black).Count(), cleared.OccupiedPoints(StoneColor.White).Count()),
            (panel.BlackStones, panel.WhiteStones));
    }

    [Fact]
    public void В_Идущей_Партии_С_Оценкой_Сумма_Чисел_Равна_Площади_Доски()
    {
        // Сумма «территория + камни + нейтрально» обязана сходиться с площадью доски в любой
        // позиции: иначе одно из чисел панели посчитано по другой доске.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadInPlayGame());
        model.ShowTerritory = true;

        var panel = ReadPanelBreakdown(model.ScoreBreakdown);

        Assert.Equal(model.Board.Size.Area, panel.Total);
    }

    [Fact]
    public void В_Идущей_Партии_С_Оценкой_Пленные_Совпадают_С_Пометками()
    {
        // Пленные — это снятые по ходам плюс помеченные мёртвыми: панель и пометки обязаны
        // говорить одно и то же число.
        var model = Create(StoneColor.White);
        _ = model.LoadGame(DeadInPlayGame());
        model.ShowTerritory = true;

        var deadWhite = model.DeadPoints.Count(point => model.Board.At(point) == StoneColor.White);

        Assert.Contains($"мертвыми {deadWhite} : 0", model.PrisonersLine, StringComparison.Ordinal);
    }

    /// <summary>Строит идущую партию, где перебор помечает группу из двух белых камней.</summary>
    /// <returns>Партия без пасов: белые (0,8) и (1,8) в углу заперты чёрными.</returns>
    /// <remarks>
    /// Позиция проверки 2026-10-02 (<c>dead-in-play.sgf</c>): ходы заканчиваются ходом чёрных,
    /// поэтому белые — игрок: соперник после загрузки не ходит и позиция остаётся той, что в файле.
    /// </remarks>
    private static SgfGame DeadInPlayGame() =>
        new(
            Size,
            Komi.For9x9,
            [
                Move.Play(new Point(4, 4), StoneColor.Black),
                Move.Play(new Point(8, 0), StoneColor.White),
                Move.Play(new Point(0, 6), StoneColor.Black),
                Move.Play(new Point(8, 1), StoneColor.White),
                Move.Play(new Point(1, 6), StoneColor.Black),
                Move.Play(new Point(0, 8), StoneColor.White),
                Move.Play(new Point(2, 7), StoneColor.Black),
                Move.Play(new Point(1, 8), StoneColor.White),
                Move.Play(new Point(2, 8), StoneColor.Black)
            ],
            null);

    /// <summary>Разбирает числа разбора площади из строки панели.</summary>
    /// <param name="breakdown">Строка <c>ScoreBreakdown</c>: территория и камни по сторонам.</param>
    /// <returns>Числа панели: территория, нейтраль и камни обоих цветов.</returns>
    /// <remarks>
    /// Разбор строки нужен намеренно: проверяется именно то, что показывает панель, а не то,
    /// что считает ядро. Числа сверяются с независимо собранной доской.
    /// </remarks>
    private static PanelBreakdown ReadPanelBreakdown(string breakdown)
    {
        var territory = Regex.Match(
            breakdown,
            @"Территория — чёрные (\d+) · белые (\d+) · нейтрально (\d+)");
        var stones = Regex.Match(breakdown, @"Камни — чёрные (\d+) · белые (\d+)");

        Assert.True(territory.Success && stones.Success, $"Разбор площади не разобран: «{breakdown}»");

        return new PanelBreakdown(
            int.Parse(territory.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(territory.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(territory.Groups[3].Value, CultureInfo.InvariantCulture),
            int.Parse(stones.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(stones.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>Числа разбора площади, показанные панелью партии.</summary>
    /// <param name="BlackTerritory">Территория чёрных.</param>
    /// <param name="WhiteTerritory">Территория белых.</param>
    /// <param name="Neutral">Нейтральные точки.</param>
    /// <param name="BlackStones">Чёрные камни на доске подсчёта.</param>
    /// <param name="WhiteStones">Белые камни на доске подсчёта.</param>
    private readonly record struct PanelBreakdown(
        int BlackTerritory,
        int WhiteTerritory,
        int Neutral,
        int BlackStones,
        int WhiteStones)
    {
        /// <summary>Все точки доски: территория обоих цветов, камни и нейтраль.</summary>
        public int Total => BlackTerritory + WhiteTerritory + Neutral + BlackStones + WhiteStones;
    }

    /// <summary>Идущая партия с доказанно мёртвой белой группой в углу.</summary>
    /// <returns>Прочитанная партия без двух пасов: статус «идёт», оценка ещё не запрошена.</returns>
    /// <remarks>
    /// Ходы те же, что в тестах конца партии, но без пасов: позиция та же, а партия идёт — так
    /// проверяется, что мёртвых находит программа, а не завершение партии.
    /// </remarks>
    private static SgfGame DeadCornerGame() =>
        new(
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

    /// <summary>Партия с замкнутым углом чёрных и двумя белыми камнями в открытой позиции.</summary>
    /// <returns>Партия, где ход белых: после загрузки AI не ходит.</returns>
    private static SgfGame CornerGame() =>
        new(
            Size,
            Komi.For9x9,
            [
                Move.Play(new Point(1, 0), StoneColor.Black),
                Move.Play(new Point(8, 8), StoneColor.White),
                Move.Play(new Point(0, 1), StoneColor.Black),
                Move.Play(new Point(7, 8), StoneColor.White),
                Move.Play(new Point(1, 1), StoneColor.Black)
            ],
            null);

    /// <summary>Создаёт модель представления со случайным соперником: партия в тесте идёт быстро.</summary>
    /// <param name="color">Цвет игрока.</param>
    /// <returns>Модель представления партии 9×9; построение — общее для тестов.</returns>
    private static MainViewModel Create(StoneColor? color = null) =>
        TestViewModel.Create(color, DifficultyLevel.Kyu30, size: Size);
}
