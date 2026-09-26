using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.Core;
using GoEngine.Core.Sgf;

namespace GoEngine.App.ViewModels;

/// <summary>Состояние партии для окна: доска, панель статуса и ход AI.</summary>
/// <remarks>
/// Модель представления ничего не рисует и не знает про Avalonia: она показывает данные
/// <see cref="GameState"/> строками и играет за AI. Все правила (легальность хода, завершение
/// партии, подсчёт) выполняет <c>Core</c>, выбор хода — <c>AI</c>.
/// Ход AI считается синхронно: на слабых уровнях это доли миллисекунды, на сильных окно
/// на время поиска не отвечает — вынос в фон запланирован на полировку (T-030).
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly Random _random;
    private GameState _game;
    private AppSettings _settings;

    /// <summary>Создаёт модель представления по настройкам.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <param name="random">Источник случайности для AI; в тестах — с фиксированным seed.</param>
    public MainViewModel(AppSettings settings, Random random)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);

        _settings = settings;
        _random = random;
        _game = CreateGame(settings);

        ShowAiMoveIfNeeded();
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

    /// <summary>Уровень AI словами.</summary>
    public string Level => $"{_settings.ToDifficultyLevel().RankKyu} кю";

    /// <summary>Цвет игрока словами.</summary>
    public string PlayerColor => _settings.ToPlayerColor() == StoneColor.Black ? "Чёрные" : "Белые";

    /// <summary>Играет ход игрока, затем ход AI, если очередь за ним.</summary>
    /// <param name="point">Точка хода.</param>
    /// <returns><c>true</c>, если ход принят.</returns>
    public bool PlayMove(Point point)
    {
        if (!TryPlay(Move.Play(point, _game.ToMove)))
        {
            return false;
        }

        ShowAiMoveIfNeeded();

        return true;
    }

    /// <summary>Отменяет ход игрока вместе с ответом AI.</summary>
    /// <returns><c>true</c>, если ход отменён.</returns>
    public bool Undo()
    {
        if (!_game.CanUndo)
        {
            return false;
        }

        var playerColor = _settings.ToPlayerColor();

        _ = _game.Undo();

        // Ответ AI отменяется вместе с ходом игрока: после отката снова ход игрока.
        while (_game.CanUndo && _game.ToMove != playerColor)
        {
            _ = _game.Undo();
        }

        NotifyAll();

        return true;
    }

    /// <summary>Возвращает отменённый ход вместе с ответом AI.</summary>
    /// <returns><c>true</c>, если ход возвращён.</returns>
    public bool Redo()
    {
        if (!_game.CanRedo)
        {
            return false;
        }

        var playerColor = _settings.ToPlayerColor();

        _ = _game.Redo();

        while (_game.CanRedo && _game.ToMove != playerColor)
        {
            _ = _game.Redo();
        }

        NotifyAll();

        return true;
    }

    /// <summary>Возвращает партию для сохранения в SGF.</summary>
    /// <returns>Данные партии: размер доски, коми и ходы.</returns>
    public SgfGame ToSgfGame() => new(_game.Board.Size, _game.Komi, _game.Moves, null);

    /// <summary>Загружает партию из SGF и продолжает её по текущим настройкам.</summary>
    /// <param name="game">Прочитанная партия.</param>
    /// <returns>Успех или причина отказа, если ходы не проходят по правилам.</returns>
    public Result LoadGame(SgfGame game)
    {
        var restored = game.ToGameState();

        if (!restored.IsSuccess)
        {
            return Result.Fail(restored.Error!);
        }

        _game = restored.Value!;
        _settings = AppSettings.From(game.Size, _settings.ToDifficultyLevel(), _settings.ToPlayerColor(), game.Komi);

        // После загрузки партия может стоять на ходе AI — он обязан ответить.
        ShowAiMoveIfNeeded();

        return Result.Ok();
    }

    /// <summary>Начинает новую партию по текущим настройкам.</summary>
    public void StartNewGame() => ApplySettings(_settings);

    /// <summary>Применяет настройки и начинает новую партию.</summary>
    /// <param name="settings">Новые настройки партии.</param>
    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        _game = CreateGame(settings);

        ShowAiMoveIfNeeded();
        NotifyAll();
    }

    /// <summary>Создаёт партию по настройкам.</summary>
    /// <param name="settings">Настройки партии.</param>
    /// <returns>Новая партия.</returns>
    private static GameState CreateGame(AppSettings settings) =>
        GameState.NewGame(settings.ToBoardSize(), settings.ToKomi());

    /// <summary>Играет ход AI, пока очередь за ним.</summary>
    private void ShowAiMoveIfNeeded()
    {
        var playerColor = _settings.ToPlayerColor();
        var level = _settings.ToDifficultyLevel();

        while (_game.Status == GameStatus.InProgress && _game.ToMove != playerColor)
        {
            var selector = AiFactory.Create(level, _random);

            if (!TryPlay(selector.SelectMove(_game)))
            {
                // Селектор обязан возвращать легальный ход: если партия его не приняла,
                // играть дальше нельзя — состояние партии остаётся как есть.
                break;
            }
        }

        NotifyAll();
    }

    /// <summary>Играет ход в партии.</summary>
    /// <param name="move">Ход.</param>
    /// <returns><c>true</c>, если правила его приняли.</returns>
    private bool TryPlay(Move move) => _game.Play(move).IsSuccess;

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
            nameof(Status),
            nameof(Level),
            nameof(PlayerColor)
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