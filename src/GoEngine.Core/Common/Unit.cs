namespace GoEngine.Core;

/// <summary>Значение-заглушка для <see cref="Result{T}"/> там, где полезного значения нет.</summary>
/// <remarks>
/// Нужен там, где важен только факт успеха, например у <see cref="Board.IsLegal"/>.
/// </remarks>
public readonly record struct Unit
{
    /// <summary>Единственное значение типа.</summary>
    public static Unit Value => default;
}
