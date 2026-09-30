namespace GoEngine.App.Services;

/// <summary>Метрики интерфейса: кегли, цели нажатия, отступы, радиусы и высоты полос.</summary>
/// <remarks>
/// <para>
/// Числа — в точках (device-independent pixels), в тех же единицах, что и размеры в Avalonia.
/// Метрики считает чистая функция <see cref="PlatformMetrics.For"/>: правило «какой размер где»
/// проверяется тестами без интерфейса, а не снимками экрана.
/// </para>
/// <para>
/// XAML не умеет вызывать C#-функцию, поэтому те же числа продублированы ресурсами
/// (<c>AppMobile*</c> в <c>Views/UiTheme.axaml</c>) и стилями (<c>Views/UiStyles.Mobile.axaml</c>).
/// Расхождение — ошибка, а не настройка.
/// </para>
/// </remarks>
public readonly record struct UiMetrics
{
    /// <summary>Признак телефона: раскладка мобильная, цель нажатия рассчитана на палец.</summary>
    public bool IsMobile { get; init; }

    /// <summary>Признак компактной раскладки: телефон в ландшафте, высоты мало.</summary>
    public bool IsCompact { get; init; }

    /// <summary>Кегль заголовка экрана, точки.</summary>
    public double TitleFontSize { get; init; }

    /// <summary>Кегль подзаголовка и заголовка карточки, точки.</summary>
    public double SubtitleFontSize { get; init; }

    /// <summary>Кегль основного текста и подписей кнопок, точки.</summary>
    public double BodyFontSize { get; init; }

    /// <summary>Кегль подписи и пояснения под текстом, точки.</summary>
    public double CaptionFontSize { get; init; }

    /// <summary>Кегль компактной кнопки в ряду действий, точки.</summary>
    public double ActionFontSize { get; init; }

    /// <summary>Наименьшая высота цели нажатия, точки.</summary>
    public double TapTargetHeight { get; init; }

    /// <summary>Отступ от края экрана (страницы), точки.</summary>
    public double PagePadding { get; init; }

    /// <summary>Внутренний отступ карточки, точки.</summary>
    public double CardPadding { get; init; }

    /// <summary>Малый промежуток между соседними элементами, точки.</summary>
    public double GapSmall { get; init; }

    /// <summary>Средний промежуток между группами элементов, точки.</summary>
    public double GapMedium { get; init; }

    /// <summary>Радиус скругления мелких элементов: кнопок, полей, значков, точки.</summary>
    public double RadiusSmall { get; init; }

    /// <summary>Радиус скругления карточек и панелей, точки.</summary>
    public double RadiusMedium { get; init; }

    /// <summary>Толщина разделительной линии, точки.</summary>
    public double DividerThickness { get; init; }

    /// <summary>Высота нижней навигации телефона, точки; 0 — полосы нет.</summary>
    public double BottomNavHeight { get; init; }

    /// <summary>Высота строки состояния над доской, точки; 0 — строки нет.</summary>
    public double StatusBarHeight { get; init; }
}

/// <summary>Размеры интерфейса под платформу и экран: одна чистая функция на все виды.</summary>
/// <remarks>
/// <para>
/// Платформа задаёт нижнюю границу цели нажатия. На телефоне это 48 точек: по этому размеру
/// уверенно попадает палец — рекомендация Android Material (48dp; Apple HIG называет 44pt,
/// 48 строже и покрывает обе системы). На настольной системе вводят курсором, он точнее пальца,
/// поэтому достаточно 32 точек: кнопки по 48 на широком экране выглядят громоздко и съедают
/// место панели управления.
/// </para>
/// <para>
/// Размер экрана задаёт остальное. На телефоне в портрете кегли и отступы умеренные, в ландшафте
/// (высота меньше <see cref="CompactHeight"/>) — компактные, иначе доска превращается в полоску.
/// На просторном настольном окне отступы и заголовок крупнее, потому что место есть.
/// </para>
/// </remarks>
public static class PlatformMetrics
{
    /// <summary>Наименьшая цель нажатия пальцем, точки: 48dp из руководства Android Material.</summary>
    public const double TouchTargetMinimum = 48;

    /// <summary>Наименьшая цель нажатия курсором, точки: мышь точнее пальца.</summary>
    public const double PointerTargetMinimum = 32;

    /// <summary>Наименьший кегль основного текста, точки: мельче — уже не читается.</summary>
    public const double BodyFontMinimum = 12;

    /// <summary>Наибольший кегль основного текста, точки: крупнее — текст спорит с заголовком.</summary>
    public const double BodyFontMaximum = 17;

    /// <summary>Высота, ниже которой телефон считается низким (ландшафт), точки.</summary>
    /// <remarks>
    /// Та же граница, что у вида: <c>BoardView</c> переводит телефон в компактную раскладку ниже
    /// этой высоты. Одно число на оба решения — иначе метрики и раскладка разошлись бы.
    /// </remarks>
    public const double CompactHeight = 460;

    /// <summary>Ширина, с которой настольное окно считается просторным, точки.</summary>
    public const double WideWindowWidth = 1100;

    // Кегли телефона. Заголовок мельче настольного (20 против 22): на узком экране крупный
    // заголовок выдавливает содержимое. В ландшафте ступени опускаются ещё на шаг — доска важнее
    // подписей, — но основной текст не опускается ниже BodyFontMinimum.
    private const double MobileTitleFontSize = 20;
    private const double MobileCompactTitleFontSize = 18;
    private const double MobileSubtitleFontSize = 16;
    private const double MobileBodyFontSize = 14;
    private const double MobileCompactBodyFontSize = 13;
    private const double MobileCaptionFontSize = 12;
    private const double MobileCompactCaptionFontSize = 11;

    // Кегли настольной версии: те же ступени, что в теме (AppFontTitle 22 и далее).
    // Просторное окно позволяет заголовку вырасти до 24 — он перестаёт теряться на широком экране.
    private const double DesktopTitleFontSize = 22;
    private const double DesktopWideTitleFontSize = 24;
    private const double DesktopSubtitleFontSize = 16;
    private const double DesktopBodyFontSize = 14;
    private const double DesktopCaptionFontSize = 12;

    // Кегль кнопки действия: 13 на телефоне — подписи «Новая партия» и «Отменить» обязаны
    // помещаться в четыре колонки по 360 точек; 12 на настольной панели шириной 320.
    private const double MobileActionFontSize = 13;
    private const double DesktopActionFontSize = 12;

    // Цели нажатия: минимум платформы плюс запас на просторном настольном окне, где место есть.
    private const double MobileTapTarget = TouchTargetMinimum;
    private const double DesktopTapTarget = PointerTargetMinimum;
    private const double DesktopWideTapTarget = 36;

    // Отступы телефона: в портрете умеренные, в ландшафте ужаты, чтобы доска осталась квадратной.
    private const double MobilePagePadding = 12;
    private const double MobileCompactPagePadding = 8;
    private const double MobileCardPadding = 10;
    private const double MobileCompactCardPadding = 8;
    private const double MobileGapSmall = 6;
    private const double MobileCompactGapSmall = 4;
    private const double MobileGapMedium = 10;
    private const double MobileCompactGapMedium = 8;

    // Отступы настольной версии: просторное окно получает больше воздуха.
    private const double DesktopPagePadding = 12;
    private const double DesktopWidePagePadding = 16;
    private const double DesktopCardPadding = 10;
    private const double DesktopWideCardPadding = 12;
    private const double DesktopGapSmall = 6;
    private const double DesktopGapMedium = 10;
    private const double DesktopWideGapMedium = 12;

    // Радиусы и разделители одинаковы на обеих платформах: форма — часть единого визуального языка
    // приложения (UiTheme.axaml), а не свойство платформы. В метриках они всё равно есть, чтобы
    // видам не приходилось искать числа по разным местам.
    private const double CommonRadiusSmall = 8;
    private const double CommonRadiusMedium = 14;
    private const double CommonDividerThickness = 1;

    // Полосы интерфейса. Нижняя навигация и строка состояния есть только на телефоне; на настольной
    // системе 0 означает «полосы нет», а не «полоса нулевой высоты».
    private const double MobileBottomNavHeight = 64;
    private const double MobileStatusBarHeight = 44;
    private const double NoBar = 0;

    /// <summary>Считает метрики интерфейса для платформы и размера экрана.</summary>
    /// <param name="mobile">Истинно для телефона: цель нажатия считается по пальцу.</param>
    /// <param name="width">Ширина экрана или окна в точках; неположительная — размер ещё не измерен.</param>
    /// <param name="height">Высота экрана или окна в точках; неположительная — размер ещё не измерен.</param>
    /// <returns>Размеры для вида: кегли, цели нажатия, отступы, радиусы и высоты полос.</returns>
    /// <remarks>
    /// <para>
    /// Функция чистая: одинаковые входные данные дают одинаковые метрики, состояние приложения
    /// и текущая раскладка на результат не влияют. Поэтому её проверяют тестами, а вид только
    /// подставляет готовые числа.
    /// </para>
    /// <para>
    /// До первого измерения (нулевые или отрицательные размеры) возвращаются базовые метрики
    /// платформы: телефон получает портретные размеры, настольная система — обычные. Исключение
    /// здесь не нужно: «ещё не измерили» — обычное состояние первого кадра, а не ошибка.
    /// </para>
    /// <para>
    /// Компактность учитывается только на телефоне: низкое настольное окно прокручивает панель,
    /// а не уменьшает кегли, поэтому высота в настольной ветке не участвует.
    /// </para>
    /// </remarks>
    public static UiMetrics For(bool mobile, double width, double height)
    {
        var measured = width > 0 && height > 0;

        if (mobile)
        {
            var compact = measured && height < CompactHeight;

            return new UiMetrics
            {
                IsMobile = true,
                IsCompact = compact,
                TitleFontSize = compact ? MobileCompactTitleFontSize : MobileTitleFontSize,
                SubtitleFontSize = MobileSubtitleFontSize,
                BodyFontSize = compact ? MobileCompactBodyFontSize : MobileBodyFontSize,
                CaptionFontSize = compact ? MobileCompactCaptionFontSize : MobileCaptionFontSize,
                ActionFontSize = MobileActionFontSize,
                TapTargetHeight = MobileTapTarget,
                PagePadding = compact ? MobileCompactPagePadding : MobilePagePadding,
                CardPadding = compact ? MobileCompactCardPadding : MobileCardPadding,
                GapSmall = compact ? MobileCompactGapSmall : MobileGapSmall,
                GapMedium = compact ? MobileCompactGapMedium : MobileGapMedium,
                RadiusSmall = CommonRadiusSmall,
                RadiusMedium = CommonRadiusMedium,
                DividerThickness = CommonDividerThickness,
                BottomNavHeight = MobileBottomNavHeight,
                StatusBarHeight = MobileStatusBarHeight
            };
        }

        var wide = measured && width >= WideWindowWidth;

        return new UiMetrics
        {
            IsMobile = false,
            IsCompact = false,
            TitleFontSize = wide ? DesktopWideTitleFontSize : DesktopTitleFontSize,
            SubtitleFontSize = DesktopSubtitleFontSize,
            BodyFontSize = DesktopBodyFontSize,
            CaptionFontSize = DesktopCaptionFontSize,
            ActionFontSize = DesktopActionFontSize,
            TapTargetHeight = wide ? DesktopWideTapTarget : DesktopTapTarget,
            PagePadding = wide ? DesktopWidePagePadding : DesktopPagePadding,
            CardPadding = wide ? DesktopWideCardPadding : DesktopCardPadding,
            GapSmall = DesktopGapSmall,
            GapMedium = wide ? DesktopWideGapMedium : DesktopGapMedium,
            RadiusSmall = CommonRadiusSmall,
            RadiusMedium = CommonRadiusMedium,
            DividerThickness = CommonDividerThickness,
            BottomNavHeight = NoBar,
            StatusBarHeight = NoBar
        };
    }
}
