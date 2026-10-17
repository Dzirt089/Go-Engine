namespace GoEngine.App.ViewModels;

using GoEngine.Core;

/// <summary>Форма решения для показа: номер, название, оценка и камни.</summary>
/// <param name="Number">Номер формы на доске и в списке, с единицы.</param>
/// <param name="Name">Название формы: «пасть тигра», «ноби».</param>
/// <param name="Good">Хорошая ли форма.</param>
/// <param name="Points">Камни формы.</param>
/// <param name="Why">Чем форма хороша или плоха и что из этого следует: объяснение для новичка.</param>
/// <remarks>
/// Номер — это то, чем различаются формы: цветом их различать нельзя (замечание пользователя
/// 2026-10-11). Номер ставится на первый камень формы и повторяется в списке. Пояснение
/// <paramref name="Why"/> приходит из распознавания формы (<c>MoveShape</c>): жалуются, что
/// названия «ноби» и «пасть тигра» нигде не расшифрованы (находка QA L10 отчёта 2026-10-17).
/// </remarks>
public readonly record struct ShapeItem(
    int Number,
    string Name,
    bool Good,
    IReadOnlyList<Point> Points,
    string Why = "")
{
    /// <summary>Подпись для списка: «1) пасть тигра».</summary>
    public string Label => $"{Number}) {Name}";
}
