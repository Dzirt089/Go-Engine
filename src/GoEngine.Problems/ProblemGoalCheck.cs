namespace GoEngine.Problems;

using GoEngine.AI;
using GoEngine.Core;

/// <summary>Проверка достижения цели задачи по правилам движка.</summary>
/// <remarks>
/// Для <see cref="ProblemGoal.Capture"/> цель достигнута, когда все камни целевой группы исчезли
/// с доски. Для <see cref="ProblemGoal.Live"/> — когда у выжившей части целевой группы есть два
/// настоящих глаза: глаза считает <see cref="EyeDetector"/> из слоя AI, своего второго определения
/// глаза здесь нет.
/// </remarks>
public static class ProblemGoalCheck
{
    /// <summary>Проверяет, достигнута ли цель задачи в позиции.</summary>
    /// <param name="problem">Задача.</param>
    /// <param name="board">Позиция.</param>
    /// <returns><c>true</c>, если цель достигнута.</returns>
    public static bool IsAchieved(Problem problem, Board board)
    {
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(board);

        return problem.Goal == ProblemGoal.Capture
            ? problem.TargetPoints.All(point => board.At(point) == StoneColor.Empty)
            : CountRealEyes(board, problem) >= 2;
    }

    /// <summary>Считает настоящие глаза целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача.</param>
    /// <returns>Число настоящих глаз, соседних с камнями группы.</returns>
    public static int CountRealEyes(Board board, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        var group = LiveGroup(board, problem);

        if (group.Count == 0)
        {
            return 0;
        }

        var eyes = 0;

        foreach (var point in board.EmptyPoints())
        {
            if (!IsAdjacentTo(group, point, board.Size))
            {
                continue;
            }

            if (EyeDetector.IsEye(board, point, problem.TargetColor))
            {
                eyes++;
            }
        }

        return eyes;
    }

    /// <summary>Возвращает выжившую часть целевой группы.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="problem">Задача.</param>
    /// <returns>Точки камней целевого цвета из начальной группы.</returns>
    public static IReadOnlyList<Point> LiveGroup(Board board, Problem problem)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(problem);

        List<Point> group = [];

        foreach (var point in problem.TargetPoints)
        {
            if (board.At(point) == problem.TargetColor)
            {
                group.Add(point);
            }
        }

        return group.AsReadOnly();
    }

    private static bool IsAdjacentTo(IReadOnlyList<Point> group, Point point, BoardSize size)
    {
        foreach (var stone in group)
        {
            foreach (var neighbour in stone.Neighbors(size))
            {
                if (neighbour == point)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
