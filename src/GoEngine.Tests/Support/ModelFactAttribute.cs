namespace GoEngine.Tests;

/// <summary>Тест, которому нужна скачанная модель KataGo.</summary>
/// <remarks>
/// Без файла модели тест не падает и не проходит молча, а помечается пропущенным с причиной:
/// сборка и проверка на агенте остаются зелёными, а причина пропуска видна в отчёте.
/// </remarks>
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Помечает тест пропущенным, если основной модели нет на диске.</summary>
    public ModelFactAttribute()
        : this(ModelFile.Name)
    {
    }

    /// <summary>Помечает тест пропущенным, если указанной модели нет на диске.</summary>
    /// <param name="fileName">Имя файла модели: у 9×9 своя сеть (D-040).</param>
    /// <remarks>Аргумент атрибута — константа, поэтому имя файла, а не готовый путь.</remarks>
    public ModelFactAttribute(string fileName)
    {
        var path = ModelFile.PathOf(fileName);

        if (!File.Exists(path))
        {
            Skip = $"модель не скачана: {path}";
        }
    }
}
