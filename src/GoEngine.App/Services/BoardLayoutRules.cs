using GoEngine.App.Rendering;

namespace GoEngine.App.Services;

/// <summary>Раскладка интерфейса: прежняя настольная или новая мобильная.</summary>
/// <remarks>
/// Режим выбирается по ширине вида одной чистой функцией (<see cref="BoardLayoutRules.DecideLayout"/>),
/// поэтому его проверяют тестами, а не снимками экрана.
/// </remarks>
public enum LayoutMode
{
    /// <summary>Настольное окно: доска слева, панель управления справа, меню и режимы сверху.</summary>
    Desktop,

    /// <summary>Телефон: доска на весь экран, действия поверх доски, навигация снизу.</summary>
    Mobile
}

/// <summary>Правила раскладки общего вида: где доска, а где панель управления.</summary>
/// <remarks>
/// Один и тот же вид работает и в настольном окне, и на телефоне. Решения о раскладке —
/// чистые функции от размеров: их можно проверить тестами без интерфейса, что и делается
/// в <c>GoEngine.App.Tests</c>. Сам вид (<c>BoardView</c>, <c>ShellView</c>) только применяет ответ.
/// </remarks>
public static class BoardLayoutRules
{
    /// <summary>Ширина, ниже которой панель справа уже не помещается: с неё начинается мобильный вид.</summary>
    public const double NarrowWidth = 760;

    /// <summary>Наименьшая разумная высота доски в вертикальной раскладке.</summary>
    public const double MinimumBoardHeight = 240;

    /// <summary>Наименьший кегль подписи координат, при котором она ещё читается на телефоне.</summary>
    public const double MinimumCoordinateTextSize = 10;

    /// <summary>Какую долю высоты занимает доска в вертикальной раскладке.</summary>
    private const double BoardHeightShare = 0.62;

    /// <summary>Во сколько раз высота должна превышать ширину, чтобы экран считался портретным.</summary>
    private const double PortraitRatio = 1.25;

    /// <summary>Доля клетки, которую занимает подпись координаты: та же, что у отрисовки доски.</summary>
    private const double CoordinateSizeRatio = BoardRenderer.CoordinateSizeRatio;

    /// <summary>Доля стороны доски, занятая полями вокруг сетки с обеих сторон.</summary>
    private const double BoardMarginsShare = 2 * BoardGeometry.MarginRatio;

    /// <summary>Выбирает раскладку интерфейса по ширине вида.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns>Мобильную раскладку для узкого вида, настольную для широкого.</returns>
    /// <remarks>
    /// Порог один — <see cref="NarrowWidth"/>: телефон в портрете уже 760 точек, настольное окно
    /// шире. Высота не участвует: она не должна переключать вид на планшете в ландшафте.
    /// Неположительные размеры дают настольную раскладку — до первого измерения вид пуст.
    /// </remarks>
    public static LayoutMode DecideLayout(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return LayoutMode.Desktop;
        }

        return width >= NarrowWidth ? LayoutMode.Desktop : LayoutMode.Mobile;
    }

    /// <summary>Считает сторону квадратной доски для мобильной раскладки.</summary>
    /// <param name="width">Ширина места, отведённого доске.</param>
    /// <param name="height">Высота места, отведённого доске.</param>
    /// <returns>Сторону квадрата, вписанного в отведённое место.</returns>
    /// <remarks>
    /// На телефоне доска занимает всё, что осталось от строки состояния, панели действий и шторки:
    /// сторона равна меньшей из доступных величин. Вверх сторона не увеличивается: доска не должна
    /// вылезать за своё место — на низком экране (телефон в ландшафте) она честно уменьшается,
    /// а лишние строки интерфейса убирает сам вид.
    /// </remarks>
    public static double MobileBoardSide(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        return Math.Min(width, height);
    }

    /// <summary>Проверяет, помещаются ли подписи координат на доске такого размера.</summary>
    /// <param name="boardSide">Сторона квадрата, в который вписана доска.</param>
    /// <param name="boardLines">Число линий сетки: 9, 13 или 19.</param>
    /// <returns><c>true</c>, если подпись координаты крупнее порога читаемости.</returns>
    /// <remarks>
    /// Координаты не рисуются, когда клетка становится слишком мелкой: на телефоне доска 19×19
    /// занимает те же 350 точек, что и 9×9, и подписи превратились бы в серую кашу по краю.
    /// Доли взяты у отрисовки доски (<see cref="BoardRenderer.CoordinateSizeRatio"/>,
    /// <see cref="BoardGeometry.MarginRatio"/>), чтобы решение не разошлось с картинкой.
    /// </remarks>
    public static bool CoordinatesFit(double boardSide, int boardLines)
    {
        if (boardSide <= 0 || boardLines < 2)
        {
            return false;
        }

        var cell = (boardSide * (1 - BoardMarginsShare)) / (boardLines - 1);

        return cell * CoordinateSizeRatio >= MinimumCoordinateTextSize;
    }

    /// <summary>Проверяет, нужна ли вертикальная раскладка: доска сверху, управление снизу.</summary>
    /// <param name="width">Ширина вида.</param>
    /// <param name="height">Высота вида.</param>
    /// <returns><c>true</c>, если панель нужно перенести под доску.</returns>
    /// <remarks>
    /// Узко — когда окно уже 760 точек (телефон в портрете, сжатое настольное окно) или когда
    /// экран заметно выше, чем шире: в портрете панель шириной 320 съела бы пол-экрана.
    /// Так раскладывается настольная панель внутри <c>BoardView</c>; мобильный вид целиком
    /// выбирает <see cref="DecideLayout"/>.
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
