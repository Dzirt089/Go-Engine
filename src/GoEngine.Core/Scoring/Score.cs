namespace GoEngine.Core;

/// <summary>Счёт партии: очки чёрных и белых вместе с коми.</summary>
/// <param name="Black">Очки чёрных: камни плюс территория.</param>
/// <param name="White">Очки белых: камни плюс территория плюс коми.</param>
/// <remarks>
/// Счёт считается по китайским правилам (<c>GO_RULES.md</c>, п. 9), где очки — это площадь:
/// каждый камень и каждая окружённая точка приносят очко.
/// </remarks>
public readonly record struct Score(double Black, double White)
{
    /// <summary>Победитель: <see cref="StoneColor.Black"/>, <see cref="StoneColor.White"/> или <see cref="StoneColor.Empty"/> при ничьей.</summary>
    public StoneColor Winner =>
        Black > White ? StoneColor.Black :
        White > Black ? StoneColor.White :
        StoneColor.Empty;

    /// <summary>Разница очков без знака: на сколько победитель опередил соперника.</summary>
    public double Margin => Math.Abs(Black - White);

    /// <summary>Возвращает счёт в виде «Чёрные 12 : 7.5 Белые».</summary>
    /// <returns>Очки обеих сторон.</returns>
    public override string ToString() => $"Чёрные {Black} : {White} Белые";
}
