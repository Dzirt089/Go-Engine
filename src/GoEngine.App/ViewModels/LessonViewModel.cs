using System.ComponentModel;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using GoEngine.Problems;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Модель представления обучения: выбранный урок, его шаг и вердикты.</summary>
/// <remarks>
/// <para>
/// Логика уроков не смешивается с партией и задачами: прохождение живёт в
/// <see cref="LessonSession"/> (проект <c>GoEngine.Problems</c>), а здесь только то, что нужно
/// виду, — тексты шага, доступность кнопок и выбор урока. Ходы применяет сессия: неверный ход
/// позицию не меняет, а шаг объяснения ходов не принимает вовсе.
/// </para>
/// <para>
/// Тексты уроков — на русском: обучение ведётся по-русски (замечание пользователя 2026-10-07).
/// Оригинальные названия приёмов приводятся в словаре терминов, а не в самих шагах.
/// </para>
/// </remarks>
public sealed class LessonViewModel : INotifyPropertyChanged
{
    /// <summary>Что показать, когда урок пройден.</summary>
    public const string CompletedText = "Урок пройден";

    /// <remarks>
    /// Список ведётся руками, поэтому в нём обязаны быть <b>все</b> свойства, зависящие от шага:
    /// пропущенное имя — это застывшая надпись на экране (та же причина, что в модели задач).
    /// </remarks>
    private static readonly string[] PropertyNames =
    [
        nameof(Labels), nameof(ListCaption), nameof(SelectedIndex), nameof(LessonTitle), nameof(LessonSummary),
        nameof(StepTitle), nameof(StepText), nameof(StepCounter), nameof(Message), nameof(HasMessage),
        nameof(IsMessageGood), nameof(Board), nameof(HintPoint), nameof(Highlight), nameof(CanHint),
        nameof(CanGoNext), nameof(CanGoBack), nameof(IsCompleted), nameof(IsStepTask),
        nameof(CanGoPreviousLesson), nameof(CanGoNextLesson),
        nameof(StepCount), nameof(StepNumber), nameof(AcceptedPoints),
        nameof(LastMove), nameof(LastCaptured), nameof(LastCapturedColor)
    ];

    private readonly IReadOnlyList<Lesson> _lessons;
    private readonly StudyProgress _progress;
    private IReadOnlyList<string> _labels;
    private LessonSession _session;

    private int _selectedIndex;
    private Point? _hint;
    private Point? _lastMove;
    private IReadOnlyList<Point> _lastCaptured = [];
    private StoneColor _lastCapturedColor = StoneColor.Empty;

    /// <summary>Логгер обучения: по нему видно, какой урок шёл перед сбоем.</summary>
    private readonly ILogger _log = AppLog.For<LessonViewModel>();

    /// <summary>Создаёт модель обучения со встроенной библиотекой уроков.</summary>
    public LessonViewModel()
        : this(LessonLibrary.All, null)
    {
    }

    /// <summary>Создаёт модель обучения с готовым набором уроков.</summary>
    /// <param name="lessons">Уроки в порядке прохождения.</param>
    /// <param name="progress">Прогресс обучения; <c>null</c> — начать с чистого листа.</param>
    /// <exception cref="DomainException">Набор уроков пуст.</exception>
    public LessonViewModel(IReadOnlyList<Lesson> lessons, StudyProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        if (lessons.Count == 0)
        {
            throw new DomainException("Библиотека уроков пуста: обучать нечему.");
        }

        _lessons = lessons;
        _progress = progress ?? StudyProgress.Empty;
        _labels = BuildLabels();

        // Курс продолжается с того места, где игрок остановился: обучение идёт по частям,
        // и каждый раз искать урок заново — потеря времени.
        _selectedIndex = Math.Max(IndexOf(_progress.LastLesson), 0);
        _session = new LessonSession(lessons[_selectedIndex]);
        Resume(_progress.ForLesson(lessons[_selectedIndex].Id));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Прогресс обучения изменился: оболочка записывает его в файл.</summary>
    /// <remarks>
    /// Запись — дело вида (как оформление и настройки): модель только сообщает о смене состояния,
    /// поэтому её поведение проверяется без диска.
    /// </remarks>
    public event EventHandler? ProgressChanged;

    /// <summary>Подписи уроков для списка выбора: с отметкой пройденного и начатого.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Подпись списка уроков: сколько пройдено.</summary>
    public string ListCaption => $"Урок · пройдено {_progress.CompletedLessons} из {_lessons.Count}";

    /// <summary>Текущий урок пройден до конца.</summary>
    public bool IsLessonCompleted => _progress.ForLesson(_session.Lesson.Id)?.Completed ?? false;

    /// <summary>Номер выбранного урока.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Название текущего урока.</summary>
    public string LessonTitle => _session.Lesson.Title;

    /// <summary>Короткое описание урока.</summary>
    public string LessonSummary => _session.Lesson.Summary;

    /// <summary>Заголовок текущего шага.</summary>
    public string StepTitle => _session.Step.Title;

    /// <summary>Текст текущего шага.</summary>
    public string StepText => _session.Step.Text;

    /// <summary>Число шагов в уроке.</summary>
    public int StepCount => _session.StepCount;

    /// <summary>Номер текущего шага: с единицы.</summary>
    public int StepNumber => _session.StepNumber;

    /// <summary>Принимаемые ходы текущего шага: любой из них выполняет задание.</summary>
    /// <remarks>
    /// Нужны проверкам и подсказке вида: список ходов живёт в уроке, а модель только показывает его.
    /// </remarks>
    public IReadOnlyList<Point> AcceptedPoints =>
        _session.Step.Answer.Select(move => move.Point).ToList().AsReadOnly();

    /// <summary>Номер шага: «Шаг 2 из 6» или «Урок пройден».</summary>
    public string StepCounter => _session.IsCompleted
        ? CompletedText
        : $"Шаг {_session.StepNumber} из {_session.StepCount}";

    /// <summary>Текущий шаг — задание: игрок ходит по доске.</summary>
    public bool IsStepTask => _session.Step.IsTask && !_session.IsCompleted;

    /// <summary>Что сказать игроку об его ходе.</summary>
    public string Message => _session.IsCompleted
        ? "Урок пройден целиком. Выберите следующий урок в списке."
        : _session.Message;

    /// <summary>Есть что сказать игроку.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>Сообщение — похвала, а не отказ: по нему вид выбирает цвет строки.</summary>
    public bool IsMessageGood => _session.Verdict == LessonVerdict.Correct || _session.IsCompleted;

    /// <summary>Доска текущего шага.</summary>
    public Board Board => _session.Board;

    /// <summary>Подсказка: точка на доске.</summary>
    public Point? HintPoint => _hint;

    /// <summary>Точки, подсвеченные шагом: на них смотрит объяснение.</summary>
    public IReadOnlyList<Point> Highlight => _session.Highlight;

    /// <summary>Можно перейти к следующему шагу.</summary>
    public bool CanGoNext => _session.CanGoNext;

    /// <summary>Можно вернуться к предыдущему шагу.</summary>
    public bool CanGoBack => _session.CanGoBack;

    /// <summary>Есть что подсказать.</summary>
    public bool CanHint => _session.CanHint;

    /// <summary>Урок пройден до конца.</summary>
    public bool IsCompleted => _session.IsCompleted;

    /// <summary>Точка последнего принятого хода: по ней доска рисует метку.</summary>
    public Point? LastMove => _lastMove;

    /// <summary>Камни, снятые последним принятым ходом.</summary>
    public IReadOnlyList<Point> LastCaptured => _lastCaptured;

    /// <summary>Цвет снятых камней.</summary>
    public StoneColor LastCapturedColor => _lastCapturedColor;

    /// <summary>Играет ход игрока в точку доски.</summary>
    /// <param name="point">Точка, по которой щёлкнул игрок.</param>
    /// <remarks>
    /// Промах по занятой точке и самоубийственный ход не считаются ходами — как в партии
    /// и в задачах: ругать игрока за промах нельзя. Исключение — шаг, который показывает запрет
    /// («ход в глаз»): там сессия сама решает, что ход зачтён.
    /// </remarks>
    public void Play(Point point)
    {
        if (_session.IsCompleted || !_session.Step.IsTask)
        {
            return;
        }

        var before = _session.Board;
        var move = Move.Play(point, _session.ToMove);

        if (!before.IsEmpty(point) || (!before.IsLegal(move).IsSuccess && !_session.Step.Answer.Contains(move)))
        {
            return;
        }

        _hint = null;

        var verdict = _session.Play(move);

        if (verdict == LessonVerdict.Correct)
        {
            if (!ReferenceEquals(before, _session.Board))
            {
                _lastMove = point;
                _lastCaptured = _session.Board.CapturedStones;
                _lastCapturedColor = _session.ToMove.Opponent();
            }

            AppLogMessages.LessonStepPassed(_log, _session.Lesson.Id, _session.StepNumber);
        }

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Показывает подсказку шага.</summary>
    public void ShowHint()
    {
        if (_session.ShowHint() is { } hint)
        {
            _hint = hint;
            NotifyAll();
        }
    }

    /// <summary>Переходит к следующему шагу; на последнем шаге урок заканчивается.</summary>
    public void Next()
    {
        if (!_session.CanGoNext)
        {
            return;
        }

        _session.Next();
        ResetAnimation();

        if (_session.IsCompleted)
        {
            AppLogMessages.LessonCompleted(_log, _session.Lesson.Id, _session.StepCount);
        }

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Возвращает предыдущий шаг.</summary>
    public void Back()
    {
        _session.Back();
        ResetAnimation();
        SaveProgress();
        NotifyAll();
    }

    /// <summary>Начинает урок заново.</summary>
    public void Restart()
    {
        _session.Restart();
        ResetAnimation();
        SaveProgress();
        NotifyAll();
    }

    /// <summary>Можно открыть предыдущий урок списка.</summary>
    public bool CanGoPreviousLesson => _selectedIndex > 0;

    /// <summary>Можно открыть следующий урок списка.</summary>
    public bool CanGoNextLesson => _selectedIndex < _lessons.Count - 1;

    /// <summary>Открывает предыдущий урок списка.</summary>
    /// <remarks>
    /// Стрелки рядом со списком уроков (замечание пользователя 2026-10-10: «в задачах ты сделал
    /// &lt; задача… &gt;, в обучении сделай также»). Переключение идёт по тому же пути, что и выбор
    /// из списка: прогресс урока сохраняется, а новый урок открывается с сохранённого шага.
    /// </remarks>
    public void PreviousLesson() => SelectLesson(_selectedIndex - 1);

    /// <summary>Открывает следующий урок списка.</summary>
    public void NextLesson() => SelectLesson(_selectedIndex + 1);

    /// <summary>Показывает урок по номеру в списке.</summary>
    /// <param name="index">Номер урока.</param>
    public void SelectLesson(int index)
    {
        if (index < 0 || index >= _lessons.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        _session = new LessonSession(_lessons[index]);
        Resume(_progress.ForLesson(_session.Lesson.Id));
        ResetAnimation();
        AppLogMessages.LessonStarted(_log, _session.Lesson.Id);

        SaveProgress();
        NotifyAll();
    }

    /// <summary>Запоминает, где игрок остановился, и сообщает об этом оболочке.</summary>
    /// <remarks>
    /// Записывается каждый шаг: файл крошечный, а «продолжить с того же места» обязано работать
    /// даже после внезапного закрытия приложения.
    /// </remarks>
    private void SaveProgress()
    {
        var lesson = _session.Lesson;

        _progress.SaveLesson(lesson.Id, _session.StepNumber, _session.IsCompleted);

        _labels = BuildLabels();
        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Продолжает урок с сохранённого шага.</summary>
    /// <param name="saved">Сохранённый прогресс урока или <c>null</c>.</param>
    /// <remarks>
    /// Прокручиваются только пройденные шаги: сессия не идёт дальше нерешённого задания, поэтому
    /// игрок возвращается к тому заданию, на котором остановился, а не в середину объяснения.
    /// </remarks>
    private void Resume(StudyItemProgress? saved)
    {
        if (saved is null || saved.Completed || saved.Step <= 1)
        {
            return;
        }

        var guard = 0;

        while (_session.StepNumber < saved.Step && _session.CanGoNext && guard++ < _session.StepCount)
        {
            _session.Next();
        }
    }

    /// <summary>Собирает подписи уроков с отметками: пройден, начат или ещё нет.</summary>
    /// <remarks>
    /// В подписи только номер и название (замечание пользователя 2026-10-10: «названия ts-00,
    /// game-00 из списков удали, только номер и название»; второе замечание того же дня: «Партия 1 -
    /// Партия… — это не годится, у нас и так не хватает места, и всем понятно, что это партия»).
    /// Поэтому ни служебного идентификатора, ни слова раздела: место в списке дорого.
    /// </remarks>
    private IReadOnlyList<string> BuildLabels() =>
        _lessons
            .Select((lesson, index) => Mark($"{index + 1}. {lesson.Title}", _progress.ForLesson(lesson.Id)))
            .ToList()
            .AsReadOnly();

    /// <summary>Отмечает урок в списке: «✓» — пройден, «▸» — начат.</summary>
    private static string Mark(string name, StudyItemProgress? saved) => saved switch
    {
        { Completed: true } => $"✓ {name}",
        { Step: > 0 } => $"▸ {name}",
        _ => name
    };

    /// <summary>Номер урока в списке по идентификатору.</summary>
    private int IndexOf(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        for (var index = 0; index < _lessons.Count; index++)
        {
            if (string.Equals(_lessons[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>Снимает метку последнего хода: шаг сменился, старая метка к нему не относится.</summary>
    private void ResetAnimation()
    {
        _hint = null;
        _lastMove = null;
        _lastCaptured = [];
        _lastCapturedColor = StoneColor.Empty;
    }

    /// <summary>Сообщает виду обо всех свойствах шага.</summary>
    private void NotifyAll()
    {
        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
