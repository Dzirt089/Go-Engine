using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

using static GoEngine.App.Tests.TestViewModel;

namespace GoEngine.App.Tests;

/// <summary>Тесты уровней Дан в интерфейсе — T-034, D-038.</summary>
/// <remarks>
/// Интерфейс не должен срывать партию из-за настроек, которые сам же и не предлагает:
/// уровень Дан без загруженной модели или на доске 9×9 заменяется на 10 кю.
/// </remarks>
public sealed class NeuralLevelFallbackTests
{
    [Fact]
    public void Уровень_Дан_Без_Модели_Не_Срывает_Партию()
    {
        // Игрок белыми: первый ход за AI, и без оценщика он обязан откатиться на уровень кю.
        var settings = AppSettings.From(BoardSize.Size19, DifficultyLevel.Dan5, StoneColor.White, Komi.For19x19);

        var model = new MainViewModel(settings, new Random(Seed));

        Assert.Equal("1", model.MoveNumber);
    }

    [Fact]
    public void Уровень_Дан_На_Доске_9x9_Не_Срывает_Партию()
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Dan5, StoneColor.White, Komi.For9x9);

        var model = new MainViewModel(settings, new Random(Seed), new FakeEvaluator());

        Assert.Equal("1", model.MoveNumber);
    }

    [Fact]
    public void Уровень_Дан_С_Моделью_Ходит_На_19x19()
    {
        var settings = AppSettings.From(BoardSize.Size19, DifficultyLevel.Dan5, StoneColor.White, Komi.For19x19);

        var model = new MainViewModel(settings, new Random(Seed), new FakeEvaluator());

        Assert.Equal("1", model.MoveNumber);
    }
}
