using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GoEngine.App.Controls;
using GoEngine.Core;

namespace GoEngine.App;

/// <summary>Главное окно партии.</summary>
/// <remarks>
/// Окно ведёт партию: щелчок по доске превращается в ход, партия проверяет его по правилам
/// и возвращает новую позицию. Пока играют двое за одним столом — уровень AI и настройки
/// появятся в T-024, панель статуса — в T-023.
/// </remarks>
public sealed partial class MainWindow : Window
{
    private readonly BoardControl? _boardControl;
    private GameState _game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);

    /// <summary>Создаёт окно партии и показывает пустую доску.</summary>
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        _boardControl = this.FindControl<BoardControl>("Board");

        if (_boardControl is not null)
        {
            _boardControl.MoveRequested += OnMoveRequested;
        }

        ShowPosition();
    }

    /// <summary>Обрабатывает щелчок по доске: играет ход, если правила его разрешают.</summary>
    /// <param name="sender">Доска.</param>
    /// <param name="e">Точка хода.</param>
    private void OnMoveRequested(object? sender, MoveRequestedEventArgs e)
    {
        var played = _game.Play(Move.Play(e.Point, _game.ToMove));

        if (played.IsSuccess)
        {
            ShowPosition();
        }
    }

    /// <summary>Показывает текущую позицию и последний ход.</summary>
    private void ShowPosition()
    {
        if (_boardControl is null)
        {
            return;
        }

        var last = _game.Moves.LastOrDefault(move => move.Type == MoveType.Play);

        _boardControl.Board = _game.Board;
        _boardControl.LastMove = last.IsNone ? null : last.Point;
    }
}
