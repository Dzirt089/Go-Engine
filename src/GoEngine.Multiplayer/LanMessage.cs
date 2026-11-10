using System.Text.Json;
using System.Text.Json.Serialization;
using GoEngine.Core;

namespace GoEngine.Multiplayer;

/// <summary>Типы сетевых сообщений.</summary>
internal static class LanMessageTypes
{
    internal const string Join = "join";
    internal const string Resume = "resume";
    internal const string JoinResult = "join_result";
    internal const string Start = "start";
    internal const string MoveRequest = "move_request";
    internal const string MoveApplied = "move_applied";
    internal const string MoveRejected = "move_rejected";
    internal const string TurnState = "turn_state";
    internal const string StateSyncRequest = "state_sync_request";
    internal const string StateSync = "state_sync";
    internal const string Ping = "ping";
    internal const string Pong = "pong";
    internal const string Bye = "bye";
    internal const string Error = "error";
    internal const string ColorDrawOffer = "color_draw_offer";
    internal const string ColorDrawGuess = "color_draw_guess";
    internal const string ColorDrawReveal = "color_draw_reveal";
    internal const string ColorDrawChoice = "color_draw_choice";
    internal const string ColorDrawDone = "color_draw_done";
}

/// <summary>Один ход в сетевом сообщении.</summary>
internal sealed record LanMoveDto(
    [property: JsonPropertyName("number")] int MoveNumber,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("x")] int? X,
    [property: JsonPropertyName("y")] int? Y,
    [property: JsonPropertyName("auto")] bool AutoPass);

/// <summary>Универсальное сетевое сообщение; незаполненные поля зависят от типа.</summary>
internal sealed record LanMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; }

    [JsonPropertyName("gameId")]
    public string? GameId { get; init; }

    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("avatarId")]
    public int? AvatarId { get; init; }

    [JsonPropertyName("boardSize")]
    public int? BoardSize { get; init; }

    [JsonPropertyName("komi")]
    public double? Komi { get; init; }

    [JsonPropertyName("guestColor")]
    public string? GuestColor { get; init; }

    [JsonPropertyName("accepted")]
    public bool? Accepted { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("moveNumber")]
    public int? MoveNumber { get; init; }

    [JsonPropertyName("moveType")]
    public string? MoveType { get; init; }

    [JsonPropertyName("x")]
    public int? X { get; init; }

    [JsonPropertyName("y")]
    public int? Y { get; init; }

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("autoPass")]
    public bool? AutoPass { get; init; }

    [JsonPropertyName("remainingMs")]
    public long? RemainingMs { get; init; }

    [JsonPropertyName("paused")]
    public bool? Paused { get; init; }

    [JsonPropertyName("moves")]
    public IReadOnlyList<LanMoveDto>? Moves { get; init; }

    [JsonPropertyName("commitment")]
    public string? Commitment { get; init; }

    [JsonPropertyName("nonce")]
    public string? Nonce { get; init; }

    [JsonPropertyName("count")]
    public int? Count { get; init; }

    [JsonPropertyName("parity")]
    public string? Parity { get; init; }

    [JsonPropertyName("winnerIsGuest")]
    public bool? WinnerIsGuest { get; init; }

    [JsonPropertyName("hostColor")]
    public string? HostColor { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

/// <summary>Кодирование и разбор сетевых сообщений.</summary>
internal static class LanMessageCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        LanMessageTypes.Join,
        LanMessageTypes.Resume,
        LanMessageTypes.JoinResult,
        LanMessageTypes.Start,
        LanMessageTypes.MoveRequest,
        LanMessageTypes.MoveApplied,
        LanMessageTypes.MoveRejected,
        LanMessageTypes.TurnState,
        LanMessageTypes.StateSyncRequest,
        LanMessageTypes.StateSync,
        LanMessageTypes.Ping,
        LanMessageTypes.Pong,
        LanMessageTypes.Bye,
        LanMessageTypes.Error,
        LanMessageTypes.ColorDrawOffer,
        LanMessageTypes.ColorDrawGuess,
        LanMessageTypes.ColorDrawReveal,
        LanMessageTypes.ColorDrawChoice,
        LanMessageTypes.ColorDrawDone
    };

    internal static byte[] Encode(LanMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.Type) || !KnownTypes.Contains(message.Type))
        {
            throw new ArgumentException("Неизвестный тип сетевого сообщения.", nameof(message));
        }

        return JsonSerializer.SerializeToUtf8Bytes(message, Options);
    }

    internal static bool TryDecode(ReadOnlySpan<byte> payload, out LanMessage? message)
    {
        message = null;
        try
        {
            var decoded = JsonSerializer.Deserialize<LanMessage>(payload, Options);
            if (decoded is null
                || string.IsNullOrWhiteSpace(decoded.Type)
                || !KnownTypes.Contains(decoded.Type))
            {
                return false;
            }

            message = decoded;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>Перевод доменного хода в сетевой формат и обратно.</summary>
internal static class MoveWire
{
    internal static LanMoveDto FromMove(int moveNumber, Move move, bool autoPass)
    {
        var point = move.Type == MoveType.Play ? move.Point : default;
        return new LanMoveDto(
            moveNumber,
            move.Type.Name,
            move.Color.Name,
            move.Type == MoveType.Play ? point.X : null,
            move.Type == MoveType.Play ? point.Y : null,
            autoPass);
    }

    internal static bool TryToMove(LanMoveDto dto, out Move move, out string? error)
    {
        move = Move.None;
        error = null;

        if (dto.MoveNumber <= 0)
        {
            error = "Номер хода должен быть положительным.";
            return false;
        }

        var type = Enumeration.TryFromName<MoveType>(dto.Type);
        var color = Enumeration.TryFromName<StoneColor>(dto.Color);
        if (type is null || color is null || color == StoneColor.Empty)
        {
            error = "Неизвестный тип или цвет хода.";
            return false;
        }

        if (type == MoveType.Play)
        {
            if (dto.X is null or < 0 or > 18 || dto.Y is null or < 0 or > 18)
            {
                error = "У хода-камня нет корректной точки.";
                return false;
            }

            move = Move.Play(new Point((byte)dto.X.Value, (byte)dto.Y.Value), color);
            return true;
        }

        move = type == MoveType.Pass ? Move.Pass(color) : Move.Resign(color);
        return true;
    }
}
