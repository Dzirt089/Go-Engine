using System.Globalization;
using System.Text.RegularExpressions;

namespace GoEngine.App.Tests;

/// <summary>Тесты палитры: светлая и тёмная темы объявляют одно и то же и читаются по контрасту.</summary>
/// <remarks>
/// <para>
/// Разметка темы приходит в сборку тестов ссылкой (<c>GoEngine.App.Tests.csproj</c>), поэтому
/// проверяется ровно тот файл, что уезжает в приложение. Контраст считается по WCAG: отношение
/// яркостей 4.5:1 — норма для обычного текста, 3:1 — для графики и крупных элементов.
/// </para>
/// <para>
/// Тест ловит две ошибки, которые иначе видит только глаз на телефоне: ресурс, объявленный
/// в одной теме и забытый в другой (тогда на тёмной теме элемент остаётся светлым), и цвет,
/// подобранный «на глаз» с недостаточным контрастом.
/// </para>
/// </remarks>
public sealed class UiThemeContrastTests
{
    /// <summary>Ресурсы каждой темы: имя темы — словарь «ключ — значение».</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Themes = ReadThemes();

    [Fact]
    public void Объявлены_Обе_Темы()
    {
        Assert.Contains("Light", Themes.Keys);
        Assert.Contains("Dark", Themes.Keys);
    }

    [Fact]
    public void Темы_Объявляют_Одни_И_Те_Же_Ресурсы()
    {
        var light = Themes["Light"].Keys.Order(StringComparer.Ordinal).ToArray();
        var dark = Themes["Dark"].Keys.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(light, dark);
    }

    [Fact]
    public void Тёмная_Тема_Темнее_Светлой()
    {
        var light = Luminance(Themes["Light"]["AppBackgroundColor"]);
        var dark = Luminance(Themes["Dark"]["AppBackgroundColor"]);

        Assert.True(dark < light, $"фон тёмной темы светлее светлой: {dark:F3} против {light:F3}");
    }

    [Fact]
    public void Тёмная_Поверхность_Темнее_Светлой()
    {
        Assert.True(Luminance(Themes["Dark"]["AppSurfaceColor"]) < Luminance(Themes["Light"]["AppSurfaceColor"]));
    }

    [Theory]
    [InlineData("AppTextColor", "AppSurfaceColor", 4.5)]
    [InlineData("AppTextColor", "AppBackgroundColor", 4.5)]
    [InlineData("AppTextMutedColor", "AppSurfaceColor", 4.5)]
    [InlineData("AppTextMutedColor", "AppBackgroundColor", 4.5)]
    [InlineData("AppAccentColor", "AppSurfaceColor", 4.5)]
    [InlineData("AppAccentColor", "AppBackgroundColor", 4.5)]
    [InlineData("AppAccentColor", "AppAccentSoftColor", 3.0)]
    [InlineData("AppWinTextColor", "AppWinSoftColor", 4.5)]
    [InlineData("AppLoseTextColor", "AppLoseSoftColor", 4.5)]
    [InlineData("AppDrawTextColor", "AppDrawSoftColor", 4.5)]
    public void Контраст_Пар_Не_Ниже_Нормы(string foreground, string background, double minimum)
    {
        foreach (var (name, values) in Themes)
        {
            var ratio = Contrast(values[foreground], values[background]);

            Assert.True(
                ratio >= minimum,
                $"{name}: {foreground} на {background} — контраст {ratio:F2}:1, нужно не ниже {minimum:F1}:1");
        }
    }

    [Fact]
    public void Опасный_Цвет_Контрастен_Обеим_Темам()
    {
        foreach (var (name, values) in Themes)
        {
            var ratio = Contrast(values["AppDangerColor"], values["AppSurfaceColor"]);

            Assert.True(ratio >= 4.5, $"{name}: AppDangerColor — контраст {ratio:F2}:1");
        }
    }

    [Fact]
    public void Текст_На_Акценте_Контрастен_Акценту()
    {
        foreach (var (name, values) in Themes)
        {
            var ratio = Contrast(values["AppOnAccentColor"], values["AppAccentColor"]);

            Assert.True(ratio >= 4.5, $"{name}: текст на акценте — контраст {ratio:F2}:1");
        }
    }

    /// <summary>Читает цвета обеих тем из разметки темы.</summary>
    /// <returns>Словарь «имя темы — цвета этой темы».</returns>
    /// <remarks>
    /// Разбор простой регуляркой по блокам: файл — данные, и проверять его содержимое тестом
    /// дешевле и надёжнее, чем поднимать окно ради двух десятков цветов. Значения берутся
    /// у цветов (<c>AppDangerColor</c> и подобных), потому что кисти ссылаются на них.
    /// </remarks>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadThemes()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(UiThemeContrastTests).Assembly.Location)!;
        var text = File.ReadAllText(Path.Combine(assemblyDirectory, "Views", "UiTheme.axaml"));
        Dictionary<string, IReadOnlyDictionary<string, string>> themes = new(StringComparer.Ordinal);

        foreach (Match block in Regex.Matches(
            text,
            "<ResourceDictionary x:Key=\"(?<theme>Light|Dark)\">(?<body>.*?)</ResourceDictionary>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(2)))
        {
            Dictionary<string, string> colors = new(StringComparer.Ordinal);

            foreach (Match color in Regex.Matches(
                block.Groups["body"].Value,
                "<Color x:Key=\"(?<key>[^\"]+)\">(?<value>#[0-9A-Fa-f]{6})</Color>",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            {
                colors[color.Groups["key"].Value] = color.Groups["value"].Value;
            }

            // Часть кистей объявлена цветом прямо в кисти (текст на акценте, опасный цвет):
            // читаем их тем же способом — ключом с окончанием «Color», как у парных ресурсов.
            foreach (Match brush in Regex.Matches(
                block.Groups["body"].Value,
                "<SolidColorBrush x:Key=\"(?<key>[A-Za-z]+)Brush\" Color=\"(?<value>#[0-9A-Fa-f]{6})\"",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            {
                colors[brush.Groups["key"].Value + "Color"] = brush.Groups["value"].Value;
            }

            themes[block.Groups["theme"].Value] = colors;
        }

        return themes;
    }

    /// <summary>Считает контраст двух цветов по WCAG.</summary>
    /// <param name="foreground">Цвет текста или знака.</param>
    /// <param name="background">Цвет подложки.</param>
    /// <returns>Отношение контраста от 1 до 21.</returns>
    private static double Contrast(string foreground, string background)
    {
        var light = Math.Max(Luminance(foreground), Luminance(background));
        var dark = Math.Min(Luminance(foreground), Luminance(background));

        return (light + 0.05) / (dark + 0.05);
    }

    /// <summary>Считает относительную яркость цвета по WCAG.</summary>
    /// <param name="hex">Цвет вида <c>#RRGGBB</c>.</param>
    /// <returns>Яркость от 0 до 1.</returns>
    private static double Luminance(string hex)
    {
        var value = hex.TrimStart('#');
        var channels = new double[3];

        for (var index = 0; index < 3; index++)
        {
            var raw = int.Parse(value.AsSpan(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            channels[index] = raw <= 0.03928 ? raw / 12.92 : Math.Pow((raw + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
    }
}
