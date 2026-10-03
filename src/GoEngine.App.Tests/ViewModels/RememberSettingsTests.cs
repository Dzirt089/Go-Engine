using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты запоминания настроек для следующей партии.</summary>
/// <remarks>
/// Регрессия H1b (независимая проверка 2026-10-03): закрытие настроек партию не пересоздаёт,
/// но и «сохранить в файл» мало — модель оставалась со старыми размером, уровнем, цветом и коми,
/// и следующая «Новая партия» начиналась по ним. <see cref="MainViewModel.RememberSettings"/>
/// запоминает выбор, не трогая текущую партию.
/// </remarks>
public sealed class RememberSettingsTests
{
    [Fact]
    public void Запомненные_Настройки_Вступают_В_Силу_В_Новой_Партии()
    {
        var model = Create();
        model.RememberSettings(Larger());

        model.StartNewGame();

        Assert.Equal(13, model.Board.Size.Value);
    }

    [Fact]
    public void Запомненные_Настройки_Не_Трогают_Текущую_Партию()
    {
        // Игрок закрыл настройки, а партия осталась той же: доска новая только с «Новой партии».
        var model = Create();

        model.RememberSettings(Larger());

        Assert.Equal(9, model.Board.Size.Value);
    }

    [Fact]
    public void Запомненные_Настройки_Не_Откатывают_Систему_Подсчёта()
    {
        // Правило подсчёта применяется сразу, пока открыты настройки, и в выбранных настройках
        // оно то же самое: запоминание не должно вернуть японскую систему.
        var model = Create();
        model.ScoringRule = ScoringRule.Chinese;

        model.RememberSettings(AppSettings.From(
            BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9, ScoringRule.Chinese));

        Assert.Equal(ScoringRule.Chinese, model.ScoringRule);
    }

    /// <summary>Создаёт модель представления партии 9×9 с соперником без сети.</summary>
    /// <returns>Модель представления партии.</returns>
    private static MainViewModel Create() => TestViewModel.Create();

    /// <summary>Настройки другой доски: 13×13 — так видно, что выбор вступил в силу.</summary>
    /// <returns>Настройки для следующей партии.</returns>
    private static AppSettings Larger() =>
        AppSettings.From(BoardSize.Size13, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For13x13);
}
