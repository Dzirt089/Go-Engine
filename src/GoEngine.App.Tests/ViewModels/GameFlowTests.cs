using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты хода партии: старт, пауза, продолжение, отмена и время партии.</summary>
/// <remarks>
/// Регрессия жалобы 2026-10-02: «Начать партию» обязана начинать игру и отсчёт времени партии,
/// «Пауза» и «Отменить партию» — работать, а пауза — запрещать ходы по-настоящему, а не только
/// на вид. Время берётся у поддельных часов (<see cref="FakeTime"/>): проверки не ждут секунд.
/// </remarks>
public sealed class GameFlowTests
{
    /// <summary>Зерно проверок: партии повторяются от запуска к запуску (AGENTS.md, п. 9).</summary>
    private const int Seed = 20260926;

    [Fact]
    public void Новая_Модель_Не_Начинает_Партию()
    {
        // Свежезапущенное приложение открывается на неначатой партии: время стоит,
        // останавливать и отменять нечего.
        var model = Model();

        Assert.False(model.IsGameStarted);
        Assert.False(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.Equal("00:00", model.ClockDisplay);
        Assert.False(model.CanPauseClock);
        Assert.False(model.CanResumeClock);
        Assert.False(model.CanAbortGame);
    }

    [Fact]
    public void Старт_Партии_Запускает_Часы()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        model.StartGame();
        time.Advance(TimeSpan.FromSeconds(75));

        Assert.True(model.IsGameStarted);
        Assert.True(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.True(model.CanPauseClock);
        Assert.True(model.CanAbortGame);
        Assert.Equal("01:15", model.ClockDisplay);
    }

    [Fact]
    public void Пауза_Останавливает_Время_И_Отклоняет_Ходы()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        model.StartGame();
        time.Advance(TimeSpan.FromSeconds(20));

        var board = model.Board;

        model.PauseGame();
        time.Advance(TimeSpan.FromSeconds(40));

        Assert.True(model.IsGamePaused);
        Assert.False(model.IsGameRunning);
        Assert.True(model.CanResumeClock);
        Assert.False(model.CanPauseClock);
        Assert.False(model.CanPlayMove);
        Assert.Equal("00:20", model.ClockDisplay);

        // Пауза настоящая: ход и пас отклоняются, а позиция остаётся прежней.
        Assert.False(model.PlayMove(new Point(4, 4)));
        Assert.False(model.Pass());
        Assert.Same(board, model.Board);
        Assert.Equal("0", model.MoveNumber);
    }

    [Fact]
    public void Продолжение_Возвращает_Ходы_И_Продолжает_Время()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        model.StartGame();
        time.Advance(TimeSpan.FromSeconds(20));
        model.PauseGame();
        time.Advance(TimeSpan.FromSeconds(40));

        model.ResumeGame();
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.True(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.True(model.CanPlayMove);
        Assert.Equal("00:30", model.ClockDisplay);
        Assert.True(model.PlayMove(new Point(4, 4)));
    }

    [Fact]
    public void Отмена_Партии_Возвращает_Состояние_До_Старта()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        model.StartGame();
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(model.PlayMove(new Point(4, 4)));

        model.AbortGame();

        Assert.False(model.IsGameStarted);
        Assert.False(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.Equal("00:00", model.ClockDisplay);
        Assert.Equal(TimeSpan.Zero, model.GameTime);
        Assert.False(model.CanAbortGame);
        Assert.False(model.CanPauseClock);

        // Ходы очищены, доска пуста, кнопка снова «Начать партию».
        Assert.Equal("0", model.MoveNumber);
        Assert.Equal(BoardSize.Size9.Value * BoardSize.Size9.Value, model.Board.EmptyPoints().Count());
        Assert.True(model.IsFirstMove);
    }

    [Fact]
    public void Отмена_Партии_За_Белых_Возвращает_Ход_Сопернику()
    {
        // Играя белыми, игрок первым не ходит: после отмены партии соперник снова открывает
        // партию — ровно так же, как при новой партии, но время при этом не идёт.
        var time = new FakeTime();
        var model = Model(color: StoneColor.White, time: time);

        model.StartGame();
        time.Advance(TimeSpan.FromMinutes(2));

        model.AbortGame();

        Assert.False(model.IsGameStarted);
        Assert.Equal("00:00", model.ClockDisplay);
        Assert.Equal("1", model.MoveNumber);
        Assert.Equal("Белые", model.ToMove);
    }

    [Fact]
    public void Смена_Доски_Начинает_Партию_С_Идущими_Часами()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        model.SelectedSizeIndex = 2;

        Assert.Equal(19, model.Board.Size.Value);
        Assert.True(model.IsGameStarted);
        Assert.True(model.IsGameRunning);
        Assert.Equal("00:00", model.ClockDisplay);
    }

    [Fact]
    public void Первый_Ход_Начинает_Партию()
    {
        // Игрок вправе начать играть, не нажимая «Начать партию»: время считается с первого хода.
        var time = new FakeTime();
        var model = Model(time: time);

        Assert.True(model.PlayMove(new Point(4, 4)));

        Assert.True(model.IsGameStarted);
        Assert.True(model.IsGameRunning);
        Assert.True(model.CanAbortGame);
    }

    [Fact]
    public void Смена_Настроек_Начинает_Партию_С_Идущими_Часами()
    {
        // Смена доски, уровня и настроек означает игру: часы идут с этого мгновения.
        var time = new FakeTime();
        var model = Model(time: time);

        model.ApplySettings(AppSettings.From(BoardSize.Size13, DifficultyLevel.Kyu30, StoneColor.Black, Komi.For13x13));

        Assert.True(model.IsGameStarted);
        Assert.True(model.IsGameRunning);
        Assert.Equal("00:00", model.ClockDisplay);
    }

    [Fact]
    public void Загрузка_Партии_Начинает_Партию_С_Идущими_Часами()
    {
        var time = new FakeTime();
        var model = Model(time: time);

        var loaded = model.LoadGame(new SgfGame(BoardSize.Size9, Komi.For9x9, [Move.Play(new Point(4, 4), StoneColor.Black)], null));

        Assert.True(loaded.IsSuccess);
        Assert.True(model.IsGameStarted);
        Assert.True(model.IsGameRunning);
    }

    [Fact]
    public void Загрузка_Завершённой_Партии_Не_Считает_Время()
    {
        // Партия из файла уже окончена: время в ней считать нечего, но и паузой это не называется.
        var time = new FakeTime();
        var model = Model(time: time);

        var loaded = model.LoadGame(new SgfGame(
            BoardSize.Size9,
            Komi.For9x9,
            [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)],
            null));

        Assert.True(loaded.IsSuccess);
        Assert.True(model.IsGameStarted);
        Assert.False(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.False(model.CanResumeClock);
        Assert.True(model.CanAbortGame);
    }

    [Fact]
    public async Task Пауза_Во_Время_Раздумий_Не_Даёт_Сопернику_Сходить()
    {
        var model = Model();

        await PauseWhileThinking(model);

        Assert.True(model.IsGamePaused);
        Assert.Equal("1", model.MoveNumber);
        Assert.False(model.IsThinking);
    }

    [Fact]
    public async Task Продолжение_После_Паузы_Даёт_Сопернику_Ответить()
    {
        var model = Model();

        await PauseWhileThinking(model);
        await model.ResumeGameAsync();

        Assert.True(model.IsGameRunning);
        Assert.False(model.IsGamePaused);
        Assert.Equal("2", model.MoveNumber);
        Assert.False(model.IsThinking);
    }

    /// <summary>Ходит игроком и сразу ставит партию на паузу: соперник остаётся в раздумьях.</summary>
    /// <param name="model">Модель партии.</param>
    /// <returns>Задача, завершающаяся после отмены поиска хода соперника.</returns>
    private static async Task PauseWhileThinking(MainViewModel model)
    {
        var turn = model.PlayMoveAsync(new Point(4, 4));

        // Поиск начинается синхронно: к этому мгновению соперник уже думает.
        Assert.True(model.IsThinking);

        model.PauseGame();

        _ = await turn;
    }

    /// <summary>Создаёт модель партии 9×9 со слабым соперником и поддельными часами.</summary>
    /// <param name="color">Цвет игрока; по умолчанию чёрные.</param>
    /// <param name="time">Источник времени; <c>null</c> — новые поддельные часы.</param>
    /// <returns>Модель представления партии.</returns>
    private static MainViewModel Model(StoneColor? color = null, FakeTime? time = null) =>
        new(
            AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu30, color ?? StoneColor.Black, Komi.For9x9),
            new Random(Seed),
            time: time ?? new FakeTime());
}
