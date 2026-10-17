namespace GoEngine.Core;

/// <summary>Состояние партии.</summary>
/// <remarks>Идентификаторы: <c>InProgress</c> = 0, <c>FinishedByTwoPasses</c> = 1,
/// <c>FinishedByResign</c> = 2, <c>FinishedByMoveLimit</c> = 3, <c>FinishedByAgreement</c> = 4.</remarks>
public sealed class GameStatus : Enumeration
{
    private const int InProgressId = 0;
    private const int TwoPassesId = 1;
    private const int ResignId = 2;
    private const int MoveLimitId = 3;
    private const int AgreementId = 4;

    private GameStatus(int id, string name) : base(id, name)
    {
    }

    /// <summary>Партия идёт.</summary>
    public static GameStatus InProgress { get; } = new(InProgressId, nameof(InProgress));

    /// <summary>Партия завершена двумя последовательными пасами (<c>GO_RULES.md</c>, п. 7–8).</summary>
    public static GameStatus FinishedByTwoPasses { get; } = new(TwoPassesId, nameof(FinishedByTwoPasses));

    /// <summary>Партия завершена сдачей одного из игроков (<c>GO_RULES.md</c>, п. 3).</summary>
    public static GameStatus FinishedByResign { get; } = new(ResignId, nameof(FinishedByResign));

    /// <summary>Партия завершена достижением лимита ходов (<c>GO_RULES.md</c>, п. 8).</summary>
    public static GameStatus FinishedByMoveLimit { get; } = new(MoveLimitId, nameof(FinishedByMoveLimit));

    /// <summary>Партия завершена по согласию сторон: игрок решил закончить, счёт подведён.</summary>
    /// <remarks>
    /// Отдельное состояние, а не «два паса»: никто не пасовал, и называть такую партию
    /// завершённой пасами значило бы соврать игроку о том, что произошло. Завершает её
    /// <see cref="GameState.FinishByAgreement"/>; счёт считается по текущей позиции,
    /// как и при двух пасах.
    /// </remarks>
    public static GameStatus FinishedByAgreement { get; } = new(AgreementId, nameof(FinishedByAgreement));

    /// <summary>Проверяет, что партия завершена.</summary>
    public bool IsFinished => this != InProgress;
}
