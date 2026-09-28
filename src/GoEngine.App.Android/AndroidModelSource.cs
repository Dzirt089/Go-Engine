using Android.Content.Res;
using GoEngine.App.Services;

namespace GoEngine.App.Android;

/// <summary>Источник моделей из пакета Android: файлы лежат в <c>assets/models</c>.</summary>
/// <remarks>
/// Внутри пакета файлы доступны только на чтение и только через <see cref="AssetManager"/>,
/// поэтому при первом запуске модели копируются в каталог приложения (см. <see cref="AndroidModelStartup"/>).
/// </remarks>
internal sealed class AndroidModelSource : IModelSource
{
    /// <summary>Папка моделей внутри пакета.</summary>
    private const string Folder = "models";

    private readonly AssetManager _assets;

    /// <summary>Создаёт источник.</summary>
    /// <param name="assets">Менеджер ресурсов пакета.</param>
    /// <exception cref="ArgumentNullException">Менеджер ресурсов не задан.</exception>
    public AndroidModelSource(AssetManager assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        _assets = assets;
    }

    /// <inheritdoc />
    public bool Contains(string fileName) => TryOpen(fileName) is not null;

    /// <inheritdoc />
    public Stream Open(string fileName) =>
        TryOpen(fileName) ?? throw new FileNotFoundException($"В пакете нет модели {fileName}.");

    /// <inheritdoc />
    /// <remarks>
    /// Модели лежат в пакете сжатыми: несжатые прибавили бы к APK около 36 МБ (71,7 и 25,6 МБ
    /// против 44,3 и 16,6 МБ в сжатом виде), а выигрыш — только знание длины. Поэтому у сжатого
    /// ресурса длина честно считается неизвестной, и установщик опирается на метку набора:
    /// без неё он копировал бы сотню мегабайт при каждом запуске.
    /// </remarks>
    public long Length(string fileName)
    {
        using var stream = TryOpen(fileName);

        return stream is { CanSeek: true } ? stream.Length : -1;
    }

    /// <summary>Пытается открыть файл модели в пакете.</summary>
    /// <param name="fileName">Имя файла модели.</param>
    /// <returns>Поток или <c>null</c>, если файла нет.</returns>
    private Stream? TryOpen(string fileName)
    {
        try
        {
            return _assets.Open($"{Folder}/{fileName}");
        }
        catch (Java.IO.FileNotFoundException)
        {
            return null;
        }
        catch (Java.IO.IOException)
        {
            return null;
        }
    }
}
