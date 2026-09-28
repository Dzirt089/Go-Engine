namespace GoEngine.App.Services;

/// <summary>Что получилось при установке моделей в каталог приложения.</summary>
/// <param name="Directory">Каталог, в который установлены модели.</param>
/// <param name="Installed">Файлы, скопированные в этот раз.</param>
/// <param name="Skipped">Файлы, которые уже были на месте и не требовали копирования.</param>
/// <param name="Missing">Файлы, которых не оказалось в источнике.</param>
public readonly record struct ModelInstallResult(
    string Directory,
    IReadOnlyList<string> Installed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Missing)
{
    /// <summary>Есть ли в каталоге хотя бы одна модель.</summary>
    public bool HasAny => Installed.Count > 0 || Skipped.Count > 0;

    /// <summary>Все ли запрошенные модели на месте.</summary>
    public bool IsComplete => Missing.Count == 0 && (Installed.Count > 0 || Skipped.Count > 0);
}

/// <summary>Установка файлов моделей из источника в каталог, доступный приложению.</summary>
/// <remarks>
/// На Android файлы моделей лежат внутри пакета и доступны только на чтение, а приложение
/// ищет их рядом с собой в каталоге `models`. Поэтому при первом запуске модели копируются
/// в каталог приложения, а при следующих — только если файла нет или он другого размера.
/// Логика намеренно не знает ни про Android, ни про ONNX: и то и другое подставляется снаружи,
/// и её можно проверить тестами (`DECISIONS.md`, D-039).
/// Отдельно про метку установки: у сжатого ресурса Android длина потока неизвестна, и сравнивать
/// копию с источником нечем — тогда файл копировался бы при каждом запуске (это сотня мегабайт
/// в потоке интерфейса и жалоба «моделей не видно»). Метка — строка о наборе моделей (например,
/// версия сборки): пока она совпадает, копирование не повторяется.
/// </remarks>
public sealed class ModelInstaller
{
    /// <summary>Имя файла метки установки: лежит в каталоге моделей и описывает набор.</summary>
    private const string MarkerFileName = ".installed";

    private readonly IModelSource _source;

    /// <summary>Создаёт установщик.</summary>
    /// <param name="source">Источник файлов моделей.</param>
    /// <exception cref="ArgumentNullException">Источник не задан.</exception>
    public ModelInstaller(IModelSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
    }

    /// <summary>Устанавливает перечисленные модели в каталог.</summary>
    /// <param name="fileNames">Имена файлов моделей.</param>
    /// <param name="targetDirectory">Каталог назначения; создаётся при необходимости.</param>
    /// <param name="marker">
    /// Метка набора моделей (например, версия сборки); <c>null</c> — метку не использовать.
    /// </param>
    /// <returns>Что скопировано, что пропущено и чего не нашлось.</returns>
    /// <exception cref="ArgumentNullException">Список имён или каталог не заданы.</exception>
    /// <exception cref="IOException">Не удалось записать файл или копия не совпала с источником.</exception>
    public ModelInstallResult Install(IReadOnlyList<string> fileNames, string targetDirectory, string? marker = null)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        _ = Directory.CreateDirectory(targetDirectory);

        var installed = new List<string>();
        var skipped = new List<string>();
        var missing = new List<string>();

        // Метка совпала и все файлы на месте — набор уже установлен, копировать нечего.
        if (marker is not null && IsMarkerCurrent(targetDirectory, marker, fileNames))
        {
            return new ModelInstallResult(targetDirectory, [], [.. fileNames], []);
        }

        foreach (var fileName in fileNames)
        {
            if (!_source.Contains(fileName))
            {
                missing.Add(fileName);
                continue;
            }

            var target = Path.Combine(targetDirectory, fileName);

            if (marker is null && IsUpToDate(target, fileName))
            {
                skipped.Add(fileName);
                continue;
            }

            Copy(fileName, target);
            installed.Add(fileName);
        }

        if (missing.Count == 0)
        {
            WriteMarker(targetDirectory, marker, fileNames);
        }

        return new ModelInstallResult(targetDirectory, installed, skipped, missing);
    }

    /// <summary>Проверяет, что файл уже на месте и того же размера.</summary>
    /// <param name="target">Путь к установленному файлу.</param>
    /// <param name="fileName">Имя файла в источнике.</param>
    /// <returns><c>true</c>, если копировать не нужно.</returns>
    private bool IsUpToDate(string target, string fileName)
    {
        var expected = _source.Length(fileName);

        if (expected < 0 || !File.Exists(target))
        {
            return false;
        }

        return new FileInfo(target).Length == expected;
    }

    /// <summary>Проверяет метку установки и целостность набора.</summary>
    /// <param name="directory">Каталог моделей.</param>
    /// <param name="marker">Ожидаемая метка.</param>
    /// <param name="fileNames">Имена файлов набора.</param>
    /// <returns><c>true</c>, если набор уже установлен и метка совпадает.</returns>
    /// <remarks>
    /// Размеры файлов записываются в метку при установке: файл мог не докачаться (приложение
    /// закрыли посреди копирования), и тогда набор ставится заново, а не считается готовым.
    /// </remarks>
    private static bool IsMarkerCurrent(string directory, string marker, IReadOnlyList<string> fileNames)
    {
        var path = Path.Combine(directory, MarkerFileName);

        if (!File.Exists(path))
        {
            return false;
        }

        string[] lines;

        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (IOException)
        {
            return false;
        }

        if (lines.Length < fileNames.Count + 1 || lines[0] != marker)
        {
            return false;
        }

        for (var index = 0; index < fileNames.Count; index++)
        {
            var parts = lines[index + 1].Split('\t');
            var file = Path.Combine(directory, fileNames[index]);

            if (parts.Length != 2 || parts[0] != fileNames[index] || !File.Exists(file))
            {
                return false;
            }

            if (!long.TryParse(parts[1], out var size) || new FileInfo(file).Length != size)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Записывает метку установленного набора.</summary>
    /// <param name="directory">Каталог моделей.</param>
    /// <param name="marker">Метка; <c>null</c> — метку не пишем.</param>
    /// <param name="fileNames">Имена файлов набора.</param>
    private static void WriteMarker(string directory, string? marker, IReadOnlyList<string> fileNames)
    {
        if (marker is null)
        {
            return;
        }

        List<string> lines = [marker];

        foreach (var fileName in fileNames)
        {
            var file = Path.Combine(directory, fileName);
            var size = File.Exists(file) ? new FileInfo(file).Length : -1;

            lines.Add($"{fileName}\t{size}");
        }

        File.WriteAllLines(Path.Combine(directory, MarkerFileName), lines);
    }

    /// <summary>Копирует файл источника в каталог назначения.</summary>
    /// <param name="fileName">Имя файла в источнике.</param>
    /// <param name="target">Путь назначения.</param>
    /// <exception cref="IOException">Скопировано меньше байт, чем обещал источник.</exception>
    private void Copy(string fileName, string target)
    {
        var expected = _source.Length(fileName);
        long written;

        using (var source = _source.Open(fileName))
        {
            using var destination = File.Create(target);

            source.CopyTo(destination);
            written = destination.Length;
        }

        if (expected >= 0 && written != expected)
        {
            File.Delete(target);

            throw new IOException($"Модель {fileName} скопирована не полностью: {written} из {expected} байт.");
        }
    }
}
