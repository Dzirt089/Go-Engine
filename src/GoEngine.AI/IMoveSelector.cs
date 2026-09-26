using GoEngine.Core;

namespace GoEngine.AI;

/// <summary>Выбор хода за игрока.</summary>
/// <remarks>
/// Селектор не меняет партию: он только предлагает ход, а применяет его вызывающий код
/// (<see cref="GameState.Play"/> или <see cref="SelfPlayHarness"/>). Селектор обязан возвращать
/// легальный ход: нелегальный ход — нарушение инварианта, а не ожидаемый исход.
/// </remarks>
public interface IMoveSelector
{
    /// <summary>Имя селектора для UI и логов партии.</summary>
    string Name { get; }

    /// <summary>Выбирает ход в текущей позиции партии.</summary>
    /// <param name="state">Партия, в которой нужно сделать ход.</param>
    /// <returns>Легальный ход за цвет <see cref="GameState.ToMove"/>.</returns>
    Move SelectMove(GameState state);
}
