using GoEngine.App.Rendering;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.App.ViewModels;

/// <summary>Подсветка форм: что построено, какая форма выбрана и какие номера стоят на доске.</summary>
/// <remarks>
/// <para>
/// Один механизм на задачи, уроки и разбор партий: форма считается по камням, о которых идёт речь
/// (ходы решения задачи, ход шага урока, ход партии). Формы различаются номерами, а не цветом
/// (замечание пользователя 2026-10-11): номер стоит и в списке, и значком на доске.
/// </para>
/// <para>
/// Пока форм не больше одной, номера не показываются: подсвечивается единственная форма, и списка нет.
/// </para>
/// </remarks>
public sealed class ShapeHighlight
{
    /// <summary>Формы позиции, от крупной к мелкой.</summary>
    public IReadOnlyList<ShapeItem> Shapes { get; private set; } = [];

    /// <summary>Выбранная форма или <c>null</c>, когда форм нет.</summary>
    public ShapeItem? Selected => Shapes.Count == 0 ? null : Shapes[Math.Clamp(_selected, 0, Shapes.Count - 1)];

    /// <summary>Больше одной формы: тогда показываются номера и список.</summary>
    public bool HasSeveral => Shapes.Count > 1;

    /// <summary>Камни выбранной формы: по ним рисуется подсветка.</summary>
    public IReadOnlyList<Point> Points => Selected?.Points ?? [];

    /// <summary>Значки с номерами на доске: по одному на форму, и только когда форм несколько.</summary>
    public IReadOnlyList<ShapeBadge> Badges => HasSeveral
        ? [.. Shapes.Select(shape => new ShapeBadge(shape.Points[0], shape.Number))]
        : [];

    /// <summary>Строка разбора: «Формы: 1) пасть тигра · 2) ноби».</summary>
    public string Summary => Shapes.Count switch
    {
        0 => string.Empty,
        1 => $"Форма: {Shapes[0].Name}",
        _ => "Формы: " + string.Join(" · ", Shapes.Select(shape => shape.Label))
    };

    /// <summary>Считает формы заново по камням позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="colour">Цвет того, кто строил формы.</param>
    /// <param name="stones">Камни, о которых идёт речь.</param>
    /// <param name="goodOnly">Оставить только хорошие формы: плохие в список решения не попадают.</param>
    /// <returns><c>true</c>, если набор форм изменился.</returns>
    /// <remarks>
    /// Отбор плохих форм нужен режиму задач: в «Формах решения» не должно быть формы с оценкой
    /// «плохая» — решение не подаётся как правильная форма (находка QA 2026-10-17: пустой
    /// треугольник попадал в чипы решённой задачи). Уроки и разбор партий показывают форму
    /// хода как есть, поэтому по умолчанию отбора нет.
    /// </remarks>
    public bool Refresh(Board board, StoneColor colour, IReadOnlyList<Point> stones, bool goodOnly = false)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(colour);
        ArgumentNullException.ThrowIfNull(stones);

        // Отбор идёт до нумерации: номера чипов и значков на доске обязаны идти подряд, 1) 2) 3),
        // без пропусков на месте отброшенной плохой формы.
        var shapes = ShapeSet.Of(board, colour, stones)
            .Where(shape => !goodOnly || shape.Good)
            .Select((shape, index) => new ShapeItem(index + 1, shape.Name, shape.Good, shape.Points))
            .ToList();

        var changed = shapes.Count != Shapes.Count || !shapes.SequenceEqual(Shapes);

        Shapes = shapes;
        _selected = 0;

        return changed;
    }

    /// <summary>Снимает подсветку: форм нет.</summary>
    /// <returns><c>true</c>, если до этого формы были.</returns>
    public bool Clear()
    {
        var had = Shapes.Count > 0;

        Shapes = [];
        _selected = 0;

        return had;
    }

    /// <summary>Выбирает форму по номеру в списке.</summary>
    /// <param name="index">Номер формы, начиная с нуля.</param>
    /// <returns><c>true</c>, если выбор изменился.</returns>
    public bool Select(int index)
    {
        if (index < 0 || index >= Shapes.Count || index == _selected)
        {
            return false;
        }

        _selected = index;

        return true;
    }

    /// <summary>Номер выбранной формы в списке.</summary>
    private int _selected;
}
