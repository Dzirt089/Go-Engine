namespace GoEngine.Core;

/// <summary>Результат хода: какие камни сняты с доски.</summary>
/// <param name="CapturedStones">Снятые камни в порядке обхода групп или пустой список.</param>
/// <remarks>
/// Снятие камней — обычный исход хода, а не нарушение инварианта движка,
/// поэтому результат возвращается значением, а не исключением (<c>AGENTS.md</c>, п. 4).
/// </remarks>
public readonly record struct CaptureResult(IReadOnlyList<Point> CapturedStones)
{
    /// <summary>Ход ничего не снял.</summary>
    public static CaptureResult None => new([]);

    /// <summary>Проверяет, что снят хотя бы один камень.</summary>
    public bool HasCaptures => CapturedStones is { Count: > 0 };
}
