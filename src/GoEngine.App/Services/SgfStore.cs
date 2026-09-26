using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Чтение и запись партии в файл SGF.</summary>
/// <remarks>
/// Файловый ввод-вывод живёт в слое <c>App</c> (<c>AGENTS.md</c>, п. 1): <c>Core</c> знает
/// только формат. Ошибки чтения и записи — ожидаемый исход, поэтому возвращается
/// <see cref="Result{T}"/>, а не исключение.
/// </remarks>
public static class SgfStore
{
    /// <summary>Расширение файлов партий.</summary>
    public const string Extension = "sgf";

    /// <summary>Записывает партию в файл.</summary>
    /// <param name="game">Партия.</param>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>Успех или причина отказа словами.</returns>
    public static Result Save(SgfGame game, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            File.WriteAllText(path, SgfWriter.Write(game));

            return Result.Ok();
        }
        catch (IOException exception)
        {
            return Result.Fail($"Не удалось записать партию: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Fail($"Нет доступа к файлу партии: {exception.Message}");
        }
    }

    /// <summary>Читает партию из файла.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>Партия или причина отказа.</returns>
    public static Result<SgfGame> Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return Result<SgfGame>.Fail($"Файл партии не найден: {path}");
        }

        try
        {
            return SgfReader.Read(File.ReadAllText(path));
        }
        catch (IOException exception)
        {
            return Result<SgfGame>.Fail($"Не удалось прочитать партию: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<SgfGame>.Fail($"Нет доступа к файлу партии: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            // Пустой файл: формат требует хотя бы один узел.
            return Result<SgfGame>.Fail($"Файл партии пуст: {exception.Message}");
        }
    }
}