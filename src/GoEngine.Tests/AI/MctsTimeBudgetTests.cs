using GoEngine.AI;
using GoEngine.Core;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.Tests;

/// <summary>Тесты бюджета MCTS: по времени и по числу playout'ов — <c>DECISIONS.md</c> D-017.</summary>
public sealed class MctsTimeBudgetTests
{
    private const int Seed = 20260926;

    [Fact]
    public async Task Mcts_Останавливается_По_TimeBudget()
    {
        var fake = new FakeTimeProvider();
        var selector = new MctsMoveSelector(
            new Random(Seed),
            new PlayoutPolicy(),
            new MctsConfig(TimeSpan.FromMilliseconds(500)),
            randomnessPercent: 0,
            timeProvider: fake);
        var board = TestPositions.CapturingRace();

        var search = Task.Run(() => selector.Search(board, StoneColor.Black, Komi.For9x9));

        // FakeTimeProvider само время не двигает: подталкиваем его, пока поиск не упрётся в бюджет.
        while (!search.IsCompleted)
        {
            fake.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(10);
        }

        Assert.True((await search).Visits > 0);
    }

    [Fact]
    public void Mcts_Останавливается_По_PlayoutBudget()
    {
        // Время стоит на месте: поиск обязан закончиться по счётчику playout'ов, а не по времени.
        var fake = new FakeTimeProvider();
        var selector = new MctsMoveSelector(
            new Random(Seed),
            new PlayoutPolicy(),
            new MctsConfig(5, MctsConfig.DefaultUcb1C),
            randomnessPercent: 0,
            timeProvider: fake);

        var root = selector.Search(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(5, root.Visits);
    }

    [Fact]
    public void MctsConfig_Оба_Заданы_Бросает()
    {
        Assert.Throws<DomainException>(() => new MctsConfig(10, TimeSpan.FromSeconds(1), MctsConfig.DefaultUcb1C));
    }

    [Fact]
    public void MctsConfig_Ни_Одно_Не_Задано_Бросает()
    {
        Assert.Throws<DomainException>(() => new MctsConfig(null, null, MctsConfig.DefaultUcb1C));
    }

    [Fact]
    public void DefaultForSize_Корректные_Значения()
    {
        var budgets = new[]
        {
            MctsConfig.DefaultForSize(BoardSize.Size9).TimeBudget,
            MctsConfig.DefaultForSize(BoardSize.Size13).TimeBudget,
            MctsConfig.DefaultForSize(BoardSize.Size19).TimeBudget
        };

        Assert.Equal(
            [TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(4000), TimeSpan.FromMilliseconds(6000)],
            budgets);
    }

    [Fact]
    public void Mcts_Детерминирован_В_Playout_Режиме()
    {
        var first = SelectorByPlayouts().SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);
        var second = SelectorByPlayouts().SelectMove(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(first, second);
    }

    [Fact]
    public void MctsConfig_Нулевое_Время_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MctsConfig(TimeSpan.Zero, MctsConfig.DefaultUcb1C));
    }

    [Fact]
    public void MctsConfig_Нулевой_Бюджет_Плейаутов_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MctsConfig(0, MctsConfig.DefaultUcb1C));
    }

    [Fact]
    public void MctsConfig_Отрицательный_Коэффициент_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MctsConfig(10, -1));
    }

    [Fact]
    public void Mcts_Имя_Содержит_Бюджет_По_Времени()
    {
        var selector = new MctsMoveSelector(
            new Random(Seed),
            new PlayoutPolicy(),
            new MctsConfig(TimeSpan.FromMilliseconds(1000)),
            randomnessPercent: 0,
            timeProvider: new FakeTimeProvider());

        Assert.Contains("1000", selector.Name);
    }

    [Fact]
    public void MctsConfig_Режим_Виден_По_Полям()
    {
        var byTime = new MctsConfig(TimeSpan.FromSeconds(1), MctsConfig.DefaultUcb1C);

        Assert.Null(byTime.PlayoutBudget);
    }

    [Fact]
    public void Mcts_Без_TimeProvider_Работает_По_Системному_Времени()
    {
        // Конструктор без TimeProvider обязан остаться рабочим: им пользуются уровни и тесты.
        var selector = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), 3);

        Assert.True(selector.Search(TestPositions.CapturingRace(), StoneColor.Black, Komi.For9x9).Visits > 0);
    }

    /// <summary>Создаёт селектор с бюджетом по числу playout'ов.</summary>
    /// <returns>Селектор с фиксированным зерном.</returns>
    private static MctsMoveSelector SelectorByPlayouts() =>
        new(new Random(Seed), new PlayoutPolicy(), new MctsConfig(4, MctsConfig.DefaultUcb1C));
}
