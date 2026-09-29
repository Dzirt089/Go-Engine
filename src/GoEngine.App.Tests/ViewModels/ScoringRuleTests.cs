using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Система подсчёта в настройках: японская по умолчанию, китайская — по выбору игрока.</summary>
/// <remarks>
/// Систему выбирает регламент турнира, поэтому она настройка, а не константа: в турнирах РФ чаще
/// японская, она и объявлена основной. Выбор обязан переживать смену доски, уровня, новую партию
/// и загрузку — иначе игрок терял бы его при каждой правке настроек.
/// </remarks>
public sealed class ScoringRuleTests
{
    [Fact]
    public void Настройки_По_Умолчанию_Дают_Японскую_Систему()
    {
        Assert.Equal(ScoringRule.Japanese, AppSettings.Default.ToScoringRule());
    }

    [Fact]
    public void Настройки_Хранят_Выбранную_Систему()
    {
        var path = TempFile();
        var settings = AppSettings.From(
            BoardSize.Size9,
            DifficultyLevel.Kyu20,
            StoneColor.Black,
            Komi.For9x9,
            ScoringRule.Chinese);

        try
        {
            Assert.True(SettingsStore.Save(settings, path).IsSuccess);
            Assert.Equal(ScoringRule.Chinese, SettingsStore.Load(path).ToScoringRule());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Файл_Без_Системы_Даёт_Японскую()
    {
        // Файл прежней версии приложения системы не знал: играем по основной, а не падаем.
        var settings = AppSettings.Parse("{\"boardSize\":9,\"komi\":5.5}");

        Assert.Equal(ScoringRule.Japanese, settings!.ToScoringRule());
    }

    [Fact]
    public void Неизвестная_Система_В_Файле_Даёт_Японскую()
    {
        var settings = AppSettings.Parse("{\"scoringRule\":\"Ing\"}");

        Assert.Equal(ScoringRule.Japanese, settings!.ToScoringRule());
    }

    [Fact]
    public void Смена_Доски_Не_Сбрасывает_Систему()
    {
        var model = TestViewModel.Create(StoneColor.Black, DifficultyLevel.Kyu30, size: BoardSize.Size9);
        model.ScoringRule = ScoringRule.Chinese;

        model.SelectedSizeIndex = 1;

        Assert.Equal(ScoringRule.Chinese, model.ScoringRule);
    }

    [Fact]
    public void Новая_Партия_Не_Сбрасывает_Систему()
    {
        var model = TestViewModel.Create(StoneColor.Black, DifficultyLevel.Kyu30, size: BoardSize.Size9);
        model.ScoringRule = ScoringRule.Chinese;

        model.StartNewGame();

        Assert.Equal(ScoringRule.Chinese, model.ScoringRule);
    }

    [Fact]
    public void Загрузка_Партии_Не_Сбрасывает_Систему()
    {
        var model = TestViewModel.Create(StoneColor.Black, DifficultyLevel.Kyu30, size: BoardSize.Size9);
        model.ScoringRule = ScoringRule.Chinese;

        _ = model.LoadGame(new SgfGame(
            BoardSize.Size9,
            Komi.For9x9,
            [Move.Play(new Point(4, 4), StoneColor.Black), Move.Play(new Point(5, 5), StoneColor.White)],
            null));

        Assert.Equal(ScoringRule.Chinese, model.ScoringRule);
    }

    /// <summary>Строит путь к временному файлу настроек.</summary>
    /// <returns>Путь, который тест обязан удалить за собой.</returns>
    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), $"go-engine-rule-{Guid.NewGuid():N}.json");
}
