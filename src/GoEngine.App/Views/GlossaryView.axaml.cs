using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;

/// <summary>Экран словаря терминов Го из обучения.</summary>
/// <remarks>
/// Вид один на всех, кто его показывает: оболочка открывает словарь из раздела «Обучение»
/// (<see cref="ShellView"/>). Термины — данные (<c>GlossaryLibrary</c>), поэтому вид не хранит
/// ни одного объяснения: он показывает то, что собрала модель.
/// </remarks>
public sealed partial class GlossaryView : UserControl
{
    /// <summary>Создаёт словарь терминов библиотеки.</summary>
    public GlossaryView()
        : this(new GlossaryViewModel())
    {
    }

    /// <summary>Создаёт словарь с готовой моделью.</summary>
    /// <param name="viewModel">Модель представления словаря.</param>
    public GlossaryView(GlossaryViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        ViewModel = viewModel;

        AvaloniaXamlLoader.Load(this);

        DataContext = viewModel;
    }

    /// <summary>Модель представления словаря.</summary>
    public GlossaryViewModel ViewModel { get; }
}
