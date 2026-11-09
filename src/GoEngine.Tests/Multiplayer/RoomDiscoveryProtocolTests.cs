using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Тесты объявления комнаты в UDP-пакете.</summary>
public sealed class RoomDiscoveryProtocolTests
{
    [Fact]
    public void Объявление_Кодируется_И_Разбирается()
    {
        var room = new RoomAnnouncement(
            "0123456789abcdef0123456789abcdef",
            hostAvatarId: 3,
            roomNumber: 42,
            boardSize: 9,
            tcpPort: 47810,
            protocolVersion: 1);

        var payload = RoomDiscoveryProtocol.Encode(room);
        var decoded = RoomDiscoveryProtocol.TryDecode(payload, out var restored);

        Assert.True(decoded);
        Assert.NotNull(restored);
        Assert.Equal(room.GameId, restored.GameId);
        Assert.Equal(42, restored.RoomNumber);
        Assert.Equal(47810, restored.TcpPort);
    }

    [Fact]
    public void Шестизначный_Код_Комнаты_Кодируется_И_Разбирается()
    {
        var room = new RoomAnnouncement("042317", 1, 42, 9, 47810, 1);

        var payload = RoomDiscoveryProtocol.Encode(room);
        var decoded = RoomDiscoveryProtocol.TryDecode(payload, out var restored);

        Assert.True(decoded);
        Assert.NotNull(restored);
        Assert.Equal("042317", restored.GameId);
    }

    [Fact]
    public void Некорректный_Размер_Доски_Отклонён()
    {
        var room = new RoomAnnouncement(
            "0123456789abcdef0123456789abcdef", 0, 42, boardSize: 10, tcpPort: 47810, protocolVersion: 1);
        var payload = RoomDiscoveryProtocol.Encode(room);

        Assert.False(RoomDiscoveryProtocol.TryDecode(payload, out _));
    }

    [Fact]
    public void Мусор_Не_Разбирается()
    {
        Assert.False(RoomDiscoveryProtocol.TryDecode(new byte[] { 1, 2, 3 }, out _));
    }
}
