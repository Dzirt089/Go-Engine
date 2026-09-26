using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты паттерновой политики playout'ов — <c>DECISIONS.md</c> D-019.</summary>
public sealed class PatternPolicyTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Pattern3x3_Код_Стабильный()
    {
        var board = StoneGrid((4, 3, StoneColor.Black), (4, 5, StoneColor.Black));

        var first = Pattern3x3.FromBoard(board, new Point(4, 4), StoneColor.White);
        var second = Pattern3x3.FromBoard(board, new Point(4, 4), StoneColor.White);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Pattern3x3_Разные_Позиции_Разные_Коды()
    {
        var first = StoneGrid((4, 3, StoneColor.Black));
        var second = StoneGrid((4, 3, StoneColor.White));

        Assert.NotEqual(
            Pattern3x3.FromBoard(first, new Point(4, 4), StoneColor.White),
            Pattern3x3.FromBoard(second, new Point(4, 4), StoneColor.White));
    }

    [Fact]
    public void Pattern3x3_Симметрия_Углов()
    {
        // Поворот фигуры на 90° не должен менять канонический код: веса симметричны.
        var board = StoneGrid((4, 3, StoneColor.Black), (3, 4, StoneColor.White));
        var rotated = StoneGrid((3, 4, StoneColor.Black), (4, 5, StoneColor.White));

        var straight = Pattern3x3.FromBoard(board, new Point(4, 4), StoneColor.White).Canonical();
        var turned = Pattern3x3.FromBoard(rotated, new Point(4, 4), StoneColor.White).Canonical();

        Assert.Equal(straight, turned);
    }

    [Fact]
    public void PatternPolicy_Веса_Включаются_Настройкой()
    {
        // Проверка самой возможности: с включёнными весами выбирается ход, а не пас.
        var board = StoneGrid((4, 4, StoneColor.Black), (2, 6, StoneColor.White));
        var policy = new PatternPolicy(new PlayoutConfig(0.0, 0.0, Patterns: true));

        var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Equal(MoveType.Play, move.Type);
    }

    [Fact]
    public void PatternWeights_Симметричные_Фигуры_Имеют_Один_Вес()
    {
        var board = StoneGrid((4, 3, StoneColor.Black), (4, 5, StoneColor.Black));
        var rotated = StoneGrid((3, 4, StoneColor.Black), (5, 4, StoneColor.Black));

        var straight = PatternWeights.WeightFor(Pattern3x3.FromBoard(board, new Point(4, 4), StoneColor.White));
        var turned = PatternWeights.WeightFor(Pattern3x3.FromBoard(rotated, new Point(4, 4), StoneColor.White));

        Assert.Equal(straight, turned);
    }

    [Fact]
    public void PatternWeights_Неизвестная_Фигура_Имеет_Вес_По_Умолчанию()
    {
        var board = StoneGrid((0, 8, StoneColor.Black));

        var weight = PatternWeights.WeightFor(Pattern3x3.FromBoard(board, new Point(8, 0), StoneColor.White));

        Assert.Equal(PatternWeights.DefaultWeight, weight);
    }

    [Fact]
    public void PatternPolicy_Atari_Capture_Приоритет()
    {
        var board = AtariToCapture();
        var policy = new PatternPolicy(new PlayoutConfig(1.0, 0.0));

        var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void PatternPolicy_Atari_Escape_Приоритет()
    {
        var board = AtariToRescue();
        var policy = new PatternPolicy(new PlayoutConfig(1.0, 0.0));

        var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Equal(new Point(1, 2), move.Point);
    }

    [Fact]
    public void PatternPolicy_Cut_Распознан()
    {
        // Две чёрные группы разделены точкой (4,4): ход туда разрезает их.
        var board = StoneGrid((4, 3, StoneColor.Black), (4, 5, StoneColor.Black), (0, 8, StoneColor.White));
        var policy = new PatternPolicy(new PlayoutConfig(0.0, 0.0, AggressiveShapes: true));

        var move = policy.SelectMove(board, StoneColor.White, new Random(Seed));

        Assert.Equal(new Point(4, 4), move.Point);
    }

    [Fact]
    public void PatternPolicy_Extend_Распознан()
    {
        var board = StoneGrid((2, 2, StoneColor.White));
        var policy = new PatternPolicy(new PlayoutConfig(0.0, 0.0, AggressiveShapes: true));

        var move = policy.SelectMove(board, StoneColor.White, new Random(Seed));

        Assert.Contains(move.Point, new Point(2, 2).Neighbors(BoardSize.Size9));
    }

    [Fact]
    public void PatternPolicy_Eye_Block_Распознан()
    {
        // Чёрные окружили три точки (4,2)–(6,2): это их глазное пространство.
        var board = EyeSpaceBoard();
        var policy = new PatternPolicy(new PlayoutConfig(0.0, 0.0, AggressiveShapes: true));
        HashSet<Point> eyeSpace = [new(4, 2), new(5, 2), new(6, 2)];

        var move = policy.SelectMove(board, StoneColor.White, new Random(Seed));

        Assert.Contains(move.Point, eyeSpace);
    }

    [Fact]
    public void PatternPolicy_Все_Ходы_Легальны()
    {
        var board = StoneGrid((4, 4, StoneColor.Black), (2, 6, StoneColor.White));
        var policy = new PatternPolicy();
        List<Result<MoveResult>> played = [];

        for (var number = 0; number < 40; number++)
        {
            var move = policy.SelectMove(board, StoneColor.Black, new Random(Seed + number));
            played.Add(board.IsLegal(move).IsSuccess
                ? Result<MoveResult>.Ok(new MoveResult(true, move, [], null))
                : Result<MoveResult>.Fail($"нелегален: {move.Point}"));
        }

        Assert.All(played, result => Assert.True(result.IsSuccess));
    }

    [Fact]
    public void PatternPolicy_Детерминирован_При_Seed()
    {
        var board = StoneGrid((4, 4, StoneColor.Black), (2, 6, StoneColor.White));
        var first = new PatternPolicy().SelectMove(board, StoneColor.Black, new Random(Seed));
        var second = new PatternPolicy().SelectMove(board, StoneColor.Black, new Random(Seed));

        Assert.Equal(first, second);
    }

    [Fact]
    public void PatternPolicy_Не_Зацикливается_На_Позиции_Без_Ходов()
    {
        var policy = new PatternPolicy();

        var move = policy.SelectMove(TestPositions.BoardWithoutLegalMoves(), StoneColor.Black, new Random(Seed));

        Assert.Equal(MoveType.Pass, move.Type);
    }

    [Fact]
    public void PlayoutPolicy_Делегирует_Паттерновой_Политике()
    {
        var board = AtariToCapture();
        var playout = new PlayoutPolicy(new PlayoutConfig(1.0, 0.0));
        var pattern = new PatternPolicy(new PlayoutConfig(1.0, 0.0));

        Assert.Equal(
            pattern.SelectMove(board, StoneColor.Black, new Random(Seed)),
            playout.SelectMove(board, StoneColor.Black, new Random(Seed)));
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

    /// <summary>Строит позицию, где белый камень в атари и чёрные снимают его ходом в (1,2).</summary>
    /// <returns>Доска с одной решающей точкой.</returns>
    private static Board AtariToCapture() =>
        StoneGrid(
            (0, 1, StoneColor.Black),
            (1, 1, StoneColor.White),
            (2, 1, StoneColor.Black),
            (1, 0, StoneColor.Black),
            (5, 5, StoneColor.White));

    /// <summary>Строит позицию, где чёрная группа в атари и спасается ходом в (1,2).</summary>
    /// <returns>Доска с одной спасающей точкой.</returns>
    private static Board AtariToRescue() =>
        StoneGrid(
            (1, 1, StoneColor.Black),
            (0, 1, StoneColor.White),
            (2, 1, StoneColor.White),
            (1, 0, StoneColor.White),
            (5, 5, StoneColor.Black));

    /// <summary>Строит позицию с чёрным глазным пространством из трёх точек.</summary>
    /// <returns>Доска, где чёрные окружили точки (4,2)–(6,2), а у белых есть камень вдали.</returns>
    private static Board EyeSpaceBoard() =>
        StoneGrid(
            (3, 2, StoneColor.Black),
            (7, 2, StoneColor.Black),
            (4, 1, StoneColor.Black),
            (5, 1, StoneColor.Black),
            (6, 1, StoneColor.Black),
            (4, 3, StoneColor.Black),
            (5, 3, StoneColor.Black),
            (6, 3, StoneColor.Black),
            (0, 8, StoneColor.White));
}