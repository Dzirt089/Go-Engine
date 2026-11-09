using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace GoEngine.Multiplayer;

/// <summary>Исключение нарушения сетевого формата.</summary>
internal sealed class LanProtocolException : Exception
{
    internal LanProtocolException(string message) : base(message)
    {
    }
}

/// <summary>Соединение закрыто до получения полного сообщения.</summary>
internal sealed class LanConnectionClosedException : Exception
{
    internal LanConnectionClosedException(string message) : base(message)
    {
    }
}

/// <summary>Кадры сетевого сообщения: 1 байт типа, 4 байта длины (big-endian), payload.</summary>
internal static class LanFrameCodec
{
    internal const byte MessageFrameType = 1;
    internal const int FrameHeaderBytes = 5;
    internal const int MaxPayloadBytes = 256 * 1024;

    internal static async Task WriteAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (payload.Length > MaxPayloadBytes)
        {
            throw new LanProtocolException("Сетевое сообщение превышает допустимый размер.");
        }

        var header = new byte[FrameHeaderBytes];
        header[0] = MessageFrameType;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), checked((uint)payload.Length));

        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (!payload.IsEmpty)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var header = new byte[FrameHeaderBytes];
        try
        {
            await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            throw new LanConnectionClosedException("Соединение закрыто до получения сообщения.");
        }

        if (header[0] != MessageFrameType)
        {
            throw new LanProtocolException("Неизвестный тип сетевого кадра.");
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(1));
        if (length > MaxPayloadBytes)
        {
            throw new LanProtocolException("Сетевое сообщение превышает допустимый размер.");
        }

        if (length == 0)
        {
            throw new LanProtocolException("Пустое сетевое сообщение.");
        }

        var payload = new byte[length];
        try
        {
            await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            throw new LanConnectionClosedException("Сетевое сообщение обрезано.");
        }

        return payload;
    }
}
