using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты вида словаря терминов.</summary>
/// <remarks>
/// Словарь — длинный список текста, поэтому он обязан быть виртуализованным: на телефоне
/// с экраном 120 Гц прокрутка невиртуализованного списка «фризила» (замечание пользователя
/// 2026-10-07). Тест сторожит это решение: панель списка — <see cref="VirtualizingStackPanel"/>.
/// </remarks>
public sealed class GlossaryViewTests
{
    [Fact]
    public void Словарь_Показывает_Все_Термины()
    {
        var view = new GlossaryView();
        var list = view.FindControl<ListBox>("TermsList")!;

        Assert.Equal(view.ViewModel.Count, (list.ItemsSource as IReadOnlyList<GlossaryLine>)!.Count);
    }

    [Fact]
    public void Словарь_Виртуализован()
    {
        var view = new GlossaryView();
        var list = view.FindControl<ListBox>("TermsList")!;

        Assert.IsType<VirtualizingStackPanel>(list.ItemsPanel.Build());
    }

    [Fact]
    public void Строка_Словаря_Не_Ловит_Нажатия()
    {
        // Строку не выбирают: жест прокрутки должен доходить до списка целиком.
        var view = new GlossaryView();
        var list = view.FindControl<ListBox>("TermsList")!;

        Assert.False(list.Focusable);

        var style = list.Styles.OfType<Avalonia.Styling.Style>()
            .FirstOrDefault(item => item.Selector?.ToString() == "ListBoxItem");

        Assert.NotNull(style);
    }
}
