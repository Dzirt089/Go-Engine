namespace GoEngine.App.Tests;

/// <summary>Разметка выбранного чипа: связка класса с признаком выбора и сам стиль.</summary>
/// <remarks>
/// Без окна и темы кнопки-чипы не материализуются, поэтому проверяется исходная разметка:
/// если пропадёт привязка <c>Classes.selected</c> или стиль, тест упадёт до живой приёмки.
/// </remarks>
public sealed class ShapeChipMarkupTests
{
    [Fact]
    public void Выбранный_Чип_Связан_С_Признаком_Выбора()
    {
        var markup = File.ReadAllText(Markup("ShapeChipsControl.axaml"));

        Assert.Contains("Classes.selected=\"{Binding IsSelected}\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Стиль_Выбранного_Чипа_Объявлен()
    {
        var styles = File.ReadAllText(Markup("UiStyles.Controls.axaml"));

        Assert.Contains("Button.app-chip.selected", styles, StringComparison.Ordinal);
    }

    /// <summary>Файл разметки, скопированный рядом с тестовой сборкой.</summary>
    /// <param name="name">Имя файла.</param>
    /// <returns>Полный путь к файлу.</returns>
    /// <remarks>
    /// Каталог берётся у самой тестовой сборки, а не у текущего процесса: тесты в этой среде
    /// запускает отдельная утилита TestRunner, и её рабочий каталог — не каталог тестов.
    /// </remarks>
    private static string Markup(string name) =>
        Path.Combine(Path.GetDirectoryName(typeof(ShapeChipMarkupTests).Assembly.Location)!, "Views", name);
}
