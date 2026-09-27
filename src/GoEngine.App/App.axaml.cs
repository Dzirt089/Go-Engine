using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GoEngine.AI;
using GoEngine.App.Views;

namespace GoEngine.App;

/// <summary>Приложение Go Engine: окно партии и его жизненный цикл.</summary>
/// <remarks>
/// Слой <c>App</c> — единственный, которому разрешены Avalonia и SkiaSharp
/// (<c>PROJECT.md</c>, <c>AGENTS.md</c>, п. 1). Бизнес-логики здесь нет: правила живут в <c>Core</c>,
/// выбор хода — в <c>AI</c>.
/// На настольных системах приложение открывает окно с меню, на Android — единственный вид
/// без меню: жизненный цикл там <see cref="ISingleViewApplicationLifetime"/>.
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>Оценка позиции нейросетью для уровней Дан.</summary>
    /// <remarks>
    /// Головы задают её до запуска: библиотека интерфейса не знает про ONNX
    /// (<c>DECISIONS.md</c>, D-034), а модель загружает та голова, у которой есть файл.
    /// <c>null</c> — уровни Дан недоступны, играют уровни кю.
    /// </remarks>
    public static IPositionEvaluator? Evaluator { get; set; }

    /// <summary>Каталог с файлами моделей: его задаёт голова, зная, где лежат файлы.</summary>
    /// <remarks>Без каталога уровни Дан недоступны — как и без оценщика (D-039).</remarks>
    public static string? ModelsDirectory { get; set; }

    /// <summary>Стороны доски, для которых нашлась модель.</summary>
    /// <remarks>
    /// Заполняет голова: она знает каталог и таблицу профилей (D-039). Пустое множество
    /// означает, что уровни Дан недоступны ни на одной доске, и играют уровни кю.
    /// </remarks>
    public static IReadOnlySet<int> ModelSizes { get; set; } = new HashSet<int>();

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new MainWindow();
                break;

            case ISingleViewApplicationLifetime mobile:
                mobile.MainView = new BoardView();
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
