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
    public async Task<Result<string>> DownloadAsync(UpdateAsset asset, string directory, CancellationToken cancellationToken = default)
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

                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = File.Create(temporary);
                await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
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
        catch (TaskCanceledException)
        {
            Delete(temporary);

            return Result<string>.Fail("Загрузка обновления прервана.");
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
