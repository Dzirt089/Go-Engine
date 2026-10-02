using System.Net;
using System.Security.Cryptography;
using System.Text;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Diagnostics;

/// <summary>Проверка приглашения обновления и хода загрузки без сети: HTTP и манифест подставные.</summary>
/// <remarks>
/// Своя единица на свою проверку — как подсчёт территории в <see cref="TerritoryCheck"/>.
/// Режим идёт через ту же модель представления, что и оболочка, поэтому проверяет то, что
/// видит игрок: тихую проверку при запуске, приглашение «доступна версия — у вас версия»,
/// честный ход загрузки по байтам, подпись «Установка…», отмену и отказ. Настоящая сеть и
/// настоящая установка не нужны: манифест и файл отдаёт подставной обработчик, а установщик
/// только запоминает путь. Задачи ожидаются синхронно: проверочный режим — не окно и не может
/// быть асинхронным (<c>async void</c> запрещён, AGENTS.md п. 11).
/// </remarks>
internal static class UpdateCheck
{
    /// <summary>Проверяет приглашение обновления и ход загрузки без сети.</summary>
    /// <returns>0, если состояния, проценты и тексты верны; иначе 1.</returns>
    public static int Run()
    {
        var failures = 0;
        var payload = Encoding.UTF8.GetBytes("файл обновления для проверки");
        var goodSha = Convert.ToHexString(SHA256.HashData(payload));
        // Каталог берётся у того же источника, что и у остальных режимов (Path.GetTempPath),
        // а не у Directory.CreateTempSubdirectory: тот спрашивает систему напрямую и в средах,
        // где пользовательский временный каталог закрыт, валит проверку отказом доступа —
        // а check.ps1 подменяет именно TEMP/TMP (режим --update падал на этом, 2026-10-02).
        var directory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"goengine-check-update-{Guid.NewGuid():N}"));

        // Манифест собирается здесь же: он часть проверки, а не отдельный файл на диске.
        string Manifest(string sha) => $$"""
            {
              "version": "0.2.15",
              "tag": "v0.2.15",
              "publishedAt": "2026-01-01T00:00:00Z",
              "assets": {
                "win-x64": {
                  "name": "update.bin",
                  "url": "https://example.org/update.bin",
                  "size": {{payload.Length}},
                  "sha256": "{{sha}}"
                }
              }
            }
            """;

        // Служба собирается с подставным HTTP: та же связка, что у приложения, но без сети.
        Services.Updates.UpdateService Service(
            string manifest,
            Services.Updates.IUpdateInstaller? installer = null,
            bool offline = false) =>
            new(
                new Services.Updates.UpdateChecker(
                    new HttpClient(new UpdateCheckHandler(manifest, payload, offline)),
                    new Uri("https://example.org/update.json"),
                    new Services.Updates.AppVersion(0, 1, 0),
                    localBuild: false),
                new Services.Updates.UpdateDownloader(new HttpClient(new UpdateCheckHandler(manifest, payload, offline))),
                installer ?? new UpdateCheckInstaller(),
                directory.FullName);

        try
        {
            // 1. Тихая проверка без сети: приглашения нет, причина отказа остаётся в логе.
            var offline = new UpdateViewModel(Service(Manifest(goodSha), offline: true));
            offline.CheckAsync(silent: true).GetAwaiter().GetResult();
            failures += CheckModes.Expect(offline.Stage == UpdateStage.Idle, "отказ сети при запуске не показывается");
            failures += CheckModes.Expect(!offline.IsBannerVisible, "при отказе сети приглашения нет");

            // 2. Проверка нашла версию: приглашение называет обе версии и даёт два варианта.
            var installer = new UpdateCheckInstaller();
            var model = new UpdateViewModel(Service(Manifest(goodSha), installer));
            model.CheckAsync().GetAwaiter().GetResult();
            installer.Stage = () => model.Stage;

            failures += CheckModes.Expect(model.Stage == UpdateStage.Available, "проверка показала приглашение");
            failures += CheckModes.Expect(
                model.Headline.Contains("Доступна версия 0.2.15", StringComparison.Ordinal)
                    && model.Headline.Contains("у вас 0.1.0", StringComparison.Ordinal),
                "в приглашении названы обе версии");
            failures += CheckModes.Expect(model.IsUpdateNowVisible && model.DismissLabel == "Позже", "у приглашения два варианта");

            // 3. Загрузка и установка: проценты доходят до ста, состояния сменяются по порядку.
            var result = model.DownloadAndInstallAsync().GetAwaiter().GetResult();
            failures += CheckModes.Expect(result.IsSuccess, "обновление скачано и передано установщику");
            failures += CheckModes.Expect(model.Percent == 100, "проценты дошли до ста");
            failures += CheckModes.Expect(model.Stage == UpdateStage.Done, "после установки состояние «Готово»");
            failures += CheckModes.Expect(model.Headline == "Готово", "успех называется словом «Готово»");
            failures += CheckModes.Expect(
                installer.StageAtInstall == UpdateStage.Installing,
                "установке предшествует подпись «Установка…»");
            failures += CheckModes.Expect(
                installer.Installed is not null && File.Exists(installer.Installed),
                "установщику передан скачанный файл");

            // 4. «Позже» убирает приглашение до следующего запуска.
            model.Later();
            failures += CheckModes.Expect(!model.IsBannerVisible, "«Позже» убирает приглашение");

            // 5. Отмена — не сбой: приглашение возвращается, и попробовать можно снова.
            var cancelling = new UpdateViewModel(Service(Manifest(goodSha)));
            cancelling.CheckAsync().GetAwaiter().GetResult();
            cancelling.DownloadAndInstallAsync(new CancellationToken(canceled: true)).GetAwaiter().GetResult();
            failures += CheckModes.Expect(cancelling.Stage == UpdateStage.Available, "отмена возвращает приглашение");
            failures += CheckModes.Expect(
                cancelling.StatusLine.Contains("отменено", StringComparison.Ordinal),
                "отмена называется словом, а не сбоем");

            // 6. Битый файл: до установщика дело не доходит, отказ называется причиной.
            var broken = new UpdateViewModel(Service(Manifest(new string('A', 64))));
            broken.CheckAsync().GetAwaiter().GetResult();
            broken.DownloadAndInstallAsync().GetAwaiter().GetResult();
            failures += CheckModes.Expect(broken.Stage == UpdateStage.Failed, "битый файл — отказ");
            failures += CheckModes.Expect(
                broken.Headline.StartsWith("Не удалось: ", StringComparison.Ordinal),
                "отказ начинается со слова «Не удалось»");
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        Console.WriteLine(failures == 0
            ? "Go Engine: приглашение обновления и ход загрузки работают."
            : $"Go Engine: ошибок обновления — {failures}.");

        return failures == 0 ? 0 : 1;
    }

    /// <summary>Подставной обработчик HTTP: отдаёт манифест и файл обновления без сети.</summary>
    /// <param name="manifest">Манифест обновления в формате JSON.</param>
    /// <param name="payload">Тело файла обновления.</param>
    /// <param name="offline">Сеть недоступна: запрос падает так же, как без соединения.</param>
    /// <remarks>
    /// Нужен потому, что <c>HttpMessageHandler</c> — абстрактный класс, а подставить его лямбдой
    /// нельзя. Настоящий адрес при этом не важен: обработчик отвечает по имени файла в пути.
    /// </remarks>
    private sealed class UpdateCheckHandler(string manifest, byte[] payload, bool offline) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (offline)
            {
                throw new HttpRequestException("сеть недоступна");
            }

            var body = request.RequestUri!.AbsolutePath.EndsWith("update.json", StringComparison.Ordinal)
                ? new StringContent(manifest)
                : new ByteArrayContent(payload);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
        }
    }

    /// <summary>Подставной установщик: ничего не ставит, а запоминает путь и этап модели.</summary>
    /// <remarks>
    /// Настоящая установка в проверочном режиме недопустима: <c>DesktopUpdateInstaller</c> запускает
    /// установщик Windows и закрывает приложение. Поэтому проверяется только то, что служба довела
    /// дело до установщика и сделала это в состоянии «Установка…».
    /// </remarks>
    private sealed class UpdateCheckInstaller : Services.Updates.IUpdateInstaller
    {
        /// <summary>Путь, который передала служба обновления.</summary>
        public string? Installed { get; private set; }

        /// <summary>Состояние модели в момент установки: им проверяется подпись «Установка…».</summary>
        public UpdateStage? StageAtInstall { get; private set; }

        /// <summary>Ответ на вопрос «какой этап у модели сейчас»: ставит сам проверочный режим.</summary>
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
