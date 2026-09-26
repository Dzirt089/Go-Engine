using GoEngine.Core;

namespace GoEngine.App.Controls;

/// <summary>Запрос хода от доски: игрок щёлкнул по точке.</summary>
/// <remarks>
/// Доска не решает, легален ли ход и чей он: она только сообщает точку. Правила проверяет
/// партия в <c>Core</c>, а обрабатывает запрос окно.
/// </remarks>
public sealed class MoveRequestedEventArgs : EventArgs
{
    /// <summary>Создаёт аргументы события.</summary>
    /// <param name="point">Точка, по которой щёлкнул игрок.</param>
    public MoveRequestedEventArgs(Point point) => Point = point;

    /// <summary>Точка, по которой щёлкнул игрок.</summary>
    public Point Point { get; }
}
