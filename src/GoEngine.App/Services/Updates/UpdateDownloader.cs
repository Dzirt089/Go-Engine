using System.Globalization;
using System.Security.Cryptography;
using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Загрузка файла обновления с проверкой размера и SHA-256.</summary>
/// <remarks>
/// Файл пишется с расширением <c>.part</c> и переименовывается только после проверки: иначе
/// оборванная загрузка оставила бы файл, похожий на готовый. При несовпадении хеша файл удаляется.
/// </remarks>
public sealed class UpdateDownloader
{
    /// <summary>Размер порции чтения: 80 КиБ — компромисс между частотой отчётов и памятью.</summary>
    /// <remarks>
    /// Порция задана здесь, а не взята у <see cref="Stream.CopyToAsync(Stream, CancellationToken)"/>:
    /// копирование «целиком» не даёт промежуточных чисел, а полосе хода нужен честный ход по байтам.
    /// </remarks>
    private const int BufferSize = 81920;

    private readonly HttpClient _http;

    /// <summary>Создаёт загрузчик.</summary>
    /// <param name="http">Клиент HTTP.</param>
    public UpdateDownloader(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
    }

    /// <summary>Скачивает файл обновления в каталог.</summary>
    /// <param name="asset">Файл из манифеста.</param>
    /// <param name="directory">Каталог, куда сохранить файл.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Полный путь к скачанному файлу или причина отказа.</returns>
    /// <remarks>Вызов без отчёта о ходе: прежний путь остаётся и просто делегирует новой перегрузке.</remarks>
    public Task<Result<string>> DownloadAsync(UpdateAsset asset, string directory, CancellationToken cancellationToken = default) =>
        DownloadAsync(asset, directory, null, cancellationToken);

    /// <summary>Скачивает файл обновления в каталог, сообщая о ходе по байтам.</summary>
    /// <param name="asset">Файл из манифеста.</param>
    /// <param name="directory">Каталог, куда сохранить файл.</param>
    /// <param name="progress">Отчёт о ходе загрузки; <c>null</c> — без отчётов.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Полный путь к скачанному файлу или причина отказа.</returns>
    /// <remarks>
    /// Отчёт вызывается из того потока, где закончилось чтение порции (загрузка идёт с
    /// <c>ConfigureAwait(false)</c>), поэтому перенос в поток интерфейса — забота вызывающего:
    /// для этого и существует <see cref="Progress{T}"/>.
    /// </remarks>
    public async Task<Result<string>> DownloadAsync(
        UpdateAsset asset,
        string directory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var target = Path.Combine(directory, asset.Name);
        var temporary = target + ".part";

        try
        {
            Directory.CreateDirectory(directory);

            using (var response = await _http
                .GetAsync(new Uri(asset.Url), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return Result<string>.Fail($"Не удалось скачать обновление: сервер ответил кодом {(int)response.StatusCode}.");
                }

                // Размер из манифеста надёжнее длины ответа: сервер вправе её не называть.
                var total = asset.Size > 0 ? asset.Size : response.Content.Headers.ContentLength ?? 0;

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = File.Create(temporary);

                var received = await CopyAsync(source, destination, total, progress, cancellationToken).ConfigureAwait(false);

                // Последний отчёт — с полным числом записанных байт: полоса доходит до конца даже
                // тогда, когда сервер назвал длину неверно, а расхождение поймает проверка размера.
                progress?.Report(new DownloadProgress(received, total));
            }

            var length = new FileInfo(temporary).Length;

            if (asset.Size > 0 && length != asset.Size)
            {
                Delete(temporary);

                return Result<string>.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Размер файла не совпал: ожидалось {asset.Size} байт, получено {length}."));
            }

            var hash = await ComputeHashAsync(temporary, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(hash, asset.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Delete(temporary);

                return Result<string>.Fail($"SHA-256 не совпал: ожидался {asset.Sha256}, получен {hash}. Файл удалён.");
            }

            File.Move(temporary, target, overwrite: true);

            return Result<string>.Ok(target);
        }
        catch (HttpRequestException exception)
        {
            Delete(temporary);

            return Result<string>.Fail($"Нет связи при загрузке обновления: {exception.Message}");
        }
        catch (IOException exception)
        {
            Delete(temporary);

            return Result<string>.Fail($"Не удалось сохранить обновление: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Delete(temporary);

            return Result<string>.Fail($"Нет доступа к каталогу загрузки: {exception.Message}");
        }
        catch (OperationCanceledException)
        {
            // Отмена приходит и как TaskCanceledException (запрос), и как OperationCanceledException
            // (чтение потока): для игрока это одно и то же — он сам остановил загрузку.
            Delete(temporary);

            return Result<string>.Fail("Загрузка обновления прервана.");
        }
    }

    /// <summary>Переписывает ответ в файл порциями, сообщая о ходе.</summary>
    /// <param name="source">Поток ответа.</param>
    /// <param name="destination">Файл, куда пишется обновление.</param>
    /// <param name="total">Ожидаемый размер файла; <c>0</c> — неизвестен.</param>
    /// <param name="progress">Отчёт о ходе; <c>null</c> — без отчётов.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сколько байт записано.</returns>
    private static async Task<long> CopyAsync(
        Stream source,
        Stream destination,
        long total,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        long received = 0;

        // Нулевой отчёт нужен полосе хода: без него она появляется только после первой порции.
        progress?.Report(new DownloadProgress(received, total));

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                return received;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            received += read;
            progress?.Report(new DownloadProgress(received, total));
        }
    }

    /// <summary>Считает SHA-256 файла.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Хеш в верхнем регистре.</returns>
    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Удаляет недокачанный файл, не мешая основному исходу.</summary>
    /// <param name="path">Путь к файлу.</param>
    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Удаление временного файла — уборка: если не вышло, причина отказа уже сформирована.
        }
    }
}
