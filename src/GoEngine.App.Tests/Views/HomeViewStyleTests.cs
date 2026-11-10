using System.Text.RegularExpressions;

namespace GoEngine.App.Tests;

/// <summary>Страж разметки главного экрана: цвета «на акценте» заданы локально (R-003, D-125).</summary>
/// <remarks>
/// <para>
/// Регрессия 2026-11-10: цвета основной карточки «Партия с ИИ» задавались стилями
/// <c>Button.home-card.primary TextBlock.home-card-title/caption</c>, объявленными до общих
/// стилей тех же TextBlock. В Avalonia при равном приоритете побеждает объявленный позже,
/// поэтому на акценте рисовались обычные цвета темы, а подпись становилась почти невидимой.
/// </para>
/// <para>
/// Тест читает ту же разметку, что уезжает в приложение (Content в GoEngine.App.Tests.csproj),
/// и требует локальных значений: они важнее любого порядка стилей и не могут «переехать»
/// при будущей правке файла.
/// </para>
/// </remarks>
public sealed class HomeViewStyleTests
{
    /// <summary>Разметка главного экрана, включённая в выходные файлы тестов.</summary>
    private static readonly string Markup = ReadMarkup();

    [Fact]
    public void Заголовок_Основной_Карточки_Задаёт_Цвет_На_Акценте_Локально()
    {
        var block = Element("GameTitle");

        Assert.Contains(
            "Foreground=\"{DynamicResource AppOnAccentBrush}\"",
            block,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Подпись_Основной_Карточки_Задаёт_Приглушённый_Цвет_На_Акценте_Локально()
    {
        var block = Element("GameCaption");

        Assert.Contains(
            "Foreground=\"{DynamicResource AppOnAccentMutedBrush}\"",
            block,
            StringComparison.Ordinal);

        // Полупрозрачность — не цвет: на светлом акценте она уводила контраст ниже нормы,
        // а на тёмном делала подпись серой. Цвет приходит отдельным токеном (D-125).
        Assert.DoesNotContain("Opacity=", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Цвет_На_Акценте_Не_Задаётся_Стилем_Который_Может_Быть_Перекрыт_Порядком()
    {
        // Этих селекторов не должно быть: их перекрывали общие стили, объявленные ниже.
        // Оставляем проверку только на создавшую дефект комбинацию селекторов.
        var selector = new Regex(
            "Style\\s+Selector=\"Button\\.home-card\\.primary\\s+TextBlock\\.home-card-(title|caption)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        Assert.DoesNotMatch(selector, Markup);
    }

    /// <summary>Возвращает разметку элемента по имени.</summary>
    /// <param name="name">Значение <c>x:Name</c>.</param>
    /// <returns>Текст элемента вместе с атрибутами.</returns>
    private static string Element(string name)
    {
        var pattern = "<TextBlock\\b[^>]*x:Name=\"" + Regex.Escape(name) + "\"[^>]*/>";
        var match = Regex.Match(Markup, pattern, RegexOptions.None, TimeSpan.FromSeconds(2));

        Assert.True(match.Success, "в HomeView.axaml нет TextBlock x:Name=\"" + name + "\"");

        return match.Value;
    }

    /// <summary>Читает разметку главного экрана из каталога тестовой сборки.</summary>
    /// <returns>Текст XAML.</returns>
    private static string ReadMarkup()
    {
        var directory = Path.GetDirectoryName(typeof(HomeViewStyleTests).Assembly.Location)!;

        return File.ReadAllText(Path.Combine(directory, "Views", "HomeView.axaml"));
    }
}
