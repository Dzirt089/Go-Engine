using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты MCTS-селектора — <c>DECISIONS.md</c> D-003.</summary>
/// <remarks>
/// Бюджеты в тестах намеренно малы: playout на 9×9 стоит миллисекунды, а прогон тестов
/// должен оставаться быстрым. Полный бюджет 5000 playout'ов — для игры, не для тестов.
/// </remarks>
public sealed class MctsMoveSelectorTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Mcts_Всегда_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

        var move = Selector(10).SelectMove(game);

        Assert.True(game.Board.IsLegal(move).IsSuccess);
    }

    [Fact]
    public void Mcts_Детерминирован_При_Фиксированном_Seed()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        var first = Selector(10).SelectMove(game);
        var second = Selector(10).SelectMove(game);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Mcts_Находит_Ход_В_Цумэго_9x9()
    {
        // Гонка захвата: единственный легальный ход чёрных (4,4) снимает 36 белых камней.
        var board = TestPositions.CapturingRace();

        var move = Selector(20).SelectMove(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(new Point(4, 4), move.Point);
    }


    [Fact]
    public void Mcts_Бюджет_Влияет_На_Силу()
    {
        // Измеримое следствие бюджета: чем он больше, тем больше playout'ов получает дерево.
        // Турнир силы между уровнями — задача T-019.
        var board = TestPositions.BlockInAtari();

        var small = Selector(5).Search(board, StoneColor.Black, Komi.For9x9);
        var large = Selector(25).Search(board, StoneColor.Black, Komi.For9x9);

        Assert.True(large.Visits > small.Visits);
    }

    [Fact]
    public void Mcts_Дерево_Получает_Бюджет_Playouts()
    {
        var board = TestPositions.BlockInAtari();

        var root = Selector(20).Search(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(20, root.Visits);
    }

    [Fact]
    public void Mcts_Не_Нарушает_Суперко_В_Партии()
    {
        // Регрессия: поиск идёт по доске без истории, поэтому выбранный ход обязан
        // проверяться на настоящей доске — иначе партия прервётся нарушением суперко.
        var harness = new SelfPlayHarness();
        var mcts = Selector(2);
        var random = new RandomMoveSelector(new Random(Seed));

        var result = harness.PlayGame(mcts, random, BoardSize.Size9, Komi.For9x9, Seed);

        Assert.True(result.Result.Status.IsFinished);
    }

    [Fact]
    public void Mcts_Пас_Когда_Ходов_Нет()
    {
        var move = Selector(5).SelectMove(TestPositions.BoardWithoutLegalMoves(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Mcts_Возвращает_Ход_Своего_Цвета()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        var move = Selector(5).SelectMove(game);

        Assert.Equal(StoneColor.White, move.Color);
    }

    [Fact]
    public void Mcts_Не_Положительный_Бюджет_Бросает()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Selector(0));
    }

    [Fact]
    public void Mcts_Имя_Содержит_Бюджет()
    {
        Assert.Contains("10", Selector(10).Name);
    }

    [Fact]
    public void MctsConfig_По_Умолчанию()
    {
        Assert.Equal(new MctsConfig(5000, 1.41), MctsConfig.Default);
    }

    /// <summary>Создаёт селектор с фиксированным зерном и заданным бюджетом.</summary>
    /// <param name="playoutBudget">Бюджет playout'ов на ход.</param>
    /// <returns>MCTS-селектор.</returns>
    private static MctsMoveSelector Selector(int playoutBudget) =>
        new(new Random(Seed), new PlayoutPolicy(), playoutBudget);
}