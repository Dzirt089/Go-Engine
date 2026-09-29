using System.Globalization;
using GoEngine.Core;
using SkiaSharp;

namespace GoEngine.App.Rendering;

/// <summary>Рисование доски и камней на холсте Skia.</summary>
/// <remarks>
/// Единственная реализация рисования: её используют и элемент управления на экране
/// (<see cref="Controls.BoardControl"/>), и проверочный рендер в PNG из командной строки.
/// Бизнес-логики здесь нет — только геометрия и краски; правила живут в <c>Core</c>.
/// Прямая работа со SkiaSharp выбрана по D-001: сотни камней и будущая анимация требуют
/// предсказуемой скорости отрисовки.
/// </remarks>
public static class BoardRenderer
{
    /// <summary>Цвет дерева доски.</summary>
    private static readonly SKColor BoardColor = new(0xE3, 0xB6, 0x6E);

    /// <summary>Цвет линий сетки.</summary>
    private static readonly SKColor GridColor = new(0x3A, 0x2A, 0x14);

    /// <summary>Цвет чёрного камня.</summary>
    private static readonly SKColor BlackStoneColor = new(0x1A, 0x1A, 0x1A);

    /// <summary>Цвет белого камня.</summary>
    private static readonly SKColor WhiteStoneColor = new(0xF5, 0xF5, 0xF0);

    /// <summary>Цвет обводки белого камня.</summary>
    private static readonly SKColor WhiteStoneOutline = new(0x8A, 0x8A, 0x8A);

    /// <summary>Толщина линий сетки в пикселях.</summary>
    private const float GridStrokeWidth = 1f;

    /// <summary>Радиус звёздной точки в пикселях.</summary>
    private const float StarRadius = 3f;

    /// <summary>Цвет подсветки точки под курсором.</summary>
    private static readonly SKColor HoverColor = new(0x40, 0x40, 0x40, 0x60);

    /// <summary>Цвет маркера последнего хода.</summary>
    private static readonly SKColor LastMoveColor = new(0xC0, 0x28, 0x18);

    /// <summary>Толщина маркера последнего хода в пикселях.</summary>
    private const float LastMoveStrokeWidth = 2f;

    /// <summary>Доля радиуса камня, на которой рисуется маркер последнего хода.</summary>
    private const float LastMoveRadiusRatio = 0.35f;

    /// <summary>Доля клетки, которую занимает подпись координаты.</summary>
    /// <remarks>
    /// Долю читает и раскладка (<c>BoardLayoutRules.CoordinatesFit</c>): подписи не рисуются,
    /// когда от неё получается кегль мельче читаемого.
    /// </remarks>
    public const float CoordinateSizeRatio = 0.42f;

    /// <summary>Доля клетки, которую занимает знак территории.</summary>
    private const float TerritorySizeRatio = 0.34f;

    /// <summary>Цвет знака нейтральной точки: серый, полупрозрачный.</summary>
    private static readonly SKColor NeutralColor = new(0x5A, 0x5A, 0x5A, 0x99);

    /// <summary>Толщина линий знака нейтральной точки в пикселях.</summary>
    private const float NeutralStrokeWidth = 1.4f;

    /// <summary>Сдвиг базовой линии подписи к её середине, в долях кегля.</summary>
    private const float CoordinateBaselineRatio = 0.35f;

    /// <summary>Цвет метки мёртвого камня на чёрном камне.</summary>
    private static readonly SKColor DeadMarkOnBlack = new(0xF5, 0xF5, 0xF0);

    /// <summary>Цвет метки мёртвого камня на белом камне.</summary>
    private static readonly SKColor DeadMarkOnWhite = new(0x1A, 0x1A, 0x1A);

    /// <summary>Доля радиуса камня, которую занимает метка мёртвого камня.</summary>
    private const float DeadMarkRadiusRatio = 0.5f;

    /// <summary>Толщина метки мёртвого камня в пикселях.</summary>
    private const float DeadMarkStrokeWidth = 2.5f;

    /// <summary>Непрозрачность приглушения мёртвого камня: камень виден, но явно уходит с доски.</summary>
    private const byte DeadMarkFadeAlpha = 132;

    /// <summary>Цвет маркера подсказки: синий, чтобы не путался с красным маркером последнего хода.</summary>
    private static readonly SKColor HintColor = new(0x0F, 0x6C, 0xBD);

    /// <summary>Толщина кольца подсказки в пикселях.</summary>
    private const float HintStrokeWidth = 3f;

    /// <summary>Доля радиуса камня, которую занимает кольцо подсказки.</summary>
    private const float HintRadiusRatio = 1.3f;

    /// <summary>Буквы столбцов: латинские без «I», как принято в Го.</summary>
    private const string ColumnLetters = "ABCDEFGHJKLMNOPQRST";

    /// <summary>Рисует доску целиком.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="width">Ширина области рисования в пикселях.</param>
    /// <param name="height">Высота области рисования в пикселях.</param>
    /// <param name="lastMove">Точка последнего хода или <c>null</c>.</param>
    /// <param name="hover">Точка под курсором или <c>null</c>.</param>
    /// <param name="animation">Кадр анимации камней или <c>null</c>.</param>
    /// <param name="territory">Владение точками для показа территории или <c>null</c>.</param>
    /// <param name="deadPoints">Камни, помеченные мёртвыми при подсчёте, или <c>null</c>.</param>
    /// <param name="hint">Точка подсказки или <c>null</c>.</param>
    /// <param name="showCoordinates">Рисовать ли подписи координат.</param>
    /// <remarks>
    /// Координаты можно выключить: на телефоне доска 19×19 занимает те же точки, что и 9×9,
    /// и подписи в поле вокруг сетки становятся нечитаемыми. Решение принимает раскладка
    /// (<c>BoardLayoutRules.CoordinatesFit</c>), а не отрисовка.
    /// </remarks>
    public static void Draw(
        SKCanvas canvas,
        Board board,
        float width,
        float height,
        Point? lastMove = null,
        Point? hover = null,
        StoneAnimation? animation = null,
        IReadOnlyList<StoneColor>? territory = null,
        IReadOnlyList<Point>? deadPoints = null,
        Point? hint = null,
        bool showCoordinates = true)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(board);

        var geometry = BoardGeometry.Fit(board.Size, width, height);

        // Фон рисуется прямоугольником отведённой области, а не canvas.Clear: Clear закрашивает
        // весь холст вместе с частями окна за границами элемента.
        using (var background = new SKPaint { Color = BoardColor, IsAntialias = false, Style = SKPaintStyle.Fill })
        {
            canvas.DrawRect(new SKRect(0, 0, width, height), background);
        }

        DrawGrid(canvas, board.Size, geometry);

        if (showCoordinates)
        {
            DrawCoordinates(canvas, board.Size, geometry);
        }

        DrawStarPoints(canvas, board.Size, geometry);
        DrawStones(canvas, board, geometry, animation);
        DrawTerritory(canvas, board, geometry, territory);
        DrawDeadMarks(canvas, board, geometry, deadPoints);
        DrawMarkers(canvas, geometry, lastMove, hover);

        if (hint is { } hintPoint)
        {
            DrawHint(canvas, geometry, hintPoint);
        }
    }

    /// <summary>Помечает камни, отмеченные мёртвыми при подсчёте.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="deadPoints">Точки мёртвых камней или <c>null</c>.</param>
    /// <remarks>
    /// Помеченный камень не просто перечёркивается: он приглушается — поверх камня ложится
    /// полупрозрачный цвет доски, и камень выглядит уходящим с неё. Без приглушения игрок
    /// не отличал бы пометку от знака территории, а подтверждение подсчёта было бы кнопкой вслепую.
    /// Крест поверх камня рисуется обратным цветом: на чёрном белый, на белом чёрный.
    /// Пустые точки пропускаются: мёртвым помечают камень, а не пересечение.
    /// </remarks>
    private static void DrawDeadMarks(SKCanvas canvas, Board board, BoardGeometry geometry, IReadOnlyList<Point>? deadPoints)
    {
        if (deadPoints is null || deadPoints.Count == 0)
        {
            return;
        }

        var half = geometry.StoneRadius * DeadMarkRadiusRatio;
        var radius = geometry.StoneRadius;

        using var fade = new SKPaint
        {
            Color = BoardColor.WithAlpha(DeadMarkFadeAlpha),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        foreach (var point in deadPoints)
        {
            var stone = board.At(point);

            if (stone == StoneColor.Empty)
            {
                continue;
            }

            using var paint = new SKPaint
            {
                Color = stone == StoneColor.Black ? DeadMarkOnBlack : DeadMarkOnWhite,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = DeadMarkStrokeWidth
            };

            var center = geometry.Pixel(point);

            canvas.DrawCircle(center, radius, fade);
            canvas.DrawLine(center.X - half, center.Y - half, center.X + half, center.Y + half, paint);
            canvas.DrawLine(center.X - half, center.Y + half, center.X + half, center.Y - half, paint);
        }
    }

    /// <summary>Рисует маркер подсказки: кольцо вокруг точки и точку в середине.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="point">Точка подсказки.</param>
    /// <remarks>
    /// Кольцо рисуется снаружи камня и отличается цветом от красного маркера последнего хода:
    /// подсказка и последний ход могут стоять на соседних точках, и их нельзя путать.
    /// </remarks>
    private static void DrawHint(SKCanvas canvas, BoardGeometry geometry, Point point)
    {
        var center = geometry.Pixel(point);

        using var ring = new SKPaint
        {
            Color = HintColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = HintStrokeWidth
        };

        using var dot = new SKPaint { Color = HintColor, IsAntialias = true, Style = SKPaintStyle.Fill };

        canvas.DrawCircle(center, geometry.StoneRadius * HintRadiusRatio, ring);
        canvas.DrawCircle(center, geometry.StoneRadius * 0.25f, dot);
    }

    /// <summary>Рисует знаки территории на пустых пересечениях.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="territory">Владение точками или <c>null</c>, если территорию не показываем.</param>
    /// <remarks>
    /// Знаки ставятся только на пустые точки: камни не закрашиваются, иначе позиция стала бы
    /// нечитаемой. Свой знак у каждой стороны и у нейтральных точек — чёрный квадрат, белый
    /// квадрат с обводкой и серый крест: цвет и форма различаются и на светлой доске, и при
    /// плохом зрении. Нейтральные точки рисуются полупрозрачно: в начале партии их почти вся
    /// доска, и плотный знак мешал бы смотреть позицию.
    /// </remarks>
    private static void DrawTerritory(SKCanvas canvas, Board board, BoardGeometry geometry, IReadOnlyList<StoneColor>? territory)
    {
        if (territory is null || territory.Count != board.Size.Area)
        {
            // Список от другой доски не рисуем: лучше пустая разметка, чем знаки не на тех точках.
            return;
        }

        using var black = new SKPaint { Color = BlackStoneColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var white = new SKPaint { Color = WhiteStoneColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var outline = new SKPaint
        {
            Color = WhiteStoneOutline,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GridStrokeWidth
        };
        using var neutral = new SKPaint
        {
            Color = NeutralColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = NeutralStrokeWidth
        };

        var half = geometry.Cell * TerritorySizeRatio / 2;
        var size = board.Size.Value;

        foreach (var point in board.EmptyPoints())
        {
            var center = geometry.Pixel(point);
            var owner = territory[(point.Y * size) + point.X];

            if (owner == StoneColor.Black)
            {
                canvas.DrawRect(Square(center, half), black);
            }
            else if (owner == StoneColor.White)
            {
                var square = Square(center, half);

                canvas.DrawRect(square, white);
                canvas.DrawRect(square, outline);
            }
            else
            {
                canvas.DrawLine(center.X - half, center.Y - half, center.X + half, center.Y + half, neutral);
                canvas.DrawLine(center.X - half, center.Y + half, center.X + half, center.Y - half, neutral);
            }
        }
    }

    /// <summary>Строит квадрат знака вокруг точки.</summary>
    /// <param name="center">Центр знака.</param>
    /// <param name="half">Половина стороны квадрата.</param>
    /// <returns>Прямоугольник знака.</returns>
    private static SKRect Square(SKPoint center, float half) =>
        new(center.X - half, center.Y - half, center.X + half, center.Y + half);

    /// <summary>Рисует подсветку наведения и маркер последнего хода.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="lastMove">Точка последнего хода или <c>null</c>.</param>
    /// <param name="hover">Точка под курсором или <c>null</c>.</param>
    private static void DrawMarkers(SKCanvas canvas, BoardGeometry geometry, Point? lastMove, Point? hover)
    {
        if (hover is { } hoverPoint)
        {
            using var hoverPaint = new SKPaint { Color = HoverColor, IsAntialias = true, Style = SKPaintStyle.Fill };

            canvas.DrawCircle(geometry.Pixel(hoverPoint), geometry.StoneRadius, hoverPaint);
        }

        if (lastMove is { } last)
        {
            using var lastPaint = new SKPaint
            {
                Color = LastMoveColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = LastMoveStrokeWidth
            };

            canvas.DrawCircle(geometry.Pixel(last), geometry.StoneRadius * LastMoveRadiusRatio, lastPaint);
        }
    }

    /// <summary>Рисует сетку доски.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="geometry">Геометрия доски.</param>
    private static void DrawGrid(SKCanvas canvas, BoardSize size, BoardGeometry geometry)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            StrokeWidth = GridStrokeWidth,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        var last = size.Value - 1;
        var start = geometry.Center(0);
        var end = geometry.Center(last);

        for (var index = 0; index < size.Value; index++)
        {
            var offset = geometry.Center(index);

            canvas.DrawLine(start, offset, end, offset, paint);
            canvas.DrawLine(offset, start, offset, end, paint);
        }
    }

    /// <summary>Рисует координаты: буквы по горизонтали, числа по вертикали.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <remarks>
    /// Подписи стоят в поле вокруг сетки: оно уже заложено в геометрию, поэтому место есть всегда
    /// и доска не сдвигается. Номер строки считается сверху вниз — как в <c>Point.ToString()</c>
    /// (<c>DECISIONS.md</c>, D-009), буква «I» пропускается, как в настоящих Го-программах.
    /// </remarks>
    private static void DrawCoordinates(SKCanvas canvas, BoardSize size, BoardGeometry geometry)
    {
        var textSize = Math.Max(8f, geometry.Cell * CoordinateSizeRatio);
        var first = geometry.Center(0);
        var last = geometry.Center(size.Value - 1);
        var gap = geometry.Origin / 2;
        var baseline = textSize * CoordinateBaselineRatio;

        using var font = new SKFont(SKTypeface.Default, textSize);
        using var paint = new SKPaint { Color = GridColor, IsAntialias = true };

        for (var index = 0; index < size.Value; index++)
        {
            var center = geometry.Center(index);
            var letter = ColumnLetters[index].ToString();
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);

            canvas.DrawText(letter, center, first - gap + baseline, SKTextAlign.Center, font, paint);
            canvas.DrawText(letter, center, last + gap + baseline, SKTextAlign.Center, font, paint);
            canvas.DrawText(number, first - gap, center + baseline, SKTextAlign.Center, font, paint);
            canvas.DrawText(number, last + gap, center + baseline, SKTextAlign.Center, font, paint);
        }
    }

    /// <summary>Рисует звёздные точки (хоси).</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="geometry">Геометрия доски.</param>
    private static void DrawStarPoints(SKCanvas canvas, BoardSize size, BoardGeometry geometry)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        foreach (var (column, line) in StarPoints(size))
        {
            canvas.DrawCircle(geometry.Center(column), geometry.Center(line), StarRadius, paint);
        }
    }

    /// <summary>Рисует камни.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="board">Позиция.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="animation">Кадр анимации камней или <c>null</c>.</param>
    private static void DrawStones(SKCanvas canvas, Board board, BoardGeometry geometry, StoneAnimation? animation)
    {
        using var black = new SKPaint { Color = BlackStoneColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var white = new SKPaint { Color = WhiteStoneColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var outline = new SKPaint
        {
            Color = WhiteStoneOutline,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = GridStrokeWidth
        };

        var appearing = animation is { IsActive: true } frame ? frame.Appearing : null;

        foreach (var point in board.OccupiedPoints(StoneColor.Black))
        {
            if (point != appearing)
            {
                canvas.DrawCircle(geometry.Pixel(point), geometry.StoneRadius, black);
            }
        }

        foreach (var point in board.OccupiedPoints(StoneColor.White))
        {
            if (point == appearing)
            {
                continue;
            }

            var center = geometry.Pixel(point);
            canvas.DrawCircle(center, geometry.StoneRadius, white);
            canvas.DrawCircle(center, geometry.StoneRadius, outline);
        }

        if (animation is { IsActive: true } current)
        {
            DrawAnimatedStones(canvas, board, geometry, current);
        }
    }

    /// <summary>Рисует кадр анимации: растущий камень и исчезающие снятые.</summary>
    /// <param name="canvas">Холст Skia.</param>
    /// <param name="board">Позиция после хода.</param>
    /// <param name="geometry">Геометрия доски.</param>
    /// <param name="animation">Кадр анимации.</param>
    private static void DrawAnimatedStones(SKCanvas canvas, Board board, BoardGeometry geometry, StoneAnimation animation)
    {
        if (animation.Appearing is { } point && board.At(point) != StoneColor.Empty)
        {
            var color = board.At(point);
            var baseColor = color == StoneColor.Black ? BlackStoneColor : WhiteStoneColor;

            using var paint = new SKPaint
            {
                Color = baseColor.WithAlpha(animation.AppearanceAlpha),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawCircle(geometry.Pixel(point), geometry.StoneRadius * (float)animation.AppearanceScale, paint);
        }

        if (animation.Disappearing.Count == 0)
        {
            return;
        }

        var ghostColor = animation.DisappearingColor == StoneColor.Black ? BlackStoneColor : WhiteStoneColor;

        using var ghost = new SKPaint
        {
            Color = ghostColor.WithAlpha(animation.DisappearanceAlpha),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        foreach (var ghostPoint in animation.Disappearing)
        {
            canvas.DrawCircle(geometry.Pixel(ghostPoint), geometry.StoneRadius, ghost);
        }
    }

    /// <summary>Перечисляет звёздные точки доски.</summary>
    /// <param name="size">Размер доски.</param>
    /// <returns>Пары «столбец, строка» от 0.</returns>
    /// <remarks>
    /// 19×19 и 13×13 — девять точек по третьим линиям; 9×9 — четыре угловые точки третьей линии
    /// и центральная: на маленькой доске полный крест из девяти точек не ставится.
    /// </remarks>
    private static IEnumerable<(int Column, int Line)> StarPoints(BoardSize size) => size.Value switch
    {
        var value when value == BoardSize.Size19.Value => Cross([3, 9, 15]),
        var value when value == BoardSize.Size13.Value => Cross([3, 6, 9]),
        _ => [(2, 2), (6, 2), (4, 4), (2, 6), (6, 6)]
    };

    /// <summary>Строит крест звёздных точек по списку линий.</summary>
    /// <param name="lines">Номера линий от 0.</param>
    /// <returns>Все пересечения линий списка.</returns>
    private static IEnumerable<(int Column, int Line)> Cross(int[] lines)
    {
        foreach (var line in lines)
        {
            foreach (var column in lines)
            {
                yield return (column, line);
            }
        }
    }
}
