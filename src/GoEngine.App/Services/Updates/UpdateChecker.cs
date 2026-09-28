using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Проверка обновления: читает манифест и сравнивает версии.</summary>
/// <remarks>
/// Отсутствие сети, таймаут и битый манифест — ожидаемые исходы (<see cref="Result{T}"/>),
/// а не исключения: игрок без интернета не должен видеть падение.
/// </remarks>
public sealed class UpdateChecker
{
    private readonly HttpClient _http;
    private readonly AppVersion _current;
    private readonly bool _localBuild;

    /// <summary>Создаёт проверку обновлений.</summary>
    /// <param name="http">Клиент HTTP; время ожидания задаёт вызывающий.</param>
    /// <param name="manifestUrl">Адрес манифеста.</param>
    /// <param name="current">Текущая версия; <c>null</c> — версия запущенной сборки.</param>
    /// <param name="localBuild">
    /// Сборка без номера версии; <c>null</c> — определить по версии запущенной сборки.
    /// </param>
    /// <remarks>
    /// Локальная сборка получает заводской номер <c>1.0.0</c>, и сравнивать его с выпусками
    /// бессмысленно: она всегда «новее» любого выпуска. Поэтому такая сборка честно сообщает,
    /// что сравнивать не с чем, а не показывает ложное «обновлений нет».
    /// </remarks>
    public UpdateChecker(HttpClient http, Uri? manifestUrl = null, AppVersion? current = null, bool? localBuild = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        ManifestUrl = manifestUrl ?? DefaultManifestUrl;
        _current = current ?? AppVersion.Current;
        _localBuild = localBuild ?? (current is null && AppVersion.IsLocalBuild);
    }

    /// <summary>Сборка без номера версии: сравнивать её с выпусками нечем.</summary>
    public bool IsLocalBuild => _localBuild;

    /// <summary>Постоянный адрес манифеста: плавающий релиз <c>latest</c>.</summary>
    /// <remarks>
    /// Адрес не меняется от выпуска к выпуску, поэтому приложение знает его заранее.
    /// Файлы внутри манифеста ссылаются на версионированный тег: скачивание не «переедет»
    /// на другую сборку посреди загрузки.
    /// </remarks>
    public static Uri DefaultManifestUrl { get; } = new("https://github.com/Dzirt089/Go-Engine/releases/download/latest/update.json");

    /// <summary>Адрес манифеста, которым пользуется проверка.</summary>
    public Uri ManifestUrl { get; }

    /// <summary>Версия, с которой сравнивается выпуск.</summary>
    public AppVersion Current => _current;

    /// <summary>Проверяет, есть ли новая версия.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Результат проверки или причина отказа.</returns>
    public async Task<Result<UpdateCheck>> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_localBuild)
        {
            return Result<UpdateCheck>.Fail(
                $"Это локальная сборка ({_current}): её собирали не для выпуска, сравнивать версии нечем.");
        }

        try
        {
            using var response = await _http.GetAsync(ManifestUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<UpdateCheck>.Fail($"Сервер обновлений ответил кодом {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var manifest = UpdateManifest.Parse(json);

            if (!manifest.IsSuccess)
            {
                return Result<UpdateCheck>.Fail(manifest.Error!);
            }

            var parsed = manifest.Value!;
            var status = parsed.Version > _current ? UpdateStatus.Available : UpdateStatus.UpToDate;

            return Result<UpdateCheck>.Ok(new UpdateCheck(status, parsed));
        }
        catch (HttpRequestException exception)
        {
            return Result<UpdateCheck>.Fail($"Нет связи с сервером обновлений: {exception.Message}");
        }
        catch (TaskCanceledException)
        {
            return Result<UpdateCheck>.Fail("Сервер обновлений не ответил вовремя.");
        }
    }
}
