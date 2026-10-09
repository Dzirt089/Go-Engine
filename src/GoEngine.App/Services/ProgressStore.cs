namespace GoEngine.App.Services;

using GoEngine.Core;

/// <summary>Чтение и запись прогресса обучения.</summary>
/// <remarks>
/// Прогресс лежит отдельным файлом рядом с настройками партии: он принадлежит обучению, а не партии,
/// и не должен сбрасываться при правке настроек на доске (то же решение, что у оформления).
/// </remarks>
public static class ProgressStore
{
    /// <summary>Имя файла прогресса.</summary>
    private const string FileName = "progress.json";

    /// <summary>Возвращает путь к файлу прогресса в профиле пользователя.</summary>
    public static string DefaultPath => Path.Combine(AppDataPaths.Root, FileName);

    /// <summary>Читает прогресс из файла.</summary>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Прогресс или пустой прогресс, если файла нет или он не разбирается.</returns>
    public static StudyProgress Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        if (!File.Exists(file))
        {
            return StudyProgress.Empty;
        }

        try
        {
            return StudyProgress.Parse(File.ReadAllText(file));
        }
        catch (IOException)
        {
            return StudyProgress.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return StudyProgress.Empty;
        }
    }

    /// <summary>Записывает прогресс в файл.</summary>
    /// <param name="progress">Прогресс обучения.</param>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Успех или причина отказа словами.</returns>
    /// <remarks>
    /// Отказ записи не мешает учиться: игрок теряет только отметки, и об этом ему говорит лог.
    /// </remarks>
    public static Result Save(StudyProgress progress, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var file = path ?? DefaultPath;

        try
        {
            var folder = Path.GetDirectoryName(file);

            if (!string.IsNullOrEmpty(folder))
            {
                _ = Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, progress.ToJson());

            return Result.Ok();
        }
        catch (IOException exception)
        {
            return Result.Fail($"Не удалось сохранить прогресс обучения: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail($"Нет доступа к файлу прогресса обучения: {exception.Message}");
        }
    }
}
