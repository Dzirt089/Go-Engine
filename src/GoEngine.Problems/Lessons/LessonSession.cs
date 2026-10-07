namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Прохождение урока: текущий шаг, доска, вердикты и переходы.</summary>
/// <remarks>
/// <para>
/// Состояние шага восстанавливается из его позиции: назад и «заново» не отменяют ходы по одному,
/// а возвращают доску к началу шага. Так урок не может разойтись со своим сценарием: у шага ровно
/// одна начальная позиция, и она собирается заново.
/// </para>
/// <para>
/// Ход принимается только на задании и только из списка принимаемых ходов
/// (<see cref="LessonStep.Answer"/>). Неверный ход позицию не меняет — как в задаче: игрок
/// пробует снова, а не отматывает партию.
/// </para>
/// </remarks>
public sealed class LessonSession
{
    private readonly Lesson _lesson;
    private Board _board;

    /// <summary>Задание текущего шага уже выполнено.</summary>
    private bool _answered;

    /// <summary>Создаёт сессию урока и ставит первый шаг.</summary>
    /// <param name="lesson">Урок.</param>
    /// <exception cref="DomainException">Позиция шага не собирается легальными ходами.</exception>
    public LessonSession(Lesson lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);

        _lesson = lesson;
        _board = Build(lesson.Steps[0].Position);
        ToMove = lesson.Steps[0].Position.ToMove;
        Verdict = LessonVerdict.None;
        Message = string.Empty;
    }

    /// <summary>Урок, который проходят.</summary>
    public Lesson Lesson => _lesson;

    /// <summary>Номер текущего шага с нуля.</summary>
    public int StepIndex { get; private set; }

    /// <summary>Текущий шаг.</summary>
    public LessonStep Step => _lesson.Steps[StepIndex];

    /// <summary>Номер текущего шага для игрока: с единицы.</summary>
    public int StepNumber => StepIndex + 1;

    /// <summary>Число шагов в уроке.</summary>
    public int StepCount => _lesson.Steps.Count;

    /// <summary>Текущий шаг — последний в уроке.</summary>
    public bool IsLastStep => StepIndex == StepCount - 1;

    /// <summary>Урок пройден: шаги кончились.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Доска текущего шага.</summary>
    public Board Board => _board;

    /// <summary>Чей ход в текущей позиции.</summary>
    public StoneColor ToMove { get; private set; }

    /// <summary>Последний вердикт хода.</summary>
    public LessonVerdict Verdict { get; private set; }

    /// <summary>Что сказать игроку: итог хода или пояснение шага.</summary>
    public string Message { get; private set; }

    /// <summary>Показанная подсказка: точка на доске.</summary>
    public Point? HintPoint { get; private set; }

    /// <summary>Точки, подсвеченные на этом шаге.</summary>
    public IReadOnlyList<Point> Highlight => Step.Position.Highlight;

    /// <summary>Задание текущего шага выполнено верным ходом.</summary>
    public bool IsStepAnswered => _answered;

    /// <summary>Можно перейти к следующему шагу.</summary>
    /// <remarks>На задании переход открыт только после верного хода: иначе задание можно пропустить.</remarks>
    public bool CanGoNext => !IsCompleted && (!Step.IsTask || _answered);

    /// <summary>Можно вернуться к предыдущему шагу.</summary>
    public bool CanGoBack => StepIndex > 0;

    /// <summary>Есть что подсказать: задание не выполнено и у него есть подсказка.</summary>
    public bool CanHint => !IsCompleted && Step.IsTask && !_answered && Step.Hint is not null;

    /// <summary>Играет ход игрока.</summary>
    /// <param name="move">Ход.</param>
    /// <returns>Вердикт хода.</returns>
    /// <remarks>
    /// Нелегальный ход (занятая точка, самоубийство, ко) отклоняется с причиной от доски:
    /// урок обязан объяснять правила, а не молча глотать ход.
    /// </remarks>
    public LessonVerdict Play(Move move)
    {
        ArgumentNullException.ThrowIfNull(move);

        if (IsCompleted)
        {
            return LessonVerdict.Completed;
        }

        if (!Step.IsTask)
        {
            Message = "На этом шаге ходить не нужно: прочитайте объяснение и нажмите «Далее».";
            Verdict = LessonVerdict.None;

            return Verdict;
        }

        if (_answered)
        {
            return LessonVerdict.Correct;
        }

        var legal = _board.IsLegal(move);

        if (!Step.Answer.Contains(move))
        {
            // Ход не тот, которого ждёт урок. Если правила запрещают его вовсе, причина важнее
            // формулировки «не подходит»: игрок узнаёт правило, а не угадывает ответ.
            Verdict = LessonVerdict.Wrong;
            Message = legal.IsSuccess ? Step.Failure : legal.Error ?? Step.Failure;

            return Verdict;
        }

        if (!legal.IsSuccess)
        {
            // Принимаемый ход, который запрещён правилами: так устроен шаг «ход в глаз».
            // Урок объясняет запрет, поэтому шаг зачитывается, а доска остаётся прежней.
            _answered = true;
            HintPoint = null;
            Verdict = LessonVerdict.Correct;
            Message = Step.Success;

            return Verdict;
        }

        _board = _board.ApplyMove(move);
        _answered = true;
        HintPoint = null;
        Verdict = LessonVerdict.Correct;
        Message = Step.Success;

        return Verdict;
    }

    /// <summary>Показывает подсказку задания.</summary>
    /// <returns>Точка подсказки или <c>null</c>, если подсказывать нечего.</returns>
    public Point? ShowHint()
    {
        if (!CanHint)
        {
            return null;
        }

        HintPoint = Step.Hint!.Value.Point;

        return HintPoint;
    }

    /// <summary>Переходит к следующему шагу; на последнем шаге урок заканчивается.</summary>
    public void Next()
    {
        if (!CanGoNext)
        {
            return;
        }

        if (IsLastStep)
        {
            IsCompleted = true;

            return;
        }

        GoTo(StepIndex + 1);
    }

    /// <summary>Возвращает предыдущий шаг.</summary>
    public void Back()
    {
        if (!CanGoBack)
        {
            ResetStep();

            return;
        }

        GoTo(StepIndex - 1);
    }

    /// <summary>Начинает урок заново: первый шаг и чистое состояние.</summary>
    public void Restart()
    {
        IsCompleted = false;
        GoTo(0);
    }

    /// <summary>Ставит шаг по номеру и собирает его позицию заново.</summary>
    /// <param name="index">Номер шага с нуля.</param>
    private void GoTo(int index)
    {
        StepIndex = index;
        _board = Build(Step.Position);
        ToMove = Step.Position.ToMove;
        _answered = false;
        HintPoint = null;
        Verdict = LessonVerdict.None;
        Message = string.Empty;
    }

    /// <summary>Возвращает текущий шаг к началу: доска, вердикт и подсказка те же, что при входе.</summary>
    private void ResetStep() => GoTo(StepIndex);

    /// <summary>Собирает позицию шага легальными ходами.</summary>
    /// <param name="position">Позиция шага.</param>
    /// <returns>Доску позиции.</returns>
    /// <exception cref="DomainException">Расстановка не собирается: это ошибка материала урока.</exception>
    private static Board Build(LessonPosition position)
    {
        if (position.Stones.Count == 0)
        {
            return new Board(position.Size);
        }

        var built = ProblemSetup.Build(position.Size, position.Stones);

        if (!built.IsSuccess)
        {
            throw new DomainException($"Позиция урока не собирается: {built.Error}");
        }

        return built.Value!;
    }
}
