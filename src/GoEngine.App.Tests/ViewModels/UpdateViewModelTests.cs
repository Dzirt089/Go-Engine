using System.Net;
using System.Security.Cryptography;
using System.Text;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.Services.Updates;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты приглашения обновления: состояния, тексты и честный ход загрузки.</summary>
/// <remarks>
/// Сеть в тестах не нужна: манифест и файл отдаёт подставной обработчик HTTP — тот же приём, что
/// и в тестах службы обновления. Утверждения идут о том, что видит игрок: строки приглашения,
/// проценты и доступные кнопки, а не о внутренних полях модели.
/// </remarks>
public sealed class UpdateViewModelTests
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("пакет обновления для приглашения");

    [Fact]
    public async Task Проверка_Показывает_Приглашение_С_Версиями()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller());

        await model.CheckAsync();

        Assert.Equal(UpdateStage.Available, model.Stage);
        Assert.Contains("Доступна версия 0.2.15", model.Headline, StringComparison.Ordinal);
        Assert.Contains("у вас 0.1.0", model.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_Без_Сети_Молчит()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller(), offline: true);
        var result = await model.CheckAsync(silent: true);

        // Приложение offline-first: отказ при запуске — обычное дело, а не карточка на экране.
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateStage.Idle, model.Stage);
        Assert.False(model.IsBannerVisible);
    }

    [Fact]
    public async Task Проверка_Без_Сети_С_Кнопки_Показывает_Причину()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller(), offline: true);
        await model.CheckAsync();

        // Проверку просил игрок: молчать нельзя, причина называется словами.
        Assert.Equal(UpdateStage.Failed, model.Stage);
        Assert.StartsWith("Не удалось: ", model.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Проверка_Актуальной_Версии_Не_Показывает_Приглашение()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.1.0"), new StubInstaller());
        await model.CheckAsync();

        Assert.False(model.IsBannerVisible);
        Assert.Contains("самая свежая", model.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Позже_Скрывает_Приглашение()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller());
        await model.CheckAsync();

        model.Later();

        Assert.False(model.IsBannerVisible);
    }

    [Fact]
    public async Task Загрузка_Доводит_Прогресс_До_Ста_И_Заканчивается_Готово()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller());
        await model.CheckAsync();

        var result = await model.DownloadAndInstallAsync();

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(100, model.Percent);
        Assert.Equal(UpdateStage.Done, model.Stage);
        Assert.Equal("Готово", model.Headline);
    }

    [Fact]
    public async Task Загрузка_Показывает_Установку_Перед_Готово()
    {
        using var directory = new TempDirectory();

        var installer = new StubInstaller();
        var model = Model(directory.Path, Manifest("0.2.15"), installer);
        await model.CheckAsync();

        // Установщик спрашивает состояние модели: подпись «Установка…» обязана стоять до него.
        installer.Stage = () => model.Stage;
        await model.DownloadAndInstallAsync();

        Assert.Equal(UpdateStage.Installing, installer.StageAtInstall);
        Assert.NotNull(installer.Installed);
    }

    [Fact]
    public async Task Отмена_Загрузки_Возвращает_Приглашение()
    {
        using var directory = new TempDirectory();

        var installer = new StubInstaller();
        var model = Model(directory.Path, Manifest("0.2.15"), installer);
        await model.CheckAsync();

        var result = await model.DownloadAndInstallAsync(new CancellationToken(canceled: true));

        // Отмена — не сбой: приглашение остаётся, и попробовать можно снова.
        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateStage.Available, model.Stage);
        Assert.Contains("отменено", model.StatusLine, StringComparison.Ordinal);
        Assert.Null(installer.Installed);
    }

    [Fact]
    public async Task Битый_Файл_Показывает_Причину_Отказа()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15", new string('A', 64)), new StubInstaller());
        await model.CheckAsync();

        var result = await model.DownloadAndInstallAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateStage.Failed, model.Stage);
        Assert.StartsWith("Не удалось: ", model.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void Отмена_Без_Загрузки_Ничего_Не_Меняет()
    {
        using var directory = new TempDirectory();

        var model = Model(directory.Path, Manifest("0.2.15"), new StubInstaller());

        model.Cancel();

        Assert.Equal(UpdateStage.Idle, model.Stage);
    }

    [Fact]
    public async Task Проверка_Недоступна_Без_Службы()
    {
        var model = new UpdateViewModel(null);

        var result = await model.CheckAsync();

        Assert.False(result.IsSuccess);
        Assert.False(model.IsSupported);
        Assert.False(model.IsBannerVisible);
        Assert.Contains("недоступна", model.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Оболочка_Проверяет_Обновление_При_Запуске_Один_Раз()
    {
        using var directory = new TempDirectory();

        var handler = new StubHandler(Manifest("0.2.15"), Payload, offline: false);
        var model = new UpdateViewModel(Service(handler, new StubInstaller(), directory.Path));
        var shell = ShellViewModel.Create(Settings(), null, null, updates: model);

        await shell.CheckUpdatesOnStartAsync();
        await shell.CheckUpdatesOnStartAsync();

        // Проверка при запуске одна: повторный показ вида не должен снова ходить в сеть.
        Assert.Equal(1, handler.ManifestRequests);
        Assert.Equal(UpdateStage.Available, model.Stage);
    }

    /// <summary>Настройки партии для сборки оболочки.</summary>
    /// <returns>Настройки по умолчанию для проверок.</returns>
    private static AppSettings Settings() =>
        AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);

    /// <summary>Собирает модель обновления с подставным HTTP.</summary>
    /// <param name="directory">Каталог для скачанного файла.</param>
    /// <param name="manifest">Манифест обновления.</param>
    /// <param name="installer">Подставной установщик.</param>
    /// <param name="offline">Сеть недоступна.</param>
    /// <returns>Модель приглашения.</returns>
    private static UpdateViewModel Model(string directory, string manifest, StubInstaller installer, bool offline = false) =>
        new(Service(new StubHandler(manifest, Payload, offline), installer, directory));

    /// <summary>Собирает службу обновления на подставном HTTP.</summary>
    /// <param name="handler">Подставной обработчик.</param>
    /// <param name="installer">Подставной установщик.</param>
    /// <param name="directory">Каталог для скачанного файла.</param>
    /// <returns>Служба обновления.</returns>
    /// <remarks>
    /// Текущая версия задана явно: сравнение с выпуском не должно зависеть от того, какой сборкой
    /// оказались тесты — иначе локальная сборка всегда считала бы себя новее выпуска.
    /// </remarks>
    private static UpdateService Service(StubHandler handler, IUpdateInstaller installer, string directory) =>
        new(
            new UpdateChecker(
                new HttpClient(handler),
                new Uri("https://example.org/update.json"),
                new AppVersion(0, 1, 0),
                localBuild: false),
            new UpdateDownloader(new HttpClient(handler)),
            installer,
            directory);

    /// <summary>Собирает манифест обновления для платформы проверок.</summary>
    /// <param name="version">Версия выпуска.</param>
    /// <param name="sha">Контрольная сумма; <c>null</c> — верная для тела обновления.</param>
    /// <returns>Манифест в формате JSON.</returns>
    private static string Manifest(string version, string? sha = null) => $$"""
        {
          "version": "{{version}}",
          "tag": "v{{version}}",
          "publishedAt": "2026-01-01T00:00:00Z",
          "assets": {
            "win-x64": {
              "name": "update.bin",
              "url": "https://example.org/update.bin",
              "size": {{Payload.Length}},
              "sha256": "{{sha ?? Convert.ToHexString(SHA256.HashData(Payload))}}"
            }
          }
        }
        """;

    /// <summary>Временный каталог, который убирается за собой сам.</summary>
    private sealed class TempDirectory : IDisposable
    {
        /// <summary>Создаёт временный каталог.</summary>
        public TempDirectory() => Path = Directory.CreateTempSubdirectory("goengine-update-model").FullName;

        /// <summary>Путь к каталогу.</summary>
        public string Path { get; }

        /// <inheritdoc />
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    /// <summary>Подставной обработчик HTTP: манифест и файл обновления без сети.</summary>
    /// <param name="manifest">Манифест обновления в формате JSON.</param>
    /// <param name="payload">Тело файла обновления.</param>
    /// <param name="offline">Сеть недоступна: запрос падает так же, как без соединения.</param>
    private sealed class StubHandler(string manifest, byte[] payload, bool offline) : HttpMessageHandler
    {
        /// <summary>Сколько раз запрошен манифест: по счётчику видно, что проверка при запуске одна.</summary>
        public int ManifestRequests { get; private set; }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (offline)
            {
                throw new HttpRequestException("сеть недоступна");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("update.json", StringComparison.Ordinal))
            {
                ManifestRequests++;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
        }
    }

    /// <summary>Подставной установщик: запоминает путь и состояние модели в момент установки.</summary>
    private sealed class StubInstaller : IUpdateInstaller
    {
        /// <summary>Путь, который передала служба обновления.</summary>
        public string? Installed { get; private set; }

        /// <summary>Состояние модели в момент установки.</summary>
        public UpdateStage? StageAtInstall { get; private set; }

        /// <summary>Ответ на вопрос «какой этап у модели сейчас»; ставит сам тест.</summary>
        public Func<UpdateStage>? Stage { get; set; }

        /// <inheritdoc />
        public string PlatformKey => "win-x64";

        /// <inheritdoc />
        public Result<string> Install(string filePath)
        {
            Installed = filePath;
            StageAtInstall = Stage?.Invoke();

            return Result<string>.Ok("Обновление установлено.");
        }

        /// <inheritdoc />
        public Result<string> OpenDownloadPage(Uri url) => Result<string>.Ok("Страница загрузки открыта.");
    }
}
