namespace GoEngine.Core;

/// <summary>Тип хода.</summary>
/// <remarks>Идентификаторы: <c>Play</c> = 0, <c>Pass</c> = 1, <c>Resign</c> = 2.</remarks>
public sealed class MoveType : Enumeration
{
    private const int PlayId = 0;
    private const int PassId = 1;
    private const int ResignId = 2;

    private MoveType(int id, string name) : base(id, name)
    {
    }

    /// <summary>Поставить камень на пустую точку.</summary>
    public static MoveType Play { get; } = new(PlayId, nameof(Play));

    /// <summary>Пропустить ход.</summary>
    public static MoveType Pass { get; } = new(PassId, nameof(Pass));

    /// <summary>Сдаться. После такого хода партия окончена.</summary>
    public static MoveType Resign { get; } = new(ResignId, nameof(Resign));
}
