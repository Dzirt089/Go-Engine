using System.Net;
using System.Security.Cryptography;
using System.Text;
using GoEngine.App.Services.Updates;

namespace GoEngine.App.Tests;

/// <summary>Тесты загрузки обновления: размер и SHA-256 проверяются до установки.</summary>
public sealed class UpdateDownloaderTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("содержимое обновления");

    [Fact]
    public async Task Загрузка_Сохраняет_Проверенный_Файл()
    {
        var directory = NewDirectory();

        try
        {
            var asset = Asset(Payload);
            var result = await Downloader(Payload, HttpStatusCode.OK).DownloadAsync(asset, directory);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(Payload, await File.ReadAllBytesAsync(result.Value!));
            Assert.False(File.Exists(result.Value + ".part"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Отказывает_При_Несовпадении_Хеша_И_Удаляет_Файл()
    {
        var directory = NewDirectory();

        try
        {
            var asset = Asset(Payload) with { Sha256 = new string('A', 64) };
            var result = await Downloader(Payload, HttpStatusCode.OK).DownloadAsync(asset, directory);

            Assert.False(result.IsSuccess);
            Assert.Contains("SHA-256", result.Error, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Отказывает_При_Несовпадении_Размера()
    {
        var directory = NewDirectory();

        try
        {
            var asset = Asset(Payload) with { Size = Payload.Length + 1 };
            var result = await Downloader(Payload, HttpStatusCode.OK).DownloadAsync(asset, directory);

            Assert.False(result.IsSuccess);
            Assert.Contains("Размер", result.Error, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Сообщает_Код_Ответа()
    {
        var directory = NewDirectory();

        try
        {
            var result = await Downloader(Payload, HttpStatusCode.Forbidden).DownloadAsync(Asset(Payload), directory);

            Assert.False(result.IsSuccess);
            Assert.Contains("403", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Создаёт_Каталог()
    {
        var root = NewDirectory();
        var directory = Path.Combine(root, "вложенный");

        try
        {
            var result = await Downloader(Payload, HttpStatusCode.OK).DownloadAsync(Asset(Payload), directory);

            Assert.True(result.IsSuccess, result.Error);
            Assert.True(File.Exists(result.Value));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Сообщает_Ход_По_Байтам()
    {
        var directory = NewDirectory();
        var reports = new List<DownloadProgress>();

        try
        {
            var asset = Asset(Payload);
            var result = await Downloader(Payload, HttpStatusCode.OK)
                .DownloadAsync(asset, directory, new CollectingProgress(reports));

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(Payload.Length, reports[^1].Received);
            Assert.Equal(100, reports[^1].Percent);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Начинает_Отчёт_С_Нуля()
    {
        var directory = NewDirectory();
        var reports = new List<DownloadProgress>();

        try
        {
            // Нулевой отчёт нужен полосе хода: без него она появляется только после первой порции.
            await Downloader(Payload, HttpStatusCode.OK)
                .DownloadAsync(Asset(Payload), directory, new CollectingProgress(reports));

            Assert.Equal(0, reports[0].Received);
            Assert.Equal(Payload.Length, reports[0].Total);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Прерывается_По_Отмене()
    {
        var directory = NewDirectory();

        try
        {
            // Отменённый признак — ожидаемый исход: загрузка возвращает причину, а не исключение.
            var result = await Downloader(Payload, HttpStatusCode.OK)
                .DownloadAsync(Asset(Payload), directory, new CancellationToken(canceled: true));

            Assert.False(result.IsSuccess);
            Assert.Contains("прервана", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "goengine-update-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        return directory;
    }

    private static UpdateAsset Asset(byte[] payload) => new(
        "update.bin",
        "https://example.org/update.bin",
        payload.Length,
        Convert.ToHexString(SHA256.HashData(payload)));

    private static UpdateDownloader Downloader(byte[] payload, HttpStatusCode status) =>
        new(new HttpClient(StubHttpHandler.Bytes(payload, status)));
}
