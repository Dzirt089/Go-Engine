using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using GoEngine.App.Views;

namespace GoEngine.App.Tests;

/// <summary>Отвязка главного вида от прежней активности: вход в сценарий крэша Android (R-001).</summary>
/// <remarks>
/// Регрессия 0.2.123: при пересоздании активности Android второй раз кладёт ShellView
/// в ContentPresenter и приложение падает с «already has a visual parent». Здесь проверяется
/// само правило без Android: вид снимается с родителя и после этого входит в новый родитель.
/// </remarks>
public sealed class MainViewHostTests
{
    [Fact]
    public void Отвязка_Снимает_Вид_С_ContentPresenter()
    {
        var view = new Button();
        var presenter = new ContentPresenter { Content = view };
        // Без окна ContentPresenter не строит ребёнка сам: UpdateChild — документированный
        // способ сделать это в проверке, ровно так вид попадает в родителя и в приложении.
        presenter.UpdateChild();
        Assert.Same(view, presenter.Content);
        Assert.Same(presenter, view.Parent);

        var detached = MainViewHost.Detach(view);

        Assert.True(detached);
        Assert.Null(presenter.Content);
        Assert.Null(view.Parent);
    }

    [Fact]
    public void Отвязка_Снимает_Вид_С_Панели()
    {
        var view = new Button();
        var panel = new Panel();
        panel.Children.Add(view);

        var detached = MainViewHost.Detach(view);

        Assert.True(detached);
        Assert.Empty(panel.Children);
        Assert.Null(view.Parent);
    }

    [Fact]
    public void Отвязка_Снимает_Вид_С_ContentControl()
    {
        // ContentControl без шаблона не создаёт ContentPresenter, поэтому шаблон применяется
        // вручную: так же вид попадает в него и в приложении.
        var view = new Button();
        var content = new ContentControl { Content = view };
        content.ApplyTemplate();
        Assert.Same(content, view.Parent);

        var detached = MainViewHost.Detach(view);

        Assert.True(detached);
        Assert.Null(content.Content);
        Assert.Null(view.Parent);
    }

    [Fact]
    public void Отвязка_Снимает_Вид_С_Decorator()
    {
        var view = new Button();
        var decorator = new Border { Child = view };

        var detached = MainViewHost.Detach(view);

        Assert.True(detached);
        Assert.Null(decorator.Child);
        Assert.Null(view.Parent);
    }

    [Fact]
    public void Повторная_Отвязка_Ничего_Не_Делает()
    {
        var view = new Button();
        var presenter = new ContentPresenter { Content = view };
        presenter.UpdateChild();

        Assert.True(MainViewHost.Detach(view));
        Assert.False(MainViewHost.Detach(view));
    }

    [Fact]
    public void После_Отвязки_Вид_Входит_В_Новый_Родитель()
    {
        // Главная проверка R-001: раньше второе добавление падало с «already has a visual parent».
        var view = new Button();
        var first = new ContentPresenter { Content = view };
        first.UpdateChild();
        Assert.True(MainViewHost.Detach(view));

        var second = new ContentPresenter { Content = view };
        second.UpdateChild();

        Assert.Same(view, second.Content);
        Assert.Same(second, view.Parent);
    }

    [Fact]
    public void Release_Без_Приложения_Ничего_Не_Делает()
    {
        // В проверках без окна Application нет: отвязка обязана молча ничего не делать,
        // иначе старт проверочных режимов падал бы на пустом месте.
        Assert.Null(Application.Current);
        Assert.False(MainViewHost.Release());
    }
}
