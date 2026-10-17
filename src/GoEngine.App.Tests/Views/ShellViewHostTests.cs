using Avalonia.Controls;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.App.Views;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Единственная оболочка на процесс: повторное создание активности не плодит копии.</summary>
/// <remarks>
/// Регрессия N1 отчёта QA 2026-10-17: при пересоздании активности Android второй раз кладёт
/// <c>ShellView</c> в <c>ContentPresenter</c> и падает с «already has a visual parent». Тест
/// проверяет само правило без Android: вид создаётся один раз, а перед повторной выдачей
/// отвязывается от прежнего родителя.
/// </remarks>
public sealed class ShellViewHostTests
{
    [Fact]
    public void Повторный_Вызов_Не_Создаёт_Второй_Вид()
    {
        var created = 0;
        var host = new ShellViewHost(() =>
        {
            created++;

            return CreateView();
        });

        var first = host.Acquire();
        var second = host.Acquire();

        Assert.Same(first, second);
        Assert.Equal(1, created);
    }

    [Fact]
    public void Повторный_Вызов_Отвязывает_Вид_От_Прежнего_Родителя()
    {
        // В приложении родитель — ContentPresenter активности; здесь его роль играет панель:
        // правило одно — вид снимается с прежнего места до повторной выдачи.
        var host = new ShellViewHost(CreateView);
        var first = new Panel();
        first.Children.Add(host.Acquire());

        var second = host.Acquire();

        Assert.Null(second.Parent);
        Assert.Empty(first.Children);
        Assert.Same(second, host.Current);
    }

    [Fact]
    public void Вид_После_Отвязки_Входит_Во_Второй_Родитель()
    {
        var host = new ShellViewHost(CreateView);
        var first = new Panel();
        first.Children.Add(host.Acquire());

        var second = new Panel();
        second.Children.Add(host.Acquire());

        Assert.Same(host.Current, second.Children[0]);
        _ = Assert.Single(second.Children);
    }

    [Fact]
    public void Закрытие_Активности_Отвязывает_Вид()
    {
        var host = new ShellViewHost(CreateView);
        var parent = new Panel();
        parent.Children.Add(host.Acquire());

        host.Release();

        Assert.Empty(parent.Children);
        Assert.Null(host.Current.Parent);
    }

    /// <summary>Создаёт оболочку с готовой моделью: без чтения файлов настроек и прогресса.</summary>
    /// <returns>Вид оболочки.</returns>
    private static ShellView CreateView()
    {
        var settings = AppSettings.From(BoardSize.Size9, DifficultyLevel.Kyu20, StoneColor.Black, Komi.For9x9);
        var game = new MainViewModel(settings, new Random(TestViewModel.Seed));
        var shell = new ShellViewModel(settings, game, new ProblemViewModel());

        return new ShellView(shell);
    }
}
