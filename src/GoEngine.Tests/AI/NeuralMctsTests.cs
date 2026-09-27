using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты MCTS с оценкой позиции сетью — T-033.</summary>
/// <remarks>
/// Поиск проверяется на подставной оценке: она отвечает мгновенно и предсказуемо, поэтому
/// тесты видят именно поведение поиска, а не качество модели. Настоящая модель — в
/// <see cref="OnnxModelTests"/> и в конце этого класса.
/// </remarks>
public sealed class NeuralMctsTests
{
    /// <summary>Бюджет, при котором ходы успевают получить больше одного посещения.</summary>
    private const int RevisitBudget = 150;

    [Fact]
    public void Сеть_Вызывается_Один_Раз_За_Итерацию_Поиска()
    {
        var evaluator = new FakeEvaluator();
        var selector = WithNet(evaluator, 5);

        _ = selector.Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);

        Assert.Equal(5, evaluator.Calls);
    }

    [Fact]
    public void Сеть_Приоритет_Ведёт_Поиск_К_Нужному_Ходу()
    {
        var best = Move.Play(new Point(4, 4), StoneColor.Black);
        var evaluator = new FakeEvaluator(prior: (board, index) => index == Index(best.Point, board.Size) ? 1.0 : 0.0001);
        var selector = WithNet(evaluator, RevisitBudget);

        var move = selector.SelectMove(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);

        Assert.Equal(best.Point, move.Point);
    }

    [Fact]
    public void Дерево_Ведёт_Поиск_К_Ходу_С_Лучшей_Оценкой()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black, usePriors: true);

        while (!tree.Root.IsFullyExpanded)
        {
            _ = tree.Expand(tree.Root);
        }

        var good = tree.Root.Children[0];
        var bad = tree.Root.Children[1];

        // Оценка дана с точки зрения того, кто ходит из узла (белые): 0,1 значит «чёрные выигрывают».
        tree.Backpropagate(good, 0.1);
        tree.Backpropagate(bad, 0.9);

        Assert.Same(good, tree.Select());
    }

    [Fact]
    public void Дерево_Отдаёт_Оценку_Узлу_Своего_Цвета()
    {
        var (tree, child, leaf) = Chain();

        // Из листа ходят чёрные, и в ребёнка тоже ходили чёрные: узел получает оценку как есть.
        tree.Backpropagate(leaf, 0.2);

        Assert.Equal(0.2, child.Wins, 9);
    }

    [Fact]
    public void Дерево_Отдаёт_Сопернику_Обратную_Оценку()
    {
        var (tree, _, leaf) = Chain();

        // В лист ходили белые, а оценка дана за чёрных: белым достаётся 1 − оценка.
        tree.Backpropagate(leaf, 0.2);

        Assert.Equal(0.8, leaf.Wins, 9);
    }

    [Fact]
    public void Дерево_Без_Сети_Считает_Результат_Партии_Как_Прежде()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);

        tree.Backpropagate(child, StoneColor.Black);

        Assert.Equal(1.0, child.Wins);
    }

    [Fact]
    public void Поиск_С_Сетью_Копит_Посещения_На_Вероятном_Ходе()
    {
        var best = Move.Play(new Point(4, 4), StoneColor.Black);
        var evaluator = new FakeEvaluator(prior: (board, index) => index == Index(best.Point, board.Size) ? 1.0 : 0.0001);
        var selector = WithNet(evaluator, RevisitBudget);

        var root = selector.Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);
        var likely = root.Children.Single(child => child.Move.Point == best.Point);

        Assert.True(likely.Visits > 1, $"посещений у хода (4,4): {likely.Visits}");
    }

    [Fact]
    public void Поиск_С_Сетью_Запоминает_Приоритеты_Из_Политики()
    {
        var best = Move.Play(new Point(4, 4), StoneColor.Black);
        var evaluator = new FakeEvaluator(prior: (board, index) => index == Index(best.Point, board.Size) ? 0.8 : 0.0001);
        var selector = WithNet(evaluator, 4);

        var root = selector.Search(new Board(BoardSize.Size9), StoneColor.Black, Komi.For9x9);
        var likely = root.Children.First();

        Assert.Equal(0.8, likely.Prior, 0.0001);
    }

    [ModelFact]
    public void Сеть_На_Пустой_Доске_Играет_В_Углу()
    {
        var loaded = GoEngine.AI.Onnx.OnnxEvaluator.Load(ModelFile.FullPath);
        Assert.True(loaded.IsSuccess, $"модель не загрузилась: {loaded.Error ?? "без причины"}");

        using var evaluator = loaded.Value!;
        var selector = new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), new MctsConfig(8), evaluator: evaluator);

        var move = selector.SelectMove(new Board(BoardSize.Size19), StoneColor.Black, Komi.For19x19);

        Assert.True(IsCornerOpening(move), $"первый ход: {move.Point}");
    }

    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Строит короткую цепочку узлов: корень → ребёнок (ход чёрных) → лист (ход белых).</summary>
    /// <returns>Дерево, ребёнка и лист.</returns>
    private static (MctsTree Tree, MctsNode Child, MctsNode Leaf) Chain()
    {
        var tree = new MctsTree(new Board(BoardSize.Size9), StoneColor.Black);
        var child = tree.Expand(tree.Root);
        var leaf = tree.Expand(child);

        return (tree, child, leaf);
    }

    /// <summary>Создаёт селектор MCTS с оценкой сети.</summary>
    /// <param name="evaluator">Оценка позиции.</param>
    /// <param name="playoutBudget">Бюджет итераций поиска.</param>
    /// <returns>Селектор с детерминированным зерном.</returns>
    private static MctsMoveSelector WithNet(IPositionEvaluator evaluator, int playoutBudget) =>
        new(new Random(Seed), new PlayoutPolicy(), new MctsConfig(playoutBudget), evaluator: evaluator);

    /// <summary>Индекс точки в векторе вероятностей: построчно, слева направо.</summary>
    /// <param name="point">Точка доски.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Индекс в политике сети.</returns>
    private static int Index(Point point, BoardSize size) => (point.Y * size.Value) + point.X;

    /// <summary>Первый ход в углу: третья или четвёртая линия от края.</summary>
    /// <param name="move">Ход.</param>
    /// <returns><c>true</c>, если ход стоит в одном из четырёх углов.</returns>
    private static bool IsCornerOpening(Move move)
    {
        var near = (int value) => value is 2 or 3;
        var far = (int value) => value is 15 or 16;

        return move.Type == MoveType.Play && (near(move.Point.X) || far(move.Point.X)) && (near(move.Point.Y) || far(move.Point.Y));
    }

    /// <summary>Подставная оценка позиции: без сети и без файла модели.</summary>
    private sealed class FakeEvaluator(Func<Board, StoneColor, double>? win = null, Func<Board, int, double>? prior = null) : IPositionEvaluator
    {
        /// <summary>Сколько раз поиск обратился к оценке.</summary>
        public int Calls { get; private set; }

        /// <inheritdoc />
        public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
        {
            Calls++;

            var area = board.Size.Area;
            var policy = new double[area + 1];

            for (var index = 0; index < area; index++)
            {
                policy[index] = prior?.Invoke(board, index) ?? (1.0 / (area + 1));
            }

            policy[area] = 1.0 / (area + 1);

            return new PositionEvaluation(policy, win?.Invoke(board, toMove) ?? 0.5, area);
        }

        /// <inheritdoc />
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
    }
}
