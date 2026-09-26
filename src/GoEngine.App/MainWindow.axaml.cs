using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.Core;

namespace GoEngine.App;

/// <summary>Главное окно партии.</summary>
/// <remarks>
/// Пока в окне только доска: ввод появится в T-022, панель статуса — в T-023,
/// меню и настройки — в T-024.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Создаёт окно партии и показывает пустую доску 9×9.</summary>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var board = this.FindControl<BoardControl>("Board");

        if (board is not null)
        {
            board.Board = new Board(BoardSize.Size9);
        }
    }
}
