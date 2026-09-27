using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>«Золотые» последовательности ходов: страховка рефакторинга среза 2 — D-051.</summary>
/// <remarks>
/// Последовательности сняты с кода до разделения поиска и оценки (бюджеты — в итерациях, не во
/// времени, зерно фиксировано), поэтому расхождение означает регрессию поведения, а не повод
/// поправить ожидание. Партии играются через <see cref="GameState"/>: так в проверку попадают
/// суперко и история партии. Оценка сети — подставная: тест проверяет поиск, а не модель.
/// </remarks>
public sealed class GoldenMoveSequenceTests
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Сколько ходов играется в каждой партии.</summary>
    private const int Moves = 24;

    /// <summary>Партия MCTS без сети, предохранитель включён по умолчанию.</summary>
    private const string MctsWithGuard =
        "B:7,8 W:6,8 B:8,8 W:3,8 B:4,8 W:1,8 B:5,8 W:8,7 B:7,7 W:2,8 B:4,7 W:0,8 B:5,7 W:6,7 B:1,7 W:7,6 B:3,6 W:8,8 B:3,7 W:7,8 B:0,7 W:7,7 B:2,7 W:1,8";

    /// <summary>Та же партия с выключенным предохранителем: проверяет проводку фильтра ходов.</summary>
    private const string MctsWithoutGuard =
        "B:7,8 W:8,8 B:1,8 W:4,8 B:2,8 W:6,8 B:5,8 W:3,8 B:7,7 W:6,7 B:0,8 W:8,7 B:5,7 W:3,7 B:4,7 W:0,7 B:1,7 W:2,7 B:4,6 W:7,6 B:3,6 W:8,6 B:5,6 W:7,8";

    /// <summary>Партия эвристического уровня со случайностью 40 %.</summary>
    private const string Heuristic =
        "B:3,6 W:2,2 B:6,2 W:2,3 B:6,6 W:0,8 B:0,0 W:8,5 B:3,1 W:8,7 B:7,6 W:6,8 B:1,0 W:8,1 B:3,2 W:3,5 B:7,0 W:1,2 B:7,7 W:3,0 B:1,5 W:5,2 B:5,0 W:4,0";

    /// <summary>Партия MCTS с оценкой сети: проверяет PUCT, приоритеты и порядок разбора ходов.</summary>
    private const string MctsWithNet =
        "B:0,0 W:1,0 B:2,0 W:3,0 B:4,0 W:5,0 B:6,0 W:7,0 B:0,1 W:8,0 B:1,1 W:2,1 B:1,0 W:3,1 B:4,1 W:5,1 B:6,1 W:7,1 B:0,2 W:8,1 B:1,2 W:2,2 B:3,2 W:4,2";

    [Fact]
    public void Mcts_С_Предохранителем_Играет_Ту_Же_Партию() =>
        Assert.Equal(MctsWithGuard, Play(() => new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), new MctsConfig(20, MctsConfig.DefaultUcb1C))));

    [Fact]
    public void Mcts_Без_Предохранителя_Играет_Ту_Же_Партию() =>
        Assert.Equal(MctsWithoutGuard, Play(() => new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), new MctsConfig(20, MctsConfig.DefaultUcb1C) { TacticalGuard = false })));

    [Fact]
    public void Эвристика_Играет_Ту_Же_Партию() =>
        Assert.Equal(Heuristic, Play(() => new HeuristicMoveSelector(new Random(Seed), randomnessPercent: 40, tacticalGuard: true)));

    [Fact]
    public void Mcts_С_Сетью_Играет_Ту_Же_Партию() =>
        Assert.Equal(MctsWithNet, Play(() => new MctsMoveSelector(new Random(Seed), new PlayoutPolicy(), new MctsConfig(5), evaluator: new FakeEvaluator())));

    /// <summary>Играет партию селектором и возвращает ходы строкой.</summary>
    /// <param name="create">Создаёт селектор партии.</param>
    /// <returns>Ходы через пробел: цвет и точка.</returns>
    private static string Play(Func<IMoveSelector> create)
    {
        var state = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = create();
        List<string> played = [];

        for (var index = 0; index < Moves && !state.Status.IsFinished; index++)
        {
            var move = selector.SelectMove(state);
            var result = state.Play(move);

            Assert.True(result.IsSuccess, $"Ход {Format(move)} номер {index + 1} отклонён партией: {result.Error}");
            played.Add(Format(move));
        }

        return string.Join(" ", played);
    }

    /// <summary>Записывает ход коротко: цвет и точка.</summary>
    /// <param name="move">Ход.</param>
    /// <returns>Например, «B:4,4» или «W:pass».</returns>
    private static string Format(Move move) =>
        move.Type == MoveType.Pass
            ? $"{(move.Color == StoneColor.Black ? "B" : "W")}:pass"
            : $"{(move.Color == StoneColor.Black ? "B" : "W")}:{move.Point.X},{move.Point.Y}";

    /// <summary>Подставная оценка позиции: детерминированные приоритеты и оценка исхода.</summary>
    /// <remarks>Повторяет оценку зонда, которым снимались «золотые» последовательности.</remarks>
    private sealed class FakeEvaluator : IPositionEvaluator
    {
        /// <inheritdoc />
        public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
        {
            var area = board.Size.Area;
            var policy = new double[area + 1];

            for (var index = 0; index < area; index++)
            {
                // Приоритет зависит от индекса точки: поиск обязан повторяться от запуска к запуску.
                policy[index] = 1.0 / (index + 2);
            }

            policy[area] = 0.01;

            var stones = board.Size.Area - board.EmptyPoints().Count();

            return new PositionEvaluation(policy, 0.5 + (0.001 * stones), area);
        }

        /// <inheritdoc />
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
    }
}
