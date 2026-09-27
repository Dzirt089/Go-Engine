using GoEngine.Core;

namespace GoEngine.AI.Onnx;

/// <summary>Кодирование позиции в признаки KataGo (версия V3).</summary>
/// <remarks>
/// Раскладка плоскостей взята из исходников KataGo (<c>cpp/neuralnet/nninputs.cpp</c>,
/// функция <c>fillRowV3</c>): 22 пространственные плоскости и 14 глобальных признаков.
///
/// Пространственные плоскости:
/// 0 — точки доски; 1, 2 — камни игрока и противника; 3, 4, 5 — камни с 1, 2 и 3 дамэ;
/// 6 — точки, запрещённые ко и суперко; 7, 8 — фаза encore (в v1 всегда нули);
/// 9…13 — пять последних ходов (9 — ход противника, 10 — свой и так далее);
/// 14…17 — лестничные признаки (в v1 не считаются, нули); 18, 19 — территория игрока
/// и противника; 20, 21 — камни второго encore (нули).
///
/// Глобальные признаки (19) заданы моделью и проверены по конвертеру `kaya-go/katago-onnx`
/// (`src/katago/game/features.py`): 0…4 — были ли пасами пять последних ходов; 5 — коми
/// с точки зрения ходящего, делённое на 20; 6, 7 — правило ко (у нас позиционное суперко:
/// 1 и 0,5); 8 — разрешено ли самоубийство (у нас нет: 0); 9 — подсчёт по территории
/// (у нас по площади: 0); 10, 11 — налог на сэки и на все группы (нет: 0); 12, 13 — фазы
/// encore (нет: 0); 14 — завершит ли пас партию; 15, 16 — асимметричные степени двойки (нет);
/// 17 — «кнопка» (нет); 18 — волна чётности коми при подсчёте по площади.
/// </remarks>
public static class KataGoFeatures
{
    /// <summary>Число пространственных плоскостей версии V3.</summary>
    public const int SpatialPlanes = 22;

    /// <summary>Число глобальных признаков, которое ждёт модель.</summary>
    public const int GlobalFeatures = 19;

    /// <summary>Индекс первой из пяти плоскостей истории ходов.</summary>
    private const int FirstHistoryPlane = 9;

    /// <summary>Сколько последних ходов попадает в плоскости истории.</summary>
    private const int HistoryPlanes = 5;

    /// <summary>Делитель коми в глобальном признаке 5 (у модели KataGo — 20).</summary>
    private const float KomiScale = 20.0f;

    /// <summary>Кодирует позицию в пространственные плоскости.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии — для плоскостей истории.</param>
    /// <returns>Плоскости по порядку: <c>22 × size × size</c> значений.</returns>
    /// <remarks>
    /// Размер доски модель принимает динамически (оси высоты и ширины объявлены как <c>−1</c>),
    /// поэтому признаки считаются для любого размера. Ширина и высота на входе модели равны.
    /// Верить оценке стоит только на 19×19: сеть обучена на этой доске, а на 9×9 её первый ход
    /// и оценка неправдоподобны (`DECISIONS.md`, D-034).
    /// </remarks>
    public static float[] EncodeSpatial(Board board, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(moves);

        var side = board.Size.Value;
        var plane = side * side;
        var spatial = new float[SpatialPlanes * plane];
        var opponent = toMove.Opponent();

        void Set(int x, int y, int index) => spatial[(index * plane) + (y * side) + x] = 1f;

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var point = new Point((byte)x, (byte)y);

                // Плоскость 0 — сама доска: вне доски точек нет, поэтому здесь всегда 1.
                Set(x, y, 0);

                var color = board.At(point);

                if (color == toMove)
                {
                    Set(x, y, 1);
                }
                else if (color == opponent)
                {
                    Set(x, y, 2);
                }

                if (color == StoneColor.Empty)
                {
                    continue;
                }

                var liberties = GroupTracker.FindGroup(board, point).Liberties.Count;

                if (liberties is >= 1 and <= 3)
                {
                    Set(x, y, 2 + liberties);
                }
            }
        }

        MarkKoBans(board, spatial, plane, side, toMove);
        MarkHistory(board, moves, spatial, plane, side, FirstHistoryPlane, HistoryPlanes);
        MarkTerritory(board, spatial, plane, side, toMove);

        return spatial;
    }

    /// <summary>Кодирует глобальные признаки позиции.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <param name="moves">Сделанные ходы партии — для признаков пасов.</param>
    /// <returns>Девятнадцать глобальных признаков в порядке модели.</returns>
    public static float[] EncodeGlobal(Board board, Komi komi, StoneColor toMove, IReadOnlyList<Move> moves)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(toMove);
        ArgumentNullException.ThrowIfNull(moves);

        var global = new float[GlobalFeatures];
        var area = board.Size.Area;
        var opponent = toMove.Opponent();

        // Признаки 0…4 — пасы в пяти последних ходах, начиная с хода противника.
        for (var index = 0; index < HistoryPlanes; index++)
        {
            var move = MoveAt(moves, moves.Count - 1 - index);

            if (move is null)
            {
                break;
            }

            var expected = index % 2 == 0 ? opponent : toMove;

            if (move.Value.Color != expected)
            {
                break;
            }

            if (move.Value.Type == MoveType.Pass)
            {
                global[index] = 1f;
            }
        }

        // Признак 5 — коми с точки зрения ходящего, приведённое к масштабу модели (делённое на 20).
        var selfKomi = toMove == StoneColor.White ? komi.Value : -komi.Value;
        global[5] = (float)(Math.Clamp(selfKomi, -area - 1.0, area + 1.0) / KomiScale);

        // Признаки 6 и 7 — правило ко: у нас позиционное суперко (1 и 0,5).
        global[6] = 1f;
        global[7] = 0.5f;

        // Признаки 8…13 — правила, которых в движке нет, поэтому нули: самоубийство запрещено (8),
        // подсчёт по площади (9), налог на сэки и на все группы (10, 11), фаза encore (12, 13).
        global[8] = 0f;
        global[9] = 0f;
        global[10] = 0f;
        global[11] = 0f;
        global[12] = 0f;
        global[13] = 0f;

        // Признак 14 — завершит ли пас партию: у нас её завершают два паса подряд.
        global[14] = PassWouldFinish(moves) ? 1f : 0f;

        // Признаки 15…17 — асимметричные степени двойки и «кнопка»: в движке их нет, нули.
        global[15] = 0f;
        global[16] = 0f;
        global[17] = 0f;

        // Признак 18 — волна чётности коми при подсчёте по площади.
        global[18] = KomiParityWave(selfKomi, area);

        return global;
    }

    /// <summary>Отмечает точки, запрещённые ко и суперко.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="spatial">Плоскости.</param>
    /// <param name="plane">Число точек на плоскости.</param>
    /// <param name="side">Сторона доски.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    /// <remarks>
    /// Точка запрещена, если ход туда легален без истории, но нелегален с ней: значит,
    /// позиция после хода уже встречалась в партии.
    /// </remarks>
    private static void MarkKoBans(Board board, float[] spatial, int plane, int side, StoneColor toMove)
    {
        var withoutHistory = board.WithoutHistory();

        foreach (var point in board.EmptyPoints())
        {
            var move = Move.Play(point, toMove);

            if (withoutHistory.IsLegal(move).IsSuccess && !board.IsLegal(move).IsSuccess)
            {
                spatial[(6 * plane) + (point.Y * side) + point.X] = 1f;
            }
        }
    }

    /// <summary>Отмечает пять последних ходов в плоскостях истории.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="moves">Ходы партии.</param>
    /// <param name="spatial">Плоскости.</param>
    /// <param name="plane">Число точек на плоскости.</param>
    /// <param name="side">Сторона доски.</param>
    /// <param name="firstPlane">Индекс первой плоскости истории.</param>
    /// <param name="count">Сколько ходов отмечать.</param>
    /// <remarks>Плоскость 9 — последний ход (противника), дальше по одному ходу назад.</remarks>
    private static void MarkHistory(
        Board board,
        IReadOnlyList<Move> moves,
        float[] spatial,
        int plane,
        int side,
        int firstPlane,
        int count)
    {
        for (var index = 0; index < count; index++)
        {
            var move = MoveAt(moves, moves.Count - 1 - index);

            if (move is null || move.Value.Type != MoveType.Play || !move.Value.Point.IsOnBoard(board.Size))
            {
                continue;
            }

            var point = move.Value.Point;

            spatial[((firstPlane + index) * plane) + (point.Y * side) + point.X] = 1f;
        }
    }

    /// <summary>Отмечает территорию игрока и противника.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="spatial">Плоскости.</param>
    /// <param name="plane">Число точек на плоскости.</param>
    /// <param name="side">Сторона доски.</param>
    /// <param name="toMove">Цвет, который ходит.</param>
    private static void MarkTerritory(Board board, float[] spatial, int plane, int side, StoneColor toMove)
    {
        var ownership = Scorer.Ownership(board);
        var opponent = toMove.Opponent();

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var owner = ownership[(y * side) + x];

                if (owner == toMove)
                {
                    spatial[(18 * plane) + (y * side) + x] = 1f;
                }
                else if (owner == opponent)
                {
                    spatial[(19 * plane) + (y * side) + x] = 1f;
                }
            }
        }
    }

    /// <summary>Возвращает ход по индексу или <c>null</c>, если индекса нет.</summary>
    /// <param name="moves">Ходы партии.</param>
    /// <param name="index">Индекс хода.</param>
    /// <returns>Ход или <c>null</c>.</returns>
    private static Move? MoveAt(IReadOnlyList<Move> moves, int index) =>
        index >= 0 && index < moves.Count ? moves[index] : null;

    /// <summary>Проверяет, завершит ли следующий пас партию.</summary>
    /// <param name="moves">Ходы партии.</param>
    /// <returns><c>true</c>, если предыдущий ход был пасом.</returns>
    private static bool PassWouldFinish(IReadOnlyList<Move> moves)
    {
        var last = MoveAt(moves, moves.Count - 1);

        return last is not null && last.Value.Type == MoveType.Pass;
    }

    /// <summary>Считает волну чётности коми для подсчёта по площади.</summary>
    /// <param name="selfKomi">Коми с точки зрения ходящего.</param>
    /// <param name="area">Число точек доски.</param>
    /// <returns>Значение от 0 до 1: пик там, где коми превращает поражения в ничьи.</returns>
    private static float KomiParityWave(double selfKomi, int area)
    {
        var drawableKomisAreEven = area % 2 == 0;
        var floor = drawableKomisAreEven
            ? Math.Floor(selfKomi / 2.0) * 2.0
            : (Math.Floor((selfKomi - 1.0) / 2.0) * 2.0) + 1.0;

        var delta = Math.Clamp(selfKomi - floor, 0.0, 2.0);

        return delta switch
        {
            < 0.5 => (float)delta,
            < 1.5 => (float)(1.0 - delta),
            _ => (float)(delta - 2.0)
        };
    }
}
