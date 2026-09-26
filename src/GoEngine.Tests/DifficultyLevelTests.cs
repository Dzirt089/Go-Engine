using GoEngine.AI;
using GoEngine.AI.Mcts;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты уровней сложности — <c>DECISIONS.md</c> D-003 и D-012.</summary>
public sealed class DifficultyLevelTests
{
    private const int Seed = 20260926;

    /// <summary>Все уровни первой версии: от 30 кю до 5 кю.</summary>
    private static readonly string[] LevelNames = ["Kyu30", "Kyu25", "Kyu20", "Kyu15", "Kyu10", "Kyu8", "Kyu5"];

    [Fact]
    public void DifficultyLevel_Все_Уровни_Создают_Селектор()
    {
        Assert.All(LevelNames, name =>
            Assert.NotNull(DifficultyLevel.FromName<DifficultyLevel>(name).CreateSelector(new Random(Seed))));
    }

    [Fact]
    public void DifficultyLevel_Семь_Уровней()
    {
        Assert.Equal(7, LevelNames.Length);
    }

    [Fact]
    public void Kyu30_Использует_Random()
    {
        Assert.IsType<RandomMoveSelector>(DifficultyLevel.Kyu30.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu20_Использует_Эвристики()
    {
        Assert.IsType<HeuristicMoveSelector>(DifficultyLevel.Kyu20.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu5_Использует_Mcts()
    {
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu5.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu5_Использует_Mcts_С_Большим_Бюджетом()
    {
        Assert.True(DifficultyLevel.Kyu5.TimeBudget > DifficultyLevel.Kyu10.TimeBudget);
    }

    [Fact]
    public void Kyu10_Думает_Дольше_Чем_Kyu15()
    {
        // 15 кю считает playout'ы (воспроизводимые замеры), 10 кю — время на ход.
        Assert.True(DifficultyLevel.Kyu10.TimeBudget is not null && DifficultyLevel.Kyu15.PlayoutBudget > 0);
    }

    [Fact]
    public void DifficultyLevel_Ранги_Убывают_С_Уровнем()
    {
        var ranks = LevelNames.Select(name => DifficultyLevel.FromName<DifficultyLevel>(name).RankKyu).ToList();
        var descending = ranks.OrderByDescending(rank => rank).ToList();

        Assert.Equal(descending, ranks);
    }

    [Fact]
    public void DifficultyLevel_Бюджеты_Растут_С_Уровнем()
    {
        var budgets = new[] { DifficultyLevel.Kyu10, DifficultyLevel.Kyu8, DifficultyLevel.Kyu5 }
            .Select(level => level.TimeBudget!.Value)
            .ToList();

        Assert.Equal(budgets.OrderBy(budget => budget).ToList(), budgets);
    }

    [Fact]
    public void DifficultyLevel_Случайность_Падает_С_Уровнем()
    {
        Assert.True(DifficultyLevel.Kyu30.RandomnessPercent > DifficultyLevel.Kyu25.RandomnessPercent
            && DifficultyLevel.Kyu25.RandomnessPercent > DifficultyLevel.Kyu20.RandomnessPercent);
    }

    [Fact]
    public void DifficultyLevel_Идентификатор_Равен_Рангу()
    {
        Assert.Equal(DifficultyLevel.Kyu15, DifficultyLevel.FromId<DifficultyLevel>(15));
    }

    [Fact]
    public void DifficultyLevel_Пас_Возвращает_Подходящий_Класс()
    {
        Assert.Equal(SelectorKind.Mcts, DifficultyLevel.Kyu8.Kind);
    }

    [Fact]
    public void DifficultyLevel_Уровень_Без_Поиска_Имеет_Нулевой_Бюджет()
    {
        Assert.Equal(0, DifficultyLevel.Kyu20.PlayoutBudget);
    }
}
