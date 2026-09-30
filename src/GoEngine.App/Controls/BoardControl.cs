using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
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

    /// <summary>Свойство разметки территории: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<IReadOnlyList<StoneColor>?> TerritoryProperty =
        AvaloniaProperty.Register<BoardControl, IReadOnlyList<StoneColor>?>(nameof(Territory));

    /// <summary>Свойство пометки мёртвых камней: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<IReadOnlyList<GoPoint>?> DeadPointsProperty =
        AvaloniaProperty.Register<BoardControl, IReadOnlyList<GoPoint>?>(nameof(DeadPoints));

    /// <summary>Свойство точки подсказки: изменение перерисовывает доску.</summary>
    public static readonly StyledProperty<GoPoint?> HintPointProperty =
        AvaloniaProperty.Register<BoardControl, GoPoint?>(nameof(HintPoint));

    /// <summary>Свойство показа координат: выключенные подписи не перерисовываются.</summary>
    public static readonly StyledProperty<bool> ShowCoordinatesProperty =
        AvaloniaProperty.Register<BoardControl, bool>(nameof(ShowCoordinates), defaultValue: true);

    /// <summary>Свойство итога партии: по нему доска подсвечивается по краю.</summary>
    public static readonly StyledProperty<BoardOutcome> OutcomeProperty =
        AvaloniaProperty.Register<BoardControl, BoardOutcome>(nameof(Outcome));

    static BoardControl()
    {
        AffectsRender<BoardControl>(
            BoardProperty,
            LastMoveProperty,
            HoverPointProperty,
            TerritoryProperty,
            DeadPointsProperty,
            HintPointProperty,
            ShowCoordinatesProperty,
            OutcomeProperty);
    }

    private static readonly TimeProvider Clock = TimeProvider.System;

    private readonly DispatcherTimer _timer;

    private StoneAnimation _animation = StoneAnimation.None;
    private DateTimeOffset _lastFrame;

    /// <summary>Создаёт доску и запускает таймер кадров анимации.</summary>
    public BoardControl()
    {
        // Кадры двигает таймер: ход не блокирует интерфейс, доска просто перерисовывается.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnAnimationTick;
    }

    /// <summary>Игрок щёлкнул по пустой точке доски.</summary>
    public event EventHandler<MoveRequestedEventArgs>? MoveRequested;

    /// <summary>Запускает анимацию хода: появление камня и исчезновение снятых.</summary>
    /// <param name="appearing">Точка поставленного камня или <c>null</c>.</param>
    /// <param name="disappearing">Точки снятых камней.</param>
    /// <param name="disappearingColor">Цвет снятых камней.</param>
    public void Animate(GoPoint? appearing, IReadOnlyList<GoPoint> disappearing, StoneColor disappearingColor)
    {
        ArgumentNullException.ThrowIfNull(disappearing);

        _animation = new StoneAnimation(appearing, disappearing, disappearingColor, 0);

        if (!_animation.IsActive)
        {
            return;
        }

        _lastFrame = Clock.GetUtcNow();
        _timer.Start();
        InvalidateVisual();
    }

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

    /// <summary>Владение точками для показа территории или <c>null</c>, если её не показываем.</summary>
    public IReadOnlyList<StoneColor>? Territory
    {
        get => GetValue(TerritoryProperty);
        set => SetValue(TerritoryProperty, value);
    }

    /// <summary>Камни, помеченные мёртвыми при подсчёте, или <c>null</c>, если подсчёт не идёт.</summary>
    public IReadOnlyList<GoPoint>? DeadPoints
    {
        get => GetValue(DeadPointsProperty);
        set => SetValue(DeadPointsProperty, value);
    }

    /// <summary>Точка подсказки или <c>null</c>, если подсказки нет.</summary>
    /// <remarks>
    /// Маркер подсказки рисуется поверх позиции и не участвует в правилах: им пользуется
    /// режим задач, чтобы показать первый правильный ход.
    /// </remarks>
    public GoPoint? HintPoint
    {
        get => GetValue(HintPointProperty);
        set => SetValue(HintPointProperty, value);
    }

    /// <summary>Рисовать ли подписи координат вокруг сетки.</summary>
    /// <remarks>
    /// На телефоне подписи выключаются, когда клетка становится мелкой: решение принимает
    /// раскладка (<c>BoardLayoutRules.CoordinatesFit</c>), а элемент только рисует по нему.
    /// </remarks>
    public bool ShowCoordinates
    {
        get => GetValue(ShowCoordinatesProperty);
        set => SetValue(ShowCoordinatesProperty, value);
    }

    /// <summary>Итог партии глазами игрока: доска подсвечивается рамкой по краю.</summary>
    /// <remarks>
    /// На телефоне и в настольном окне рамка одного цвета: синий выигрыш, красный проигрыш,
    /// серый ничья. Решение принимает модель представления, элемент только передаёт его рендереру.
    /// </remarks>
    public BoardOutcome Outcome
    {
        get => GetValue(OutcomeProperty);
        set => SetValue(OutcomeProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var board = Board;

        if (board is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var frame = _animation.IsActive ? _animation : (StoneAnimation?)null;

        // Обрезка по своим границам обязательна: рисование идёт прямо на холсте Skia
        // (ICustomDrawOperation), а он не обрезан по границам элемента. Без неё заливка фона
        // доски закрывала бы соседние элементы — так доска закрашивала строку состояния
        // и фон мобильной раскладки.
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.Custom(new BoardDrawOperation(
                board,
                new Rect(Bounds.Size),
                LastMove,
                HoverPoint,
                frame,
                Territory,
                DeadPoints,
                HintPoint,
                ShowCoordinates,
                Outcome));
        }
    }

    /// <summary>Продвигает кадр анимации.</summary>
    /// <param name="sender">Таймер кадров.</param>
    /// <param name="e">Событие таймера.</param>
    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var now = Clock.GetUtcNow();

        _animation = _animation.Advance((now - _lastFrame).TotalSeconds);
        _lastFrame = now;

        if (!_animation.IsActive)
        {
            _timer.Stop();
        }

        InvalidateVisual();
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
    private readonly StoneAnimation? _animation;
    private readonly IReadOnlyList<StoneColor>? _territory;
    private readonly IReadOnlyList<GoPoint>? _deadPoints;
    private readonly GoPoint? _hint;
    private readonly bool _showCoordinates;
    private readonly BoardOutcome _outcome;

    internal BoardDrawOperation(
        Board board,
        Rect bounds,
        GoPoint? lastMove,
        GoPoint? hover,
        StoneAnimation? animation,
        IReadOnlyList<StoneColor>? territory,
        IReadOnlyList<GoPoint>? deadPoints,
        GoPoint? hint,
        bool showCoordinates,
        BoardOutcome outcome)
    {
        _board = board;
        _lastMove = lastMove;
        _hover = hover;
        _animation = animation;
        _territory = territory;
        _deadPoints = deadPoints;
        _hint = hint;
        _showCoordinates = showCoordinates;
        _outcome = outcome;
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
            _hover,
            _animation,
            _territory,
            _deadPoints,
            _hint,
            _showCoordinates,
            _outcome);
    }
}
