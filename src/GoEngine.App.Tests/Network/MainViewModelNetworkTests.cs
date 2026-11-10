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
    public void Нельзя_Загрузить_SGF_Во_Время_Сетевой_Партии()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        var loaded = model.LoadGame(new SgfGame(
            BoardSize.Size9,
            Komi.For9x9,
            [Move.Play(new Point(4, 4), StoneColor.Black)],
            null));

        Assert.False(loaded.IsSuccess);
        Assert.Equal("0", model.MoveNumber);
        Assert.True(model.IsNetworkGame);
    }

    [Fact]
    public async Task Выход_Из_Сети_Сбрасывает_Доску_И_Часы()
    {
        // Регрессия реального Android: после «Выйти из сетевой игры» на доске оставалась
        // сетевая позиция, а шапка и часы выглядели как идущая партия с ИИ.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.White, 1, 90_000));

        await model.LeaveNetworkGameAsync();

        Assert.False(model.IsNetworkGame);
        Assert.Equal("0", model.MoveNumber);
        Assert.False(model.IsGameStarted);
        Assert.Equal("00:00", model.ClockShortDisplay);
        Assert.Equal(StoneColor.Empty, model.Board.At(new Point(4, 4)));
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

    [Fact]
    public async Task Смена_Хода_Показывает_Твой_Ход()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();

        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        Assert.True(model.IsNetworkNoticeVisible);
        Assert.Contains("Твой ход", model.NetworkNoticeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ход_Соперника_Называется_Думает()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();

        // Хозяин играет белыми: ход чёрных — соперника.
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.White, 1, Cancel);

        Assert.Contains("думает", model.NetworkStatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("Ход Лисы", model.NetworkStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Подсказки_За_30_И_10_Секунд_Показываются()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        time.Advance(TimeSpan.FromSeconds(60));
        model.TickClock();

        Assert.True(model.IsNetworkTimerWarningVisible);
        Assert.Equal("Походи, а то будет пас", model.NetworkTimerWarningText);
        Assert.False(model.IsNetworkTimerUrgent);

        time.Advance(TimeSpan.FromSeconds(20));
        model.TickClock();

        Assert.True(model.IsNetworkTimerUrgent);
    }

    [Fact]
    public async Task Авто_Пас_Показывает_Время_Вышло()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var sent = host.NextMoveAsync();

        time.Advance(TimeSpan.FromSeconds(90));
        var move = await sent.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(move.AutoPass);
        Assert.True(model.IsNetworkNoticeVisible);
        Assert.Contains("Время вышло", model.NetworkNoticeText, StringComparison.Ordinal);
        Assert.Contains("пас", model.NetworkNoticeText, StringComparison.Ordinal);
    }

    [Fact]
    public void Гость_Видит_Сообщение_Об_Авто_Пасе_Соперника()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.White, 1, 90_000));

        // Белый гость: хозяин (чёрные) просрочил ход — вынужденный пас переводит очередь гостю.
        client.RaiseMoveApplied(new MoveAppliedEventArgs(
            1, Move.Pass(StoneColor.Black), autoPass: true, remainingMs: 90_000));

        Assert.True(model.IsNetworkNoticeVisible);
        Assert.Contains("Время вышло", model.NetworkNoticeText, StringComparison.Ordinal);
        Assert.Contains("Твой ход", model.NetworkNoticeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Обрыв_Показывает_Баннер_И_Обратный_Отсчёт()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        host.RaiseDisconnected();

        Assert.True(model.IsNetworkDisconnected);
        Assert.True(model.IsNetworkBannerVisible);
        Assert.Contains("Ждём возвращения", model.NetworkBannerText, StringComparison.Ordinal);
        var before = model.NetworkReconnectSecondsLeft;
        Assert.InRange(before, 55, 60);

        time.Advance(TimeSpan.FromSeconds(10));
        model.TickClock();

        Assert.InRange(model.NetworkReconnectSecondsLeft, 45, 50);
    }

    [Fact]
    public async Task Возврат_Соперника_Показывает_Сообщение()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        host.RaiseDisconnected();

        host.RaiseReconnected();

        Assert.False(model.IsNetworkDisconnected);
        Assert.False(model.IsNetworkBannerVisible);
        Assert.Contains("вернулся", model.NetworkNoticeText, StringComparison.Ordinal);
    }

    [Fact]
    public void Гость_Видит_Подсказку_О_Таймере_По_Остатку_Соперника()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.White, 1, 90_000));

        // Хозяин сходил, очередь у гостя; остаток ведёт хозяин и приходит в событии.
        client.RaiseMoveApplied(new MoveAppliedEventArgs(
            1, Move.Play(new Point(2, 2), StoneColor.Black), autoPass: false, remainingMs: 90_000));
        time.Advance(TimeSpan.FromSeconds(60));
        model.TickClock();

        Assert.True(model.IsNetworkTimerWarningVisible);
        Assert.Equal("Походи, а то будет пас", model.NetworkTimerWarningText);
    }

    [Fact]
    public async Task Терминальный_Обрыв_Хозяина_Показывает_Выход()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        host.RaiseFailed(new LanErrorEventArgs(LanErrorCodes.Stalled, "Партия прервана: оба игрока не делают ходы."));

        Assert.True(model.IsNetworkTerminal);
        Assert.Equal("Партия прервана", model.NetworkBannerTitle);
        Assert.Equal("В меню", model.NetworkBannerLeaveText);
    }

    [Fact]
    public void Терминальный_Обрыв_Показывает_Итог_И_Выход()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.Black, 1, 90_000));

        client.RaiseFailed(new LanErrorEventArgs(LanErrorCodes.ReconnectTimeout, "Не удалось вернуться в партию за отведённое время."));

        Assert.True(model.IsNetworkTerminal);
        Assert.True(model.IsNetworkBannerVisible);
        Assert.Equal("Партия прервана", model.NetworkBannerTitle);
        Assert.Contains("вернуться", model.NetworkBannerText, StringComparison.Ordinal);
        Assert.Equal("В меню", model.NetworkBannerLeaveText);
        Assert.False(model.IsNetworkBannerCanWait);
        Assert.Contains("вернуться", model.NetworkStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task В_Сетевой_Партии_Часы_ИИ_Не_Идут()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        Assert.False(model.IsGameRunning);
        Assert.Equal("00:00", model.ClockShortDisplay);

        time.Advance(TimeSpan.FromSeconds(5));
        model.TickClock();

        Assert.Equal("00:00", model.ClockShortDisplay);
    }

    [Fact]
    public async Task Терминал_Не_Перекрывает_Уже_Оконченную_Партию()
    {
        // B1 ревью P4: после обычного итога выход соперника не должен превращать итог
        // в «Партия прервана» и накрывать итоговую карточку оверлеем.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        Assert.True(await model.PassAsync());
        host.RaiseGuestMove(new GuestMoveRequestEventArgs(2, Move.Pass(StoneColor.White)));
        Assert.True(model.HasOutcome);

        host.RaiseGuestLeft();

        Assert.False(model.IsNetworkTerminal);
        Assert.False(model.IsNetworkBannerVisible);
        Assert.True(model.HasOutcome);
    }

    [Fact]
    public void Терминал_Не_Перекрывает_Итог_Гостя()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(
            client,
            new GuestSessionStarted("0123456789abcdef0123456789abcdef", 9, 5.5, StoneColor.White, 1, 90_000));

        // Два паса завершают партию: ход чёрных пришёл от хозяина, белый пас — свой.
        client.RaiseMoveApplied(new MoveAppliedEventArgs(1, Move.Pass(StoneColor.Black), autoPass: false, remainingMs: 90_000));
        client.RaiseMoveApplied(new MoveAppliedEventArgs(2, Move.Pass(StoneColor.White), autoPass: false, remainingMs: 90_000));
        Assert.True(model.HasOutcome);

        client.RaiseFailed(new LanErrorEventArgs(LanErrorCodes.HostLeft, "Хозяин вышел из игры."));

        Assert.False(model.IsNetworkTerminal);
        Assert.False(model.IsNetworkBannerVisible);
        Assert.True(model.HasOutcome);
    }

    [Fact]
    public async Task Протокольная_Ошибка_Даёт_Терминал_А_Не_Вечное_Ожидание()
    {
        // M1 ревью P4: любая сетевая ошибка, кроме отклонённого хода, — конец партии.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);

        host.RaiseFailed(new LanErrorEventArgs(LanErrorCodes.ProtocolError, "Сетевое сообщение повреждено."));

        Assert.True(model.IsNetworkTerminal);
        Assert.False(model.IsNetworkDisconnected);
        Assert.Equal("В меню", model.NetworkBannerLeaveText);
        Assert.False(model.IsNetworkBannerCanWait);
    }

    [Fact]
    public async Task Сетевое_Сообщение_Гаснет_Само()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        Assert.True(model.IsNetworkNoticeVisible);

        time.Advance(TimeSpan.FromSeconds(4));
        model.TickClock();

        Assert.False(model.IsNetworkNoticeVisible);
    }

    [Fact]
    public async Task Баннер_Обрыва_Остаться_Скрывает_До_Нового_Обрыва()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time, Options(time));
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        host.RaiseDisconnected();
        Assert.True(model.IsNetworkBannerVisible);

        model.DismissNetworkBanner();

        Assert.False(model.IsNetworkBannerVisible);
        Assert.True(model.IsNetworkDisconnected);
        time.Advance(TimeSpan.FromSeconds(5));
        model.TickClock();
        Assert.False(model.IsNetworkBannerVisible);
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