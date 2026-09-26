using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App;

/// <summary>Главное окно партии.</summary>
/// <remarks>
/// Окно связывает доску с моделью представления: щелчок по доске превращается в ход,
/// который проверяет партия в <c>Core</c>. Пока играют двое за одним столом — уровень AI
/// и настройки появятся в T-024.
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly BoardControl? _boardControl;

    /// <summary>Создаёт окно партии и показывает пустую доску 9×9.</summary>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        ViewModel = new MainViewModel(GameState.NewGame(BoardSize.Size9, Komi.For9x9));
        DataContext = ViewModel;

        _boardControl = this.FindControl<BoardControl>("Board");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }
    }

    /// <summary>Модель представления окна.</summary>
    public MainViewModel ViewModel { get; }

    /// <summary>Обрабатывает щелчок по доске.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e) => ViewModel.PlayMove(e.Point);
}
