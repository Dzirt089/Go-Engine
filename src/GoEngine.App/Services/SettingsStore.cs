using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Чтение и запись настроек партии.</summary>
/// <remarks>
/// Ошибки ввода-вывода — ожидаемый исход, а не нарушение инварианта (<c>AGENTS.md</c>, п. 4):
/// отсутствие или порча файла возвращают настройки по умолчанию, а не исключение.
/// </remarks>
public static class SettingsStore
{
    /// <summary>Имя файла настроек.</summary>
    private const string FileName = "settings.json";

    /// <summary>Имя папки настроек в профиле пользователя.</summary>
    private const string FolderName = "GoEngine";

    /// <summary>Возвращает путь к файлу настроек в профиле пользователя.</summary>
    /// <returns>Полный путь к файлу.</returns>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        FolderName,
        FileName);

    /// <summary>Читает настройки из файла.</summary>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Настройки: из файла, иначе значения по умолчанию.</returns>
    public static AppSettings Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        if (!File.Exists(file))
        {
            return AppSettings.Default;
        }

        try
        {
            return AppSettings.Parse(File.ReadAllText(file)) ?? AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    /// <summary>Записывает настройки в файл.</summary>
    /// <param name="settings">Настройки.</param>
    /// <param name="path">Путь к файлу; <c>null</c> — путь по умолчанию.</param>
    /// <returns>Успех или причина отказа словами.</returns>
    public static Result Save(AppSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var file = path ?? DefaultPath;

        try
        {
            var folder = Path.GetDirectoryName(file);

            if (!string.IsNullOrEmpty(folder))
            {
                _ = Directory.CreateDirectory(folder);
            }

            File.WriteAllText(file, settings.ToJson());

            return Result.Ok();
        }
        catch (IOException exception)
        {
            return Result.Fail($"Не удалось сохранить настройки: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail($"Нет доступа к файлу настроек: {exception.Message}");
        }
    }
}
