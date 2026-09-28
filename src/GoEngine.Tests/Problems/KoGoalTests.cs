using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Цель «добиться ко-борьбы»: механический критерий и его границы.</summary>
/// <remarks>
/// Критерий не глазной и не «на усмотрение»: ход снимает ровно один камень, ответный ход
/// по форме возможен, но восстанавливает ровно ту позицию, что стояла до хода, — значит,
/// в партии он запрещён правилом ко. Проверяется сравнением камней по каждой точке, история
/// партии для этого не нужна.
/// </remarks>
public sealed class KoGoalTests
{
    [Fact]
    public void КоДоступен_Стороне_Снимающей_Один_Камень()
    {
        Assert.True(ProblemGoalCheck.KoAvailable(KoBoard(), StoneColor.Black));
    }

    [Fact]
    public void В_Форме_Ко_Оба_Ко_Пункта_Видны_Обеим_Сторонам()
    {
        // Форма симметрична: у чёрных ко в центре, у белых — в углу. Критерий видит и то и другое,
        // поэтому «ко доступен стороне» — свойство позиции, а не одной стороны.
        Assert.True(ProblemGoalCheck.KoAvailable(KoBoard(), StoneColor.White));
    }

    [Fact]
    public void КоНеДоступен_В_Позиции_Без_Ко()
    {
        var board = PlainCaptureBoard();

        Assert.False(ProblemGoalCheck.KoAvailable(board, StoneColor.Black));
        Assert.False(ProblemGoalCheck.KoAvailable(board, StoneColor.White));
    }

    [Fact]
    public void Цель_КоДостигнута_В_Позиции_С_Ко()
    {
        var board = KoBoard();

        Assert.True(ProblemGoalCheck.IsAchieved(ProblemFor(board, ProblemGoal.Ko), board));
    }

    [Fact]
    public void Обычный_Захват_Без_Ко_Не_Считается()
    {
        // Белый камень в атари снаружи: чёрные снимают его, но ответ не восстанавливает позицию.
        Assert.False(ProblemGoalCheck.KoAvailable(PlainCaptureBoard(), StoneColor.Black));
    }

    [Fact]
    public void КоОтсутствует_В_Начальной_Позиции_Задачи()
    {
        // Цель не выполнена в старте: иначе задача решается без хода.
        var stones = new List<ProblemStone>
        {
            new(new Point(1, 1), StoneColor.White),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 2), StoneColor.Black),
            new(new Point(2, 2), StoneColor.Black),
        };

        var board = Build(stones);

        Assert.False(ProblemGoalCheck.IsAchieved(ProblemFor(board, ProblemGoal.Ko), board));
    }

    /// <summary>Классическая форма ко: чёрные снимают один камень, ответ запрещён повтором.</summary>
    /// <returns>Доска с ко.</returns>
    private static Board KoBoard()
    {
        List<ProblemStone> stones =
        [
            new(new Point(1, 0), StoneColor.Black),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 2), StoneColor.Black),
            new(new Point(2, 0), StoneColor.White),
            new(new Point(1, 1), StoneColor.White),
            new(new Point(2, 2), StoneColor.White),
            new(new Point(3, 1), StoneColor.White),
        ];

        return Build(stones);
    }

    /// <summary>Захват без ко: белый камень в атари, ответ позицию не восстанавливает.</summary>
    /// <returns>Доска без ко.</returns>
    private static Board PlainCaptureBoard() =>
        Build(
        [
            new(new Point(1, 1), StoneColor.White),
            new(new Point(0, 1), StoneColor.Black),
            new(new Point(1, 0), StoneColor.Black),
            new(new Point(2, 0), StoneColor.Black),
        ]);

    private static Board Build(IReadOnlyList<ProblemStone> stones)
    {
        var built = ProblemSetup.Build(BoardSize.Size9, stones);

        Assert.True(built.IsSuccess, built.Error);

        return built.Value!;
    }

    private static Problem ProblemFor(Board board, ProblemGoal goal)
    {
        List<ProblemStone> stones = [];

        foreach (var point in board.AllPoints())
        {
            if (board.At(point) != StoneColor.Empty)
            {
                stones.Add(new ProblemStone(point, board.At(point)));
            }
        }

        var target = board.AllPoints().First(point => board.At(point) == StoneColor.White);
        var move = board.AllPoints().Select(p => Move.Play(p, StoneColor.Black)).First(m => board.IsLegal(m).IsSuccess);

        return new Problem(
            "ko-test",
            "ко-тест",
            BoardSize.Size9,
            new Komi(5.5),
            StoneColor.Black,
            goal,
            stones,
            target,
            20,
            "проверка критерия",
            ProblemNode.One(move, ProblemNode.Leaf));
    }
}
