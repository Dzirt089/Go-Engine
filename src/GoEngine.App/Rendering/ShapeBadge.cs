using GoEngine.Core;

namespace GoEngine.App.Rendering;

/// <summary>Номер формы на доске: значок с цифрой у камня, как булавка на карте.</summary>
/// <param name="Point">Камень, у которого стоит значок.</param>
/// <param name="Number">Номер формы, с единицы.</param>
/// <remarks>
/// Замечание пользователя 2026-10-11: «номера прямо на доске (значок с цифрой у каждого участка,
/// как булавки на карте)». Значок ставится на первый камень формы и только когда форм больше одной:
/// одна форма в номере не нуждается.
/// </remarks>
public readonly record struct ShapeBadge(Point Point, int Number);
