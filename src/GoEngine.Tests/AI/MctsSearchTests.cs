using GoEngine.AI;
using GoEngine.Core;
using Microsoft.Extensions.Time.Testing;

namespace GoEngine.Tests;

/// <summary>Тесты поиска MCTS и шва оценки — D-051.</summary>
/// <remarks>
/// Поиск знает только про дерево и бюджет: сколько раз вызвана оценка и что она делает с деревом,
/// решает реализация <see cref="IMctsEvaluation"/>. Здесь это проверяется подставной оценкой.
/// </remarks>
public sealed class MctsSearchTests
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    [Fact]
    public void Поиск_Вызывает_Оценку_Ровно_Бюджет_Раз()
    {
        var evaluation = new CountingEvaluation();

        _ = new MctsSearch(new MctsConfig(7), evaluation).Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);

        Assert.Equal(7, evaluation.Calls);
    }

    [Fact]
    public void Поиск_Отдаёт_Корень_С_Разобранными_Ходами()
    {
        var root = new MctsSearch(new MctsConfig(7), new CountingEvaluation())
            .Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);

        Assert.NotEmpty(root.Children);
    }

    [Fact]
    public void Поиск_По_Времени_Останавливается_По_Часам_Провайдера()
    {
        // Время идёт только через TimeProvider: DateTime.Now и Stopwatch в AI запрещены (AGENTS.md, п. 6).
        // Часы двигает сама оценка: так шаг времени задан тестом и результат не зависит от машины.
        var time = new FakeTimeProvider();
        var evaluation = new CountingEvaluation(() => time.Advance(TimeSpan.FromMilliseconds(300)));
        var search = new MctsSearch(new MctsConfig(TimeSpan.FromSeconds(1)), evaluation, time);

        _ = search.Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);

        Assert.Equal(4, evaluation.Calls);
    }

    [Fact]
    public void Оценка_Доигрываниями_Не_Даёт_Приоритетов()
    {
        var evaluation = new PlayoutEvaluation(new PlayoutPolicy(), new Random(Seed));

        Assert.False(evaluation.GivesPriors);
    }

    [Fact]
    public void Оценка_Сетью_Даёт_Приоритеты()
    {
        var evaluation = new NeuralEvaluation(new StubEvaluator());

        Assert.True(evaluation.GivesPriors);
    }

    [Fact]
    public void Поиск_Требует_Оценку()
    {
        Assert.Throws<ArgumentNullException>(() => new MctsSearch(MctsConfig.Default, null!));
    }

    /// <summary>Подставная оценка: считает вызовы и расширяет узел, ничего не оценивая.</summary>
    /// <param name="onEvaluate">Что делать на каждом вызове: например, двинуть часы теста.</param>
    private sealed class CountingEvaluation(Action? onEvaluate = null) : IMctsEvaluation
    {
        /// <summary>Сколько раз поиск обратился к оценке.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public bool GivesPriors => false;

        /// <inheritdoc />
        public void Evaluate(MctsTree tree, MctsNode node, IReadOnlyList<Move> history, Komi komi)
        {
            Calls++;
            onEvaluate?.Invoke();

            var leaf = node.IsFullyExpanded ? node : tree.Expand(node);
            tree.Backpropagate(leaf, StoneColor.Black);
        }
    }

    /// <summary>Подставная сеть: нужна только для признака приоритетов.</summary>
    private sealed class StubEvaluator : IPositionEvaluator
    {
        /// <inheritdoc />
        public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves) =>
            new(new double[board.Size.Area + 1], 0.5, board.Size.Area);

        /// <inheritdoc />
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
    }
}
