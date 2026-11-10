using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

[Collection("TLS-loopback")]

/// <summary>Loopback-тест сетевой партии: хозяин и гость обмениваются ходом.</summary>
public sealed class LanSessionLoopbackTests
{
    [TlsFact]
    public async Task Хозяин_И_Гость_Обмениваются_Ходом()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 42, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);
        var room = hostRoom.Value!;
        Assert.NotNull(room);

        var hostConnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var moveRequest = new TaskCompletionSource<GuestMoveRequestEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.GuestJoinRequested += (_, _) => _ = host.AcceptGuestAsync(StoneColor.White, 5.5, timeout.Token);
        host.GuestConnected += (_, _) => hostConnected.TrySetResult();
        host.GuestMoveRequested += (_, args) => moveRequest.TrySetResult(args);

        await using var guest = new LanGameClient(options);
        var started = await guest.JoinAsync(room, avatarId: 1, timeout.Token);
        Assert.True(started.IsSuccess, started.Error);
        await hostConnected.Task.WaitAsync(timeout.Token);

        var applied = new TaskCompletionSource<MoveAppliedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guestFailed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guestDisconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        guest.MoveApplied += (_, args) => applied.TrySetResult(args);
        guest.Failed += (_, args) => guestFailed.TrySetResult($"{args.Code}: {args.Message}");
        guest.Disconnected += (_, _) => guestDisconnected.TrySetResult();

        var requested = await guest.RequestMoveAsync(Move.Play(new Point(4, 4), StoneColor.White), moveNumber: 1, timeout.Token);
        Assert.True(requested.IsSuccess, requested.Error);
        var request = await moveRequest.Task.WaitAsync(timeout.Token);
        Assert.Equal(MoveType.Play, request.Move.Type);

        var sent = await host.SendMoveAppliedAsync(request.Move, autoPass: false, moveNumber: request.MoveNumber, remainingMs: 90_000, timeout.Token);
        Assert.True(sent.IsSuccess, sent.Error);
        var completed = await Task.WhenAny(applied.Task, guestFailed.Task, guestDisconnected.Task).WaitAsync(timeout.Token);
        if (completed != applied.Task)
        {
            var reason = guestFailed.Task.IsCompleted ? await guestFailed.Task : "соединение закрыто";
            Assert.Fail($"Гость не получил ход: {reason}");
        }

        var answer = await applied.Task;
        Assert.Equal(MoveType.Play, answer.Move.Type);
        Assert.Equal(90_000, answer.RemainingMs);
    }

    [TlsFact]
    public async Task Гость_Ждёт_Решения_Хозяина_Дольше_Рукопожатия()
    {
        // Регрессия живого прогона: гость обрывал ожидание через HandshakeTimeout в 15 секунд,
        // хотя хозяин ещё мог нажать «Пустить». Решение человека живёт отдельный таймаут.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var options = new MultiplayerOptions
        {
            PreferredTcpPort = 0,
            HandshakeTimeout = TimeSpan.FromSeconds(5),
            JoinDecisionTimeout = TimeSpan.FromSeconds(20)
        };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 43, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);

        var requested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.GuestJoinRequested += (_, _) => requested.TrySetResult(true);

        await using var guest = new LanGameClient(options);
        var join = guest.JoinAsync(hostRoom.Value!, avatarId: 1, timeout.Token);

        // Хозяин получил заявку, но думает дольше HandshakeTimeout: гость обязан ждать
        // решения человека (JoinDecisionTimeout), а не обрывать соединение.
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(15), timeout.Token);
        await Task.Delay(TimeSpan.FromSeconds(7), timeout.Token);

        var accepted = await host.AcceptGuestAsync(StoneColor.White, 5.5, timeout.Token);
        Assert.True(accepted.IsSuccess, accepted.Error);

        var started = await join;
        Assert.True(started.IsSuccess, started.Error);
    }

    [TlsFact]
    public async Task Заявка_Снимается_Когда_Гость_Закрыл_Соединение()
    {
        // R-007: хозяин не должен висеть на «хочет играть», если гость уже ушёл.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 44, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);

        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.GuestJoinRequested += (_, _) => requested.TrySetResult();
        host.GuestJoinCancelled += (_, _) => cancelled.TrySetResult();

        var guest = new LanGameClient(options);
        var join = guest.JoinAsync(hostRoom.Value!, avatarId: 1, timeout.Token);
        await requested.Task.WaitAsync(timeout.Token);

        await guest.DisposeAsync();

        var completed = await Task.WhenAny(cancelled.Task, Task.Delay(TimeSpan.FromSeconds(5), timeout.Token));
        Assert.True(completed == cancelled.Task, "хозяин не снял заявку после закрытия гостя");

        try
        {
            _ = await join;
        }
        catch (OperationCanceledException)
        {
            // Гость закрыл соединение в ожидании решения: отмена ожидаема.
        }
    }

    [TlsFact]
    public async Task Заявка_Истекает_Без_Ответа_Хозяина()
    {
        // R-007: ожидание решения хозяина ограничено JoinDecisionTimeout.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions
        {
            PreferredTcpPort = 0,
            JoinDecisionTimeout = TimeSpan.FromSeconds(2)
        };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 45, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);

        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.GuestJoinRequested += (_, _) => requested.TrySetResult();
        host.GuestJoinCancelled += (_, _) => cancelled.TrySetResult();

        await using var guest = new LanGameClient(options);
        var join = guest.JoinAsync(hostRoom.Value!, avatarId: 1, timeout.Token);
        await requested.Task.WaitAsync(timeout.Token);

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10), timeout.Token);
        _ = join;
    }

    [TlsFact]
    public async Task Resume_После_Конца_Партии_Не_Оживляет_Партию()
    {
        // R-007: после MarkFinished() resume отклоняется, партия не оживает.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 46, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);
        host.GuestJoinRequested += (_, _) => _ = host.AcceptGuestAsync(StoneColor.White, 5.5, timeout.Token);
        var room = hostRoom.Value!;

        string? gameId;
        string? token;
        var first = await LanTlsConnection.ConnectAsync(room.HostAddress, room.TcpPort, options.TimeProvider, timeout.Token);
        try
        {
            await first.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.Join,
                ProtocolVersion = options.ProtocolVersion,
                GameId = room.GameId,
                AvatarId = 1
            }, timeout.Token);
            var accepted = await first.ReceiveAsync(timeout.Token);
            Assert.True(accepted.Accepted);
            gameId = accepted.GameId;
            token = accepted.Token;
            host.MarkFinished();
        }
        finally
        {
            await first.DisposeAsync();
        }

        var resume = await LanTlsConnection.ConnectAsync(room.HostAddress, room.TcpPort, options.TimeProvider, timeout.Token);
        try
        {
            await resume.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.Resume,
                ProtocolVersion = options.ProtocolVersion,
                GameId = gameId,
                Token = token,
                AvatarId = 1
            }, timeout.Token);
            var rejected = await resume.ReceiveAsync(timeout.Token);

            Assert.False(rejected.Accepted);
            Assert.Equal("Партия уже окончена.", rejected.Message);
        }
        finally
        {
            await resume.DisposeAsync();
        }
    }

    [TlsFact]
    public async Task Нигири_Проходит_Через_Сеть_От_Предложения_До_Цветов()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 47, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);
        host.GuestJoinRequested += (_, _) => _ = host.AcceptGuestAsync(StoneColor.White, 5.5, timeout.Token);

        await using var guest = new LanGameClient(options);
        var started = await guest.JoinAsync(hostRoom.Value!, avatarId: 1, timeout.Token);
        Assert.True(started.IsSuccess, started.Error);

        var random = new Random(7);
        var count = ColorDraw.CreateCount(random);
        var nonce = ColorDraw.CreateNonce(random);
        var commitment = ColorDraw.Commitment(count, nonce);
        var offer = new TaskCompletionSource<ColorDrawOfferEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reveal = new TaskCompletionSource<ColorDrawRevealEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = new TaskCompletionSource<ColorDrawDoneEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var guess = new TaskCompletionSource<ColorDrawGuessEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var choice = new TaskCompletionSource<ColorDrawChoiceEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        guest.ColorDrawOfferReceived += (_, args) => offer.TrySetResult(args);
        guest.ColorDrawRevealReceived += (_, args) => reveal.TrySetResult(args);
        guest.ColorDrawDoneReceived += (_, args) => done.TrySetResult(args);
        host.ColorDrawGuessReceived += (_, args) => guess.TrySetResult(args);
        host.ColorDrawChoiceReceived += (_, args) => choice.TrySetResult(args);

        Assert.True((await host.SendColorDrawOfferAsync(commitment, 15_000, timeout.Token)).IsSuccess);
        var offerArgs = await offer.Task.WaitAsync(timeout.Token);
        Assert.Equal(commitment, offerArgs.Commitment);

        Assert.True((await guest.SendColorDrawGuessAsync(ColorDraw.GuessOdd, timeout.Token)).IsSuccess);
        var guessArgs = await guess.Task.WaitAsync(timeout.Token);
        Assert.Equal(ColorDraw.GuessOdd, guessArgs.Parity);

        var winnerIsGuest = ColorDraw.IsGuessCorrect(count, guessArgs.Parity);
        Assert.True((await host.SendColorDrawRevealAsync(count, nonce, winnerIsGuest, timeout.Token)).IsSuccess);
        var revealArgs = await reveal.Task.WaitAsync(timeout.Token);
        Assert.True(ColorDraw.Verify(revealArgs.Count, revealArgs.Nonce, commitment));
        Assert.Equal(winnerIsGuest, revealArgs.WinnerIsGuest);

        Assert.True((await guest.SendColorDrawChoiceAsync(StoneColor.White, timeout.Token)).IsSuccess);
        var choiceArgs = await choice.Task.WaitAsync(timeout.Token);
        Assert.Equal(StoneColor.White, choiceArgs.Color);

        Assert.True((await host.SendColorDrawDoneAsync(StoneColor.Black, StoneColor.White, timeout.Token)).IsSuccess);
        var doneArgs = await done.Task.WaitAsync(timeout.Token);
        Assert.Equal(StoneColor.Black, doneArgs.HostColor);
        Assert.Equal(StoneColor.White, doneArgs.GuestColor);
    }

    [TlsFact]
    public async Task Старый_Клиент_Получает_Понятную_Ошибку_Версии()
    {
        // Версия 2 добавила нигири: клиент версии 1 должен получить отказ с объяснением.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 48, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);
        var room = hostRoom.Value!;

        var connection = await LanTlsConnection.ConnectAsync(room.HostAddress, room.TcpPort, options.TimeProvider, timeout.Token);
        try
        {
            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.Join,
                ProtocolVersion = 1,
                GameId = room.GameId,
                AvatarId = 1
            }, timeout.Token);
            var rejected = await connection.ReceiveAsync(timeout.Token);

            Assert.False(rejected.Accepted);
            Assert.Equal(LanErrorCodes.ProtocolMismatch, rejected.Code);
            Assert.Contains("версия", rejected.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [TlsFact]
    public async Task Хозяин_Отклоняет_Некорректный_Выбор_Цвета()
    {
        // F2: choice «Empty» не должен приниматься хозяином-арбитром.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var options = new MultiplayerOptions { PreferredTcpPort = 0 };
        await using var host = new LanGameHost(options);
        var hostRoom = await host.StartHostingAsync(hostAvatarId: 0, roomNumber: 49, boardSize: 9, timeout.Token);
        Assert.True(hostRoom.IsSuccess, hostRoom.Error);
        host.GuestJoinRequested += (_, _) => _ = host.AcceptGuestAsync(StoneColor.White, 5.5, timeout.Token);
        var room = hostRoom.Value!;

        var failed = new TaskCompletionSource<LanErrorEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Failed += (_, args) => failed.TrySetResult(args);

        var connection = await LanTlsConnection.ConnectAsync(room.HostAddress, room.TcpPort, options.TimeProvider, timeout.Token);
        try
        {
            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.Join,
                ProtocolVersion = options.ProtocolVersion,
                GameId = room.GameId,
                AvatarId = 1
            }, timeout.Token);
            var accepted = await connection.ReceiveAsync(timeout.Token);
            Assert.True(accepted.Accepted);

            await connection.SendAsync(new LanMessage
            {
                Type = LanMessageTypes.ColorDrawChoice,
                ProtocolVersion = options.ProtocolVersion,
                GameId = room.GameId,
                Color = "Empty"
            }, timeout.Token);

            var error = await failed.Task.WaitAsync(timeout.Token);
            Assert.Equal(LanErrorCodes.ProtocolError, error.Code);
            Assert.Contains("цвет", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }
}
