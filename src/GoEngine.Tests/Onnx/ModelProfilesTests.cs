using GoEngine.AI.Onnx;

namespace GoEngine.Tests;

/// <summary>Тесты таблицы профилей моделей — T-035, T-036 (D-039, D-040).</summary>
/// <remarks>
/// Профиль — это обещание, что модель годится для размера. Специализированная модель обучена
/// ровно под 9×9, поэтому её профиль покрывает только 9: расширять его без замера нельзя.
/// </remarks>
public sealed class ModelProfilesTests
{
    [Fact]
    public void Для_Доски_9x9_Берётся_Специализированная_Модель()
    {
        Assert.Equal(ModelProfiles.Finetuned9x9, ModelProfiles.FindForBoardSize(9));
    }

    [Fact]
    public void Для_Доски_13x13_Берётся_Основная_Модель()
    {
        Assert.Equal(ModelProfiles.Main19x19, ModelProfiles.FindForBoardSize(13));
    }

    [Fact]
    public void Для_Доски_19x19_Берётся_Основная_Модель()
    {
        Assert.Equal(ModelProfiles.Main19x19, ModelProfiles.FindForBoardSize(19));
    }

    [Fact]
    public void Профиль_Девятки_Покрывает_Только_9x9()
    {
        Assert.Equal(9, ModelProfiles.Finetuned9x9.MinBoardSize);
        Assert.Equal(9, ModelProfiles.Finetuned9x9.MaxBoardSize);
    }

    [Fact]
    public void Файлы_Профилей_Разные()
    {
        Assert.NotEqual(ModelProfiles.Finetuned9x9.FileName, ModelProfiles.Main19x19.FileName);
    }
}
