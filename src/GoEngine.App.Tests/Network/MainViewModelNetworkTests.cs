using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Multiplayer;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.App.Tests;

/// <summary>Тесты сетевого режима модели партии: таймер 90 секунд, авто-пас и пауза при обрыве.</summary>
public sealed class MainViewModelNetworkTests
{
    private static readonly CancellationToken Cancel = CancellationToken.None;

    [Fact]
    public async Task Хозяин_Авто_Пас_По_Таймауту()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        var started = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        Assert.True(started.IsSuccess, started.Error);

        var next = host.NextMoveAsync();
        time.Advance(TimeSpan.FromSeconds(90));
        var sent = await next.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(sent.AutoPass);
        Assert.Equal(1, sent.MoveNumber);
        Assert.Equal("1", model.MoveNumber);
        Assert.Equal(StoneColorLabels.Label(StoneColor.White), model.ToMove);
        Assert.Equal("Идёт", model.Status);
    }

    [Fact]
    public async Task Два_Авто_Паса_Не_Завершают_Партию()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        var first = host.NextMoveAsync();
        time.Advance(TimeSpan.FromSeconds(90));
        _ = await first.WaitAsync(TimeSpan.FromSeconds(2));

        var second = host.NextMoveAsync();
        time.Advance(TimeSpan.FromSeconds(90));
        _ = await second.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("2", model.MoveNumber);
        Assert.Equal("Идёт", model.Status);
        Assert.Equal(StoneColorLabels.Label(StoneColor.Black), model.ToMove);
    }

    [Fact]
    public async Task Три_Авто_Паса_Прерывают_Зависшую_Партию()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time, stallLimit: 3));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        for (var index = 0; index < 3; index++)
        {
            var next = host.NextMoveAsync();
            time.Advance(TimeSpan.FromSeconds(90));
            _ = await next.WaitAsync(TimeSpan.FromSeconds(2));
        }

        Assert.True(model.IsNetworkGame);
        Assert.True(host.Stopped);
        Assert.Equal(LanErrorCodes.Stalled, host.LastError?.Code);
        Assert.Contains("оба игрока", model.NetworkStatusText, StringComparison.Ordinal);

        await model.LeaveNetworkGameAsync();
        Assert.False(model.IsNetworkGame);
    }

    [Fact]
    public async Task Хозяин_Применяет_Ход_Гостя()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        var local = host.NextMoveAsync();
        Assert.True(await model.PlayMoveAsync(new Point(4, 4)));
        _ = await local.WaitAsync(TimeSpan.FromSeconds(2));

        var guestMove = host.NextMoveAsync();
        host.RaiseGuestMove(new GuestMoveRequestEventArgs(2, Move.Play(new Point(5, 5), StoneColor.White)));
        var applied = await guestMove.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, applied.MoveNumber);
        Assert.False(applied.AutoPass);
        Assert.Equal(StoneColor.White, model.Board.At(new Point(5, 5)));
        Assert.Equal("2", model.MoveNumber);
    }

    [Fact]
    public void Гость_Применяет_Подтверждённый_Ход()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        var started = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));
        Assert.True(started.IsSuccess, started.Error);

        client.RaiseMoveApplied(new MoveAppliedEventArgs(1, Move.Play(new Point(4, 4), StoneColor.Black), autoPass: false, remainingMs: 90_000));

        Assert.Equal("1", model.MoveNumber);
        Assert.Equal(StoneColor.Black, model.Board.At(new Point(4, 4)));
        Assert.Equal(StoneColorLabels.Label(StoneColor.White), model.ToMove);
    }

    [Fact]
    public async Task Обрыв_Ставит_Таймер_На_Паузу_А_Возврат_Досылает_Состояние()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time, reconnect: TimeSpan.FromMinutes(10)));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        host.RaiseDisconnected();
        time.Advance(TimeSpan.FromSeconds(90));

        Assert.Empty(host.SentMoves);
        Assert.True(model.IsNetworkGame);

        host.RaiseReconnected();

        Assert.True(host.StateSyncCount > 0);
        Assert.NotEmpty(host.TurnStates);
    }

    [Fact]
    public async Task Гость_Не_Применяет_Свой_Ход_До_Подтверждения()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        var accepted = await model.PlayMoveAsync(new Point(4, 4));

        Assert.True(accepted);
        Assert.Single(client.Requests);
        Assert.Equal(StoneColor.Empty, model.Board.At(new Point(4, 4)));
        Assert.Equal("0", model.MoveNumber);
    }

    [Fact]
    public async Task Гость_Может_Сходить_После_Отклонённого_Хода()
    {
        // Регрессия живого прогона: на нелегальный ход хозяин молчал, и гость навсегда
        // оставался с «Ждём подтверждения хода…».
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        Assert.True(await model.PlayMoveAsync(new Point(4, 4)));
        client.RaiseFailed(new LanErrorEventArgs(LanErrorCodes.MoveRejected, "Ход не по правилам."));

        Assert.True(model.IsNetworkGame);
        Assert.True(model.CanPlayMove);
        Assert.True(await model.PlayMoveAsync(new Point(5, 5)));
        Assert.Equal(2, client.Requests.Count);
    }

    [Fact]
    public void Гость_Восстанавливает_Состояние_После_Переподключения()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        client.RaiseStateSync(new StateSyncEventArgs(
        [
            new AppliedMove(1, Move.Play(new Point(4, 4), StoneColor.Black), autoPass: false),
            new AppliedMove(2, Move.Play(new Point(5, 5), StoneColor.White), autoPass: false)
        ], 60_000, paused: false));

        Assert.Equal("2", model.MoveNumber);
        Assert.Equal(StoneColor.Black, model.Board.At(new Point(4, 4)));
        Assert.Equal(StoneColor.White, model.Board.At(new Point(5, 5)));
    }

    [Fact]
    public void Гость_Ставит_Таймер_На_Паузу_При_Обрыве()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        client.RaiseDisconnected();

        Assert.Contains("Связь потеряна", model.NetworkStatusText, StringComparison.Ordinal);
    }

    private static MainViewModel CreateModel(FakeTimeProvider time, MultiplayerOptions options)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        return new MainViewModel(AppSettings.Default, new Random(20261109), time: time, networkOptions: options);
    }

    private static MultiplayerOptions Options(
        FakeTimeProvider time,
        int stallLimit = 3,
        TimeSpan? reconnect = null) => new()
        {
            TimeProvider = time,
            MoveTimeout = TimeSpan.FromSeconds(90),
            ReconnectTimeout = reconnect ?? TimeSpan.FromSeconds(60),
            AutoPassStallLimit = stallLimit
        };
}