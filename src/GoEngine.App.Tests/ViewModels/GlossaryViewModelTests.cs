using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Problems;

namespace GoEngine.App.Tests;

/// <summary>Тесты словаря терминов Го из обучения.</summary>
/// <remarks>
/// Обучение ведётся по-русски: объяснение термина обязано быть русским, а оригинальное название
/// (японское или китайское) стоит рядом как справка (замечание пользователя 2026-10-07).
/// </remarks>
public sealed class GlossaryViewModelTests
{
    [Fact]
    public void Словарь_Не_Пуст()
    {
        var model = new GlossaryViewModel();

        Assert.True(model.Count >= 10, $"терминов в словаре: {model.Count}");
    }

    [Fact]
    public void У_Каждого_Термина_Есть_Название_И_Объяснение()
    {
        var model = new GlossaryViewModel();

        Assert.All(
            model.Terms,
            term => Assert.False(
                string.IsNullOrWhiteSpace(term.Name) || string.IsNullOrWhiteSpace(term.Text),
                $"термин «{term.Name}» без названия или объяснения"));
    }

    [Fact]
    public void Объяснения_На_Русском()
    {
        var model = new GlossaryViewModel();

        Assert.All(
            model.Terms,
            term => Assert.True(
                term.Text.Any(ch => ch >= 'А' && ch <= 'я'),
                $"термин «{term.Name}»: объяснение без кириллицы"));
    }

    [Fact]
    public void Термины_Уроков_Ссылаются_На_Уроки()
    {
        var model = new GlossaryViewModel();

        Assert.Contains(model.Terms, term => term.Lessons.StartsWith("Уроки:", StringComparison.Ordinal));
    }

    [Fact]
    public void Ссылка_Называет_Урок_Словами()
    {
        var model = new GlossaryViewModel();
        var atari = model.Terms.First(term => term.Name == "Атари");

        Assert.Contains("Атари", atari.Lessons, StringComparison.Ordinal);
    }

    [Fact]
    public void Термины_Словаря_Идут_По_Алфавиту()
    {
        var model = new GlossaryViewModel();
        var names = model.Terms.Select(term => term.Name).ToArray();

        Assert.Equal(names.OrderBy(name => name, StringComparer.CurrentCulture), names);
    }
}
