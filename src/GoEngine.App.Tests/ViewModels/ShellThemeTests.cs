using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты выбора оформления в оболочке: состояние, событие и отметка текущей темы.</summary>
/// <remarks>
/// Модель только хранит выбор и сообщает о нём: тему применяет приложение
/// (<c>App.ApplyTheme</c>), а вид пишет файл. Так правило проверяется без окна.
/// </remarks>
public sealed class ShellThemeTests
{
    [Fact]
    public void Вначале_Тема_Системная()
    {
        Assert.True(Shell().IsSystemTheme);
    }

    [Fact]
    public void Выбор_Тёмной_Темы_Меняет_Состояние()
    {
        var shell = Shell();

        shell.SelectTheme(ThemeChoice.Dark);

        Assert.True(shell.IsDarkTheme);
        Assert.False(shell.IsSystemTheme);
    }

    [Fact]
    public void Выбор_Светлой_Темы_Снимает_Отметку_Тёмной()
    {
        var shell = Shell();

        shell.SelectTheme(ThemeChoice.Dark);
        shell.SelectTheme(ThemeChoice.Light);

        Assert.True(shell.IsLightTheme);
        Assert.False(shell.IsDarkTheme);
    }

    [Fact]
    public void Выбор_Темы_Сообщает_Виду()
    {
        var shell = Shell();
        ThemeChoice? reported = null;
        shell.ThemeChanged += (_, theme) => reported = theme;

        shell.SelectTheme(ThemeChoice.Dark);

        Assert.Equal(ThemeChoice.Dark, reported);
    }

    [Fact]
    public void Выбор_Темы_Сообщает_О_Свойствах_Меню()
    {
        var shell = Shell();
        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.SelectTheme(ThemeChoice.Dark);

        Assert.Contains(nameof(ShellViewModel.Theme), changed);
        Assert.Contains(nameof(ShellViewModel.IsDarkTheme), changed);
        Assert.Contains(nameof(ShellViewModel.IsSystemTheme), changed);
    }

    [Fact]
    public void Повторный_Выбор_Той_Же_Темы_Ничего_Не_Меняет()
    {
        // Иначе файл оформления переписывался бы на каждое нажатие уже выбранной строки.
        var shell = Shell();
        var changed = 0;
        shell.PropertyChanged += (_, _) => changed++;

        shell.SelectTheme(ThemeChoice.System);

        Assert.Equal(0, changed);
    }

    [Fact]
    public void Тема_Приходит_Из_Настроек_Оболочки()
    {
        var shell = ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null,
            theme: ThemeChoice.Dark);

        Assert.True(shell.IsDarkTheme);
    }

    private static ShellViewModel Shell() =>
        ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null);
}
