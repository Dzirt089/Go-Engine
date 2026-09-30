using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты метрик платформы: телефон против настольной системы и размер экрана.</summary>
/// <remarks>
/// Метрики считает чистая функция (<see cref="PlatformMetrics.For"/>), поэтому правило проверяется
/// тестами, а не снимками экрана. Границы взяты из требований: цель нажатия не меньше 48 точек
/// на телефоне (палец) и 32 на настольной системе (курсор), основной текст — от 12 до 17 точек,
/// ступени кеглей убывают от заголовка к подписи.
/// Регрессия 2026-09-30: размеры кнопок и шрифтов не соответствовали платформе — на телефоне
/// панель действий втискивалась в 360 точек, а на настольной системе кнопки были рассчитаны
/// на палец.
/// </remarks>
public sealed class PlatformMetricsTests
{
    /// <summary>Телефон в портрете: размеры со снимка пользователя (360×784).</summary>
    private static readonly UiMetrics Phone = PlatformMetrics.For(true, 360, 784);

    /// <summary>Телефон в ландшафте: высоты мало, раскладка компактная.</summary>
    private static readonly UiMetrics PhoneLandscape = PlatformMetrics.For(true, 740, 360);

    /// <summary>Обычное настольное окно: 1024×768, до порога просторного не дотягивает.</summary>
    private static readonly UiMetrics Desktop = PlatformMetrics.For(false, 1024, 768);

    /// <summary>Просторное настольное окно: 1600×1000, места хватает на крупные размеры.</summary>
    private static readonly UiMetrics DesktopWide = PlatformMetrics.For(false, 1600, 1000);

    /// <summary>Узкое настольное окно со снимка пользователя (361×793).</summary>
    private static readonly UiMetrics DesktopNarrow = PlatformMetrics.For(false, 361, 793);

    [Fact]
    public void Телефон_Даёт_Цель_Нажатия_Не_Меньше_Минимума()
    {
        // 48 точек — размер, по которому попадает палец (руководство Android Material).
        Assert.True(Phone.TapTargetHeight >= PlatformMetrics.TouchTargetMinimum);
    }

    [Fact]
    public void Телефон_В_Ландшафте_Даёт_Цель_Нажатия_Не_Меньше_Минимума()
    {
        // Низкий экран ужимает отступы и кегли, но не цель нажатия: палец тот же.
        Assert.True(PhoneLandscape.TapTargetHeight >= PlatformMetrics.TouchTargetMinimum);
    }

    [Fact]
    public void Настольное_Окно_Даёт_Цель_Нажатия_Не_Меньше_Минимума()
    {
        Assert.True(Desktop.TapTargetHeight >= PlatformMetrics.PointerTargetMinimum);
    }

    [Fact]
    public void Узкое_Настольное_Окно_Даёт_Цель_Нажатия_Не_Меньше_Минимума()
    {
        Assert.True(DesktopNarrow.TapTargetHeight >= PlatformMetrics.PointerTargetMinimum);
    }

    [Fact]
    public void Широкое_Настольное_Окно_Даёт_Цель_Нажатия_Не_Меньше_Минимума()
    {
        // На просторном окне цель нажатия крупнее обычной, но правило «не меньше 32» держится.
        Assert.True(DesktopWide.TapTargetHeight >= PlatformMetrics.PointerTargetMinimum);
    }

    [Fact]
    public void Телефон_Даёт_Основной_Кегль_В_Допустимых_Границах()
    {
        Assert.InRange(Phone.BodyFontSize, PlatformMetrics.BodyFontMinimum, PlatformMetrics.BodyFontMaximum);
    }

    [Fact]
    public void Телефон_В_Ландшафте_Даёт_Основной_Кегль_В_Допустимых_Границах()
    {
        // Компактная раскладка опускает кегль на ступень, но не ниже границы читаемости.
        Assert.InRange(
            PhoneLandscape.BodyFontSize,
            PlatformMetrics.BodyFontMinimum,
            PlatformMetrics.BodyFontMaximum);
    }

    [Fact]
    public void Настольное_Окно_Даёт_Основной_Кегль_В_Допустимых_Границах()
    {
        Assert.InRange(Desktop.BodyFontSize, PlatformMetrics.BodyFontMinimum, PlatformMetrics.BodyFontMaximum);
    }

    [Fact]
    public void Широкое_Настольное_Окно_Даёт_Основной_Кегль_В_Допустимых_Границах()
    {
        Assert.InRange(
            DesktopWide.BodyFontSize,
            PlatformMetrics.BodyFontMinimum,
            PlatformMetrics.BodyFontMaximum);
    }

    [Fact]
    public void Телефон_Убывает_Кегли_От_Заголовка_К_Подписи()
    {
        Assert.True(СтупениУбывают(Phone));
    }

    [Fact]
    public void Телефон_В_Ландшафте_Убывает_Кегли_От_Заголовка_К_Подписи()
    {
        Assert.True(СтупениУбывают(PhoneLandscape));
    }

    [Fact]
    public void Настольное_Окно_Убывает_Кегли_От_Заголовка_К_Подписи()
    {
        Assert.True(СтупениУбывают(Desktop));
    }

    [Fact]
    public void Широкое_Настольное_Окно_Убывает_Кегли_От_Заголовка_К_Подписи()
    {
        Assert.True(СтупениУбывают(DesktopWide));
    }

    [Fact]
    public void Телефон_В_Ландшафте_Считается_Компактным()
    {
        Assert.True(PhoneLandscape.IsCompact);
    }

    [Fact]
    public void Телефон_В_Портрете_Не_Считается_Компактным()
    {
        Assert.False(Phone.IsCompact);
    }

    [Fact]
    public void Низкое_Настольное_Окно_Не_Считается_Компактным()
    {
        // На настольной системе низкое окно прокручивает панель, а не ужимает кегли.
        Assert.False(PlatformMetrics.For(false, 1280, 300).IsCompact);
    }

    [Fact]
    public void Компактный_Телефон_Даёт_Меньше_Отступов()
    {
        Assert.True(PhoneLandscape.PagePadding < Phone.PagePadding);
    }

    [Fact]
    public void Компактный_Телефон_Даёт_Меньший_Основной_Кегль()
    {
        Assert.True(PhoneLandscape.BodyFontSize < Phone.BodyFontSize);
    }

    [Fact]
    public void Телефон_Даёт_Крупнее_Цель_Нажатия_Чем_Настольная_Система()
    {
        // Смысл метрик: палец против курсора. Если правило сломать, кнопки станут одного размера
        // и на телефоне снова будет тесно (жалоба 2026-09-30).
        Assert.True(Phone.TapTargetHeight > Desktop.TapTargetHeight);
    }

    [Fact]
    public void Просторное_Окно_Даёт_Крупнее_Заголовок()
    {
        Assert.True(DesktopWide.TitleFontSize > Desktop.TitleFontSize);
    }

    [Fact]
    public void Просторное_Окно_Даёт_Больше_Отступов()
    {
        Assert.True(DesktopWide.PagePadding > Desktop.PagePadding);
    }

    [Fact]
    public void Телефон_Показывает_Нижнюю_Навигацию()
    {
        Assert.True(Phone.BottomNavHeight > 0);
    }

    [Fact]
    public void Телефон_Показывает_Строку_Состояния()
    {
        Assert.True(Phone.StatusBarHeight > 0);
    }

    [Fact]
    public void Настольное_Окно_Не_Показывает_Нижнюю_Навигацию()
    {
        // 0 означает «полосы нет», а не полосу нулевой высоты.
        Assert.Equal(0d, Desktop.BottomNavHeight);
    }

    [Fact]
    public void Настольное_Окно_Не_Показывает_Строку_Состояния()
    {
        Assert.Equal(0d, Desktop.StatusBarHeight);
    }

    [Fact]
    public void Нулевой_Размер_Даёт_Базовые_Метрики_Телефона()
    {
        // До первого измерения вид получает обычные размеры платформы, а не нули.
        Assert.Equal(Phone, PlatformMetrics.For(true, 0, 0));
    }

    [Fact]
    public void Отрицательный_Размер_Даёт_Базовые_Метрики_Настольной_Системы()
    {
        Assert.Equal(Desktop, PlatformMetrics.For(false, -1, -1));
    }

    [Fact]
    public void Разделитель_Одинаков_На_Обеих_Платформах()
    {
        // Толщина линии — часть единого визуального языка, а не свойство платформы.
        Assert.Equal(Phone.DividerThickness, Desktop.DividerThickness);
    }

    /// <summary>Проверяет порядок ступеней: заголовок не мельче подписи.</summary>
    /// <param name="metrics">Метрики, которые проверяются.</param>
    /// <returns><c>true</c>, если ступени не возрастают от заголовка к подписи.</returns>
    /// <remarks>
    /// Проверка идёт по всей лестнице: подзаголовок не крупнее заголовка, основной текст не крупнее
    /// подзаголовка, подпись не крупнее основного текста. Одно нарушение — и вид выглядит
    /// неопрятно: подпись спорит с заголовком.
    /// </remarks>
    private static bool СтупениУбывают(UiMetrics metrics) =>
        metrics.TitleFontSize >= metrics.SubtitleFontSize &&
        metrics.SubtitleFontSize >= metrics.BodyFontSize &&
        metrics.BodyFontSize >= metrics.CaptionFontSize;
}
