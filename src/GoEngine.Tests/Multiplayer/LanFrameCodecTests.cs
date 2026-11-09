using System.Buffers.Binary;
using System.Text;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Тесты кадров сетевого протокола.</summary>
public sealed class LanFrameCodecTests
{
    private static readonly CancellationToken Cancel = CancellationToken.None;

    [Fact]
    public async Task Кадр_Кодируется_И_Читается_Обратно()
    {
        var payload = Encoding.UTF8.GetBytes("привет");
        using var stream = new MemoryStream();

        await LanFrameCodec.WriteAsync(stream, payload, Cancel);
        stream.Position = 0;
        var read = await LanFrameCodec.ReadAsync(stream, Cancel);

        Assert.Equal(payload, read);
    }

    [Fact]
    public async Task Слишком_Большой_Кадр_Отклонён()
    {
        using var stream = new MemoryStream();
        var payload = new byte[LanFrameCodec.MaxPayloadBytes + 1];

        _ = await Assert.ThrowsAsync<LanProtocolException>(() => LanFrameCodec.WriteAsync(stream, payload, Cancel));
    }

    [Fact]
    public async Task Обрезанный_Кадр_Даёт_Закрытие_Соединения()
    {
        using var stream = new MemoryStream();
        var header = new byte[LanFrameCodec.FrameHeaderBytes];
        header[0] = LanFrameCodec.MessageFrameType;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), 10);
        await stream.WriteAsync(header);
        stream.Position = 0;

        _ = await Assert.ThrowsAsync<LanConnectionClosedException>(() => LanFrameCodec.ReadAsync(stream, Cancel));
    }

    [Fact]
    public async Task Неизвестный_Тип_Кадра_Отклонён()
    {
        using var stream = new MemoryStream();
        var header = new byte[LanFrameCodec.FrameHeaderBytes];
        header[0] = 42;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(1), 1);
        await stream.WriteAsync(header);
        await stream.WriteAsync(new byte[] { 1 });
        stream.Position = 0;

        _ = await Assert.ThrowsAsync<LanProtocolException>(() => LanFrameCodec.ReadAsync(stream, Cancel));
    }
}
