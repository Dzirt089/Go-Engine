using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using GoEngine.App.Rendering;
using GoEngine.Core;
using AvaloniaPoint = Avalonia.Point;

namespace GoEngine.App.Controls;

/// <summary>Элемент управления, рисующий доску и камни.</summary>
/// <remarks>
/// Рисование делегируется <see cref="BoardRenderer"/>: элемент отвечает только за то, когда
/// и в каких границах рисовать. Бизнес-логики здесь нет — правила живут в <c>Core</c>.
/// </remarks>
public sealed class BoardControl : Control
{
    /// <summary>Свойство позиции: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<Board?> BoardProperty =
        AvaloniaProperty.Register<BoardControl, Board?>(nameof(Board));

    static BoardControl() => AffectsRender<BoardControl>(BoardProperty);

    /// <summary>Позиция, которую рисует элемент.</summary>
    public Board? Board
    {
        get => GetValue(BoardProperty);
        set => SetValue(BoardProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var board = Board;

        if (board is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new BoardDrawOperation(board, new Rect(Bounds.Size)));
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

    internal BoardDrawOperation(Board board, Rect bounds)
    {
        _board = board;
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

        BoardRenderer.Draw(api.SkCanvas, _board, (float)Bounds.Width, (float)Bounds.Height);
    }
}
