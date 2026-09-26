namespace GoEngine.Core;

/// <summary>Итог завершённой партии.</summary>
/// <param name="Status">Как завершилась партия.</param>
/// <param name="Score">Счёт партии или <c>null</c>, если партия закончилась сдачей.</param>
/// <param name="Winner">Победитель или <c>null</c>, если победитель ещё не определён.</param>
/// <param name="Reason">Причина завершения словами или <c>null</c>, если причина видна из <paramref name="Status"/>.</param>
/// <remarks>
/// Счёт по китайским правилам (<c>GO_RULES.md</c>, п. 9) считается только тогда, когда партия
/// дошла до подсчёта: после двух пасов или по лимиту ходов. При сдаче счёта нет.
/// </remarks>
public readonly record struct GameResult(GameStatus Status, Score? Score, StoneColor? Winner, string? Reason);
