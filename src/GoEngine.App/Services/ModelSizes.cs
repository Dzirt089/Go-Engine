namespace GoEngine.App.Services;

/// <summary>Диапазон размеров доски, который обслуживает файл модели.</summary>
/// <param name="FileName">Имя файла модели.</param>
/// <param name="MinBoardSize">Наименьшая сторона доски.</param>
/// <param name="MaxBoardSize">Наибольшая сторона доски.</param>
public readonly record struct ModelRange(string FileName, int MinBoardSize, int MaxBoardSize);

/// <summary>Какие размеры доски обеспечены моделью.</summary>
/// <remarks>
/// Таблица соответствия «файл → диапазон размеров» живёт в слое `AI.Onnx`
/// (`ModelProfiles`), а этот помощник только отвечает на вопрос «для каких досок модель
/// на месте». Им пользуются обе головы: настольная и Android.
/// </remarks>
public static class ModelSizes
{
    /// <summary>Собирает размеры доски, для которых файлы моделей на месте.</summary>
    /// <param name="ranges">Диапазоны размеров по файлам моделей.</param>
    /// <param name="presentFiles">Имена файлов, которые действительно есть на диске.</param>
    /// <returns>Стороны доски, обеспеченные моделью.</returns>
    /// <exception cref="ArgumentNullException">Список диапазонов или файлов не задан.</exception>
    public static IReadOnlySet<int> Available(
        IReadOnlyList<ModelRange> ranges,
        IReadOnlyCollection<string> presentFiles)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(presentFiles);

        var sizes = new HashSet<int>();

        foreach (var range in ranges)
        {
            if (!presentFiles.Contains(range.FileName, StringComparer.Ordinal))
            {
                continue;
            }

            for (var size = range.MinBoardSize; size <= range.MaxBoardSize; size++)
            {
                _ = sizes.Add(size);
            }
        }

        return sizes;
    }
}
