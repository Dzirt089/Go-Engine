using GoEngine.AI;
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
    public void Kyu30_Играет_Поиском()
    {
        // Слабые уровни тоже играют MCTS: эвристики и случайные ходы выглядели «тупыми»
        // рядом с соседними ступенями (D-054).
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu30.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu20_Играет_Поиском()
    {
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu20.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu25_Играет_Поиском()
    {
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu25.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu5_Использует_Mcts()
    {
        Assert.IsType<MctsMoveSelector>(DifficultyLevel.Kyu5.CreateSelector(new Random(Seed)));
    }

    [Fact]
    public void Kyu5_Ищет_Глубже_Чем_Kyu10()
    {
        Assert.True(
            DifficultyLevel.Kyu5.NeuralBudget!.Value.For9x9 > DifficultyLevel.Kyu10.NeuralBudget!.Value.For9x9);
    }

    [Fact]
    public void Kyu10_Ищет_Глубже_Чем_Kyu15()
    {
        // Бюджеты в итерациях: время хода зависит от доски, а сравнение ступеней — нет (D-054).
        Assert.True(DifficultyLevel.Kyu10.NeuralBudget is not null && DifficultyLevel.Kyu15.PlayoutBudget > 0);
    }

    [Fact]
    public void DifficultyLevel_Ранги_Убывают_С_Уровнем()
    {
        var ranks = LevelNames.Select(name => DifficultyLevel.FromName<DifficultyLevel>(name).RankKyu).ToList();
        var descending = ranks.OrderByDescending(rank => rank).ToList();

        Assert.Equal(descending, ranks);
    }

    [Fact]
    public void DifficultyLevel_Бюджеты_Сети_Растут_С_Уровнем()
    {
        var budgets = new[]
        {
            DifficultyLevel.Kyu10, DifficultyLevel.Kyu8, DifficultyLevel.Kyu5,
            DifficultyLevel.Kyu1, DifficultyLevel.Dan1, DifficultyLevel.Dan5
        }
        .Select(level => level.NeuralBudget!.Value.For9x9)
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
    public void DifficultyLevel_Уровней_Без_Поиска_Больше_Нет()
    {
        // Уровни без поиска остались только как селекторы (эвристика и случайные ходы) —
        // в лестнице их нет: все семь уровней кю играют MCTS (D-054).
        var ladder = new[]
        {
            DifficultyLevel.Kyu30, DifficultyLevel.Kyu25, DifficultyLevel.Kyu20, DifficultyLevel.Kyu15,
            DifficultyLevel.Kyu10, DifficultyLevel.Kyu8, DifficultyLevel.Kyu5
        };

        Assert.All(ladder, level => Assert.Equal(SelectorKind.Mcts, level.Kind));
        Assert.All(ladder, level => Assert.True(level.PlayoutBudget > 0 || level.TimeBudget is not null));
    }
}