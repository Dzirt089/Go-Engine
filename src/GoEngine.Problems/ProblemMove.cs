namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Принимаемый ход дерева решения и его продолжение.</summary>
/// <param name="Move">Ход.</param>
/// <param name="Next">Узел после хода; лист означает, что цель достигнута.</param>
public readonly record struct ProblemMove(Move Move, ProblemNode Next)
{
    /// <summary>Создаёт принимаемый ход постановки камня.</summary>
    /// <param name="point">Точка доски.</param>
    /// <param name="color">Цвет камня.</param>
    /// <param name="next">Продолжение после хода.</param>
    /// <returns>Принимаемый ход.</returns>
    public static ProblemMove Play(Point point, StoneColor color, ProblemNode next) =>
        new(Move.Play(point, color), next);

    /// <summary>Создаёт принимаемый пас.</summary>
    /// <param name="color">Цвет, который пасует.</param>
    /// <param name="next">Продолжение после паса.</param>
    /// <returns>Принимаемый пас.</returns>
    public static ProblemMove Pass(StoneColor color, ProblemNode next) =>
        new(Move.Pass(color), next);
}
