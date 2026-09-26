namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Проведение партии AI против AI.</summary>
/// <remarks>
/// Класс не логирует (<c>AGENTS_GO.md</c>, п. 8) и не создаёт своего <see cref="Random"/>:
/// случайность полностью принадлежит селекторам. Селектор обязан возвращать легальный ход;
/// нелегальный ход — нарушение инварианта AI, поэтому партия прерывается <see cref="AiException"/>.
/// </remarks>
public sealed class SelfPlayHarness
{
    /// <summary>Играет партию до завершения.</summary>
    /// <param name="black">Селектор чёрных.</param>
    /// <param name="white">Селектор белых.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="seed">Зерно партии: селекторы должны быть созданы с тем же зерном,
    /// тогда партия воспроизводима.</param>
    /// <returns>Итог партии, все её ходы и финальная доска.</returns>
    /// <exception cref="AiException">Селектор вернул ход, который партия не приняла.</exception>
    public SelfPlayResult PlayGame(IMoveSelector black, IMoveSelector white, BoardSize size, Komi komi, int seed)
    {
        ArgumentNullException.ThrowIfNull(black);
        ArgumentNullException.ThrowIfNull(white);
        ArgumentOutOfRangeException.ThrowIfNegative(seed);

        var game = GameState.NewGame(size, komi);

        while (!game.Status.IsFinished)
        {
            var selector = game.ToMove == StoneColor.Black ? black : white;
            var move = selector.SelectMove(game);
            var played = game.Play(move);

            if (!played.IsSuccess)
            {
                throw new AiException(
                    $"Селектор «{selector.Name}» вернул нелегальный ход {move}: {played.Error}");
            }
        }

        return new SelfPlayResult(game.Finish().Value, game.Moves, game.Board);
    }
}
