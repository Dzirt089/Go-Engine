using System.ComponentModel;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using GoEngine.Problems;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Модель представления разбора партии: выбранная партия, её ход и вопросы.</summary>
/// <remarks>
/// <para>
/// Логика разбора не смешивается с уроками и партией: ходы партии и вопросы живут в
/// <see cref="ReviewSession"/> (проект <c>GoEngine.Problems</c>), а здесь только то, что нужно
/// виду, — тексты разбора, доступность кнопок и выбор партии. Партию ведёт запись: «Далее»
/// играет ход партии, а вопрос лишь даёт игроку найти этот ход самому.
/// </para>
/// <para>
/// Тексты разбора — на русском: обучение ведётся по-русски (замечание пользователя 2026-10-07).
/// </para>
/// </remarks>
public sealed class ReviewViewModel : INotifyPropertyChanged
{
    /// <summary>Что показать, когда партия просмотрена.</summary>
    public const string CompletedText = "Партия просмотрена";

    /// <remarks>
    /// Список ведётся руками, поэтому в нём обязаны быть <b>все</b> свойства, зависящие от хода:
    /// пропущенное имя — это застывшая надпись на экране (та же причина, что в модели уроков).
    /// </remarks>
    private static readonly string[] PropertyNames =
    [
        nameof(Labels), nameof(ListCaption), nameof(SelectedIndex), nameof(GameTitle), nameof(GameSummary), nameof(Players),
        nameof(Result), nameof(NoteTitle), nameof(NoteText), nameof(HasNote), nameof(IsQuiz),
        nameof(MoveCounter), nameof(Message), nameof(HasMessage), nameof(IsMessageGood), nameof(Board),
        nameof(HintPoint), nameof(CanHint), nameof(CanGoNext), nameof(CanGoBack), nameof(IsCompleted),
        nameof(MoveCount), nameof(MoveNumber), nameof(QuizPoints),
        nameof(LastMove), nameof(LastCaptured), nameof(LastCapturedColor)
    ];

    private readonly IReadOnlyList<ReviewGame> _games;
    private readonly StudyProgress _progress;
    private IReadOnlyList<string> _labels;
    private ReviewSession _session;

    private int _selectedIndex;
    private Point? _hint;
    private Point? _lastMove;
    private IReadOnlyList<Point> _lastCaptured = [];
    private StoneColor _lastCapturedColor = StoneColor.Empty;

    /// <summary>Логгер разбора: по нему видно, какая партия шла перед сбоем.</summary>
    private readonly ILogger _log = AppLog.For<ReviewViewModel>();

    /// <summary>Создаёт модель разбора со встроенной библиотекой партий.</summary>
    public ReviewViewModel()
        : this(ReviewLibrary.All, null)
    {
    }

    /// <summary>Создаёт модель разбора с готовым набором партий.</summary>
    /// <param name="games">Партии в порядке разбора.</param>
    /// <param name="progress">Прогресс обучения; <c>null</c> — начать с чистого листа.</param>
    /// <exception cref="DomainException">Набор партий пуст.</exception>
    public ReviewViewModel(IReadOnlyList<ReviewGame> games, StudyProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(games);

        if (games.Count == 0)
        {
            throw new DomainException("Библиотека обучающих партий пуста: разбирать нечего.");
        }

        _games = games;
        _progress = progress ?? StudyProgress.Empty;
        _labels = BuildLabels();

        // Разбор продолжается с того же хода: партия идёт сотни ходов, и начинать её заново
        // каждый раз — потеря времени.
        _selectedIndex = Math.Max(IndexOf(_progress.LastGame), 0);
        _session = new ReviewSession(games[_selectedIndex]);

        var saved = _progress.ForGame(games[_selectedIndex].Id);

        if (saved is { Step: > 0 } && !saved.Completed)
        {
            _ = _session.GoTo(saved.Step);
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Прогресс обучения изменился: оболочка записывает его в файл.</summary>
    public event EventHandler? ProgressChanged;

    /// <summary>Подписи партий для списка выбора: с отметкой разобранного и начатого.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Подпись списка партий: сколько разобрано.</summary>
    public string ListCaption => $"Партия · разобрано {_progress.CompletedGames} из {_games.Count}";

    /// <summary>Текущая партия разобрана до конца.</summary>
    public bool IsGameCompleted => _progress.ForGame(_session.Game.Id)?.Completed ?? false;

    /// <summary>Номер выбранной партии.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Название текущей партии.</summary>
    public string GameTitle => _session.Game.Title;

    /// <summary>Чему учит партия.</summary>
    public string GameSummary => _session.Game.Summary;

    /// <summary>Кто с кем играет: сторона игрока и соперник.</summary>
    public string Players => _session.Game.Players;

    /// <summary>Итог партии словами.</summary>
    public string Result => _session.Game.Result;

    /// <summary>Заголовок разбора текущей позиции.</summary>
    public string NoteTitle => _session.Note?.Title ?? string.Empty;

    /// <summary>Текст разбора текущей позиции.</summary>
    public string NoteText => _session.Note?.Text ?? string.Empty;

    /// <summary>В текущей позиции есть разбор.</summary>
    public bool HasNote => _session.Note is not null;

    /// <summary>В текущей позиции есть вопрос: ход ищет игрок.</summary>
    public bool IsQuiz => _session.Note?.Quiz is not null && !_session.IsCompleted;

    /// <summary>Принимаемые ходы вопроса: нужны проверкам и подсказке вида.</summary>
    public IReadOnlyList<Point> QuizPoints => _session.Note?.Quiz is { } quiz
        ? quiz.Answer.Select(move => move.Point).ToList().AsReadOnly()
        : [];

    /// <summary>Номер хода: «Ход 12 из 87» или «Партия просмотрена».</summary>
    public string MoveCounter => _session.IsCompleted
        ? CompletedText
        : $"Ход {_session.MoveNumber} из {_session.MoveCount}";

    /// <summary>Что сказать игроку об его ходе.</summary>
    public string Message => _session.IsCompleted
        ? "Партия просмотрена целиком. Выберите следующую в списке."
        : _session.Message;

    /// <summary>Есть что сказать игроку.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>Сообщение — похвала, а не отказ: по нему вид выбирает цвет строки.</summary>
    public bool IsMessageGood => _session.Verdict == LessonVerdict.Correct || _session.IsCompleted;

    /// <summary>Доска текущей позиции разбора.</summary>
    public Board Board => _session.Board;

    /// <summary>Подсказка: точка на доске.</summary>
    public Point? HintPoint => _hint;

    /// <summary>Можно перейти к следующему ходу партии.</summary>
    public bool CanGoNext => _session.CanGoNext;

    /// <summary>Можно вернуться на ход назад.</summary>
    public bool CanGoBack => _session.MoveNumber > 0;

    /// <summary>Есть что подсказать.</summary>
    public bool CanHint => _session.CanHint;

    /// <summary>Партия просмотрена до конца.</summary>
    public bool IsCompleted => _session.IsCompleted;

    /// <summary>Сколько ходов в партии.</summary>
    public int MoveCount => _session.MoveCount;

    /// <summary>Сколько ходов партии сыграно.</summary>
    public int MoveNumber => _session.MoveNumber;

    /// <summary>Точка последнего сыгранного хода: по ней доска рисует метку.</summary>
    public Point? LastMove => _lastMove;

    /// <summary>Камни, снятые последним ходом партии.</summary>
    public IReadOnlyList<Point> LastCaptured => _lastCaptured;

    /// <summary>Цвет снятых камней.</summary>
    public StoneColor LastCapturedColor => _lastCapturedColor;

    /// <summary>Принимает ход игрока в точке доски: ответ на вопрос разбора.</summary>
    /// <param name="point">Точка, по которой щёлкнул игрок.</param>
    /// <remarks>
    /// Промах по занятой точке и невозможный ход не считаются ходами — как в партии, задачах
    /// и уроках: ругать игрока за промах нельзя.
    /// </remarks>
    public void Play(Point point)
    {
        if (_session.IsCompleted)
        {
            return;
        }

        // Ход без вопроса принимает сессия: она же объясняет игроку, что здесь искать нечего,
        // — молчаливое «ничего не произошло» путает сильнее, чем короткая подсказка.
        var board = _session.Board;
        var move = Move.Play(point, _session.ToMove);

        if (!board.IsEmpty(point) || !board.IsLegal(move).IsSuccess)
        {
            return;
        }

        _hint = null;

        var number = _session.MoveNumber;
        var verdict = _session.Play(move);

        if (verdict == LessonVerdict.Correct)
        {
            Remember();
            AppLogMessages.ReviewQuizPassed(_log, _session.Game.Id, number + 1);
        }

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Показывает подсказку к вопросу.</summary>
    public void ShowHint()
    {
        if (_session.ShowHint() is { } hint)
        {
            _hint = hint;
            NotifyAll();
        }
    }

    /// <summary>Играет следующий ход партии.</summary>
    public void Next()
    {
        if (!_session.CanGoNext)
        {
            return;
        }

        if (!_session.Next())
        {
            return;
        }

        Remember();

        if (_session.IsCompleted)
        {
            AppLogMessages.ReviewCompleted(_log, _session.Game.Id, _session.MoveCount);
        }

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Возвращает разбор на ход назад.</summary>
    public void Back()
    {
        if (_session.Back())
        {
            ResetMove();
            SaveProgress();
        }

        NotifyAll();
    }

    /// <summary>Начинает разбор партии заново.</summary>
    public void Restart()
    {
        _session.Restart();
        ResetMove();
        SaveProgress();
        NotifyAll();
    }

    /// <summary>Открывает партию по номеру в списке.</summary>
    /// <param name="index">Номер партии.</param>
    public void SelectGame(int index)
    {
        if (index < 0 || index >= _games.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        _session = new ReviewSession(_games[index]);
        ResetMove();
        AppLogMessages.ReviewStarted(_log, _session.Game.Id);

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Запоминает, где игрок остановился, и сообщает об этом оболочке.</summary>
    /// <remarks>
    /// Записывается каждый ход: файл крошечный, а «продолжить разбор» обязано работать даже
    /// после внезапного закрытия приложения.
    /// </remarks>
    private void SaveProgress()
    {
        _progress.SaveGame(_session.Game.Id, _session.MoveNumber, _session.IsCompleted);

        _labels = BuildLabels();
        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Собирает подписи партий с отметками: разобрана, начата или ещё нет.</summary>
    private IReadOnlyList<string> BuildLabels() =>
        _games.Select(game => Mark(game, _progress.ForGame(game.Id))).ToList().AsReadOnly();

    /// <summary>Отмечает партию в списке: «✓» — разобрана, «▸» — начата.</summary>
    private static string Mark(ReviewGame game, StudyItemProgress? saved) => saved switch
    {
        { Completed: true } => $"✓ {game.Label}",
        { Step: > 0 } => $"▸ {game.Label}",
        _ => game.Label
    };

    /// <summary>Номер партии в списке по идентификатору.</summary>
    private int IndexOf(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        for (var index = 0; index < _games.Count; index++)
        {
            if (string.Equals(_games[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>Запоминает последний ход: доска рисует по нему метку и снятые камни.</summary>
    /// <remarks>
    /// Решение принимается по сыгранному ходу, а не по сравнению досок: доска позиции неизменяема,
    /// но сравнивать её с прежней незачем — пас метки не оставляет, а обычный ход оставляет.
    /// </remarks>
    private void Remember()
    {
        if (_session.LastMove.Type != MoveType.Play)
        {
            ResetMove();

            return;
        }

        _lastMove = _session.LastMove.Point;
        _lastCaptured = _session.Board.CapturedStones;
        _lastCapturedColor = _session.LastMove.Color.Opponent();
    }

    /// <summary>Снимает метку последнего хода: позиция сменилась, старая метка к ней не относится.</summary>
    private void ResetMove()
    {
        _hint = null;
        _lastMove = null;
        _lastCaptured = [];
        _lastCapturedColor = StoneColor.Empty;
    }

    /// <summary>Сообщает виду обо всех свойствах позиции.</summary>
    private void NotifyAll()
    {
        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
