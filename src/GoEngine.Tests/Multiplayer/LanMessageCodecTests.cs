using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Тесты кодирования сетевых сообщений и перевода ходов.</summary>
public sealed class LanMessageCodecTests
{
    [Fact]
    public void Сообщение_О_Ходе_Кодируется_И_Разбирается()
    {
        var message = new LanMessage
        {
            Type = LanMessageTypes.MoveApplied,
            GameId = "0123456789abcdef0123456789abcdef",
            MoveNumber = 7,
            MoveType = MoveType.Play.Name,
            Color = StoneColor.Black.Name,
            X = 4,
            Y = 5,
            AutoPass = false,
            RemainingMs = 90_000
        };

        var payload = LanMessageCodec.Encode(message);
        var decoded = LanMessageCodec.TryDecode(payload, out var read);

        Assert.True(decoded);
        Assert.NotNull(read);
        Assert.Equal(LanMessageTypes.MoveApplied, read.Type);
        Assert.Equal(7, read.MoveNumber);
        Assert.Equal(4, read.X);
    }

    [Fact]
    public void Неизвестный_Тип_Сообщения_Не_Кодируется()
    {
        var message = new LanMessage { Type = "unknown" };

        _ = Assert.Throws<ArgumentException>(() => LanMessageCodec.Encode(message));
    }

    [Fact]
    public void Ход_Камень_Переводится_В_Сеть_И_Обратно()
    {
        var move = Move.Play(new Point(4, 5), StoneColor.White);

        var dto = MoveWire.FromMove(3, move, autoPass: false);
        var success = MoveWire.TryToMove(dto, out var restored, out var error);

        Assert.True(success, error);
        Assert.Equal(move, restored);
    }

    [Fact]
    public void Авто_Пас_Переводится_С_Флагом()
    {
        var move = Move.Pass(StoneColor.Black);

        var dto = MoveWire.FromMove(4, move, autoPass: true);

        Assert.True(dto.AutoPass);
        Assert.Equal(MoveType.Pass.Name, dto.Type);
    }

    [Fact]
    public void Сдача_Переводится_Обратно()
    {
        var dto = MoveWire.FromMove(5, Move.Resign(StoneColor.White), autoPass: false);

        Assert.True(MoveWire.TryToMove(dto, out var restored, out _));
        Assert.Equal(MoveType.Resign, restored.Type);
    }

    [Fact]
    public void Точка_За_Доской_Отклонена()
    {
        var dto = new LanMoveDto(1, MoveType.Play.Name, StoneColor.Black.Name, 19, 0, false);

        Assert.False(MoveWire.TryToMove(dto, out _, out _));
    }
}
