using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoEngine.Multiplayer;

/// <summary>Бинарный формат объявления комнаты (JSON в UDP-датаграмме).</summary>
internal static class RoomDiscoveryProtocol
{
    internal const string Magic = "goengine-room";
    internal const int ProtocolVersion = 1;
    internal const int MaxPacketBytes = 2048;

    private static readonly JsonSerializerOptions Options = new();

    internal static byte[] Encode(RoomAnnouncement room)
    {
        var packet = new RoomPacket
        {
            Magic = Magic,
            ProtocolVersion = room.ProtocolVersion,
            GameId = room.GameId,
            HostAvatarId = room.HostAvatarId,
            RoomNumber = room.RoomNumber,
            BoardSize = room.BoardSize,
            TcpPort = room.TcpPort
        };

        return JsonSerializer.SerializeToUtf8Bytes(packet, Options);
    }

    internal static bool TryDecode(ReadOnlySpan<byte> payload, out RoomAnnouncement? room)
    {
        room = null;
        if (payload.Length is 0 or > MaxPacketBytes)
        {
            return false;
        }

        try
        {
            var packet = JsonSerializer.Deserialize<RoomPacket>(payload, Options);
            if (packet is null
                || !string.Equals(packet.Magic, Magic, StringComparison.Ordinal)
                || packet.ProtocolVersion != ProtocolVersion
                || !Guid.TryParseExact(packet.GameId, "N", out _)
                || packet.HostAvatarId is < 0 or > 7
                || packet.RoomNumber is < 1 or > 999
                || packet.BoardSize is not (9 or 13 or 19)
                || packet.TcpPort is < 1 or > 65535)
            {
                return false;
            }

            room = new RoomAnnouncement(
                packet.GameId!,
                packet.HostAvatarId!.Value,
                packet.RoomNumber!.Value,
                packet.BoardSize!.Value,
                packet.TcpPort!.Value,
                packet.ProtocolVersion!.Value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed record RoomPacket
    {
        [JsonPropertyName("magic")]
        public string? Magic { get; init; }

        [JsonPropertyName("v")]
        public int? ProtocolVersion { get; init; }

        [JsonPropertyName("game")]
        public string? GameId { get; init; }

        [JsonPropertyName("avatar")]
        public int? HostAvatarId { get; init; }

        [JsonPropertyName("room")]
        public int? RoomNumber { get; init; }

        [JsonPropertyName("board")]
        public int? BoardSize { get; init; }

        [JsonPropertyName("port")]
        public int? TcpPort { get; init; }
    }
}
