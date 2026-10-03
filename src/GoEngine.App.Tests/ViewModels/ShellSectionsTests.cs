using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты разделов нижней навигации телефона: партия, задачи, настройки.</summary>
/// <remarks>
/// На телефоне настройки — отдельный экран, а не окно поверх партии, поэтому выбранным может быть
/// только один раздел. Подсветку раздела показывает модель оболочки, а не вид: так поведение
/// проверяется без интерфейса. Стартовое состояние — раздел «Партия», закрытые настройки
/// и карточка «Меню» над доской — проверяет <c>ShellStartTests</c>: регрессия 2026-10-03 состояла
/// в том, что приложение открывалось на разделе настроек.
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
    }

    [Fact]
    public void Вначале_Раздел_Настроек_Не_Выбран()
    {
        // Регрессия 2026-10-03: на телефоне при запуске был выбран раздел настроек — приложение
        // открывалось на экране настроек вместо партии.
        var shell = Shell();

        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Экран_Настроек_Выделяет_Свой_Раздел()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();

        Assert.True(shell.IsSettingsSection);
    }

    [Fact]
    public void Экран_Настроек_Снимает_Подсветку_Партии()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();

        Assert.False(shell.IsGameSection);
    }

    [Fact]
    public void Экран_Настроек_Оставляет_Режим_Партии()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();

        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Закрытие_Настроек_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();
        shell.HideSettingsScreen();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Закрытие_Настроек_Снимает_Раздел_Настроек()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();
        shell.HideSettingsScreen();

        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Переход_В_Задачи_Закрывает_Экран_Настроек()
    {
        var shell = Shell();

        shell.ShowSettingsScreen();
        shell.ShowProblems();

        Assert.False(shell.IsSettingsSection);
    }

    [Fact]
    public void Переход_В_Задачи_Выделяет_Их_Раздел()
    {
        var shell = Shell();

        shell.ShowProblems();

        Assert.True(shell.IsProblemSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Задач_Возвращает_Раздел_Партии()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowGame();

        Assert.True(shell.IsGameSection);
    }

    [Fact]
    public void Переход_В_Партию_Из_Задач_Снимает_Раздел_Задач()
    {
        var shell = Shell();

        shell.ShowProblems();
        shell.ShowGame();

        Assert.False(shell.IsProblemSection);
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
