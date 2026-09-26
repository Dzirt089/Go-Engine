namespace GoEngine.AI;

using GoEngine.Core;

/// <summary>Итог партии, сыгранной AI против AI.</summary>
/// <param name="Result">Итог партии: способ завершения, счёт и победитель.</param>
/// <param name="Moves">Все ходы партии в порядке игры.</param>
/// <param name="FinalBoard">Доска на момент завершения партии.</param>
/// <remarks>
/// Запись партии нужна тестам силы и разбору ошибок: по <paramref name="Moves"/> партию
/// можно воспроизвести на новом <see cref="GameState"/>, а <paramref name="FinalBoard"/> —
/// позиция, по которой считался счёт.
/// </remarks>
public readonly record struct SelfPlayResult(GameResult Result, IReadOnlyList<Move> Moves, Board FinalBoard);
