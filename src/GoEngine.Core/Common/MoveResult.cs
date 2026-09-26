namespace GoEngine.Core;

/// <summary>Итог попытки хода: легален ли ход, что снято и почему отказ.</summary>
/// <param name="IsLegal">Признак легального хода.</param>
/// <param name="Move">Проверяемый ход.</param>
/// <param name="Captured">Камни, снятые ходом; пусто, если ход нелегален или ничего не снято.</param>
/// <param name="Reason">Причина отказа на русском языке или <c>null</c> у легального хода.</param>
/// <remarks>
/// Тип описывает исход хода целиком — и отказ, и снятие камней, — чтобы слой AI и партия
/// работали с одним значением вместо исключений.
/// </remarks>
public readonly record struct MoveResult(bool IsLegal, Move Move, IReadOnlyList<Point> Captured, string? Reason);
