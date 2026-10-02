using System.Globalization;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using Microsoft.Extensions.Logging;

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

    /// <summary>Логгер обновлений: проверка ходит в сеть, и её отказ должен быть виден в логе.</summary>
    private readonly ILogger _log = AppLog.For<UpdateService>();

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
    public async Task<Result<UpdateCheck>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var result = await _checker.CheckAsync(cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            AppLogMessages.UpdateFailed(_log, result.Error ?? "причина неизвестна");

            return result;
        }

        var check = result.Value!;

        if (check.IsAvailable)
        {
            AppLogMessages.UpdateChecked(_log, AppVersion.Display, "доступна версия " + check.Version);
            AppLogMessages.UpdateAvailable(_log, check.Version.ToString(), AssetSize(check));
        }
        else
        {
            AppLogMessages.UpdateChecked(_log, AppVersion.Display, "установлена последняя версия");
        }

        return result;
    }

    /// <summary>Размер файла обновления для платформы: нужен для строки лога.</summary>
    /// <param name="check">Результат проверки.</param>
    /// <returns>Размер в байтах или 0, если файла для платформы нет.</returns>
    private long AssetSize(UpdateCheck check)
    {
        var asset = check.Manifest.AssetFor(_installer.PlatformKey);

        return asset.IsSuccess ? asset.Value!.Size : 0;
    }

    /// <summary>Скачивает файл обновления для платформы, сообщая о ходе по байтам.</summary>
    /// <param name="check">Результат проверки, в котором нашлось обновление.</param>
    /// <param name="progress">Отчёт о ходе загрузки; <c>null</c> — без отчётов.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Полный путь к скачанному файлу или причина отказа.</returns>
    /// <remarks>
    /// Загрузка отделена от установки ради подписи состояния: интерфейсу нужно показать
    /// «Скачивание… N%», а затем «Установка…», и момент между ними виден только здесь.
    /// Обычный путь для тех, кому это не нужно, — <see cref="DownloadAndInstallAsync(UpdateCheck, CancellationToken)"/>.
    /// </remarks>
    public async Task<Result<string>> DownloadAsync(
        UpdateCheck check,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var asset = check.Manifest.AssetFor(_installer.PlatformKey);

        if (!asset.IsSuccess)
        {
            return Result<string>.Fail(asset.Error!);
        }

        var downloaded = await _downloader
            .DownloadAsync(asset.Value!, _downloadDirectory, progress, cancellationToken)
            .ConfigureAwait(false);

        if (!downloaded.IsSuccess)
        {
            AppLogMessages.UpdateFailed(_log, downloaded.Error ?? "загрузка не удалась");

            return Result<string>.Fail(downloaded.Error!);
        }

        AppLogMessages.UpdateDownloaded(_log, check.Version.ToString(), downloaded.Value!);

        return downloaded;
    }

    /// <summary>Устанавливает скачанный файл средствами платформы.</summary>
    /// <param name="filePath">Полный путь к скачанному файлу.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    /// <remarks>
    /// Метод синхронный: установщик только запускает установку средствами системы и сразу
    /// возвращает слово игроку. На Android после этого приложение уходит в системный установщик —
    /// это нормальный ход событий, а не сбой.
    /// </remarks>
    public Result<string> Install(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var installed = _installer.Install(filePath);

        if (!installed.IsSuccess)
        {
            AppLogMessages.UpdateFailed(_log, installed.Error ?? "установка не удалась");
        }

        return installed;
    }

    /// <summary>Скачивает и устанавливает обновление.</summary>
    /// <param name="check">Результат проверки, в котором нашлось обновление.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    public Task<Result<string>> DownloadAndInstallAsync(UpdateCheck check, CancellationToken cancellationToken = default) =>
        DownloadAndInstallAsync(check, null, cancellationToken);

    /// <summary>Скачивает и устанавливает обновление, сообщая о ходе загрузки.</summary>
    /// <param name="check">Результат проверки, в котором нашлось обновление.</param>
    /// <param name="progress">Отчёт о ходе загрузки; <c>null</c> — без отчётов.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    /// <remarks>Связка двух шагов: <see cref="DownloadAsync"/> и <see cref="Install"/>.</remarks>
    public async Task<Result<string>> DownloadAndInstallAsync(
        UpdateCheck check,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var downloaded = await DownloadAsync(check, progress, cancellationToken).ConfigureAwait(false);

        return downloaded.IsSuccess
            ? Install(downloaded.Value!)
            : Result<string>.Fail(downloaded.Error!);
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
