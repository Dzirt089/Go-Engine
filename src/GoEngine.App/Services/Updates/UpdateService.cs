using System.Globalization;
using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Проверка, загрузка и установка обновления — одним действием для интерфейса.</summary>
public sealed class UpdateService
{
    /// <summary>Общий клиент HTTP: один на приложение, соединения переиспользуются.</summary>
    private static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly UpdateChecker _checker;
    private readonly UpdateDownloader _downloader;
    private readonly IUpdateInstaller _installer;
    private readonly string _downloadDirectory;

    /// <summary>Создаёт службу обновления.</summary>
    /// <param name="checker">Проверка версии.</param>
    /// <param name="downloader">Загрузка файла.</param>
    /// <param name="installer">Установка средствами платформы.</param>
    /// <param name="downloadDirectory">Каталог для скачанных файлов.</param>
    public UpdateService(UpdateChecker checker, UpdateDownloader downloader, IUpdateInstaller installer, string downloadDirectory)
    {
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadDirectory);

        _checker = checker;
        _downloader = downloader;
        _installer = installer;
        _downloadDirectory = downloadDirectory;
    }

    /// <summary>Версия запущенной сборки.</summary>
    public AppVersion Current => _checker.Current;

    /// <summary>Ключ платформы, для которой берётся файл обновления.</summary>
    public string PlatformKey => _installer.PlatformKey;

    /// <summary>Создаёт службу для текущей платформы.</summary>
    /// <param name="installer">Установщик платформы.</param>
    /// <param name="downloadDirectory">Каталог для скачанных файлов.</param>
    /// <returns>Служба обновления.</returns>
    public static UpdateService CreateDefault(IUpdateInstaller installer, string downloadDirectory)
    {
        ArgumentNullException.ThrowIfNull(installer);

        // Таймаут задан у общего клиента: проверка не должна подвешивать окно.
        return new UpdateService(new UpdateChecker(Shared), new UpdateDownloader(Shared), installer, downloadDirectory);
    }

    /// <summary>Проверяет обновление.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Результат проверки или причина отказа.</returns>
    public Task<Result<UpdateCheck>> CheckAsync(CancellationToken cancellationToken = default) =>
        _checker.CheckAsync(cancellationToken);

    /// <summary>Скачивает и устанавливает обновление.</summary>
    /// <param name="check">Результат проверки, в котором нашлось обновление.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    public async Task<Result<string>> DownloadAndInstallAsync(UpdateCheck check, CancellationToken cancellationToken = default)
    {
        var asset = check.Manifest.AssetFor(_installer.PlatformKey);

        if (!asset.IsSuccess)
        {
            return Result<string>.Fail(asset.Error!);
        }

        var downloaded = await _downloader.DownloadAsync(asset.Value!, _downloadDirectory, cancellationToken).ConfigureAwait(false);

        if (!downloaded.IsSuccess)
        {
            return Result<string>.Fail(downloaded.Error!);
        }

        return _installer.Install(downloaded.Value!);
    }

    /// <summary>Открывает страницу выпуска: запасной путь, когда установка из приложения невозможна.</summary>
    /// <param name="check">Результат проверки, в котором нашлось обновление.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    public Result<string> OpenDownloadPage(UpdateCheck check)
    {
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://github.com/Dzirt089/Go-Engine/releases/tag/{check.Manifest.Tag}");

        return _installer.OpenDownloadPage(new Uri(url));
    }
}
