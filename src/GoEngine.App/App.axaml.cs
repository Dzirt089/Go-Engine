using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GoEngine.App;

/// <summary>Приложение Go Engine: окно партии и его жизненный цикл.</summary>
/// <remarks>
/// Слой <c>App</c> — единственный, которому разрешены Avalonia и SkiaSharp
/// (<c>PROJECT.md</c>, <c>AGENTS.md</c>, п. 1). Бизнес-логики здесь нет: правила живут в <c>Core</c>,
/// выбор хода — в <c>AI</c>.
/// </remarks>
public sealed partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
