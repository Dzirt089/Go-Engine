using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GoEngine.Core;

namespace GoEngine.App.ViewModels;

/// <summary>Состояние партии для окна: кто ходит, номер хода, коми, счёт и статус.</summary>
/// <remarks>
/// Модель представления ничего не рисует и не знает про Avalonia: она показывает данные
/// <see cref="GameState"/> строками для панели статуса. Все правила (легальность хода,
/// завершение партии, подсчёт) выполняет <c>Core</c>.
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private GameState _game;

    /// <summary>Создаёт модель представления для партии.</summary>
    /// <param name="game">Партия.</param>
    public MainViewModel(GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Текущая позиция.</summary>
    public Board Board => _game.Board;

    /// <summary>Точка последнего хода или <c>null</c>, если ходов ещё не было.</summary>
    public Point? LastMove
    {
        get
        {
            var last = _game.Moves.LastOrDefault(move => move.Type == MoveType.Play);

            return last.IsNone ? null : last.Point;
        }
    }

    /// <summary>Кто ходит: «Чёрные» или «Белые».</summary>
    public string ToMove => _game.ToMove == StoneColor.Black ? "Чёрные" : "Белые";

    /// <summary>Номер последнего хода.</summary>
    public string MoveNumber => _game.MoveNumber.ToString(CultureInfo.InvariantCulture);

    /// <summary>Коми партии.</summary>
    public string Komi => _game.Komi.ToString();

    /// <summary>Текущий счёт по китайским правилам.</summary>
    public string Score => Scorer.Calculate(_game.Board, _game.Komi).ToString();

    /// <summary>Состояние партии словами.</summary>
    public string Status => _game.Status switch
    {
        var status when status == GameStatus.FinishedByTwoPasses => "Завершена двумя пасами",
        var status when status == GameStatus.FinishedByResign => "Завершена сдачей",
        var status when status == GameStatus.FinishedByMoveLimit => "Завершена по лимиту ходов",
        _ => "Идёт"
    };

    /// <summary>Играет ход, если правила его разрешают.</summary>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если ход принят.</returns>
    public bool PlayMove(Point point)
    {
        var played = _game.Play(Move.Play(point, _game.ToMove));

        if (!played.IsSuccess)
        {
            return false;
        }

        NotifyAll();

        return true;
    }

    /// <summary>Начинает новую партию.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="komi">Коми партии.</param>
    public void StartNewGame(BoardSize size, Komi komi)
    {
        _game = GameState.NewGame(size, komi);

        NotifyAll();
    }

    /// <summary>Сообщает об изменении всех свойств партии.</summary>
    /// <remarks>Свойства выводятся из одной партии, поэтому после хода меняются все сразу.</remarks>
    private void NotifyAll()
    {
        foreach (var name in new[]
        {
            nameof(Board),
            nameof(LastMove),
            nameof(ToMove),
            nameof(MoveNumber),
            nameof(Komi),
            nameof(Score),
            nameof(Status)
        })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>Сообщает об изменении свойства.</summary>
    /// <param name="propertyName">Имя свойства.</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
