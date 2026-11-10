using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Коды ошибок сетевого режима для интерфейса и логов.</summary>
public static class LanErrorCodes
{
    /// <summary>Комната занята или уже играет.</summary>
    public const string RoomBusy = "lan.room_busy";

    /// <summary>Версия протокола не совпадает.</summary>
    public const string ProtocolMismatch = "lan.protocol_mismatch";

    /// <summary>Игра с таким идентификатором не найдена.</summary>
    public const string GameNotFound = "lan.game_not_found";

    /// <summary>Хозяин отклонил подключение.</summary>
    public const string JoinRejected = "lan.join_rejected";

    /// <summary>Соединение потеряно.</summary>
    public const string ConnectionLost = "lan.connection_lost";

    /// <summary>Соперник вышел из партии.</summary>
    public const string HostLeft = "lan.host_left";

    /// <summary>Не удалось переподключиться за отведённое время.</summary>
    public const string ReconnectTimeout = "lan.reconnect_timeout";

    /// <summary>Сетевое сообщение повреждено или не поддерживается.</summary>
    public const string ProtocolError = "lan.protocol_error";

    /// <summary>Хозяин отклонил ход гостя: ход не по правилам или не в свою очередь.</summary>
    public const string MoveRejected = "lan.move_rejected";

    /// <summary>Партия прервана: оба игрока не делают ходы.</summary>
    public const string Stalled = "lan.stalled";

    /// <summary>Хозяин не ответил на заявку гостя за отведённое время.</summary>
    public const string JoinTimeout = "lan.join_timeout";
}

/// <summary>Ошибка сетевого режима.</summary>
public sealed class LanErrorEventArgs : EventArgs
{
    internal LanErrorEventArgs(string code, string message)
    {
        Code = code;
        Message = message;
    }

    /// <summary>Код ошибки из <see cref="LanErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>Понятное игроку сообщение на русском.</summary>
    public string Message { get; }
}

/// <summary>Гость просит пустить его в комнату; хозяин отвечает «Пустить» или «Не сейчас».</summary>
public sealed class GuestJoinRequestEventArgs : EventArgs
{
    internal GuestJoinRequestEventArgs(int guestAvatarId, string remoteAddress)
    {
        GuestAvatarId = guestAvatarId;
        RemoteAddress = remoteAddress;
    }

    /// <summary>Номер зверя-аватара гостя.</summary>
    public int GuestAvatarId { get; }

    /// <summary>Адрес гостя для диагностики; в интерфейсе детям не показываем.</summary>
    public string RemoteAddress { get; }
}

/// <summary>Гость просит сделать ход; хозяин проверяет очередь и правила.</summary>
public sealed class GuestMoveRequestEventArgs : EventArgs
{
    internal GuestMoveRequestEventArgs(int moveNumber, Move move)
    {
        MoveNumber = moveNumber;
        Move = move;
    }

    /// <summary>Ожидаемый номер хода, который назвал гость.</summary>
    public int MoveNumber { get; }

    /// <summary>Предложенный ход.</summary>
    public Move Move { get; }
}

/// <summary>Гость просит полное состояние партии после переподключения.</summary>
public sealed class GuestStateSyncRequestedEventArgs : EventArgs
{
}

/// <summary>Ход применён и подтверждён хозяином.</summary>
public sealed class MoveAppliedEventArgs : EventArgs
{
    internal MoveAppliedEventArgs(int moveNumber, Move move, bool autoPass, int remainingMs)
    {
        MoveNumber = moveNumber;
        Move = move;
        AutoPass = autoPass;
        RemainingMs = remainingMs;
    }

    /// <summary>Номер применённого хода.</summary>
    public int MoveNumber { get; }

    /// <summary>Применённый ход.</summary>
    public Move Move { get; }

    /// <summary>Ход был вынужденным пасом по таймеру.</summary>
    public bool AutoPass { get; }

    /// <summary>Сколько миллисекунд осталось на следующий ход.</summary>
    public int RemainingMs { get; }
}

/// <summary>Состояние таймера хода: идёт или стоит на паузе из-за обрыва.</summary>
public sealed class TurnStateEventArgs : EventArgs
{
    internal TurnStateEventArgs(int remainingMs, bool paused)
    {
        RemainingMs = remainingMs;
        Paused = paused;
    }

    /// <summary>Сколько миллисекунд осталось на ход.</summary>
    public int RemainingMs { get; }

    /// <summary>Таймер стоит на паузе.</summary>
    public bool Paused { get; }
}

/// <summary>Полное состояние партии после переподключения.</summary>
public sealed class StateSyncEventArgs : EventArgs
{
    internal StateSyncEventArgs(IReadOnlyList<AppliedMove> moves, int remainingMs, bool paused)
    {
        Moves = moves;
        RemainingMs = remainingMs;
        Paused = paused;
    }

    /// <summary>Все ходы партии по порядку, включая вынужденные пасы.</summary>
    public IReadOnlyList<AppliedMove> Moves { get; }

    /// <summary>Сколько миллисекунд осталось на текущий ход.</summary>
    public int RemainingMs { get; }

    /// <summary>Таймер стоит на паузе.</summary>
    public bool Paused { get; }
}

/// <summary>Ход в полном состоянии партии.</summary>
public sealed class AppliedMove
{
    /// <summary>Создаёт запись хода.</summary>
    /// <param name="moveNumber">Номер хода.</param>
    /// <param name="move">Доменный ход.</param>
    /// <param name="autoPass">Ход был вынужденным пасом.</param>
    public AppliedMove(int moveNumber, Move move, bool autoPass)
    {
        MoveNumber = moveNumber;
        Move = move;
        AutoPass = autoPass;
    }

    /// <summary>Номер хода.</summary>
    public int MoveNumber { get; }

    /// <summary>Доменный ход.</summary>
    public Move Move { get; }

    /// <summary>Ход был вынужденным пасом.</summary>
    public bool AutoPass { get; }
}

/// <summary>Данные начавшейся гостевой партии.</summary>
public sealed class GuestSessionStarted
{
    internal GuestSessionStarted(
        string gameId,
        int boardSize,
        double komi,
        StoneColor guestColor,
        int hostAvatarId,
        int remainingMs)
    {
        GameId = gameId;
        BoardSize = boardSize;
        Komi = komi;
        GuestColor = guestColor;
        HostAvatarId = hostAvatarId;
        RemainingMs = remainingMs;
    }

    /// <summary>Идентификатор партии.</summary>
    public string GameId { get; }

    /// <summary>Сторона доски: 9, 13 или 19.</summary>
    public int BoardSize { get; }

    /// <summary>Коми партии.</summary>
    public double Komi { get; }

    /// <summary>Цвет гостя.</summary>
    public StoneColor GuestColor { get; }

    /// <summary>Номер зверя-аватара хозяина.</summary>
    public int HostAvatarId { get; }

    /// <summary>Сколько миллисекунд осталось на первый ход.</summary>
    public int RemainingMs { get; }
}
