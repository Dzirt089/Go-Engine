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

    /// <summary>Наименьшая ширина панели на настольном экране.</summary>
    /// <remarks>
    /// 360 точек — строка текста в 45–55 знаков: меньше — и разбор начинает рваться на слоги,
    /// а кнопки в ряду не помещаются (жалоба пользователя 2026-10-11: панель 320 была узкой).
    /// </remarks>
    public const double MinimumDesktopPanelWidth = 360;

    /// <summary>Наибольшая ширина панели на настольном экране.</summary>
    /// <remarks>
    /// Дальше панель только съедала бы место у доски: длинную строку читать труднее, чем короткую,
    /// и в продуктах колонку текста ограничивают, а не растягивают на весь экран.
    /// </remarks>
    public const double MaximumDesktopPanelWidth = 520;

    /// <summary>Доля ширины окна, которую занимает панель на настольном экране.</summary>
    /// <remarks>
    /// Треть ширины: на 1366 точках это 410, на 1920 — предел 520. Доля важнее фиксированной
    /// ширины: на большом экране панель растёт вместе с окном, но не бесконечно.
    /// </remarks>
    private const double DesktopPanelShare = 0.30;

    /// <summary>Зазор между доской и краями её места.</summary>
    /// <remarks>Отступ в 24 точки — шаг сетки отступов интерфейса: доска не липнет к краю окна.</remarks>
    public const double DesktopGutter = 24;

    /// <summary>Наибольшая сторона доски на настольном экране.</summary>
    /// <remarks>
    /// Больше 900 точек доска на мониторе не нужна: клетка становится крупнее ладони, взгляд
    /// не охватывает позицию целиком, а лишнее место лучше отдать панели разбора.
    /// </remarks>
    public const double MaximumBoardSide = 900;

    /// <summary>Считает ширину панели на настольном экране.</summary>
    /// <param name="viewWidth">Ширина вида.</param>
    /// <returns>Ширину панели: доля окна в границах читаемости.</returns>
    public static double DesktopPanelWidth(double viewWidth)
    {
        if (viewWidth <= 0)
        {
            return MinimumDesktopPanelWidth;
        }

        return Math.Clamp(
            viewWidth * DesktopPanelShare,
            MinimumDesktopPanelWidth,
            MaximumDesktopPanelWidth);
    }

    /// <summary>Считает сторону доски в настольной раскладке.</summary>
    /// <param name="areaWidth">Ширина места, отведённого доске.</param>
    /// <param name="viewHeight">Высота вида.</param>
    /// <returns>Сторону квадрата: доска вписана в место с зазорами и не больше предела.</returns>
    /// <remarks>
    /// Доска квадратная и центруется: свободное место делится поровну слева и справа (жалоба
    /// пользователя 2026-10-11: «доску центровать»). Сторона берётся по меньшей из доступных
    /// величин, поэтому на низком окне доска уменьшается, а не обрезается.
    /// </remarks>
    public static double DesktopBoardSide(double areaWidth, double viewHeight)
    {
        if (areaWidth <= 0 || viewHeight <= 0)
        {
            return 0;
        }

        var side = Math.Min(areaWidth - (2 * DesktopGutter), viewHeight - (2 * DesktopGutter));

        return Math.Min(Math.Max(side, MinimumBoardHeight), MaximumBoardSide);
    }

    /// <summary>Наименьший кегль подписи координат, при котором она ещё читается на телефоне.</summary>
    public const double MinimumCoordinateTextSize = 10;

    /// <summary>Какую долю высоты занимает доска в вертикальной раскладке.</summary>
    /// <remarks>
    /// Половина экрана, а не две трети: в разделах обучения (задачи, уроки, разбор партий) под доской
    /// стоит панель с текстом, и текст важнее размера доски. Замечание пользователя 2026-10-15:
    /// «дай больше места на экране для текста». Раньше доска занимала 62 % высоты, и панели
    /// оставалось меньше трети экрана.
    /// </remarks>
    private const double BoardHeightShare = 0.50;

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

    /// <summary>Наименьшая ширина панели действий телефона: четыре подписи в ряд ещё помещаются.</summary>
    /// <remarks>
    /// Четыре кнопки по 80 точек: подпись «Отменить» кеглем 13 занимает около 60 точек, плюс
    /// внутренние отступы и рамка кнопки — 80 точек ей хватает с запасом. Ниже этой ширины
    /// подписи начали бы обрезаться, поэтому по ширине доски панель уменьшается только до неё.
    /// </remarks>
    public const double MinimumActionBarWidth = 320;

    /// <summary>Считает ширину панели действий телефона.</summary>
    /// <param name="boardSide">Сторона доски: панель выравнивается по ней.</param>
    /// <param name="availableWidth">Ширина, отведённая раскладкой под панель.</param>
    /// <returns>Ширину панели: не шире отведённого места и не уже читаемой.</returns>
    /// <remarks>
    /// Панель действий стоит под доской и должна быть с ней одной ширины, а не «плавающей»
    /// карточкой по центру: раньше её ширина зависела от подписей кнопок, она была уже доски
    /// и заезжала за её нижний край (жалоба пользователя 2026-10-02 «наезжает на доску»).
    /// Исключение — узкий экран: доска там ограничена высотой, и панель ровно по ней обрезала бы
    /// подписи, поэтому у панели есть наименьшая читаемая ширина.
    /// </remarks>
    public static double MobileActionBarWidth(double boardSide, double availableWidth)
    {
        if (availableWidth <= 0)
        {
            return 0;
        }

        return Math.Min(Math.Max(boardSide, MinimumActionBarWidth), availableWidth);
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
