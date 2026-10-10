namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Проведение обучающей партии: ход за ходом с разбором и вопросами.</summary>
/// <remarks>
/// <para>
/// Сессия идёт по записи партии: игрок листает ходы, читает разбор и в отмеченных местах ищет
/// ход сам. Ход партии всегда играется записью, а не ходом игрока: даже если вопрос принял
/// равноценное продолжение, партия продолжается своим ходом — разбор показывает именно её.
/// </para>
/// <para>
/// Неверный ответ позицию не меняет: игрок пробует снова, как в задании урока. Вердикты те же
/// (<see cref="LessonVerdict"/>): ход не требовался, верный, неверный, партия просмотрена.
/// </para>
/// </remarks>
public sealed class ReviewSession
{
    private Board _board;
    private int _played;
    private int _passes;
    private bool _answered;

    /// <summary>Начинает разбор партии с начала.</summary>
    /// <param name="game">Обучающая партия.</param>
    /// <exception cref="ArgumentNullException">Партия не задана.</exception>
    public ReviewSession(ReviewGame game)
    {
        ArgumentNullException.ThrowIfNull(game);

        Game = game;
        _board = game.StartPosition();
        Message = game.Summary;
    }

    /// <summary>Разбираемая партия.</summary>
    public ReviewGame Game { get; }

    /// <summary>Доска в текущей позиции разбора.</summary>
    public Board Board => _board;

    /// <summary>Сколько ходов партии уже сыграно.</summary>
    public int MoveNumber => _played;

    /// <summary>Сколько ходов в партии.</summary>
    public int MoveCount => Game.MoveCount;

    /// <summary>Последний сыгранный ход партии.</summary>
    public Move LastMove { get; private set; } = Move.None;

    /// <summary>Камни, снятые последним ходом партии.</summary>
    public IReadOnlyList<Point> CapturedStones => _board.CapturedStones;

    /// <summary>Сколько пасов подряд сделано: два паса заканчивают партию.</summary>
    public int ConsecutivePasses => _passes;

    /// <summary>Заметка разбора к текущей позиции.</summary>
    public ReviewNote? Note => Game.NoteBefore(_played);

    /// <summary>Что произошло последним ходом партии, словами.</summary>
    /// <remarks>
    /// Жалоба пользователя 2026-10-10: между заметками экран молчал, и о ходе противника
    /// (или о своём, когда партию ведёт приложение) сказать было нечего. Строка собирается
    /// по правилам — координата, линия, снятия, атари, — и показывается там, где своей заметки нет.
    /// </remarks>
    public string MoveInfo => _played == 0
        ? string.Empty
        : MoveDescriber.Describe(MoveDescriber.FactsOf(_board, Game.Moves[_played - 1], _played), Game.Size);

    /// <summary>Чей ход в текущей позиции партии.</summary>
    /// <remarks>
    /// Цвета в записи чередуются, а первым ходит <see cref="ReviewGame.FirstColor"/>: без форы
    /// это чёрные, с форой — белые (камни форы уже стоят на доске).
    /// </remarks>
    public StoneColor ToMove => _played % 2 == 0 ? Game.FirstColor : Game.FirstColor.Opponent();

    /// <summary>Партия просмотрена до конца.</summary>
    public bool IsCompleted => _played >= Game.MoveCount;

    /// <summary>Ход игрока в позиции: ответ на вопрос разбора.</summary>
    public LessonVerdict Verdict { get; private set; } = LessonVerdict.None;

    /// <summary>Что сказать игроку: разбор, похвала или объяснение ошибки.</summary>
    public string Message { get; private set; }

    /// <summary>Точка подсказки или <c>null</c>, если подсказка не показана.</summary>
    public Point? HintPoint { get; private set; }

    /// <summary>Ответ на вопрос текущей позиции уже найден.</summary>
    public bool IsAnswered => _answered;

    /// <summary>В позиции есть нерешённый вопрос: подсказку показать можно.</summary>
    public bool CanHint => !IsCompleted && !_answered && Note?.Quiz is not null;

    /// <summary>Можно перейти к следующему ходу: вопроса нет или он решён.</summary>
    public bool CanGoNext => !IsCompleted && (Note?.Quiz is null || _answered);

    /// <summary>Показывает подсказку к вопросу.</summary>
    /// <returns>Точка подсказки или <c>null</c>, если подсказывать нечего.</returns>
    public Point? ShowHint()
    {
        if (!CanHint)
        {
            return null;
        }

        HintPoint = Note?.Quiz?.Hint?.Point;

        return HintPoint;
    }

    /// <summary>Принимает ход игрока: ответ на вопрос разбора.</summary>
    /// <param name="move">Ход игрока.</param>
    /// <returns>Вердикт: верный, неверный, не требовался или партия просмотрена.</returns>
    public LessonVerdict Play(Move move)
    {
        ArgumentNullException.ThrowIfNull(move);

        if (IsCompleted)
        {
            return LessonVerdict.Completed;
        }

        var quiz = Note?.Quiz;

        if (quiz is null)
        {
            Message = "Здесь искать ход не нужно: посмотрите разбор и нажмите «Далее».";
            Verdict = LessonVerdict.None;

            return Verdict;
        }

        if (_answered)
        {
            return LessonVerdict.Correct;
        }

        if (!quiz.Answer.Contains(move))
        {
            // Причина отказа правил важнее формулировки «не подходит»: игрок узнаёт правило,
            // а не угадывает ответ (то же решение, что в уроке).
            var legal = Board.IsLegal(move);

            Verdict = LessonVerdict.Wrong;
            Message = legal.IsSuccess ? quiz.Failure : legal.Error ?? quiz.Failure;

            return Verdict;
        }

        HintPoint = null;
        Message = quiz.Success;
        _answered = true;

        // Ход партии играется записью: разбор показывает её продолжение, даже если вопрос принял
        // равноценный ход игрока. Вердикт ставится после хода: Advance сбрасывает его для
        // следующей позиции, а похвалу за верный ответ игрок должен увидеть.
        _ = Advance();

        Verdict = LessonVerdict.Correct;

        return Verdict;
    }

    /// <summary>Играет следующий ход партии.</summary>
    /// <returns>Ход сделан; <c>false</c> — сначала нужно ответить на вопрос или партия кончилась.</returns>
    public bool Next()
    {
        if (IsCompleted)
        {
            return false;
        }

        if (!CanGoNext)
        {
            Message = "Сначала найдите ход в этой позиции: партия продолжается по разбору.";

            return false;
        }

        return Advance();
    }

    /// <summary>Возвращает разбор на ход назад.</summary>
    /// <returns>Разбор сдвинулся; <c>false</c> — это начало партии.</returns>
    public bool Back()
    {
        if (_played == 0)
        {
            return false;
        }

        return GoTo(_played - 1);
    }

    /// <summary>Начинает разбор партии заново.</summary>
    public void Restart() => GoTo(0);

    /// <summary>Переходит к позиции после указанного числа ходов.</summary>
    /// <param name="moveNumber">Сколько ходов партии сыграть (0 — начало).</param>
    /// <returns>Переход выполнен; <c>false</c> — номер вне партии.</returns>
    public bool GoTo(int moveNumber)
    {
        if (moveNumber < 0 || moveNumber > Game.MoveCount)
        {
            return false;
        }

        _board = Game.StartPosition();
        _played = 0;
        _passes = 0;
        _answered = false;
        HintPoint = null;
        Verdict = LessonVerdict.None;
        LastMove = Move.None;
        Message = Game.Summary;

        while (_played < moveNumber)
        {
            if (!Advance())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Играет очередной ход партии и сдвигает разбор.</summary>
    private bool Advance()
    {
        if (_played >= Game.MoveCount)
        {
            return false;
        }

        var move = Game.Moves[_played];
        var legality = _board.IsLegal(move);

        if (!legality.IsSuccess)
        {
            // Запись проверена конструктором партии: сюда попасть нельзя.
            throw new DomainException($"Партия {Game.Id}: ход {_played + 1} ({move}) не принят — {legality.Error}");
        }

        _board = _board.ApplyMove(move);
        _played++;
        _passes = move.Type == MoveType.Pass ? _passes + 1 : 0;
        _answered = false;
        HintPoint = null;
        Verdict = LessonVerdict.None;
        LastMove = move;
        Message = Game.NoteBefore(_played)?.Text ?? Message;

        return true;
    }
}
