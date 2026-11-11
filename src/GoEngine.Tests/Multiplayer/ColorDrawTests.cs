using GoEngine.Core;
using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Юнит-тесты нигири: обязательство, раскрытие, чётность и протокол.</summary>
public sealed class ColorDrawTests
{
    [Fact]
    public void Обязательство_Проверяется_Только_Своим_Числом_И_Солью()
    {
        var random = new Random(20261110);
        var count = ColorDraw.CreateCount(random);
        var nonce = ColorDraw.CreateNonce(random);
        var commitment = ColorDraw.Commitment(count, nonce);

        Assert.True(ColorDraw.Verify(count, nonce, commitment));
        Assert.False(ColorDraw.Verify(count % 9 + 1, nonce, commitment));
        Assert.False(ColorDraw.Verify(count, nonce + "00", commitment));
    }

    [Fact]
    public void Гость_Угадал_Когда_Чётность_Совпала()
    {
        Assert.True(ColorDraw.IsGuessCorrect(7, ColorDraw.GuessOdd));
        Assert.False(ColorDraw.IsGuessCorrect(7, ColorDraw.GuessEven));
        Assert.True(ColorDraw.IsGuessCorrect(4, ColorDraw.GuessEven));
        Assert.Equal(ColorDraw.GuessOdd, ColorDraw.ParityOf(9));
        Assert.Equal(ColorDraw.GuessEven, ColorDraw.ParityOf(2));
    }

    [Fact]
    public void Раскрытие_Детерминировано_При_Фиксированном_Зерне()
    {
        var first = new Random(42);
        var second = new Random(42);

        Assert.Equal(ColorDraw.CreateCount(first), ColorDraw.CreateCount(second));
        Assert.Equal(ColorDraw.CreateNonce(first), ColorDraw.CreateNonce(second));
    }

    [Fact]
    public void Версия_Протокола_Повышена_Для_Нигири()
    {
        Assert.Equal(2, new MultiplayerOptions().ProtocolVersion);
        Assert.True(new MultiplayerOptions().ColorDrawTimeout >= TimeSpan.FromSeconds(15));
        Assert.Equal(StoneColor.White, ColorDraw.Opposite(StoneColor.Black));
        Assert.Equal(StoneColor.Black, ColorDraw.Opposite(StoneColor.White));
    }

    [Theory]
    [InlineData(1, "камень")]
    [InlineData(2, "камня")]
    [InlineData(4, "камня")]
    [InlineData(5, "камней")]
    [InlineData(9, "камней")]
    public void Слово_Камень_Согласуется_С_Числом(int count, string expected) =>
        Assert.Equal(expected, ColorDraw.StonesWord(count));
}
