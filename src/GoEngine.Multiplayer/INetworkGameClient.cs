using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Порт гостя комнаты: его использует модель представления и подменяют тесты.</summary>
public interface INetworkGameClient : IAsyncDisposable
{
    /// <summary>Хозяин прислал применённый ход.</summary>
    event EventHandler<MoveAppliedEventArgs>? MoveApplied;

    /// <summary>Хозяин сообщил состояние таймера.</summary>
    event EventHandler<TurnStateEventArgs>? TurnStateReceived;

    /// <summary>Хозяин прислал полное состояние партии.</summary>
    event EventHandler<StateSyncEventArgs>? StateSyncReceived;

    /// <summary>Связь потеряна.</summary>
    event EventHandler? Disconnected;

    /// <summary>Удалось вернуться в партию.</summary>
    event EventHandler? Reconnected;

    /// <summary>Ошибка, после которой партия не может продолжаться.</summary>
    event EventHandler<LanErrorEventArgs>? Failed;

    /// <summary>Есть активное соединение.</summary>
    bool IsConnected { get; }

    /// <summary>Подключается к комнате.</summary>
    Task<Result<GuestSessionStarted>> JoinAsync(RoomInfo room, int avatarId, CancellationToken cancellationToken);

    /// <summary>Попросить хозяина применить ход.</summary>
    Task<Result> RequestMoveAsync(Move move, int moveNumber, CancellationToken cancellationToken);

    /// <summary>Попросить полное состояние партии.</summary>
    Task<Result> RequestStateSyncAsync(CancellationToken cancellationToken);

    /// <summary>Выйти из партии.</summary>
    Task LeaveAsync();
}
