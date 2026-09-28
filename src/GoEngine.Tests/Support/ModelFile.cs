namespace GoEngine.Tests;

/// <summary>Файл модели KataGo, скачанный в <c>models/</c>.</summary>
/// <remarks>
/// Модель весит около 72 МБ и в индекс git не попадает (`.gitignore`): её скачивают отдельно
/// по адресу из `models/README.md`. Тесты, которым нужна модель, без неё пропускаются
/// (см. <see cref="ModelFactAttribute"/>), поэтому проверка на агенте остаётся зелёной.
/// </remarks>
internal static class ModelFile
{
    /// <summary>Имя файла модели.</summary>
    public const string Name = "kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx";

    /// <summary>Полный путь к модели.</summary>
    /// <remarks>
    /// Корень репозитория ищется подъёмом от каталога тестовой сборки по признаку
    /// (`PROJECT.md` и папка `TASKS`): так путь не зависит от конфигурации сборки.
    /// </remarks>
    public static string FullPath { get; } = System.IO.Path.Combine(Root(), "models", Name);

    /// <summary>Имя специализированной модели 9×9 (T-036, <c>DECISIONS.md</c>, D-040).</summary>
    public const string Finetuned9x9Name = "kata9x9-finetuned.uint8.onnx";

    /// <summary>Есть ли модель на диске.</summary>
    public static bool Exists => File.Exists(FullPath);

    /// <summary>Полный путь к файлу модели по имени.</summary>
    /// <param name="fileName">Имя файла в каталоге моделей.</param>
    /// <returns>Путь к файлу модели.</returns>
    public static string PathOf(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FullPath)!, fileName);
    }

    /// <summary>Полный путь к специализированной модели 9×9.</summary>
    public static string Finetuned9x9Path { get; } = PathOf(Finetuned9x9Name);

    /// <summary>Есть ли модель 9×9 на диске.</summary>
    public static bool Finetuned9x9Exists => File.Exists(Finetuned9x9Path);

    /// <summary>Ищет корень репозитория вверх по каталогам.</summary>
    /// <returns>Каталог с `PROJECT.md` и решением `src/GoEngine.sln`, либо каталог сборки.</returns>
    /// <remarks>
    /// Признаки — только те файлы, что лежат в репозитории: прежде вторым признаком была папка
    /// `TASKS`, но рабочие документы агента из репозитория убрали, и у свежего клона поиск
    /// корня сломался бы (тесты с моделями искали бы их в каталоге сборки).
    /// </remarks>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "PROJECT.md")) &&
                Directory.Exists(System.IO.Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
