using System.ComponentModel;
using System.Globalization;
using GoEngine.App.Services;
using GoEngine.App.Services.Logging;
using GoEngine.Core;
using GoEngine.Problems;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Модель представления режима задач: одна задача библиотеки и ход игрока в ней.</summary>
/// <remarks>
/// Логика задач не смешивается с партией: состояние решения живёт в <see cref="ProblemSession"/>
/// (проект <c>GoEngine.Problems</c>), а здесь только то, что нужно виду, — тексты, доступность
/// кнопок и переходы между задачами. Ходы применяет сессия: неверный ход позицию не меняет,
/// ответ соперника приходит автоматически (<c>DECISIONS.md</c>, D-061).
/// </remarks>
public sealed class ProblemViewModel : INotifyPropertyChanged
{
    /// <summary>Ход принят, решение продолжается.</summary>
    public const string CorrectText = "Верно";

    /// <summary>Ход не решает задачу; позиция при этом не меняется.</summary>
    /// <remarks>
    /// Не «неверно»: за заявленным горизонтом задача может не принять более медленный выигрыш,
    /// и объявлять такой ход ошибкой игрока нельзя.
    /// </remarks>
    public const string WrongText = "Это не решает задачу";

    /// <summary>Задача решена.</summary>
    public const string SolvedText = "Задача решена";

    /// <summary>Подсказка о правилах режима: показывается всегда.</summary>
    public const string GuideText =
        "Ход не решает задачу — доска остаётся прежней. Подсказка показывает первый правильный ход.";

    private static readonly string[] PropertyNames =
    [
        nameof(Current), nameof(Board), nameof(LastMove), nameof(Title), nameof(GoalText), nameof(ToMoveText),
        nameof(SizeText), nameof(RankText), nameof(Verdict), nameof(HasVerdict), nameof(IsSolved), nameof(CanBack),
        nameof(CanHint), nameof(CanGoPrevious), nameof(CanGoNext), nameof(StatusText), nameof(SelectedIndex),
        nameof(LastCaptured), nameof(LastCapturedColor)
    ];

    private readonly IReadOnlyList<Problem> _problems;
    private readonly IReadOnlyList<string> _labels;
    private ProblemSession _session;

    private int _index;
    private string _verdict = string.Empty;

    /// <summary>Логгер задач: по нему видно, какие задачи решались перед сбоем.</summary>
    private readonly ILogger _log = AppLog.For<ProblemViewModel>();
    private Point? _lastMove;
    private IReadOnlyList<Point> _lastCaptured = [];
    private StoneColor _lastCapturedColor = StoneColor.Empty;

    /// <summary>Создаёт модель по всей встроенной библиотеке задач.</summary>
    public ProblemViewModel() : this(ProblemLibrary.All)
    {
    }

    /// <summary>Создаёт модель по заданному списку задач.</summary>
    /// <param name="problems">Задачи; список не должен быть пустым.</param>
    /// <exception cref="ArgumentException">Список задач пуст — решать нечего.</exception>
    public ProblemViewModel(IReadOnlyList<Problem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        if (problems.Count == 0)
        {
            throw new ArgumentException("Библиотека задач пуста: решать нечего.", nameof(problems));
        }

        _problems = problems;
        _labels = [.. problems.Select(LabelOf)];
        _session = new ProblemSession(problems[0]);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Все задачи, доступные в режиме задач.</summary>
    public IReadOnlyList<Problem> Problems => _problems;

    /// <summary>Подсказка о правилах режима: показывается в панели всегда.</summary>
    public string Guide => GuideText;

    /// <summary>Текущая задача.</summary>
    public Problem Current => _session.Problem;

    /// <summary>Позиция текущей задачи.</summary>
    public Board Board => _session.Board;

    /// <summary>Последний поставленный камень: и ход игрока, и ответ соперника.</summary>
    /// <remarks>
    /// После неверного хода метка не двигается: позиция не изменилась, и подсвечивать нечего.
    /// Ответ соперника сессия ставит сама, поэтому камень ищется сравнением досок до и после хода.
    /// </remarks>
    public Point? LastMove => _lastMove;

    /// <summary>Камни, снятые последним ходом: игрока или ответа соперника.</summary>
    /// <remarks>
    /// Сессия применяет ответ соперника сразу за ходом игрока, поэтому «последний ход» — это пара.
    /// Анимация показывает важнейшее из пары: если было снятие, показывается снятие, иначе —
    /// появление последнего поставленного камня.
    /// </remarks>
    public IReadOnlyList<Point> LastCaptured => _lastCaptured;

    /// <summary>Цвет снятых камней: такой был у них до снятия.</summary>
    public StoneColor LastCapturedColor => _lastCapturedColor;

    /// <summary>Название задачи.</summary>
    public string Title => Current.Name;

    /// <summary>Цель задачи словами: «убить группу», «обеспечить жизнь группы», «убить группу, не снимая…».</summary>
    public string GoalText => Current.Goal.Descriptions ?? Current.Goal.Name;

    /// <summary>Чей ход в текущей позиции.</summary>
    public string ToMoveText => StoneColorLabels.Label(_session.ToMove);

    /// <summary>Размер доски задачи.</summary>
    public string SizeText => BoardSizes.Label(Current.Size);

    /// <summary>Номинальная сложность задачи в кю.</summary>
    public string RankText => string.Create(CultureInfo.InvariantCulture, $"{Current.Rank} кю");

    /// <summary>Словами о результате последнего хода; пусто, пока ходов не было.</summary>
    public string Verdict => _verdict;

    /// <summary>Есть ли что показать игроку про последний ход.</summary>
    public bool HasVerdict => _verdict.Length > 0;

    /// <summary>Задача решена.</summary>
    public bool IsSolved => _session.State == ProblemState.Solved;

    /// <summary>Есть что отменить.</summary>
    public bool CanBack => _session.CanBack;

    /// <summary>Есть подсказка: в текущей позиции известен принимаемый ход.</summary>
    public bool CanHint => _session.Hint is not null;

    /// <summary>Принимаемые ходы текущей позиции: их считает сессия решения.</summary>
    /// <remarks>Нужны проверкам режима и подсказке вида: своего списка ходов модель не ведёт.</remarks>
    public IReadOnlyList<Move> AcceptedMoves => _session.AcceptedMoves;

    /// <summary>Можно перейти к предыдущей задаче.</summary>
    public bool CanGoPrevious => _index > 0;

    /// <summary>Можно перейти к следующей задаче.</summary>
    public bool CanGoNext => _index < _problems.Count - 1;

    /// <summary>Какая задача по счёту: «Задача 3 из 19».</summary>
    public string StatusText =>
        string.Create(CultureInfo.InvariantCulture, $"Задача {_index + 1} из {_problems.Count}");

    /// <summary>Подписи задач для списка выбора: шифр, цель и сложность.</summary>
    public IReadOnlyList<string> Labels => _labels;

    /// <summary>Выбранная задача в списке; установка переключает задачу.</summary>
    public int SelectedIndex
    {
        get => _index;
        set
        {
            if (value == _index || value < 0 || value >= _problems.Count)
            {
                return;
            }

            Select(value);
        }
    }

    /// <summary>Играет ход игрока в точку доски.</summary>
    /// <param name="point">Точка, по которой щёлкнул игрок.</param>
    /// <remarks>
    /// Промах по занятой точке и самоубийственный ход ходом не считаются: в партии такой щелчок
    /// тоже ничего не делает, и ругать игрока за промах нельзя.
    /// </remarks>
    public void Play(Point point)
    {
        var before = _session.Board;

        if (!before.IsEmpty(point) || !before.IsLegal(Move.Play(point, _session.ToMove)).IsSuccess)
        {
            return;
        }

        var verdict = _session.Play(point);

        if (verdict == ProblemVerdict.Wrong || verdict == ProblemVerdict.Failed)
        {
            // Позиция не изменилась — метку последнего хода не трогаем, доска выглядит как прежде.
            _verdict = verdict == ProblemVerdict.Wrong ? WrongText : verdict.Descriptions ?? verdict.Name;
            AppLogMessages.ProblemMoveRejected(_log, Current.Id, BoardCoordinates.Label(point, Current.Size));
        }
        else
        {
            UpdateAnimation(before, _session.Board, point);
            _verdict = verdict == ProblemVerdict.Solved ? SolvedText : CorrectText;

            if (verdict == ProblemVerdict.Solved)
            {
                AppLogMessages.ProblemSolved(_log, Current.Id, _session.SolverMoveCount);
            }
        }

        NotifyAll();
    }

    /// <summary>Показывает подсказку: первый правильный ход в текущей позиции.</summary>
    public void ShowHint()
    {
        if (_session.Hint is not { } hint)
        {
            return;
        }

        _verdict = string.Create(
            CultureInfo.InvariantCulture,
            $"Подсказка: {BoardCoordinates.Label(hint.Point, Current.Size)}");

        NotifyAll();
    }

    /// <summary>Отменяет последний ход игрока вместе с ответом соперника.</summary>
    public void Back()
    {
        if (!_session.Back())
        {
            return;
        }

        _verdict = string.Empty;
        ClearAnimation();
        NotifyAll();
    }

    /// <summary>Возвращает задачу в начальную позицию.</summary>
    public void Reset()
    {
        _session.Reset();
        _verdict = string.Empty;
        ClearAnimation();
        NotifyAll();
    }

    /// <summary>Переходит к следующей задаче, если она есть.</summary>
    public void Next()
    {
        if (CanGoNext)
        {
            Select(_index + 1);
        }
    }

    /// <summary>Переходит к предыдущей задаче, если она есть.</summary>
    public void Previous()
    {
        if (CanGoPrevious)
        {
            Select(_index - 1);
        }
    }

    /// <summary>Переключает модель на задачу по индексу списка.</summary>
    /// <param name="index">Индекс задачи в списке.</param>
    public void Select(int index)
    {
        if (index < 0 || index >= _problems.Count)
        {
            return;
        }

        _index = index;

        // Новая задача — новая сессия: состояние решения не переносится между задачами.
        _session = new ProblemSession(_problems[index]);
        _verdict = string.Empty;
        ClearAnimation();

        NotifyAll();
    }

    /// <summary>Собирает данные анимации: что появилось и что снято.</summary>
    /// <param name="before">Позиция до хода игрока.</param>
    /// <param name="after">Позиция после хода игрока и ответа соперника.</param>
    /// <param name="played">Точка хода игрока.</param>
    /// <remarks>
    /// Если снятие было у хода игрока — показываем его: игрок должен видеть, что его ход сработал.
    /// Иначе показываем снятие ответа соперника вместе с его камнем, а если снятий не было —
    /// просто последний поставленный камень.
    /// </remarks>
    private void UpdateAnimation(Board before, Board after, Point played)
    {
        var solver = Current.SolverColor;
        var takenBySolver = CapturedBy(before, after, solver.Opponent());

        if (takenBySolver.Count > 0)
        {
            _lastMove = played;
            _lastCaptured = takenBySolver;
            _lastCapturedColor = solver.Opponent();

            return;
        }

        var takenByReply = CapturedBy(before, after, solver);
        var reply = NewStone(before, after);

        _lastMove = reply is { } stone && stone != played ? stone : played;
        _lastCaptured = takenByReply;
        _lastCapturedColor = takenByReply.Count > 0 ? solver : StoneColor.Empty;
    }

    /// <summary>Точки цвета, которые были заняты до хода и опустели после него.</summary>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="after">Позиция после хода.</param>
    /// <param name="color">Цвет камней, которые могли быть сняты.</param>
    /// <returns>Снятые точки в порядке обхода доски.</returns>
    private static IReadOnlyList<Point> CapturedBy(Board before, Board after, StoneColor color)
    {
        List<Point> captured = [];

        foreach (var point in before.AllPoints())
        {
            if (before.At(point) == color && after.IsEmpty(point))
            {
                captured.Add(point);
            }
        }

        return captured.AsReadOnly();
    }

    /// <summary>Сбрасывает данные анимации: доска выглядит как прежде.</summary>
    private void ClearAnimation()
    {
        _lastMove = null;
        _lastCaptured = [];
        _lastCapturedColor = StoneColor.Empty;
    }

    /// <summary>Ищет камень, появившийся на доске после хода.</summary>
    /// <param name="before">Позиция до хода.</param>
    /// <param name="after">Позиция после хода.</param>
    /// <returns>Точка нового камня или <c>null</c>, если новых камней нет.</returns>
    private static Point? NewStone(Board before, Board after)
    {
        foreach (var point in after.AllPoints())
        {
            if (before.At(point) == StoneColor.Empty && after.At(point) != StoneColor.Empty)
            {
                return point;
            }
        }

        return null;
    }

    /// <summary>Подпись задачи для списка: шифр, цель, сложность и пометка слабой задачи.</summary>
    /// <param name="problem">Задача.</param>
    /// <returns>Например, «cg-001 · убить группу · 30 кю».</returns>
    private static string LabelOf(Problem problem)
    {
        var label = string.Create(
            CultureInfo.InvariantCulture,
            $"{problem.Id} · {problem.Goal.Descriptions ?? problem.Goal.Name} · {problem.Rank} кю");

        return problem.IsWeak ? label + " · слабая" : label;
    }

    /// <summary>Сообщает виду, что изменились все свойства задачи.</summary>
    private void NotifyAll()
    {
        foreach (var name in PropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
