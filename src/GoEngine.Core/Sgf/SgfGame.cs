namespace GoEngine.Core.Sgf;

using GoEngine.Core;

/// <summary>Партия в формате SGF: размер доски, коми, ходы и комментарий.</summary>
/// <param name="Size">Размер доски.</param>
/// <param name="Komi">Коми партии.</param>
/// <param name="Moves">Ходы партии по порядку: постановка камня и пас.</param>
/// <param name="Comment">Комментарий к партии (<c>C</c> корневого узла) или <c>null</c>.</param>
/// <remarks>
/// Сдача (<see cref="MoveType.Resign"/>) ходом не записывается: в SGF её место — свойство
/// результата <c>RE</c>, а не ход. Чтение и запись не работают с файлами: файловый ввод-вывод
/// остаётся слою <c>App</c>, а здесь только формат.
/// </remarks>
public readonly record struct SgfGame(BoardSize Size, Komi Komi, IReadOnlyList<Move> Moves, string? Comment)
{
    /// <summary>Создаёт партию по разобранным данным.</summary>
    /// <returns>Партия или причина отказа, если ход не проходит по правилам.</returns>
    public Result<GameState> ToGameState()
    {
        var game = GameState.NewGame(Size, Komi);

        foreach (var move in Moves)
        {
            var played = game.Play(move);

            if (!played.IsSuccess)
            {
                return Result<GameState>.Fail($"Ход {move.Point} в партии из SGF не проходит по правилам: {played.Error}");
            }
        }

        return Result<GameState>.Ok(game);
    }
}
