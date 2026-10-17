using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Конец партии по согласию: партия закрывается, счёт считается, итог показывается.</summary>
/// <remarks>
/// Блокер B1 отчёта QA 2026-10-17: игрок пасовал три раза подряд, а партия продолжалась, потому
/// что правило двух пасов требует паса от обеих сторон, а соперник-ИИ пасовать не обязан.
/// «Закончить партию» — второй, явный путь к итогу; он не делает хода и не меняет позицию.
/// </remarks>
public sealed class FinishGameTests
{
    [Fact]
    public void До_Первого_Хода_Партию_Закончить_Нельзя()
    {
        // Свежезапущенное приложение: партия не начата, и итог по пустой доске был бы не итогом.
        var model = TestViewModel.Create();

        Assert.False(model.CanFinishGame);
        Assert.False(model.FinishGame());
        Assert.False(model.HasOutcome);
    }

    [Fact]
    public void Партия_Заканчивается_И_Показывает_Итог()
    {
        var model = TestViewModel.Create();
        model.StartGame();
        Assert.True(model.PlayMove(new Point(4, 4)));

        Assert.True(model.FinishGame());

        Assert.Equal("Завершена по согласию сторон", model.Status);
        Assert.True(model.HasOutcome);
        Assert.False(string.IsNullOrWhiteSpace(model.Outcome));
        Assert.NotEmpty(model.Score);
    }

    [Fact]
    public void Пас_Не_Мешает_Закончить_Партию()
    {
        // Пас остаётся законным ходом: игрок вправе пасовать, но и после паса партию можно
        // закрыть. Раньше именно этот сценарий «висел» — ИИ отвечал новым камнем.
        var model = TestViewModel.Create();
        model.StartGame();

        Assert.True(model.Pass());
        Assert.True(model.CanFinishGame);
        Assert.True(model.FinishGame());
        Assert.True(model.HasOutcome);
    }

    [Fact]
    public void После_Конца_Партии_Пас_И_Конец_Недоступны()
    {
        var model = TestViewModel.Create();
        model.StartGame();
        _ = model.PlayMove(new Point(4, 4));

        _ = model.FinishGame();

        Assert.False(model.CanFinishGame);
        Assert.False(model.CanPass);
        Assert.False(model.FinishGame());
    }

    [Fact]
    public void Конец_Партии_Не_Делает_Ход_И_Останавливает_Часы()
    {
        var model = TestViewModel.Create();
        model.StartGame();
        _ = model.PlayMove(new Point(4, 4));
        var board = model.Board;
        var moveNumber = model.MoveNumber;

        _ = model.FinishGame();

        Assert.Same(board, model.Board);
        Assert.Equal(moveNumber, model.MoveNumber);
        Assert.False(model.IsGameRunning);

        // Ходов в оконченной партии нет: щелчок по доске ничего не ставит.
        Assert.False(model.PlayMove(new Point(5, 5)));
    }
}
