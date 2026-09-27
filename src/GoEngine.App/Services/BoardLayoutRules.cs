namespace GoEngine.App.Services;

/// <summary>Правила раскладки общего вида: где доска, а где панель управления.</summary>
/// <remarks>
/// Один и тот же вид работает и в настольном окне, и на телефоне. Решение о раскладке —
/// чистая функция от размеров: её можно проверить тестами без интерфейса, что и делается
/// в <c>GoEngine.App.Tests</c>. Сам вид (<c>BoardView</c>) только применяет ответ.
/// </remarks>
public static class BoardLayoutRules
{
    /// <summary>Ширина, ниже которой панель справа уже не помещается.</summary>
    public const double NarrowWidth = 760;

    /// <summary>Наименьшая разумная высота доски в вертикальной раскладке.</summary>
    public const double MinimumBoardHeight = 240;

    /// <summary>Какую долю высоты занимает доска в вертикальной раскладке.</summary>
    private const double BoardHeightShare = 0.62;

    /// <summary>Во сколько раз высота должна превышать ширину, чтобы экран считался портретным.</summary>
    private const double PortraitRatio = 1.25;

    /// <summary>Проверяет, нужна ли вертикальная раскладка: доска сверху, управление снизу.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns><c>true</c>, если панель нужно перенести под доску.</returns>
    /// <remarks>
    /// Узко — когда окно уже 760 точек (телефон в портрете, сжатое настольное окно) или когда
    /// экран заметно выше, чем шире: в портрете панель шириной 320 съела бы пол-экрана.
    /// </remarks>
    public static bool IsNarrow(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        return width < NarrowWidth || height > width * PortraitRatio;
    }

    /// <summary>Считает высоту доски для вертикальной раскладки.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns>Высота, при которой доска остаётся квадратной и помещается вместе с панелью.</returns>
    /// <remarks>Доска квадратная: её высота равна ширине, но не больше доли экрана и не меньше
    /// разумного минимума — иначе на очень низком экране панель управления не оставит места.</remarks>
    public static double BoardHeight(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var share = height * BoardHeightShare;

        return Math.Clamp(Math.Min(width, share), MinimumBoardHeight, height);
    }
}
