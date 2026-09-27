namespace GoEngine.Tests;

/// <summary>Тест, которому нужна скачанная модель KataGo.</summary>
/// <remarks>
/// Без файла модели тест не падает и не проходит молча, а помечается пропущенным с причиной:
/// сборка и проверка на агенте остаются зелёными, а причина пропуска видна в отчёте.
/// </remarks>
public sealed class ModelFactAttribute : FactAttribute
{
    /// <summary>Помечает тест пропущенным, если модели нет на диске.</summary>
    public ModelFactAttribute()
    {
        if (!ModelFile.Exists)
        {
            Skip = $"модель не скачана: {ModelFile.FullPath}";
        }
    }
}
