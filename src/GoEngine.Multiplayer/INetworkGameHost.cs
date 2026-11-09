using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Порт хозяина комнаты: его использует модель представления и подменяют тесты.</summary>
public interface INetworkGameHost : IAsyncDisposable
{
    /// <summary>Гость подключился и ждёт решения хозяина.</summary>
    event EventHandler<GuestJoinRequestEventArgs>? GuestJoinRequested;

    /// <summary>Гость просит сделать ход.</summary>
    event EventHandler<GuestMoveRequestEventArgs>? GuestMoveRequested;

    /// <summary>Гость просит полное состояние партии после переподключения.</summary>
    event EventHandler<GuestStateSyncRequestedEventArgs>? GuestStateSyncRequested;

    /// <summary>Гость подключён, партия началась.</summary>
    event EventHandler? GuestConnected;

    /// <summary>Связь с гостем потеряна.</summary>
    event EventHandler? GuestDisconnected;

    /// <summary>Гость вернулся после обрыва.</summary>
    event EventHandler? GuestReconnected;

    /// <summary>Гость корректно вышел из партии.</summary>
    event EventHandler? GuestLeft;

    /// <summary>Сетевая ошибка.</summary>
    event EventHandler<LanErrorEventArgs>? Failed;

    /// <summary>Комната, пока хозяин её ждёт.</summary>
    RoomInfo? Room { get; }

    /// <summary>Партия идёт.</summary>
    bool IsInGame { get; }

    /// <summary>Есть активное соединение.</summary>
    bool IsConnected { get; }

    /// <summary>Запускает комнату.</summary>
    Task<Result<RoomInfo>> StartHostingAsync(int hostAvatarId, int roomNumber, int boardSize, CancellationToken cancellationToken);

    /// <summary>Пустить гостя и начать партию.</summary>
    Task<Result> AcceptGuestAsync(StoneColor guestColor, double komi, CancellationToken cancellationToken);

    /// <summary>Отклонить подключение.</summary>
    Task<Result> RejectGuestAsync(string reason, CancellationToken cancellationToken);

    /// <summary>Сообщить о применённом ходе.</summary>
    Task<Result> SendMoveAppliedAsync(Move move, bool autoPass, int moveNumber, int remainingMs, CancellationToken cancellationToken);

    /// <summary>Сообщить состояние таймера.</summary>
    Task<Result> SendTurnStateAsync(int remainingMs, bool paused, CancellationToken cancellationToken);

    /// <summary>Отправить полное состояние партии.</summary>
    Task<Result> SendStateSyncAsync(IReadOnlyList<AppliedMove> moves, int remainingMs, bool paused, CancellationToken cancellationToken);

    /// <summary>Сообщить об ошибке.</summary>
    Task<Result> SendErrorAsync(string code, string message, CancellationToken cancellationToken);

    /// <summary>Сообщить гостю, что его ход отклонён, не закрывая соединение.</summary>
    /// <param name="code">Код ошибки из <see cref="LanErrorCodes"/>.</param>
    /// <param name="message">Понятное сообщение на русском.</param>
    /// <param name="cancellationToken">Отмена отправки.</param>
    /// <returns>Успех или причина отказа.</returns>
    Task<Result> SendMoveRejectedAsync(string code, string message, CancellationToken cancellationToken);

    /// <summary>Остановить комнату.</summary>
    Task StopAsync();
}
