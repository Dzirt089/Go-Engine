using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>«Золотые» последовательности ходов: страховка рефакторинга среза 2 — D-051.</summary>
/// <remarks>
/// Последовательности сняты с кода до разделения поиска и оценки (бюджеты — в итерациях, не во
/// времени, зерно фиксировано), поэтому расхождение означает регрессию поведения, а не повод
/// поправить ожидание. Партии играются через <see cref="GameState"/>: так в проверку попадают
/// суперко и история партии. Оценка сети — подставная: тест проверяет поиск, а не модель.
/// Две партии MCTS обновлены осознанно, когда появились правила конца партии
/// (<see cref="EndgamePolicy"/>): ход, заполняющий свою территорию и не дающий ничего, больше
/// не играется. В партии с предохранителем это ход 18 — было <c>W:8,8</c> (своя территория,
/// прирост очков 0), стало <c>W:3,7</c>, дальше партия расходится (шесть ходов из двадцати четырёх);
/// в партии без предохранителя — ход 24, было <c>W:7,8</c>, стало <c>W:6,6</c>, остальные 23 хода
/// совпали. Партии MCTS с подставной сетью это не затронуло.
/// Обе партии MCTS и партия эвристики обновлены осознанно, когда появилось правило пленных
/// (<see cref="PrisonerPolicy"/>, жалоба 2026-10-03): движок перестал играть в дамэ своих доказанно
/// мёртвых камней, потому что такой ход только добавляет пленного. Везде менялся один ход, после
/// которого партия расходится:
/// эвристика — ход 24, было <c>W:4,0</c> (примыкает к мёртвому камню D1), стало <c>W:0,1</c>,
/// 23 хода из 24 совпали;
/// MCTS без предохранителя — ход 12, было <c>W:8,7</c> (единственное дамэ мёртвого камня J9),
/// стало <c>W:5,7</c>, 11 ходов из 24 совпали;
/// MCTS с предохранителем — ход 14, было <c>W:6,7</c> (единственное дамэ мёртвого камня G9),
/// стало <c>W:1,7</c>, 13 ходов из 24 совпали.
/// Это не ослабление поиска: в обоих случаях белые продлевали камень, который перебор уже признал
/// мёртвым, и чёрные снимали его следующим ходом.
/// </remarks>
public sealed class GoldenMoveSequenceTests
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Сколько ходов играется в каждой партии.</summary>
    private const int Moves = 24;

    /// <summary>Партия MCTS без сети, предохранитель включён по умолчанию.</summary>
    private const string MctsWithGuard =
        "B:7,8 W:6,8 B:8,8 W:3,8 B:4,8 W:1,8 B:5,8 W:8,7 B:7,7 W:2,8 B:4,7 W:0,8 B:5,7 W:1,7 B:6,7 W:3,7 B:8,6 W:0,7 B:7,6 W:5,6 B:6,6 W:2,7 B:3,6 W:1,5";

    /// <summary>Та же партия с выключенным предохранителем: проверяет проводку фильтра ходов.</summary>
    private const string MctsWithoutGuard =
        "B:7,8 W:8,8 B:1,8 W:4,8 B:2,8 W:6,8 B:5,8 W:3,8 B:7,7 W:6,7 B:0,8 W:5,7 B:8,7 W:3,7 B:1,7 W:4,7 B:0,7 W:2,7 B:6,6 W:8,6 B:4,6 W:7,6 B:5,6 W:8,8";

    /// <summary>Партия эвристического уровня со случайностью 40 %.</summary>
    private const string Heuristic =
        "B:3,6 W:2,2 B:6,2 W:2,3 B:6,6 W:0,8 B:0,0 W:8,5 B:3,1 W:8,7 B:7,6 W:6,8 B:1,0 W:8,1 B:3,2 W:3,5 B:7,0 W:1,2 B:7,7 W:3,0 B:1,5 W:5,2 B:5,0 W:0,1";

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
