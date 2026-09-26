using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using GoEngine.App.Rendering;
using GoEngine.Core;
using AvaloniaPoint = Avalonia.Point;
using GoPoint = GoEngine.Core.Point;

namespace GoEngine.App.Controls;

/// <summary>Элемент управления, рисующий доску и принимающий щелчки по точкам.</summary>
/// <remarks>
/// Рисование делегируется <see cref="BoardRenderer"/>, попадание щелчка в точку считает
/// <see cref="BoardGeometry"/>. Правил здесь нет: доска сообщает точку событием
/// <see cref="MoveRequested"/>, а решает, можно ли туда ходить, партия в <c>Core</c>.
/// </remarks>
public sealed class BoardControl : Control
{
    /// <summary>Свойство позиции: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<Board?> BoardProperty =
        AvaloniaProperty.Register<BoardControl, Board?>(nameof(Board));

    /// <summary>Свойство последнего хода: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<GoPoint?> LastMoveProperty =
        AvaloniaProperty.Register<BoardControl, GoPoint?>(nameof(LastMove));

    /// <summary>Свойство точки под курсором: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<GoPoint?> HoverPointProperty =
        AvaloniaProperty.Register<BoardControl, GoPoint?>(nameof(HoverPoint));

    static BoardControl()
    {
        AffectsRender<BoardControl>(BoardProperty, LastMoveProperty, HoverPointProperty);
    }

    /// <summary>Игрок щёлкнул по пустой точке доски.</summary>
    public event EventHandler<MoveRequestedEventArgs>? MoveRequested;

    /// <summary>Позиция, которую рисует элемент.</summary>
    public Board? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    /// <summary>Точка последнего хода или <c>null</c>, если ходов ещё не было.</summary>
    public GoPoint? LastMove
    {
        get => GetValue(LastMoveProperty);
        set => SetValue(LastMoveProperty, value);
    }

    /// <summary>Точка под курсором или <c>null</c>, если курсор вне доски.</summary>
    public GoPoint? HoverPoint
    {
        get => GetValue(HoverPointProperty);
        set => SetValue(HoverPointProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var board = Board;

        if (board is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new BoardDrawOperation(board, new Rect(Bounds.Size), LastMove, HoverPoint));
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerPressed(e);

        if (PointAt(e.GetPosition(this)) is { } point)
        {
            MoveRequested?.Invoke(this, new MoveRequestedEventArgs(point));
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerMoved(e);

        var point = PointAt(e.GetPosition(this));
        var hover = point is { } candidate && Board?.IsEmpty(candidate) == true ? candidate : (GoPoint?)null;

        if (hover != HoverPoint)
        {
            SetCurrentValue(HoverPointProperty, hover);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        base.OnPointerExited(e);

        SetCurrentValue(HoverPointProperty, null);
    }

    /// <summary>Переводит пиксельные координаты окна в точку доски.</summary>
    /// <param name="position">Координаты указателя внутри элемента.</param>
    /// <returns>Точка доски или <c>null</c>, если указатель вне пересечений линий.</returns>
    private GoPoint? PointAt(AvaloniaPoint position)
    {
        var board = Board;

        if (board is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return null;
        }

        var geometry = BoardGeometry.Fit(board.Size, (float)Bounds.Width, (float)Bounds.Height);

        return geometry.PointAt((float)position.X, (float)position.Y);
    }
}

/// <summary>Операция рисования доски прямо на холсте Skia.</summary>
/// <remarks>
/// Прямой доступ к Skia даёт предсказуемую скорость на сотнях камней (D-001).
/// Если хост-платформа не предоставляет Skia, рисование просто пропускается.
/// </remarks>
internal sealed class BoardDrawOperation : ICustomDrawOperation
{
    private readonly Board _board;
    private readonly GoPoint? _lastMove;
    private readonly GoPoint? _hover;

    internal BoardDrawOperation(Board board, Rect bounds, GoPoint? lastMove, GoPoint? hover)
    {
        _board = board;
        _lastMove = lastMove;
        _hover = hover;
        Bounds = bounds;
    }

    /// <inheritdoc />
    public Rect Bounds { get; }

    /// <inheritdoc />
    public bool HitTest(AvaloniaPoint p) => Bounds.Contains(p);

    /// <inheritdoc />
    public bool Equals(ICustomDrawOperation? other) => false;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public void Render(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature lease)
        {
            return;
        }

        using var api = lease.Lease();

        BoardRenderer.Draw(
            api.SkCanvas,
            _board,
            (float)Bounds.Width,
            (float)Bounds.Height,
            _lastMove,
            _hover);
    }
}
