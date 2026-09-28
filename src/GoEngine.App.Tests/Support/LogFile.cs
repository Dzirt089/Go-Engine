namespace GoEngine.App.Tests;

/// <summary>Чтение файлов лога так, как это делает само приложение.</summary>
/// <remarks>
/// Писатель держит суточный файл открытым на запись, поэтому обычное <c>File.ReadAllLines</c>
/// с доступом «только чтение» получает отказ. Тесты читают файл с общим доступом на чтение
/// и запись — ровно так, как его читает перехват падений при копировании.
/// </remarks>
internal static class LogFile
{
    /// <summary>Читает все строки файла, не мешая писателю.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>Строки файла.</returns>
    public static string[] Lines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        List<string> lines = [];

        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return [.. lines];
    }

    /// <summary>Читает весь текст файла, не мешая писателю.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <returns>Содержимое файла.</returns>
    public static string Text(string path) => string.Join(Environment.NewLine, Lines(path));
}
