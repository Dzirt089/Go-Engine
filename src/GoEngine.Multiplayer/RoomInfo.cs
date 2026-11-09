namespace GoEngine.Multiplayer;

/// <summary>Комната в локальной сети, найденная гостем.</summary>
/// <remarks>
/// Это только данные для списка и подключения: адрес и порт не являются identity.
/// Доверие к хозяину подтверждает сам хозяин кнопкой «Пустить»; содержимое комнаты — только
/// аватар, номер, размер доски и версия протокола, без имён и персональных данных.
/// </remarks>
public sealed class RoomInfo
{
    /// <summary>Создаёт описание комнаты.</summary>
    /// <param name="gameId">Уникальный идентификатор партии.</param>
    /// <param name="hostAvatarId">Номер зверя-аватара хозяина.</param>
    /// <param name="roomNumber">Номер комнаты для выбора детьми.</param>
    /// <param name="boardSize">Сторона доски: 9, 13 или 19.</param>
    /// <param name="tcpPort">TCP-порт хозяина.</param>
    /// <param name="hostAddress">IPv4-адрес хозяина в локальной сети.</param>
    /// <param name="protocolVersion">Версия сетевого протокола.</param>
    /// <param name="lastSeen">Когда комната последний раз объявлялась.</param>
    public RoomInfo(
        string gameId,
        int hostAvatarId,
        int roomNumber,
        int boardSize,
        int tcpPort,
        string hostAddress,
        int protocolVersion,
        DateTimeOffset lastSeen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostAddress);
        ArgumentOutOfRangeException.ThrowIfNegative(hostAvatarId);
        ArgumentOutOfRangeException.ThrowIfNegative(roomNumber);
        ArgumentOutOfRangeException.ThrowIfNegative(boardSize);
        ArgumentOutOfRangeException.ThrowIfNegative(tcpPort);
        ArgumentOutOfRangeException.ThrowIfNegative(protocolVersion);

        GameId = gameId;
        HostAvatarId = hostAvatarId;
        RoomNumber = roomNumber;
        BoardSize = boardSize;
        TcpPort = tcpPort;
        HostAddress = hostAddress;
        ProtocolVersion = protocolVersion;
        LastSeen = lastSeen;
    }

    /// <summary>Уникальный идентификатор партии.</summary>
    public string GameId { get; }

    /// <summary>Номер зверя-аватара хозяина.</summary>
    public int HostAvatarId { get; }

    /// <summary>Номер комнаты для выбора детьми.</summary>
    public int RoomNumber { get; }

    /// <summary>Сторона доски: 9, 13 или 19.</summary>
    public int BoardSize { get; }

    /// <summary>TCP-порт хозяина.</summary>
    public int TcpPort { get; }

    /// <summary>IPv4-адрес хозяина в локальной сети.</summary>
    public string HostAddress { get; }

    /// <summary>Версия сетевого протокола.</summary>
    public int ProtocolVersion { get; }

    /// <summary>Когда комната последний раз объявлялась.</summary>
    public DateTimeOffset LastSeen { get; }
}
