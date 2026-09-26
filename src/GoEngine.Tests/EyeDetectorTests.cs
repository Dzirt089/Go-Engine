using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты распознавания глаз и eye-safe playout — <c>DECISIONS.md</c> D-013, D-016.</summary>
public sealed class EyeDetectorTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Глаз_Настоящий_В_Углу_Распознан()
    {
        // Угловая точка: два соседа по стороне и единственная диагональ — свои.
        var board = StoneGrid(
            (1, 0, StoneColor.Black),
            (0, 1, StoneColor.Black),
            (1, 1, StoneColor.Black));

        Assert.True(EyeDetector.IsEye(board, new Point(0, 0), StoneColor.Black));
    }

    [Fact]
    public void Глаз_Настоящий_На_Краю_Распознан()
    {
        // Краевая точка: три соседа по стороне и обе диагонали — свои.
        var board = StoneGrid(
            (0, 0, StoneColor.Black),
            (0, 2, StoneColor.Black),
            (1, 1, StoneColor.Black),
            (1, 0, StoneColor.Black),
            (1, 2, StoneColor.Black));

        Assert.True(EyeDetector.IsEye(board, new Point(0, 1), StoneColor.Black));
    }

    [Fact]
    public void Глаз_Настоящий_В_Центре_Распознан()
    {
        // Центральная точка: четыре соседа и три диагонали из четырёх — свои.
        var board = StoneGrid(
            (3, 4, StoneColor.Black),
            (5, 4, StoneColor.Black),
            (4, 3, StoneColor.Black),
            (4, 5, StoneColor.Black),
            (3, 3, StoneColor.Black),
            (5, 3, StoneColor.Black),
            (3, 5, StoneColor.Black));

        Assert.True(EyeDetector.IsEye(board, new Point(4, 4), StoneColor.Black));
    }

    [Fact]
    public void Ложный_Глаз_Пойман()
    {
        // В центре нужно три диагонали из четырёх, а здесь заняты только две: это ложный глаз,
        // противник поставит камень в угрожающую диагональ и снимет группу.
        var board = StoneGrid(
            (3, 4, StoneColor.Black),
            (5, 4, StoneColor.Black),
            (4, 3, StoneColor.Black),
            (4, 5, StoneColor.Black),
            (3, 3, StoneColor.Black),
            (5, 3, StoneColor.Black));

        Assert.False(EyeDetector.IsEye(board, new Point(4, 4), StoneColor.Black));
    }

    [Fact]
    public void Ложный_Глаз_На_Краю_Пойман()
    {
        // На краю нужны обе диагонали, а занята только одна.
        var board = StoneGrid(
            (0, 0, StoneColor.Black),
            (0, 2, StoneColor.Black),
            (1, 1, StoneColor.Black),
            (1, 0, StoneColor.Black));

        Assert.False(EyeDetector.IsEye(board, new Point(0, 1), StoneColor.Black));
    }

    [Fact]
    public void Глаз_Требует_Камней_Своего_Цвета()
    {
        var board = StoneGrid(
            (3, 4, StoneColor.Black),
            (5, 4, StoneColor.Black),
            (4, 3, StoneColor.Black),
            (4, 5, StoneColor.Black),
            (3, 3, StoneColor.Black),
            (5, 3, StoneColor.Black),
            (3, 5, StoneColor.Black));

        Assert.False(EyeDetector.IsEye(board, new Point(4, 4), StoneColor.White));
    }

    [Fact]
    public void EyeDetector_Занятая_Точка_Не_Глаз()
    {
        var board = StoneGrid((4, 4, StoneColor.Black));

        Assert.False(EyeDetector.IsEye(board, new Point(4, 4), StoneColor.Black));
    }

    [Fact]
    public void Глаз_Противника_Не_Трогаем_В_Playout()
    {
        // Ограничение цвета: чёрный глаз не мешает белым ходить в эту точку — глаз свой только для чёрных.
        var board = StoneGrid(
            (1, 0, StoneColor.Black),
            (0, 1, StoneColor.Black),
            (1, 1, StoneColor.Black));

        Assert.False(EyeDetector.IsEye(board, new Point(0, 0), StoneColor.White));
    }

    [Fact]
    public void Playout_Не_Заполняет_Свой_Глаз_Пока_Есть_Ходы()
    {
        var board = StoneGrid(
            (1, 0, StoneColor.Black),
            (0, 1, StoneColor.Black),
            (1, 1, StoneColor.Black));
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 0.0));
        List<Move> moves = [];

        for (var seed = 0; seed < 50; seed++)
        {
            moves.Add(policy.SelectMove(board, StoneColor.Black, new Random(Seed + seed)));
        }

        Assert.DoesNotContain(moves, move => move.Point == new Point(0, 0));
    }

    [Fact]
    public void Playout_Заполняет_Свой_Глаз_Только_Если_Больше_Нет_Ходов()
    {
        // Все точки доски, кроме трёх глаз чёрных, заняты чёрными. Других ходов нет,
        // поэтому playout обязан заполнить глаз, а не пропустить ход; заполнение одного глаза
        // оставляет группе два дамэ, поэтому оно безопасно.
        var board = FilledExceptThreeEyes();
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 0.0));
        HashSet<Point> eyes = [new(0, 0), new(8, 0), new(0, 8)];

        var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Contains(move.Point, eyes);
    }

    [Fact]
    public void Playout_Не_Заполняет_Глаз_Ценой_Своей_Группы()
    {
        // Глаз — последнее дамэ группы: заполнять его нельзя даже вынужденно,
        // иначе playout сам убивает живую группу (замеры T-019b).
        var board = FilledExceptCorners();
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 0.0));

        var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void Playout_Не_Ломает_Свою_Живую_Группу()
    {
        // Живая группа с глазом: playout не должен закрывать последнее дамэ группы.
        var board = StoneGrid(
            (1, 0, StoneColor.Black),
            (0, 1, StoneColor.Black),
            (1, 1, StoneColor.Black),
            (5, 5, StoneColor.White));
        var policy = new PlayoutPolicy(new PlayoutConfig(0.0, 0.0));

        for (var seed = 0; seed < 50; seed++)
        {
            var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed + seed));

            if (move.Type != MoveType.Play)
            {
                continue;
            }

            // После хода политики у чёрной группы обязаны остаться дамэ:
            // иначе playout сам убивает свою живую группу.
            var after = board.ApplyMove(move);
            var group = GroupTracker.FindGroup(after, new Point(1, 0));

            Assert.True(group.Liberties.Count > 0);
        }
    }

    [Fact]
    public void Playout_Все_Ходы_Легальны_С_Учётом_Глаз()
    {
        var board = FilledExceptCorners();
        var policy = new PlayoutPolicy();
        List<Result<MoveResult>> played = [];
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        _ = game.Play(Move.Pass(StoneColor.Black));

        for (var number = 0; number < 20; number++)
        {
            var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed + number));
            played.Add(board.IsLegal(move).IsSuccess ? Result<MoveResult>.Ok(new MoveResult(true, move, [], null)) : Result<MoveResult>.Fail("нелегален"));
        }

        Assert.All(played, result => Assert.True(result.IsSuccess));
    }

    /// <summary>Строит позицию из перечисленных камней.</summary>
    /// <param name="stones">Точки и цвета камней.</param>
    /// <returns>Доска 9×9 с этими камнями.</returns>
    private static Board StoneGrid(params (int X, int Y, StoneColor Color)[] stones)
    {
        var board = new Board(BoardSize.Size9);

        foreach (var (x, y, color) in stones)
        {
            board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), color));
        }

        return board;
    }

    /// <summary>Заполняет доску чёрными камнями, оставляя пустыми два угла — глаза чёрных.</summary>
    /// <returns>Доска, где заполнение глаза оставляет чёрную группу без дамэ.</returns>
    private static Board FilledExceptCorners()
    {
        var board = new Board(BoardSize.Size9);
        var first = new Point(0, 0);
        var second = new Point(8, 8);

        foreach (var point in board.AllPoints())
        {
            if (point != first && point != second)
            {
                board = board.ApplyMove(Move.Play(point, StoneColor.Black));
            }
        }

        return board;
    }

    /// <summary>Заполняет доску чёрными камнями, оставляя пустыми три глаза чёрных.</summary>
    /// <returns>Доска, где безопасных ходов нет, а заполнение глаза не губит группу.</returns>
    private static Board FilledExceptThreeEyes()
    {
        var board = new Board(BoardSize.Size9);
        HashSet<Point> eyes = [new(0, 0), new(8, 0), new(0, 8)];

        foreach (var point in board.AllPoints())
        {
            if (!eyes.Contains(point))
            {
                board = board.ApplyMove(Move.Play(point, StoneColor.Black));
            }
        }

        return board;
    }
}