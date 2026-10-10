using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Тесты раскладки обучения и разбора партий: управление внизу, текст — в середине.</summary>
/// <remarks>
/// Замечание пользователя 2026-10-10: на телефоне управление занимало четверть экрана, а выбор
/// урока и партии «уезжал» при прокрутке текста. Панель перестроена в три полосы: шапка с выбором,
/// прокручиваемый текст и одна строка управления. Эти тесты сторожат решение числами:
/// строка управления — одна и не выше 52 точек, полезный текст занимает больше половины панели,
/// а выбор материала стоит в шапке и не прокручивается.
/// </remarks>
public sealed class StudyLayoutTests
{
    [Fact]
    public void Выбор_Урока_Стоит_В_Шапке_Панели()
    {
        var view = new LessonView(new LessonViewModel());
        var box = view.FindControl<ComboBox>("LessonBox")!;

        Assert.Equal("PanelHeader", box.FindLogicalAncestorOfType<Grid>()!.Name);
        Assert.Null(box.FindLogicalAncestorOfType<ScrollViewer>());
    }

    [Fact]
    public void Выбор_Партии_Стоит_В_Шапке_Панели()
    {
        var view = new ReviewView(new ReviewViewModel());
        var box = view.FindControl<ComboBox>("GameBox")!;

        Assert.Equal("PanelHeader", box.FindLogicalAncestorOfType<Grid>()!.Name);
        Assert.Null(box.FindLogicalAncestorOfType<ScrollViewer>());
    }

    [Fact]
    public void Словарь_Терминов_Стоит_В_Шапке_Панели()
    {
        // Словарь — редкое действие: в шапке он не спорит за место с главной кнопкой «Далее».
        var lesson = new LessonView(new LessonViewModel());
        var review = new ReviewView(new ReviewViewModel());

        Assert.Equal("PanelHeader", lesson.FindControl<Button>("GlossaryButton")!.FindLogicalAncestorOfType<Grid>()!.Name);
        Assert.Equal("PanelHeader", review.FindControl<Button>("GlossaryButton")!.FindLogicalAncestorOfType<Grid>()!.Name);
    }

    [Fact]
    public void Управление_Урока_Собрано_В_Одну_Строку()
    {
        var view = new LessonView(new LessonViewModel());
        Arrange(view);

        WithoutOverflow(view.FindControl<Grid>("PanelActions")!, 4);
    }

    [Fact]
    public void Управление_Разбора_Собрано_В_Одну_Строку()
    {
        var view = new ReviewView(new ReviewViewModel());
        Arrange(view);

        WithoutOverflow(view.FindControl<Grid>("PanelActions")!, 4);
    }

    [Fact]
    public void Текст_Урока_Занимает_Большую_Часть_Панели()
    {
        var view = new LessonView(new LessonViewModel());
        Arrange(view);

        var panel = view.FindControl<Border>("Panel")!;
        var scroll = view.FindControl<ScrollViewer>("NoteScroll")!;

        Assert.True(
            scroll.Bounds.Height >= panel.Bounds.Height * 0.5,
            $"текст {scroll.Bounds.Height:F0} из панели {panel.Bounds.Height:F0}");
    }

    [Fact]
    public void Текст_Разбора_Занимает_Большую_Часть_Панели()
    {
        var view = new ReviewView(new ReviewViewModel());
        Arrange(view);

        var panel = view.FindControl<Border>("Panel")!;
        var scroll = view.FindControl<ScrollViewer>("NoteScroll")!;

        Assert.True(
            scroll.Bounds.Height >= panel.Bounds.Height * 0.5,
            $"разбор {scroll.Bounds.Height:F0} из панели {panel.Bounds.Height:F0}");
    }

    [Fact]
    public void Главное_Действие_Шире_Значков()
    {
        // «Далее» — главное действие урока: оно занимает остаток строки, а значки стоят по 44.
        var view = new LessonView(new LessonViewModel());
        Arrange(view);

        var next = view.FindControl<Button>("NextButton")!;
        var hint = view.FindControl<Button>("HintButton")!;

        var panel = view.FindControl<Border>("Panel")!;
        var actions = view.FindControl<Grid>("PanelActions")!;

        Assert.True(
            next.Bounds.Width > hint.Bounds.Width,
            $"«Далее» {next.Bounds.Width:F0}, значок {hint.Bounds.Width:F0}; вид {view.Bounds.Width:F0}x{view.Bounds.Height:F0}, панель {panel.Bounds.Width:F0}x{panel.Bounds.Height:F0}, строка {actions.Bounds.Width:F0}x{actions.Bounds.Height:F0}");
    }

    /// <summary>Проверяет, что управление уложилось в одну строку и ничего не вылезло.</summary>
    /// <param name="actions">Строка управления.</param>
    /// <param name="count">Сколько кнопок в ней ожидается.</param>
    private static void WithoutOverflow(Grid actions, int count)
    {
        var buttons = actions.GetLogicalChildren().OfType<Button>().ToArray();

        Assert.Equal(count, buttons.Length);
        Assert.True(actions.Bounds.Height <= 52, $"строка управления {actions.Bounds.Height:F0} точек");

        Assert.All(
            buttons,
            button => Assert.True(
                button.Bounds.Right <= actions.Bounds.Width + 1,
                $"кнопка вылезла за строку: {button.Bounds.Right:F0} > {actions.Bounds.Width:F0}"));
    }

    /// <summary>Укладывает вид в размер телефона: раскладку вид выбирает по своему размеру.</summary>
    /// <param name="view">Вид раздела.</param>
    /// <remarks>
    /// Содержимое вида укладывается напрямую: без окна и темы шаблон <see cref="UserControl"/>
    /// не создаётся, и вложенные элементы остаются нулевого размера — проверка «сколько места
    /// занимает управление» превратилась бы в проверку нулей (проверено прогоном).
    /// </remarks>
    private static void Arrange(UserControl view)
    {
        view.Measure(new Size(380, 780));
        view.Arrange(new Rect(0, 0, 380, 780));

        if (view.Content is Control content)
        {
            content.Measure(new Size(380, 780));
            content.Arrange(new Rect(0, 0, 380, 780));
        }
    }
}
