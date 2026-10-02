using System.Net;
using GoEngine.App.Services.Updates;

namespace GoEngine.App.Tests;

/// <summary>Тесты проверки обновления: сеть и битый манифест — ожидаемые исходы, а не падения.</summary>
public sealed class UpdateCheckerTests
{
    private const string Newer = """
        { "version": "0.2.15", "tag": "v0.2.15", "assets": { "win-x64": { "name": "a", "url": "u", "sha256": "s" } } }
        """;

    [Fact]
    public async Task Проверка_Находит_Новую_Версию()
    {
        var checker = Create(Newer, new AppVersion(0, 2, 9));

        var result = await checker.CheckAsync();

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.IsAvailable);
        Assert.Equal(UpdateStatus.Available, result.Value.Status);
        Assert.Equal(new AppVersion(0, 2, 15), result.Value.Version);
    }

    [Fact]
    public async Task Проверка_Считает_Свою_Версию_Свежей()
    {
        var checker = Create(Newer, new AppVersion(0, 2, 15));

        var result = await checker.CheckAsync();

        Assert.True(result.IsSuccess, result.Error);
        Assert.False(result.Value.IsAvailable);
        Assert.Equal(UpdateStatus.UpToDate, result.Value.Status);
    }

    [Fact]
    public async Task Проверка_Предлагает_Обновление_Локальной_Сборке_Без_Версии()
    {
        var checker = Create(Newer, AppVersion.Unknown);

        var result = await checker.CheckAsync();

        Assert.True(result.IsSuccess, result.Error);
        Assert.True(result.Value.IsAvailable);
    }

    [Fact]
    public async Task Проверка_Сообщает_Код_Ответа()
    {
        var checker = Create(null, new AppVersion(0, 2, 9), HttpStatusCode.NotFound);

        var result = await checker.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("404", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_Переживает_Отсутствие_Сети()
    {
        var http = new HttpClient(new StubHttpHandler(_ => throw new HttpRequestException("сеть недоступна")));
        var checker = new UpdateChecker(http, new Uri("https://example.org/update.json"), new AppVersion(0, 2, 9));

        var result = await checker.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Нет связи", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_Переживает_Таймаут()
    {
        var http = new HttpClient(new StubHttpHandler(_ => throw new TaskCanceledException("время вышло")));
        var checker = new UpdateChecker(http, new Uri("https://example.org/update.json"), new AppVersion(0, 2, 9));

        var result = await checker.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("вовремя", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_Отказывает_На_Битом_Манифесте()
    {
        var checker = Create("не json", new AppVersion(0, 2, 9));

        var result = await checker.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Адрес_Манифеста_Постоянный()
    {
        Assert.Equal(
            "https://github.com/Dzirt089/Go-Engine/releases/download/latest/update.json",
            UpdateChecker.DefaultManifestUrl.ToString());
    }

    [Fact]
    public async Task Проверка_Объясняет_Локальную_Сборку()
    {
        // Локальная сборка получает заводской номер 1.0.0: он «новее» любого выпуска, поэтому
        // сравнение бессмысленно, и приложение обязано сказать это словами, а не молчать.
        var checker = Create(Newer, AppVersion.Current, localBuild: true);

        var result = await checker.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("локальная сборка", result.Error, StringComparison.Ordinal);
        Assert.True(checker.IsLocalBuild);
    }

    private static UpdateChecker Create(
        string? body,
        AppVersion current,
        HttpStatusCode status = HttpStatusCode.OK,
        bool? localBuild = null)
    {
        var http = new HttpClient(StubHttpHandler.Text(body ?? string.Empty, status));

        return new UpdateChecker(http, new Uri("https://example.org/update.json"), current, localBuild);
    }
}
