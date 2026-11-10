using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.App.Tests;

/// <summary>Подставной хозяин комнаты: записывает отправленные сообщения и поднимает события.</summary>
internal sealed class FakeNetworkHost : INetworkGameHost
{
    private TaskCompletionSource<SentMove> _nextMove = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public event EventHandler<GuestJoinRequestEventArgs>? GuestJoinRequested;

    public event EventHandler<GuestMoveRequestEventArgs>? GuestMoveRequested;

    public event EventHandler<GuestStateSyncRequestedEventArgs>? GuestStateSyncRequested;

    public event EventHandler? GuestConnected;

    public event EventHandler? GuestDisconnected;

    public event EventHandler? GuestReconnected;

    public event EventHandler? GuestLeft;

    public event EventHandler<LanErrorEventArgs>? Failed;

    public event EventHandler<ColorDrawGuessEventArgs>? ColorDrawGuessReceived;

    public event EventHandler<ColorDrawChoiceEventArgs>? ColorDrawChoiceReceived;

    public RoomInfo? Room => null;

    public bool IsInGame { get; set; } = true;

    public bool IsConnected { get; set; } = true;

    public List<SentMove> SentMoves { get; } = [];

    public List<(int Remaining, bool Paused)> TurnStates { get; } = [];

    public int StateSyncCount { get; private set; }

    public bool Stopped { get; private set; }

    public Task<SentMove> NextMoveAsync() => _nextMove.Task;

    public Task<Result<RoomInfo>> StartHostingAsync(int hostAvatarId, int roomNumber, int boardSize, CancellationToken cancellationToken) =>
        Task.FromResult(Result<RoomInfo>.Fail("В тесте комната создаётся заранее."));

    public Task<Result> AcceptGuestAsync(StoneColor guestColor, double komi, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Ok());

    public Task<Result> RejectGuestAsync(string reason, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Ok());

    public Task<Result> SendMoveAppliedAsync(Move move, bool autoPass, int moveNumber, int remainingMs, CancellationToken cancellationToken)
    {
        SentMoves.Add(new SentMove(move, autoPass, moveNumber, remainingMs));
        var next = _nextMove;
        _nextMove = new TaskCompletionSource<SentMove>(TaskCreationOptions.RunContinuationsAsynchronously);
        next.TrySetResult(SentMoves[^1]);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendTurnStateAsync(int remainingMs, bool paused, CancellationToken cancellationToken)
    {
        TurnStates.Add((remainingMs, paused));
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendStateSyncAsync(IReadOnlyList<AppliedMove> moves, int remainingMs, bool paused, CancellationToken cancellationToken)
    {
        StateSyncCount++;
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendErrorAsync(string code, string message, CancellationToken cancellationToken)
    {
        LastError = (code, message);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendMoveRejectedAsync(string code, string message, CancellationToken cancellationToken)
    {
        LastMoveRejection = (code, message);
        return Task.FromResult(Result.Ok());
    }

    public List<(string Commitment, int TimeoutMs)> ColorDrawOffers { get; } = [];

    public List<(int Count, string Nonce, bool WinnerIsGuest)> ColorDrawReveals { get; } = [];

    public List<(StoneColor Host, StoneColor Guest)> ColorDrawDones { get; } = [];

    public (string Code, string Message)? LastMoveRejection { get; private set; }

    public (string Code, string Message)? LastError { get; private set; }

    public Task<Result> SendColorDrawOfferAsync(string commitment, int timeoutMs, CancellationToken cancellationToken)
    {
        ColorDrawOffers.Add((commitment, timeoutMs));
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendColorDrawRevealAsync(int count, string nonce, bool winnerIsGuest, CancellationToken cancellationToken)
    {
        ColorDrawReveals.Add((count, nonce, winnerIsGuest));
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendColorDrawDoneAsync(StoneColor hostColor, StoneColor guestColor, CancellationToken cancellationToken)
    {
        ColorDrawDones.Add((hostColor, guestColor));
        return Task.FromResult(Result.Ok());
    }

    public Task StopAsync()
    {
        Stopped = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void RaiseGuestJoin(GuestJoinRequestEventArgs args) => GuestJoinRequested?.Invoke(this, args);

    public void RaiseGuestConnected() => GuestConnected?.Invoke(this, EventArgs.Empty);

    public void RaiseGuestStateSyncRequested() => GuestStateSyncRequested?.Invoke(this, new GuestStateSyncRequestedEventArgs());

    public void RaiseFailed(LanErrorEventArgs args) => Failed?.Invoke(this, args);

    public void RaiseGuestMove(GuestMoveRequestEventArgs args) => GuestMoveRequested?.Invoke(this, args);

    public void RaiseDisconnected() => GuestDisconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseReconnected() => GuestReconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseGuestLeft() => GuestLeft?.Invoke(this, EventArgs.Empty);

    public void RaiseColorDrawGuess(string parity) =>
        ColorDrawGuessReceived?.Invoke(this, new ColorDrawGuessEventArgs(parity));

    public void RaiseColorDrawChoice(StoneColor color) =>
        ColorDrawChoiceReceived?.Invoke(this, new ColorDrawChoiceEventArgs(color));

    internal sealed record SentMove(Move Move, bool AutoPass, int MoveNumber, int RemainingMs);
}

/// <summary>Подставной гость: записывает запросы хода и поднимает подтверждённые ходы.</summary>
internal sealed class FakeNetworkClient : INetworkGameClient
{
    public event EventHandler<MoveAppliedEventArgs>? MoveApplied;

    public event EventHandler<TurnStateEventArgs>? TurnStateReceived;

    public event EventHandler<StateSyncEventArgs>? StateSyncReceived;

    public event EventHandler? Disconnected;

    public event EventHandler? Reconnected;

    public event EventHandler<LanErrorEventArgs>? Failed;

    public event EventHandler<ColorDrawOfferEventArgs>? ColorDrawOfferReceived;

    public event EventHandler<ColorDrawRevealEventArgs>? ColorDrawRevealReceived;

    public event EventHandler<ColorDrawDoneEventArgs>? ColorDrawDoneReceived;

    public bool IsConnected { get; set; } = true;

    public List<(Move Move, int MoveNumber)> Requests { get; } = [];

    public bool Left { get; private set; }

    public Task<Result<GuestSessionStarted>> JoinAsync(RoomInfo room, int avatarId, CancellationToken cancellationToken) =>
        Task.FromResult(Result<GuestSessionStarted>.Fail("В тесте гость уже подключён."));

    public Task<Result> RequestMoveAsync(Move move, int moveNumber, CancellationToken cancellationToken)
    {
        Requests.Add((move, moveNumber));
        return Task.FromResult(Result.Ok());
    }

    public List<string> ColorDrawGuesses { get; } = [];

    public List<StoneColor> ColorDrawChoices { get; } = [];

    public Task<Result> SendColorDrawGuessAsync(string parity, CancellationToken cancellationToken)
    {
        ColorDrawGuesses.Add(parity);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> SendColorDrawChoiceAsync(StoneColor color, CancellationToken cancellationToken)
    {
        ColorDrawChoices.Add(color);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result> RequestStateSyncAsync(CancellationToken cancellationToken) => Task.FromResult(Result.Ok());

    public Task LeaveAsync()
    {
        Left = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void RaiseMoveApplied(MoveAppliedEventArgs args) => MoveApplied?.Invoke(this, args);

    public void RaiseTurnState(TurnStateEventArgs args) => TurnStateReceived?.Invoke(this, args);

    public void RaiseStateSync(StateSyncEventArgs args) => StateSyncReceived?.Invoke(this, args);

    public void RaiseDisconnected() => Disconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseReconnected() => Reconnected?.Invoke(this, EventArgs.Empty);

    public void RaiseFailed(LanErrorEventArgs args) => Failed?.Invoke(this, args);

    public void RaiseColorDrawOffer(string commitment, int timeoutMs) =>
        ColorDrawOfferReceived?.Invoke(this, new ColorDrawOfferEventArgs(commitment, timeoutMs));

    public void RaiseColorDrawReveal(int count, string nonce, bool winnerIsGuest) =>
        ColorDrawRevealReceived?.Invoke(this, new ColorDrawRevealEventArgs(count, nonce, winnerIsGuest));

    public void RaiseColorDrawDone(StoneColor hostColor, StoneColor guestColor) =>
        ColorDrawDoneReceived?.Invoke(this, new ColorDrawDoneEventArgs(hostColor, guestColor));
}
