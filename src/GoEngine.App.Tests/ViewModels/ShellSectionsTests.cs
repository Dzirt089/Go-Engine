using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты разделов нижней навигации телефона: партия, задачи, настройки.</summary>
/// <remarks>
/// На телефоне настройки — отдельный экран, а не окно поверх партии, поэтому выбранным может быть
/// только один раздел. Подсветку раздела показывает модель оболочки, а не вид: так поведение
/// проверяется без интерфейса.
/// </remarks>
public sealed class ShellSectionsTests
{
    private static ShellViewModel Shell() =>
        ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null);

    [Fact]
    public void Вначале_Выбран_Раздел_Партии()
    {
        var shell = Shell();

        Assert.True(shell.IsGameSection);
        Assert.False(shell.IsProblemSection);
        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Экран_Настроек_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();

        Assert.True(shell.IsSettingsSection);
        Assert.False(shell.IsGameSection);
        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Закрытие_Настроек_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();
        shell.HideSettingsScreen();

        Assert.True(shell.IsGameSection);
        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Переход_В_Задачи_Закрывает_Экран_Настроек()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();
        shell.ShowProblems();

        Assert.True(shell.IsProblemSection);
        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Задач_Выделяет_Только_Партию()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
        Assert.False(shell.IsProblemSection);
        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Смена_Раздела_Сообщает_Обо_Всех_Свойствах_Навигации()
    {
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowSettingsScreen();

        Assert.Contains(nameof(ShellViewModel.IsGameSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsProblemSection), changed);
        Assert.Contains(nameof(ShellViewModel.IsSettingsSection), changed);
    }
}
