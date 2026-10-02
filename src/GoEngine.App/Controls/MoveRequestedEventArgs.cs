using GoEngine.Core;

namespace GoEngine.App.Controls;

/// <summary>Запрос от доски: игрок щёлкнул по точке.</summary>
/// <remarks>
/// Доска не решает, легален ли ход и чей он: она только сообщает точку и кнопку мыши. Правила
/// проверяет партия в <c>Core</c>, а обрабатывает запрос вид.
/// </remarks>
public sealed class MoveRequestedEventArgs : EventArgs
{
    /// <summary>Создаёт аргументы события.</summary>
    /// <param name="point">Точка, по которой щёлкнул игрок.</param>
    /// <param name="marksDead">
    /// Щелчок правой кнопкой: пометить группу мёртвой или снять пометку, а не играть ход.
    /// </param>
    public MoveRequestedEventArgs(Point point, bool marksDead = false)
    {
        Point = point;
        MarksDead = marksDead;
    }

    /// <summary>Точка, по которой щёлкнул игрок.</summary>
    public Point Point { get; }

    /// <summary>Щелчок помечает мёртвую группу, а не играет ход.</summary>
    /// <remarks>
    /// Правой кнопкой игрок говорит «эта группа мертва»: тогда её камни уходят в пленные,
    /// а её точки — в территорию соперника, и счёт меняется сразу. Левая кнопка остаётся ходом —
    /// отбирать её под пометки нельзя.
    /// </remarks>
    public bool MarksDead { get; }
}
