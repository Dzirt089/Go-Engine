using System.Security.Cryptography;
using System.Text;
using GoEngine.App.Services.Updates;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты службы обновления: связка «проверка — загрузка — установка».</summary>
public sealed class UpdateServiceTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("пакет обновления");

    [Fact]
    public async Task Установка_Отказывает_Если_Файла_Для_Платформы_Нет()
    {
        var directory = Directory.CreateTempSubdirectory("goengine-update-service");

        try
        {
            var service = Service(directory.FullName, new StubInstaller("win-x64"));
            var check = new UpdateCheck(UpdateStatus.Available, Manifest("linux-x64"));

            var result = await service.DownloadAndInstallAsync(check);

            Assert.False(result.IsSuccess);
            Assert.Contains("win-x64", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Установка_Скачивает_Проверяет_И_Отдаёт_Установщику()
    {
        var directory = Directory.CreateTempSubdirectory("goengine-update-service");

        try
        {
            var installer = new StubInstaller("win-x64");
            var service = Service(directory.FullName, installer);
            var check = new UpdateCheck(UpdateStatus.Available, Manifest("win-x64"));

            var result = await service.DownloadAndInstallAsync(check);

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal("установлено", result.Value);
            Assert.NotNull(installer.Installed);
            Assert.Equal(Payload, await File.ReadAllBytesAsync(installer.Installed));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Установка_Не_Доходит_До_Установщика_При_Битом_Файле()
    {
        var directory = Directory.CreateTempSubdirectory("goengine-update-service");

        try
        {
            var installer = new StubInstaller("win-x64");
            var service = Service(directory.FullName, installer);

            // Хеш в манифесте заведомо не тот: загрузка обязана отказать и не звать установщик.
            var check = new UpdateCheck(UpdateStatus.Available, Manifest("win-x64", new string('B', 64)));

            var result = await service.DownloadAndInstallAsync(check);

            Assert.False(result.IsSuccess);
            Assert.Null(installer.Installed);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_Сообщает_Прогресс_До_Конца()
    {
        var directory = Directory.CreateTempSubdirectory("goengine-update-service");

        try
        {
            var reports = new List<DownloadProgress>();
            var service = Service(directory.FullName, new StubInstaller("win-x64"));
            var check = new UpdateCheck(UpdateStatus.Available, Manifest("win-x64"));

            var result = await service.DownloadAsync(check, new CollectingProgress(reports));

            Assert.True(result.IsSuccess, result.Error);
            Assert.Equal(100, reports[^1].Percent);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Загрузка_И_Установка_Разделены()
    {
        var directory = Directory.CreateTempSubdirectory("goengine-update-service");

        try
        {
            var installer = new StubInstaller("win-x64");
            var service = Service(directory.FullName, installer);
            var check = new UpdateCheck(UpdateStatus.Available, Manifest("win-x64"));

            // Загрузка кончается до установки: интерфейсу нужно успеть показать «Установка…».
            var downloaded = await service.DownloadAsync(check);

            Assert.True(downloaded.IsSuccess, downloaded.Error);
            Assert.Null(installer.Installed);

            var installed = service.Install(downloaded.Value!);

            Assert.True(installed.IsSuccess, installed.Error);
            Assert.Equal(downloaded.Value, installer.Installed);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static UpdateService Service(string directory, IUpdateInstaller installer)
    {
        // Манифест в этих тестах приходит готовым: проверяется связка «загрузка — установка».
        var http = new HttpClient(StubHttpHandler.Bytes(Payload));

        return new UpdateService(
            new UpdateChecker(http, new Uri("https://example.org/update.json"), AppVersion.Unknown),
            new UpdateDownloader(http),
            installer,
            directory);
    }

    private static UpdateManifest Manifest(string platformKey, string? sha = null)
    {
        var json = $$"""
            {
              "version": "0.2.15",
              "tag": "v0.2.15",
              "assets": {
                "{{platformKey}}": {
                  "name": "update.bin",
                  "url": "https://example.org/update.bin",
                  "size": {{Payload.Length}},
                  "sha256": "{{sha ?? Convert.ToHexString(SHA256.HashData(Payload))}}"
                }
              }
            }
            """;

        return UpdateManifest.Parse(json).Value!;
    }

    /// <summary>Подставной установщик: запоминает путь, ничего не запускает.</summary>
    /// <param name="platformKey">Ключ платформы.</param>
    private sealed class StubInstaller(string platformKey) : IUpdateInstaller
    {
        /// <summary>Путь, который передал установщику сервис.</summary>
        public string? Installed { get; private set; }

        /// <inheritdoc />
        public string PlatformKey { get; } = platformKey;

        /// <inheritdoc />
        public Result<string> Install(string filePath)
        {
            Installed = filePath;

            return Result<string>.Ok("установлено");
        }

        /// <inheritdoc />
        public Result<string> OpenDownloadPage(Uri url) => Result<string>.Ok("страница открыта");
    }
}
