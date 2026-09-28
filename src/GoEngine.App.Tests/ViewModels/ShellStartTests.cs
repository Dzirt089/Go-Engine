using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты стартового экрана мобильной версии.</summary>
/// <remarks>
/// Жалоба «не хватает кнопки начать матч: заходишь — и матч уже идёт». На телефоне оболочка
/// открывается на экране настроек, партия начинается по кнопке «Начать партию»; на настольной
/// версии поведение прежнее — партия видна сразу.
/// </remarks>
public sealed class ShellStartTests
{
    private static ShellViewModel Shell(bool startOnSettings) =>
        ShellViewModel.Create(
            AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9),
            null,
            null,
            startOnSettings);

    [Fact]
    public void Мобильная_Оболочка_Начинает_С_Настроек()
    {
        var shell = Shell(startOnSettings: true);

        Assert.True(shell.StartOnSettings);
        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Настольная_Оболочка_Начинает_С_Партии()
    {
        var shell = Shell(startOnSettings: false);

        Assert.False(shell.StartOnSettings);
        Assert.True(shell.IsGameMode);
    }

    [Fact]
    public void Переключение_Режимов_Работает_При_Стартовом_Экране()
    {
        var shell = Shell(startOnSettings: true);

        shell.ShowProblems();
        Assert.True(shell.IsProblemMode);

        shell.ShowGame();
        Assert.True(shell.IsGameMode);
    }
}
