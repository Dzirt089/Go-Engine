namespace GoEngine.Core;

/// <summary>Группа камней одного цвета, соединённых по стороне.</summary>
/// <param name="Color">Цвет камней группы: <see cref="StoneColor.Black"/> или <see cref="StoneColor.White"/>.</param>
/// <param name="Stones">Точки, занятые камнями группы.</param>
/// <param name="Liberties">Дамэ группы — пустые точки, соседние с камнями по стороне.</param>
/// <remarks>
/// Группа неизменяема: <see cref="Stones"/> и <see cref="Liberties"/> доступны только для чтения,
/// потому что меняются вместе с доской, а не сами по себе. Диагональные касания в группу не входят
/// (<c>GO_RULES.md</c>, п. 4).
/// </remarks>
public readonly record struct Group(StoneColor Color, IReadOnlyList<Point> Stones, IReadOnlySet<Point> Liberties)
{
    /// <summary>Проверяет, что у группы осталась ровно одна дамэ.</summary>
    /// <remarks>Атари — состояние группы, а не хода: после ответа противника группа снимается.</remarks>
    public bool IsInAtari => Liberties.Count == 1;

    /// <summary>Проверяет, что у группы не осталось дамэ.</summary>
    /// <remarks>Такая группа снимается с доски сразу после хода противника (<c>GO_RULES.md</c>, п. 4).</remarks>
    public bool IsCaptured => Liberties.Count == 0;
}
