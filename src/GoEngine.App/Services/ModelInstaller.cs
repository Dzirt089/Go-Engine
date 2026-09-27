namespace GoEngine.App.Services;

/// <summary>Что получилось при установке моделей в каталог приложения.</summary>
/// <param name="Directory">Каталог, в который установлены модели.</param>
/// <param name="Installed">Файлы, скопированные в этот раз.</param>
/// <param name="Skipped">Файлы, которые уже были на месте нужного размера.</param>
/// <param name="Missing">Файлы, которых не оказалось в источнике.</param>
public readonly record struct ModelInstallResult(
    string Directory,
    IReadOnlyList<string> Installed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Missing)
{
    /// <summary>Есть ли в каталоге хотя бы одна модель.</summary>
    public bool HasAny => Installed.Count > 0 || Skipped.Count > 0;
}

/// <summary>Установка файлов моделей из источника в каталог, доступный приложению.</summary>
/// <remarks>
/// На Android файлы моделей лежат внутри пакета и доступны только на чтение, а приложение
/// ищет их рядом с собой в каталоге `models`. Поэтому при первом запуске модели копируются
/// в каталог приложения, а при следующих — только если файла нет или он другого размера.
/// Логика намеренно не знает ни про Android, ни про ONNX: и то и другое подставляется снаружи,
/// и её можно проверить тестами (`DECISIONS.md`, D-039).
/// </remarks>
public sealed class ModelInstaller
{
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
    /// <returns>Что скопировано, что пропущено и чего не нашлось.</returns>
    /// <exception cref="ArgumentNullException">Список имён или каталог не заданы.</exception>
    /// <exception cref="IOException">Не удалось записать файл в каталог назначения.</exception>
    public ModelInstallResult Install(IReadOnlyList<string> fileNames, string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        _ = Directory.CreateDirectory(targetDirectory);

        var installed = new List<string>();
        var skipped = new List<string>();
        var missing = new List<string>();

        foreach (var fileName in fileNames)
        {
            if (!_source.Contains(fileName))
            {
                missing.Add(fileName);
                continue;
            }

            var target = Path.Combine(targetDirectory, fileName);

            if (IsUpToDate(target, fileName))
            {
                skipped.Add(fileName);
                continue;
            }

            Copy(fileName, target);
            installed.Add(fileName);
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

    /// <summary>Копирует файл источника в каталог назначения.</summary>
    /// <param name="fileName">Имя файла в источнике.</param>
    /// <param name="target">Путь назначения.</param>
    private void Copy(string fileName, string target)
    {
        using var source = _source.Open(fileName);
        using var destination = File.Create(target);

        source.CopyTo(destination);
    }
}
