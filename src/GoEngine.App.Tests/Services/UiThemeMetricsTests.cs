using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GoEngine.App.Services;

namespace GoEngine.App.Tests;

/// <summary>Тесты темы: числа размеров совпадают с метриками платформы.</summary>
/// <remarks>
/// <para>
/// XAML не умеет вызывать C#-функцию, поэтому числа метрик продублированы ресурсами
/// <c>UiTheme.axaml</c>, и там же написано, что расхождение — ошибка, а не настройка
/// (<c>Services/PlatformMetrics.cs</c>, ветка mobile). Этот тест и есть проверка той записи:
/// он читает саму разметку темы и сверяет числа с <see cref="PlatformMetrics.For"/>.
/// </para>
/// <para>
/// Проверяются ключи, у которых есть пара в метриках. Ключ, удалённый из темы, роняет тест:
/// молча потерять размер нельзя.
/// </para>
/// <para>
/// Общий <c>AppTouchTarget</c> намеренно не сверяется с настольной веткой метрик: этот ресурс
/// описывает цель нажатия 48 точек (палец) и используется основными кнопками. Настольные
/// размеры живут литералами в <c>UiStyles.Controls.axaml</c> — так и написано в теме.
/// </para>
/// </remarks>
public sealed class UiThemeMetricsTests
{
    /// <summary>Ресурсы темы, прочитанные из разметки: имя ключа — значение как записано.</summary>
    private static readonly IReadOnlyDictionary<string, string> ThemeValues = ReadTheme();

    /// <summary>Телефон в портрете: размеры со снимка пользователя (360×784).</summary>
    private static readonly UiMetrics Phone = PlatformMetrics.For(true, 360, 784);

    /// <summary>Обычное настольное окно: 1024×768, до порога просторного не дотягивает.</summary>
    private static readonly UiMetrics Desktop = PlatformMetrics.For(false, 1024, 768);

    [Fact]
    public void Цель_нажатия_Телефона_Совпадает_С_Метриками()
    {
        // 48 точек — размер, по которому попадает палец (руководство Android Material).
        Assert.Equal(Number(Phone.TapTargetHeight), Value("AppMobileTapTarget"));
    }

    [Fact]
    public void Кегли_Совпадают_С_Метриками_Платформы()
    {
        Assert.Equal(Number(Phone.ActionFontSize), Value("AppMobileFontAction"));
        Assert.Equal(Number(Phone.BodyFontSize), Value("AppMobileFontBody"));
        Assert.Equal(Number(Desktop.TitleFontSize), Value("AppFontTitle"));
        Assert.Equal(Number(Desktop.SubtitleFontSize), Value("AppFontSubtitle"));
        Assert.Equal(Number(Desktop.BodyFontSize), Value("AppFontBody"));
        Assert.Equal(Number(Desktop.CaptionFontSize), Value("AppFontCaption"));
    }

    [Fact]
    public void Радиусы_Совпадают_С_Метриками_Платформы()
    {
        Assert.Equal(Number(Phone.RadiusSmall), Value("AppMobileRadiusSmall"));
        Assert.Equal(Number(Desktop.RadiusSmall), Value("AppRadiusSmall"));
        Assert.Equal(Number(Desktop.RadiusMedium), Value("AppRadiusMedium"));
    }

    [Fact]
    public void Отступы_Совпадают_С_Метриками_Платформы()
    {
        // Кнопка в теме задаёт отступ парой «по горизонтали, по вертикали»; метрика хранит
        // только горизонтальный отступ — по вертикали кнопка растягивается высотой.
        Assert.Equal(Number(Phone.CardPadding), First(Value("AppMobilePaddingAction")));
        Assert.Equal(Number(Phone.PagePadding), Value("AppPaddingPage"));
        Assert.Equal(Number(Desktop.GapSmall), Value("AppGapSmall"));
        Assert.Equal(Number(Desktop.GapMedium), Value("AppGapMedium"));
    }

    [Fact]
    public void Высоты_Полос_Совпадают_С_Метриками_Платформы()
    {
        Assert.Equal(Number(Phone.BottomNavHeight), Value("AppNavHeight"));
        Assert.Equal(Number(Phone.StatusBarHeight), Value("AppStatusHeight"));
    }

    /// <summary>Читает значения ресурсов темы из разметки, лежащей рядом со сборкой тестов.</summary>
    /// <returns>Словарь «ключ — значение как записано в файле».</returns>
    /// <remarks>
    /// Файл приходит из проекта приложения ссылкой (см. <c>GoEngine.App.Tests.csproj</c>), поэтому
    /// проверяется ровно та разметка, что уезжает в приложение, а не копия чисел в тестовом коде.
    /// </remarks>
    private static IReadOnlyDictionary<string, string> ReadTheme()
    {
        // Каталог сборки тестов известен по её собственному файлу: тесты гоняет и утилита
        // Tools.TestRunner, у которой AppContext.BaseDirectory — её собственный каталог.
        var assemblyDirectory = Path.GetDirectoryName(typeof(UiThemeMetricsTests).Assembly.Location)!;
        var text = File.ReadAllText(Path.Combine(assemblyDirectory, "Views", "UiTheme.axaml"));
        Dictionary<string, string> values = new(StringComparer.Ordinal);

        foreach (Match match in Regex.Matches(
            text,
            "<(?:x:Double|CornerRadius|Thickness)\\s+x:Key=\"(?<key>[^\"]+)\"\\s*>(?<value>[^<]+)<",
            RegexOptions.None,
            TimeSpan.FromSeconds(2)))
        {
            values[match.Groups["key"].Value] = match.Groups["value"].Value.Trim();
        }

        return values;
    }

    /// <summary>Значение ресурса темы по ключу.</summary>
    /// <param name="key">Ключ ресурса.</param>
    /// <returns>Значение как записано в разметке.</returns>
    /// <exception cref="InvalidOperationException">Ключа в теме нет — размер потерян.</exception>
    private static string Value(string key) =>
        ThemeValues.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException($"В теме нет ресурса {key}: размер потерян.");

    /// <summary>Первое число значения-пары: «10,0» — это 10.</summary>
    /// <param name="value">Значение ресурса.</param>
    /// <returns>Первое число строкой.</returns>
    private static string First(string value) => value.Split(',')[0].Trim();

    /// <summary>Печатает число так, как его пишет разметка: без лишних нулей.</summary>
    /// <param name="value">Число метрики.</param>
    /// <returns>Строка для сравнения со значением ресурса.</returns>
    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
