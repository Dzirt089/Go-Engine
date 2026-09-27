using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты остановки движка в конце партии: селекторы пасуют, а не доедают свои очки.</summary>
/// <remarks>
/// Требование пользователя: человек сказал пас — соперник обязан остановиться, а партия должна
/// закончиться двумя пасами (<c>GO_RULES.md</c>, п. 8). Позиции собираются прямо на доске,
/// а бюджеты заданы в итерациях, поэтому тесты детерминированы и не зависят от загрузки машины.
/// </remarks>
public sealed class EndgameSelectionTests
{
    /// <summary>Зерно проверок: партия и выбор хода должны повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    /// <summary>Сколько итераций у MCTS в этих тестах: важно не качество поиска, а отбор ходов.</summary>
    private const int MctsIterations = 20;

    private static Point P(int x, int y) => new((byte)x, (byte)y);

    /// <summary>Создаёт MCTS с бюджетом в итерациях.</summary>
    private static MctsMoveSelector CreateMcts() =>
        new(new Random(Seed), new PlayoutPolicy(), new MctsConfig(MctsIterations, MctsConfig.DefaultUcb1C));

    /// <summary>Создаёт эвристический селектор: уровням без сети пас нужен так же, как MCTS.</summary>
    /// <param name="randomnessPercent">Доля случайных ходов в процентах.</param>
    private static HeuristicMoveSelector CreateHeuristic(int randomnessPercent = 0) =>
        new(new Random(Seed), randomnessPercent);

    /// <summary>Строит поделённую доску: у каждого цвета остались только свои две точки.</summary>
    /// <returns>Доска, где все легальные ходы заполняют свою же территорию.</returns>
    /// <remarks>
    /// Чёрные занимают колонки 0–4, белые — 5–8, у каждых оставлены две пустые точки внутри своих
    /// камней: (1,1) и (1,7) у чёрных, (6,1) и (6,7) у белых. Пустая точка окружена камнями одного
    /// цвета, поэтому она — территория этого цвета, а для соперника ход в неё самоубийственен:
    /// легальных ходов, кроме заполнения своей территории, у обоих цветов не остаётся.
    /// </remarks>
    private static Board DividedBoard()
    {
        var board = new Board(BoardSize.Size9);
        Point[] blackHoles = [P(1, 1), P(1, 7)];
        Point[] whiteHoles = [P(6, 1), P(6, 7)];

        for (var x = 0; x <= 4; x++)
        {
            for (var y = 0; y <= 8; y++)
            {
                if (!blackHoles.Contains(P(x, y)))
                {
                    board = board.ApplyMove(Move.Play(P(x, y), StoneColor.Black));
                }
            }
        }

        for (var x = 5; x <= 8; x++)
        {
            for (var y = 0; y <= 8; y++)
            {
                if (!whiteHoles.Contains(P(x, y)))
                {
                    board = board.ApplyMove(Move.Play(P(x, y), StoneColor.White));
                }
            }
        }

        return board;
    }

    /// <summary>Собирает ту же позицию ходами партии: так проверяется сценарий жалобы целиком.</summary>
    /// <returns>Партия, где ход белых — последний ход чёрных уже сделан.</returns>
    /// <remarks>
    /// Ходы обязаны чередоваться, поэтому белые восемь раз пасуют: сорок три камня чёрных и тридцать
    /// четыре камня белых дают восемьдесят пять ходов, последний — за чёрных. Ни один ход не снимает
    /// камней: у каждой группы остаются её две точки, и позиция получается ровно как на доске выше.
    /// </remarks>
    private static GameState DividedGame()
    {
        var state = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        Point[] blackHoles = [P(1, 1), P(1, 7)];
        Point[] whiteHoles = [P(6, 1), P(6, 7)];
        List<Point> blackStones = [];
        List<Point> whiteStones = [];

        for (var x = 0; x <= 4; x++)
        {
            for (var y = 0; y <= 8; y++)
            {
                if (!blackHoles.Contains(P(x, y)))
                {
                    blackStones.Add(P(x, y));
                }
            }
        }

        for (var x = 5; x <= 8; x++)
        {
            for (var y = 0; y <= 8; y++)
            {
                if (!whiteHoles.Contains(P(x, y)))
                {
                    whiteStones.Add(P(x, y));
                }
            }
        }

        for (var index = 0; index < 34; index++)
        {
            Play(state, Move.Play(blackStones[index], StoneColor.Black));
            Play(state, Move.Play(whiteStones[index], StoneColor.White));
        }

        for (var index = 34; index < blackStones.Count; index++)
        {
            Play(state, Move.Play(blackStones[index], StoneColor.Black));

            if (index < blackStones.Count - 1)
            {
                Play(state, Move.Pass(StoneColor.White));
            }
        }

        return state;
    }

    /// <summary>Играет ход и падает, если партия его отклонила: сборка позиции — часть проверки.</summary>
    /// <param name="state">Партия.</param>
    /// <param name="move">Ход.</param>
    private static void Play(GameState state, Move move)
    {
        var result = state.Play(move);

        Assert.True(result.IsSuccess, $"Ход {(move.Type == MoveType.Pass ? "пас" : move.Point.ToString())} отклонён: {result.Error}");
    }

    /// <summary>Строит позицию с захватом: чёрная группа в атари, у белых есть снимающий ход.</summary>
    private static Board CapturePosition() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(P(0, 0), StoneColor.Black))
            .ApplyMove(Move.Play(P(1, 0), StoneColor.Black))
            .ApplyMove(Move.Play(P(0, 1), StoneColor.White))
            .ApplyMove(Move.Play(P(2, 0), StoneColor.White));

    /// <summary>Строит позицию, где спасти свою группу можно только внутри своей территории.</summary>
    private static Board RescuePosition() =>
        new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(P(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(P(4, 3), StoneColor.White))
            .ApplyMove(Move.Play(P(4, 5), StoneColor.White))
            .ApplyMove(Move.Play(P(3, 4), StoneColor.White))
            .ApplyMove(Move.Play(P(5, 3), StoneColor.Black))
            .ApplyMove(Move.Play(P(5, 5), StoneColor.Black))
            .ApplyMove(Move.Play(P(6, 4), StoneColor.Black));

    [Fact]
    public void Поделённая_Доска_Собрана_Правильно()
    {
        // Если сборка позиции сломается, остальные тесты начнут проверять не то, что задумано.
        var board = DividedBoard();
        var black = LegalMoves.For(board, StoneColor.Black);
        var white = LegalMoves.For(board, StoneColor.White);
        var ownership = Scorer.Ownership(board);

        Assert.Equal(4, board.EmptyPoints().Count());
        Assert.All(black, move => Assert.Equal(StoneColor.Black, ownership[(move.Point.Y * board.Size.Value) + move.Point.X]));
        Assert.All(white, move => Assert.Equal(StoneColor.White, ownership[(move.Point.Y * board.Size.Value) + move.Point.X]));
        Assert.Equal(2, black.Count);
        Assert.Equal(2, white.Count);
    }

    [Fact]
    public void Эвристика_На_Поделённой_Доске_Пасует()
    {
        var move = CreateHeuristic().SelectMove(DividedBoard(), StoneColor.Black);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Эвристика_Наугад_На_Поделённой_Доске_Пасует()
    {
        // Случайная ветка тоже обязана останавливаться: именно она «доедала очки».
        var move = CreateHeuristic(randomnessPercent: 100).SelectMove(DividedBoard(), StoneColor.Black);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Mcts_На_Поделённой_Доске_Пасует()
    {
        var move = CreateMcts().SelectMove(DividedBoard(), StoneColor.Black, Komi.For9x9);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Эвристика_После_Паса_Соперника_Пасует_На_Поделённой_Доске()
    {
        var move = CreateHeuristic().SelectMove(DividedBoard(), StoneColor.Black, [Move.Pass(StoneColor.White)]);

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Эвристика_Не_Пасует_Когда_Есть_Захват()
    {
        // Захват растёт в очках, значит пасовать нельзя: правило не должно мешать тактике.
        var move = CreateHeuristic().SelectMove(CapturePosition(), StoneColor.White, [Move.Pass(StoneColor.Black)]);

        Assert.Equal(P(1, 1), move.Point);
    }

    [Fact]
    public void Эвристика_Спасает_Группу_В_Атари_Вместо_Паса()
    {
        // Точка спасения лежит в своей территории: без исключения правило заставило бы пасовать.
        var move = CreateHeuristic().SelectMove(RescuePosition(), StoneColor.Black, [Move.Pass(StoneColor.White)]);

        Assert.Equal(P(5, 4), move.Point);
    }

    [Fact]
    public void Mcts_Не_Пасует_Когда_Есть_Захват()
    {
        var move = CreateMcts().SelectMove(CapturePosition(), StoneColor.White, Komi.For9x9, [Move.Pass(StoneColor.Black)]);

        Assert.NotEqual(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Эвристика_В_Начале_Партии_Не_Пасует()
    {
        // На почти пустой доске вся пустая область формально «чужая»: правило не должно пасовать.
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(P(4, 4), StoneColor.Black));

        var move = CreateHeuristic().SelectMove(board, StoneColor.White);

        Assert.NotEqual(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Mcts_В_Начале_Партии_Не_Пасует()
    {
        var board = new Board(BoardSize.Size9).ApplyMove(Move.Play(P(4, 4), StoneColor.Black));

        var move = CreateMcts().SelectMove(board, StoneColor.White, Komi.For9x9);

        Assert.NotEqual(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Соперник_Отвечает_Пасом_На_Пас_Человека()
    {
        var state = DividedGame();
        var rival = CreateHeuristic();
        Play(state, Move.Pass(StoneColor.White));

        var reply = rival.SelectMove(state);

        Assert.Equal(MoveType.Pass, reply.Type);
    }

    [Fact]
    public void Партия_Завершается_Двумя_Пасами()
    {
        // Сценарий жалобы целиком: человек пасует, соперник пасует — партия закончена, очки целы.
        var state = DividedGame();
        var rival = CreateHeuristic();
        Play(state, Move.Pass(StoneColor.White));
        Play(state, rival.SelectMove(state));

        Assert.Equal(GameStatus.FinishedByTwoPasses, state.Status);
    }

    [Fact]
    public void Партия_С_Mcts_Завершается_Двумя_Пасами()
    {
        var state = DividedGame();
        var rival = CreateMcts();
        Play(state, Move.Pass(StoneColor.White));
        Play(state, rival.SelectMove(state));

        Assert.Equal(GameStatus.FinishedByTwoPasses, state.Status);
    }
}
