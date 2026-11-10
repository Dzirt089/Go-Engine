using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Multiplayer;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.App.Tests;

/// <summary>Тесты нигири-розыгрыша цвета в сетевой партии (R-004): хозяин и гость.</summary>
/// <remarks>
/// Хозяин ведёт розыгрыш и таймеры: предлагает обязательство, ждёт ответ, раскрывает горсть
/// и ждёт выбор победителя. Гость только отвечает и выбирает цвет. Проверки идут на подставных
/// сессиях и @@FakeTimeProvider@@, как остальные сетевые тесты.
/// </remarks>
public sealed class ColorDrawNetworkTests
{
    private static readonly CancellationToken Cancel = CancellationToken.None;

    [Fact]
    public async Task Хозяин_Начинает_Нигири_И_Ждёт_Ответ()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var draw = model.BeginColorDrawAsync(Cancel);

        try
        {
            await WaitUntilAsync(() => host.ColorDrawOffers.Count == 1, "хозяин не предложил нигири");

            Assert.True(model.IsColorDrawActive);
            Assert.False(model.ColorDrawShowGuess);
            Assert.False(model.ColorDrawShowChoice);
            Assert.False(model.IsNetworkMyTurn);
            Assert.Contains("угадывает", model.ColorDrawStateText, StringComparison.Ordinal);
            var (commitment, timeoutMs) = host.ColorDrawOffers[0];
            Assert.False(string.IsNullOrWhiteSpace(commitment));
            Assert.InRange(timeoutMs, 15_000, 20_000);
        }
        finally
        {
            await model.LeaveNetworkGameAsync();
            await draw.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task Нигири_Завершается_И_Цвета_Совпадают()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var draw = model.BeginColorDrawAsync(Cancel);
        await WaitUntilAsync(() => host.ColorDrawOffers.Count == 1, "нет предложения");
        var commitment = host.ColorDrawOffers[0].Commitment;

        host.RaiseColorDrawGuess(ColorDraw.GuessOdd);
        await WaitUntilAsync(() => host.ColorDrawReveals.Count == 1, "нет раскрытия");
        var (count, nonce, guestWon) = host.ColorDrawReveals[0];
        Assert.True(ColorDraw.Verify(count, nonce, commitment));

        if (guestWon)
        {
            await WaitUntilAsync(() => model.ColorDrawStateText.Contains("выбирает", StringComparison.Ordinal), "гость не выбирает");
            host.RaiseColorDrawChoice(StoneColor.White);
        }
        else
        {
            await WaitUntilAsync(() => model.ColorDrawShowChoice, "хозяин не выбирает");
            await model.ChooseColorDrawColorAsync(StoneColor.Black);
        }

        await draw.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(model.IsColorDrawActive);
        var (hostColor, guestColor) = Assert.Single(host.ColorDrawDones);
        Assert.Equal(guestWon ? StoneColor.White : StoneColor.Black, hostColor);
        Assert.Equal(guestWon ? StoneColor.Black : StoneColor.White, guestColor);
        Assert.Equal(hostColor == StoneColor.Black ? "чёрные" : "белые", model.NetworkPlayerColorText);
        Assert.True(model.IsGameStarted);
    }

    [Fact]
    public async Task Гость_Угадывает_И_Выбирает_Цвет()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(client, Session(StoneColor.White));
        var random = new Random(5);
        var count = 7;
        var nonce = ColorDraw.CreateNonce(random);
        var commitment = ColorDraw.Commitment(count, nonce);

        client.RaiseColorDrawOffer(commitment, 15_000);

        Assert.True(model.IsColorDrawActive);
        Assert.True(model.ColorDrawShowGuess);
        Assert.False(model.IsNetworkMyTurn);

        await model.GuessColorDrawAsync(odd: true);

        Assert.Contains(ColorDraw.GuessOdd, client.ColorDrawGuesses);
        Assert.False(model.ColorDrawShowGuess);

        var winnerIsGuest = ColorDraw.IsGuessCorrect(count, ColorDraw.GuessOdd);
        client.RaiseColorDrawReveal(count, nonce, winnerIsGuest);

        Assert.True(model.ColorDrawShowChoice);
        await model.ChooseColorDrawColorAsync(StoneColor.White);

        Assert.Contains(StoneColor.White, client.ColorDrawChoices);

        client.RaiseColorDrawDone(StoneColor.Black, StoneColor.White);

        Assert.False(model.IsColorDrawActive);
        Assert.Equal("белые", model.NetworkPlayerColorText);
    }

    [Fact]
    public async Task Гость_Видит_Неверное_Раскрытие_Как_Ошибку()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(client, Session(StoneColor.White));
        var random = new Random(9);
        var nonce = ColorDraw.CreateNonce(random);
        var commitment = ColorDraw.Commitment(7, nonce);

        client.RaiseColorDrawOffer(commitment, 15_000);
        client.RaiseColorDrawReveal(8, nonce, winnerIsGuest: false);

        Assert.True(model.IsNetworkTerminal);
        Assert.False(model.IsColorDrawActive);
        Assert.Contains("раскрытие", model.NetworkBannerText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Нигири_По_Таймауту_Всё_Равно_Завершается()
    {
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var draw = model.BeginColorDrawAsync(Cancel);
        await WaitUntilAsync(() => host.ColorDrawOffers.Count == 1, "нет предложения");

        // Гость не ответил: хозяин угадывает сам.
        time.Advance(TimeSpan.FromSeconds(15));
        await WaitUntilAsync(() => host.ColorDrawReveals.Count == 1, "нет раскрытия по таймауту");
        var guestWon = host.ColorDrawReveals[0].WinnerIsGuest;

        if (guestWon)
        {
            await WaitUntilAsync(() => model.ColorDrawStateText.Contains("выбирает", StringComparison.Ordinal), "нет ожидания выбора");
            time.Advance(TimeSpan.FromSeconds(15));
        }
        else
        {
            await WaitUntilAsync(() => model.ColorDrawShowChoice, "нет выбора хозяина");
            await model.ChooseColorDrawColorAsync(StoneColor.Black);
        }

        await draw.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(model.IsColorDrawActive);
        Assert.Single(host.ColorDrawDones);
    }

    [Fact]
    public async Task Гость_Не_Верит_Подменённому_Победителю()
    {
        // F1: хозяин может прислать валидный хеш и перевёрнутый winnerIsGuest — гость
        // проверяет победителя по своему ответу и закрывает розыгрыш ошибкой.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(client, Session(StoneColor.White));
        var random = new Random(11);
        var nonce = ColorDraw.CreateNonce(random);
        var commitment = ColorDraw.Commitment(7, nonce);
        client.RaiseColorDrawOffer(commitment, 15_000);
        await model.GuessColorDrawAsync(odd: true);

        // 7 — нечет, ответ «нечет»: честный победитель — гость; подменяем на «не угадал».
        client.RaiseColorDrawReveal(7, nonce, winnerIsGuest: false);

        Assert.True(model.IsNetworkTerminal);
        Assert.False(model.IsColorDrawActive);
        Assert.Contains("противоречивый", model.NetworkBannerText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Хозяин_Не_Применяет_Некорректный_Выбор_Цвета()
    {
        // F2: Empty из гостя не должен попасть в настройки/цвет партии.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var draw = model.BeginColorDrawAsync(Cancel);
        await WaitUntilAsync(() => host.ColorDrawOffers.Count == 1, "нет предложения");
        host.RaiseColorDrawGuess(ColorDraw.GuessOdd);
        await WaitUntilAsync(() => host.ColorDrawReveals.Count == 1, "нет раскрытия");

        host.RaiseColorDrawChoice(StoneColor.Empty);

        await Task.Delay(50);
        Assert.True(model.IsColorDrawActive);
        Assert.Empty(host.ColorDrawDones);

        // Легитимный выбор всё ещё принимается.
        if (host.ColorDrawReveals[0].WinnerIsGuest)
        {
            host.RaiseColorDrawChoice(StoneColor.White);
        }
        else
        {
            await model.ChooseColorDrawColorAsync(StoneColor.Black);
        }

        await draw.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(model.IsColorDrawActive);
    }

    [Fact]
    public async Task Терминал_Во_Время_Розыгрыша_Не_Перезаписывается()
    {
        // F3: выход соперника во время розыгрыша оставляет одно терминальное сообщение.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var host = new FakeNetworkHost();
        _ = await model.StartNetworkHostAsync(host, 9, 5.5, StoneColor.Black, 1, Cancel);
        var draw = model.BeginColorDrawAsync(Cancel);
        await WaitUntilAsync(() => host.ColorDrawOffers.Count == 1, "нет предложения");

        host.RaiseGuestLeft();
        await draw.WaitAsync(TimeSpan.FromSeconds(3));
        var message = model.NetworkBannerText;

        time.Advance(TimeSpan.FromSeconds(15));
        model.TickClock();

        Assert.False(model.IsColorDrawActive);
        Assert.True(model.IsNetworkTerminal);
        Assert.Equal(message, model.NetworkBannerText);
        Assert.Empty(host.ColorDrawDones);
    }

    [Fact]
    public async Task Гость_Закрывает_Розыгрыш_Если_Хозяин_Пропал()
    {
        // F4: у гостя свой дедлайн на раскрытие/итог — оверлей не висит вечно.
        var time = new FakeTimeProvider();
        using var model = CreateModel(time);
        var client = new FakeNetworkClient();
        _ = model.StartNetworkGuest(client, Session(StoneColor.White));
        var random = new Random(13);
        var nonce = ColorDraw.CreateNonce(random);
        client.RaiseColorDrawOffer(ColorDraw.Commitment(7, nonce), 15_000);
        await model.GuessColorDrawAsync(odd: true);

        time.Advance(TimeSpan.FromSeconds(16));
        model.TickClock();

        Assert.True(model.IsNetworkTerminal);
        Assert.False(model.IsColorDrawActive);
        Assert.Contains("не завершился", model.NetworkBannerText, StringComparison.OrdinalIgnoreCase);
    }

    private static MainViewModel CreateModel(FakeTimeProvider time)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var options = new MultiplayerOptions { TimeProvider = time };

        return new MainViewModel(AppSettings.Default, new Random(20261110), time: time, networkOptions: options);
    }

    private static GuestSessionStarted Session(StoneColor color) =>
        new("0123456789abcdef0123456789abcdef", 9, 5.5, color, 1, 90_000);

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Не дождались: {what}");
            await Task.Delay(10);
        }
    }
}
