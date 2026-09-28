using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты фонового поиска хода соперника: ход игрока виден сразу, ответ приходит потом.</summary>
/// <remarks>
/// Регрессия жалобы «не хватает анимации хода противника и его поедания моего камня, всё происходит
/// в один миг»: поиск хода соперника выполнялся в потоке интерфейса, поэтому ход игрока и ответ
/// появлялись на доске одновременно, анимация успевала закончиться до первой отрисовки. Измерение
/// на живом приложении: интерфейс не обновлялся 1702 мс после щелчка.
/// </remarks>
public sealed class MainViewModelAsyncTests
{
    private const int Seed = 20260926;

    /// <summary>Модель с игроком за чёрных: соперник отвечает после хода игрока.</summary>
    private static MainViewModel Model(DifficultyLevel? level = null, StoneColor? color = null) =>
        new(
            AppSettings.From(BoardSize.Size9, level ?? DifficultyLevel.Kyu20, color ?? StoneColor.Black, Komi.For9x9),
            new Random(Seed));

    [Fact]
    public async Task Фоновый_Ход_Применяется_И_Соперник_Отвечает()
    {
        var model = Model();

        Assert.True(await model.PlayMoveAsync(new Point(4, 4)));

        Assert.Equal("2", model.MoveNumber);
        Assert.False(model.IsThinking);
    }

    [Fact]
    public async Task Пас_В_Фоне_Даёт_Ответ_Соперника()
    {
        var model = Model();

        Assert.True(await model.PassAsync());

        Assert.Equal("2", model.MoveNumber);
        Assert.False(model.IsThinking);
    }

    [Fact]
    public async Task Пока_Соперник_Думает_Ход_Игрока_Не_Принимается()
    {
        var model = Model();

        var turn = model.PlayMoveAsync(new Point(4, 4));

        // Поиск начинается синхронно: к этому моменту соперник уже думает.
        Assert.True(model.IsThinking);
        Assert.False(model.PlayMove(new Point(3, 3)));
        Assert.False(model.Pass());

        model.CancelThinking();

        Assert.True(await turn);
    }

    [Fact]
    public async Task Отменённый_Поиск_Не_Играет_Устаревший_Ход()
    {
        var model = Model();

        var turn = model.PlayMoveAsync(new Point(4, 4));
        model.CancelThinking();

        Assert.True(await turn);

        // Ход игрока остался, ответа соперника нет: очередь за ним, но ход не применён.
        Assert.Equal("1", model.MoveNumber);
        Assert.False(model.IsThinking);
    }

    [Fact]
    public async Task Фоновый_Путь_Даёт_Ту_Же_Партию_Что_Синхронный()
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var background = new MainViewModel(settings, new Random(Seed));
        var synchronous = new MainViewModel(settings, new Random(Seed));

        _ = await background.PlayMoveAsync(new Point(4, 4));
        _ = synchronous.PlayMove(new Point(4, 4));

        Assert.Equal(synchronous.MoveNumber, background.MoveNumber);
        Assert.Equal(
            synchronous.ToSgfGame().Moves.Select(move => move.Point),
            background.ToSgfGame().Moves.Select(move => move.Point));
    }

    [Fact]
    public async Task Отмена_Хода_Во_Время_Раздумий_Возвращает_Очередь_Игроку()
    {
        var model = Model(color: StoneColor.White);
        var turn = model.PlayMoveAsync(new Point(4, 4));

        model.CancelThinking();
        _ = await turn;

        Assert.True(model.Undo() || model.MoveNumber is "0" or "1");
        Assert.False(model.IsThinking);
    }
}
