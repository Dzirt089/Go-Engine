namespace GoEngine.Core;

/// <summary>Цвет камня или пустая точка.</summary>
/// <remarks>Идентификаторы: <c>Empty</c> = 0, <c>Black</c> = 1, <c>White</c> = 2.</remarks>
public sealed class StoneColor : Enumeration
{
    private const int EmptyId = 0;
    private const int BlackId = 1;
    private const int WhiteId = 2;

    private StoneColor(int id, string name) : base(id, name)
    {
    }

    /// <summary>Пустая точка доски.</summary>
    public static StoneColor Empty { get; } = new(EmptyId, nameof(Empty));

    /// <summary>Чёрные камни. Ходят первыми.</summary>
    public static StoneColor Black { get; } = new(BlackId, nameof(Black));

    /// <summary>Белые камни. Ходят вторыми.</summary>
    public static StoneColor White { get; } = new(WhiteId, nameof(White));

    /// <summary>Возвращает противоположный цвет.</summary>
    /// <returns><see cref="White"/> для <see cref="Black"/> и наоборот.</returns>
    /// <exception cref="DomainException">Вызван у <see cref="Empty"/>: у пустой точки нет цвета.</exception>
    public StoneColor Opponent() => Id switch
    {
        BlackId => White,
        WhiteId => Black,
        _ => throw new DomainException("У пустой точки нет противоположного цвета.")
    };
}
