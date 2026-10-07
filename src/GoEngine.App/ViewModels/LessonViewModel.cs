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
        nameof(Labels), nameof(SelectedIndex), nameof(LessonTitle), nameof(LessonSummary),
        nameof(StepTitle), nameof(StepText), nameof(StepCounter), nameof(Message), nameof(HasMessage),
        nameof(IsMessageGood), nameof(Board), nameof(HintPoint), nameof(Highlight), nameof(CanHint),
        nameof(CanGoNext), nameof(CanGoBack), nameof(IsCompleted), nameof(IsStepTask),
        nameof(StepCount), nameof(StepNumber), nameof(AcceptedPoints),
        nameof(LastMove), nameof(LastCaptured), nameof(LastCapturedColor)
    ];

    private readonly IReadOnlyList<Lesson> _lessons;
    private readonly IReadOnlyList<string> _labels;
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
        : this(LessonLibrary.All)
    {
    }

    /// <summary>Создаёт модель обучения с готовым набором уроков.</summary>
    /// <param name="lessons">Уроки в порядке прохождения.</param>
    /// <exception cref="DomainException">Набор уроков пуст.</exception>
    public LessonViewModel(IReadOnlyList<Lesson> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        if (lessons.Count == 0)
        {
            throw new DomainException("Библиотека уроков пуста: обучать нечему.");
        }

        _lessons = lessons;
        _labels = lessons.Select(lesson => lesson.Label).ToList().AsReadOnly();
        _session = new LessonSession(lessons[0]);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Подписи уроков для списка выбора.</summary>
    public IReadOnlyList<string> Labels => _labels;

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

        NotifyAll();
    }

    /// <summary>Возвращает предыдущий шаг.</summary>
    public void Back()
    {
        _session.Back();
        ResetAnimation();
        NotifyAll();
    }

    /// <summary>Начинает урок заново.</summary>
    public void Restart()
    {
        _session.Restart();
        ResetAnimation();
        NotifyAll();
    }

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
        ResetAnimation();
        AppLogMessages.LessonStarted(_log, _session.Lesson.Id);

        NotifyAll();
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
