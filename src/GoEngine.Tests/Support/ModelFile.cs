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

    /// <summary>Есть ли модель на диске.</summary>
    public static bool Exists => File.Exists(FullPath);

    /// <summary>Ищет корень репозитория вверх по каталогам.</summary>
    /// <returns>Каталог с `PROJECT.md` и `TASKS`, либо каталог сборки.</returns>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "PROJECT.md")) &&
                Directory.Exists(System.IO.Path.Combine(directory.FullName, "TASKS")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
