namespace GoEngine.Multiplayer;

/// <summary>Что хозяин объявляет в локальной сети.</summary>
internal sealed class RoomAnnouncement
{
    internal RoomAnnouncement(
        string gameId,
        int hostAvatarId,
        int roomNumber,
        int boardSize,
        int tcpPort,
        int protocolVersion)
    {
        GameId = gameId;
        HostAvatarId = hostAvatarId;
        RoomNumber = roomNumber;
        BoardSize = boardSize;
        TcpPort = tcpPort;
        ProtocolVersion = protocolVersion;
    }

    internal string GameId { get; }

    internal int HostAvatarId { get; }

    internal int RoomNumber { get; }

    internal int BoardSize { get; }

    internal int TcpPort { get; }

    internal int ProtocolVersion { get; }
}
