using System.Text;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Чтение и запись партии в SGF: файлом или потоком.</summary>
/// <remarks>
/// Файловый ввод-вывод живёт в слое <c>App</c> (<c>AGENTS.md</c>, п. 1): <c>Core</c> знает
/// только формат. Ошибки чтения и записи — ожидаемый исход, поэтому возвращается
/// <see cref="Result{T}"/>, а не исключение.
/// Потоковые перегрузки нужны мобильным системам: там выбранный файл — это поток, локального
/// пути у него может не быть вовсе, а <c>TryGetLocalPath()</c> возвращает <c>null</c>.
/// Путевые перегрузки оставлены для настольных систем и тестов и работают через потоки,
/// поэтому правило разбора и записи в проекте одно.
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
            using var stream = File.Create(path);

            return Save(game, stream);
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

    /// <summary>Записывает партию в поток.</summary>
    /// <param name="game">Партия.</param>
    /// <param name="stream">Поток для записи; остаётся открытым.</param>
    /// <returns>Успех или причина отказа словами.</returns>
    public static Result Save(SgfGame game, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            // Поток не закрываем: им распоряжается вызывающий (файл из диалога выбора файла).
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);

            writer.Write(SgfWriter.Write(game));
            writer.Flush();

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
        catch (NotSupportedException exception)
        {
            // Поток открыт только для чтения: запись в него невозможна.
            return Result.Fail($"Поток не годится для записи партии: {exception.Message}");
        }
        catch (ArgumentException exception)
        {
            return Result.Fail($"Поток не годится для записи партии: {exception.Message}");
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
            using var stream = File.OpenRead(path);

            return Load(stream);
        }
        catch (IOException exception)
        {
            return Result<SgfGame>.Fail($"Не удалось прочитать партию: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result<SgfGame>.Fail($"Нет доступа к файлу партии: {exception.Message}");
        }
    }

    /// <summary>Читает партию из потока.</summary>
    /// <param name="stream">Поток с содержимым файла; остаётся открытым.</param>
    /// <returns>Партия или причина отказа.</returns>
    public static Result<SgfGame> Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            return SgfReader.Read(reader.ReadToEnd());
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
            // Пустой поток: формат требует хотя бы один узел.
            return Result<SgfGame>.Fail($"Файл партии пуст: {exception.Message}");
        }
    }
}
