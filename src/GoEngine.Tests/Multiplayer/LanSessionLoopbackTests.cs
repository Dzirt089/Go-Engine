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
}