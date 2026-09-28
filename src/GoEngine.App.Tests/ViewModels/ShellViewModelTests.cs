using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты оболочки двух режимов: переключение не ломает партию.</summary>
/// <remarks>
/// Главное свойство оболочки — режимы не пересоздаются: обе модели живут, пока открыто приложение,
/// поэтому возврат в «Партию» показывает ту же позицию (D-061).
/// </remarks>
public sealed class ShellViewModelTests
{
    [Fact]
    public void Начинается_С_Режима_Партии()
    {
        var shell = Create();

        Assert.True(shell.IsGameMode);
        Assert.False(shell.IsProblemMode);
        Assert.Contains("Партия", shell.ModeHint, StringComparison.Ordinal);
    }

    [Fact]
    public void Переключение_Режимов_Меняет_Состояние_И_Сообщает_Об_Этом()
    {
        var shell = Create();
        List<string> changed = [];

        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

        shell.ShowProblems();

        Assert.True(shell.IsProblemMode);
        Assert.False(shell.IsGameMode);
        Assert.Contains("Задачи", shell.ModeHint, StringComparison.Ordinal);
        Assert.Contains(nameof(ShellViewModel.IsGameMode), changed);
        Assert.Contains(nameof(ShellViewModel.IsProblemMode), changed);
        Assert.Contains(nameof(ShellViewModel.ModeHint), changed);

        changed.Clear();
        shell.ShowProblems();

        Assert.Empty(changed);
    }

    [Fact]
    public void Партия_Переживает_Переключение_Режимов()
    {
        var shell = Create();

        Assert.True(shell.Game.PlayMove(new Point(4, 4)));

        var moves = shell.Game.MoveNumber;
        var board = Snapshot(shell.Game.Board);

        shell.ShowProblems();

        Assert.Equal(moves, shell.Game.MoveNumber);

        shell.ShowGame();

        Assert.True(shell.IsGameMode);
        Assert.Same(shell.Game, shell.Game);
        Assert.Equal(moves, shell.Game.MoveNumber);
        Assert.Equal(board, Snapshot(shell.Game.Board));
    }

    [Fact]
    public void Задачи_Переживают_Переключение_Режимов()
    {
        var shell = Create();

        shell.Problems.Next();
        var index = shell.Problems.SelectedIndex;

        shell.ShowGame();
        shell.ShowProblems();

        Assert.Equal(index, shell.Problems.SelectedIndex);
        Assert.Same(shell.Problems, shell.Problems);
    }

    [Fact]
    public void Сборка_Оболочки_Берёт_Переданный_Список_Задач()
    {
        var settings = AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu30, StoneColor.Black, new Komi(5.5));
        var problems = ProblemLibrary.All.Take(3).ToArray();

        var shell = ShellViewModel.Create(settings, evaluator: null, problems: problems);

        Assert.Equal(3, shell.Problems.Problems.Count);
        Assert.Same(settings, shell.Settings);
    }

    /// <summary>Собирает оболочку с детерминированной партией и встроенной библиотекой задач.</summary>
    /// <returns>Оболочка двух режимов.</returns>
    private static ShellViewModel Create()
    {
        var settings = AppSettings.From(BoardSize.Size9, GoEngine.AI.DifficultyLevel.Kyu30, StoneColor.Black, new Komi(5.5));
        var game = new MainViewModel(settings, new Random(TestViewModel.Seed));

        return new ShellViewModel(settings, game, new ProblemViewModel());
    }

    /// <summary>Снимок позиции: камни по всем точкам.</summary>
    /// <param name="board">Доска.</param>
    /// <returns>Строка состояния доски.</returns>
    private static string Snapshot(Board board) =>
        string.Join('|', board.AllPoints().Select(point => $"{point.X},{point.Y}:{board.At(point).Name}"));
}
