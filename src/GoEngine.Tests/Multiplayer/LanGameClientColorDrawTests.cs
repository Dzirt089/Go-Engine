using GoEngine.Multiplayer;

namespace GoEngine.Tests;

/// <summary>Буферизация предложения нигири, пришедшего раньше подписки (регресс «неверное раскрытие»).</summary>
public sealed class LanGameClientColorDrawTests
{
    [Fact]
    public void Предложение_До_Подписки_Доставляется_Первому_Подписчику()
    {
        var client = new LanGameClient();
        ColorDrawOfferEventArgs? received = null;
        client.DispatchColorDrawOffer(new ColorDrawOfferEventArgs("commitment-1", 15_000));

        client.ColorDrawOfferReceived += (_, args) => received = args;

        Assert.NotNull(received);
        Assert.Equal("commitment-1", received!.Commitment);
        Assert.Equal(15_000, received.TimeoutMs);
    }

    [Fact]
    public void Предложения_После_Подписки_Идут_Без_Дублей()
    {
        var client = new LanGameClient();
        var offers = new List<ColorDrawOfferEventArgs>();
        client.ColorDrawOfferReceived += (_, args) => offers.Add(args);

        client.DispatchColorDrawOffer(new ColorDrawOfferEventArgs("a", 1));
        client.DispatchColorDrawOffer(new ColorDrawOfferEventArgs("b", 2));

        Assert.Equal(2, offers.Count);
        Assert.Equal(["a", "b"], offers.Select(offer => offer.Commitment));
    }
}
