using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты лестницы уровней: каждый уровень кю играет поиском, бюджеты растут к сильным.</summary>
/// <remarks>
/// Эвристики и случайные ходы остались селекторами, но в лестнице их нет: 30, 25 и 20 кю выглядели
/// «тупыми» рядом с 15 кю с сетью (жалоба пользователя после живой игры). Бюджет с сетью задан
/// в итерациях и падает с ростом доски: один вызов сети на 19×19 примерно вчетверо дороже, чем
/// на 9×9 (D-047), поэтому одинаковое число итераций означало бы разное время ожидания (D-054).
/// </remarks>
public sealed class LevelLadderTests
{
    private const int Seed = 20260926;

    /// <summary>Каталог моделей: файлов в нём нет, наличие модели решает подставная оценка.</summary>
    private const string ModelsDirectory = "models";

    /// <summary>Лестница кю от слабого уровня к сильному.</summary>
    private static readonly DifficultyLevel[] KyuLadder =
    [
        DifficultyLevel.Kyu30,
        DifficultyLevel.Kyu25,
        DifficultyLevel.Kyu20,
        DifficultyLevel.Kyu15,
        DifficultyLevel.Kyu10,
        DifficultyLevel.Kyu8,
        DifficultyLevel.Kyu5
    ];

    /// <summary>Ступени, которым сеть нужна всегда: 1 кю, 1 дан, 5 дан.</summary>
    private static readonly DifficultyLevel[] NeuralLadder =
    [
        DifficultyLevel.Kyu1,
        DifficultyLevel.Dan1,
        DifficultyLevel.Dan5
    ];

    /// <summary>Вся лестница от 30 кю до 5 дана в порядке возрастания силы.</summary>
    /// <returns>Уровни по порядку.</returns>
    private static IEnumerable<DifficultyLevel> Ladder() => KyuLadder.Concat(NeuralLadder);

    [Fact]
    public void Бюджеты_Итераций_Строго_Растут_На_Досках_9x9_И_13x13()
    {
        foreach (var size in new[] { BoardSize.Size9, BoardSize.Size13 })
        {
            var iterations = Ladder()
                .Select(level => level.NeuralBudget!.Value.For(size))
                .ToList();

            Assert.Equal(iterations.OrderBy(value => value).ToList(), iterations);
            Assert.Equal(iterations.Count, iterations.Distinct().Count());
        }
    }

    [Fact]
    public void На_19x19_Бюджеты_Не_Убывают_А_Равенство_Разводит_Случайность()
    {
        // На 19×19 один вызов сети дороже, поэтому у слабых ступеней итераций столько же,
        // сколько у соседа выше: их различает случайность — она у слабого уровня строго больше.
        var ladder = Ladder().ToList();
        var iterations = ladder.Select(level => level.NeuralBudget!.Value.For19x19).ToList();

        Assert.Equal(iterations.OrderBy(value => value).ToList(), iterations);

        for (var index = 1; index < ladder.Count; index++)
        {
            if (iterations[index] == iterations[index - 1])
            {
                Assert.True(
                    ladder[index - 1].RandomnessPercent > ladder[index].RandomnessPercent,
                    $"{ladder[index - 1].Name} и {ladder[index].Name} не различаются ни бюджетом, ни случайностью");
            }
        }
    }

    [Fact]
    public void На_Большой_Доске_Итераций_Не_Больше_Чем_На_Малой()
    {
        foreach (var level in KyuLadder.Concat(NeuralLadder))
        {
            var budget = level.NeuralBudget!.Value;

            Assert.True(budget.For19x19 <= budget.For13x13, level.Name);
            Assert.True(budget.For13x13 <= budget.For9x9, level.Name);
        }
    }

    [Fact]
    public void Случайность_Не_Растёт_С_Уровнем()
    {
        var randomness = KyuLadder.Select(level => level.RandomnessPercent).ToList();

        Assert.Equal(randomness.OrderByDescending(value => value).ToList(), randomness);
        Assert.True(randomness[0] > randomness[^1]);
    }

    [Fact]
    public void Слабые_Уровни_Детерминированы_Без_Сети()
    {
        // Бюджет в итерациях, а не во времени: партия воспроизводима при фиксированном зерне.
        foreach (var level in new[] { DifficultyLevel.Kyu30, DifficultyLevel.Kyu25, DifficultyLevel.Kyu20, DifficultyLevel.Kyu15 })
        {
            Assert.Null(level.TimeBudget);
            Assert.True(level.PlayoutBudget > 0, level.Name);

            var first = level.CreateSelector(new Random(Seed)).SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));
            var second = level.CreateSelector(new Random(Seed)).SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));

            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void С_Сетью_Уровень_Кю_Считает_Итерации_А_Не_Время()
    {
        foreach (var level in new[] { DifficultyLevel.Kyu30, DifficultyLevel.Kyu25, DifficultyLevel.Kyu20, DifficultyLevel.Kyu15 })
        {
            var selector = level.CreateSelector(new Random(Seed), BoardSize.Size9, new FakeEvaluator(), ModelsDirectory);

            Assert.Contains("итераций с сетью", selector.Name, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void С_Сетью_Уровень_Детерминирован()
    {
        foreach (var level in new[] { DifficultyLevel.Kyu30, DifficultyLevel.Kyu25, DifficultyLevel.Kyu20, DifficultyLevel.Kyu15 })
        {
            Assert.Equal(SelectWithNetwork(level), SelectWithNetwork(level));
        }
    }

    [Fact]
    public void Слабый_Уровень_Выбирает_Легальный_Ход_На_Пустой_Доске()
    {
        foreach (var level in KyuLadder)
        {
            var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
            var move = level.CreateSelector(new Random(Seed)).SelectMove(game);

            Assert.True(game.Board.IsLegal(move).IsSuccess, level.Name);
        }
    }

    /// <summary>Выбирает ход уровня с подставной сетью.</summary>
    /// <param name="level">Уровень.</param>
    /// <returns>Первый ход партии на 9×9.</returns>
    private static Move SelectWithNetwork(DifficultyLevel level) =>
        level.CreateSelector(new Random(Seed), BoardSize.Size9, new FakeEvaluator(), ModelsDirectory)
            .SelectMove(GameState.NewGame(BoardSize.Size9, Komi.For9x9));

    /// <summary>Подставная оценка позиции: тестам не нужна модель.</summary>
    private sealed class FakeEvaluator : IPositionEvaluator
    {
        /// <inheritdoc />
        public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
        {
            var area = board.Size.Area;
            var policy = new double[area + 1];
            policy[0] = 1.0;

            return new PositionEvaluation(policy, 0.5, area);
        }

        /// <inheritdoc />
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
    }
}
