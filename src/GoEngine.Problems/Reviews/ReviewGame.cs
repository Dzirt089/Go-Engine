namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Обучающая партия: полная запись игры и разбор по ходам.</summary>
/// <remarks>
/// <para>
/// Партия — это данные (<c>Games/*.json</c>): ходы, заметки разбора и результат. Конструктор
/// проверяет запись целиком: каждый ход обязан быть легальным в своей позиции, заметка — ссылаться
/// на существующий ход, а принимаемый ход вопроса — быть легальным и совпадать с ходом партии.
/// Так битая партия не доходит до игрока: она падает на загрузке библиотеки, а не в середине
/// разбора.
/// </para>
/// <para>
/// Запись ведётся ходами партии, а не расстановкой: разбор показывает течение игры, поэтому
/// доска собирается проигрыванием ходов (<see cref="ReviewSession"/>).
/// </para>
/// </remarks>
public sealed class ReviewGame
{
    /// <summary>Создаёт обучающую партию.</summary>
    /// <param name="id">Идентификатор партии.</param>
    /// <param name="title">Название.</param>
    /// <param name="summary">Короткое описание: чему учит партия.</param>
    /// <param name="source">Источник материала.</param>
    /// <param name="players">Кто с кем играет: сторона игрока и соперник.</param>
    /// <param name="result">Итог партии словами.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="moves">Все ходы партии в порядке игры.</param>
    /// <param name="notes">Разбор по ходам.</param>
    /// <param name="handicap">Камни форы, поставленные до первого хода; пусто — партия без форы.</param>
    /// <exception cref="DomainException">Запись партии неполна или ход в ней невозможен.</exception>
    public ReviewGame(
        string id,
        string title,
        string summary,
        string source,
        string players,
        string result,
        BoardSize size,
        Komi komi,
        IReadOnlyList<Move> moves,
        IReadOnlyList<ReviewNote> notes,
        IReadOnlyList<Point>? handicap = null)
    {
        ArgumentNullException.ThrowIfNull(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(players);
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        ArgumentNullException.ThrowIfNull(moves);
        ArgumentNullException.ThrowIfNull(notes);

        if (moves.Count == 0)
        {
            throw new DomainException($"Партия {id} не содержит ходов.");
        }

        Id = id;
        Title = title;
        Summary = summary;
        Source = source;
        Players = players;
        Result = result;
        Size = size;
        Komi = komi;
        Moves = moves;
        Notes = notes;
        Handicap = handicap ?? [];

        Verify(id, size, komi, moves, notes, Handicap);
    }

    /// <summary>Идентификатор партии.</summary>
    public string Id { get; }

    /// <summary>Название партии.</summary>
    public string Title { get; }

    /// <summary>Чему учит партия.</summary>
    public string Summary { get; }

    /// <summary>Источник материала.</summary>
    public string Source { get; }

    /// <summary>Сторона игрока и соперник.</summary>
    public string Players { get; }

    /// <summary>Итог партии словами.</summary>
    public string Result { get; }

    /// <summary>Размер доски.</summary>
    public BoardSize Size { get; }

    /// <summary>Коми партии.</summary>
    public Komi Komi { get; }

    /// <summary>Все ходы партии в порядке игры.</summary>
    public IReadOnlyList<Move> Moves { get; }

    /// <summary>Разбор по ходам.</summary>
    public IReadOnlyList<ReviewNote> Notes { get; }

    /// <summary>Камни форы: стоят на доске до первого хода партии.</summary>
    /// <remarks>
    /// Фора — это не ходы, а начальная расстановка: в партии с форой первым ходит белый, поэтому
    /// цвет ходов считается от <see cref="FirstColor"/>.
    /// </remarks>
    public IReadOnlyList<Point> Handicap { get; }

    /// <summary>Чей ход первым: с форой (два камня и больше) начинают белые.</summary>
    public StoneColor FirstColor => Handicap.Count >= 2 ? StoneColor.White : StoneColor.Black;

    /// <summary>Подпись партии для списка выбора: идентификатор и название.</summary>
    public string Label => $"{Id} · {Title}";

    /// <summary>Число ходов партии.</summary>
    public int MoveCount => Moves.Count;

    /// <summary>Число вопросов: сколько ходов игрок ищет сам.</summary>
    public int QuizCount => Notes.Count(static note => note.Quiz is not null);

    /// <summary>Собирает позицию перед первым ходом: камни форы или пустая доска.</summary>
    /// <returns>Начальная позиция партии.</returns>
    /// <remarks>
    /// Нужна сессии разбора: партия играется не из пустой доски, а из своей начальной расстановки.
    /// Проверка записи уже убедилась, что расстановка возможна, поэтому отказ здесь — нарушение
    /// инварианта, а не ожидаемый исход.
    /// </remarks>
    public Board StartPosition() => Start(Id, Size, Handicap);

    /// <summary>Заметка разбора к позиции перед ходом с номером <paramref name="index"/>.</summary>
    /// <param name="index">Номер хода (0 — начало партии).</param>
    /// <returns>Заметка или <c>null</c>, если разбора для этой позиции нет.</returns>
    public ReviewNote? NoteBefore(int index)
    {
        foreach (var note in Notes)
        {
            if (note.Before == index)
            {
                return note;
            }
        }

        return null;
    }

    /// <summary>Собирает начальную позицию партии: камни форы или пустая доска.</summary>
    /// <param name="id">Идентификатор партии: нужен для сообщения об ошибке.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="handicap">Камни форы.</param>
    /// <returns>Начальная позиция.</returns>
    /// <exception cref="DomainException">Камни форы поставить нельзя.</exception>
    /// <remarks>
    /// Расстановка собирается <see cref="ProblemSetup"/> — тем же способом, что позиции уроков:
    /// у доски нет публичного способа поставить камень в обход правил.
    /// </remarks>
    private static Board Start(string id, BoardSize size, IReadOnlyList<Point> handicap)
    {
        if (handicap.Count == 0)
        {
            // Пустая доска: ProblemSetup пустую расстановку не принимает (она бессмысленна для задач),
            // а партия без форы начинается именно с неё — как в позициях уроков (LessonSession).
            return new Board(size);
        }

        foreach (var point in handicap)
        {
            if (!point.IsOnBoard(size))
            {
                throw new DomainException($"Партия {id}: камень форы {point} вне доски {size}.");
            }
        }

        var stones = handicap.Select(point => new ProblemStone(point, StoneColor.Black)).ToList().AsReadOnly();
        var built = ProblemSetup.Build(size, stones);

        return built.IsSuccess
            ? built.Value!
            : throw new DomainException($"Партия {id}: камни форы не поставить — {built.Error}");
    }

    /// <summary>Проверяет запись партии: ходы возможны, заметки и вопросы согласованы.</summary>
    private static void Verify(
        string id,
        BoardSize size,
        Komi komi,
        IReadOnlyList<Move> moves,
        IReadOnlyList<ReviewNote> notes,
        IReadOnlyList<Point> handicap)
    {
        var start = Start(id, size, handicap);
        var board = start;
        var positions = new List<Board>(moves.Count + 1) { start };

        for (var index = 0; index < moves.Count; index++)
        {
            var legality = board.IsLegal(moves[index]);

            if (!legality.IsSuccess)
            {
                throw new DomainException($"Партия {id}: ход {index + 1} ({moves[index]}) невозможен — {legality.Error}");
            }

            board = board.ApplyMove(moves[index]);
            positions.Add(board);
        }

        foreach (var note in notes)
        {
            if (note.Before > moves.Count)
            {
                throw new DomainException(
                    $"Партия {id}: заметка «{note.Title}» ссылается на ход {note.Before}, а в партии их {moves.Count}.");
            }

            if (note.Quiz is null)
            {
                continue;
            }

            if (note.Before == moves.Count)
            {
                throw new DomainException(
                    $"Партия {id}: вопрос «{note.Title}» стоит после последнего хода — искать нечего.");
            }

            // Позиция собирается заново до нужного хода: снимки делят одну историю партии
            // (PositionHistory пополняется каждым ходом), а с потерей истории меняется и вердикт
            // правил (проверено: ход, легальный в партии, «становился» самоубийственным).
            // Свежий проигрыш даёт ровно ту позицию и ту историю, что видит игрок.
            var quizBoard = start;

            for (var step = 0; step < note.Before; step++)
            {
                quizBoard = quizBoard.ApplyMove(moves[step]);
            }
            var sideToMove = moves.Count > 0
                ? (note.Before % 2 == 0 ? moves[0].Color : moves[0].Color.Opponent())
                : StoneColor.Black;

            foreach (var move in note.Quiz.Answer)
            {
                if (move.Color != sideToMove)
                {
                    throw new DomainException(
                        $"Партия {id}: вопрос «{note.Title}» принимает ход за {move.Color.Name}, а в позиции ход {sideToMove.Name}.");
                }

                var legal = quizBoard.IsLegal(move);

                if (!legal.IsSuccess)
                {
                    throw new DomainException(
                        $"Партия {id}: принимаемый ход {move.Point} вопроса «{note.Title}» невозможен — {legal.Error}");
                }
            }

            if (!note.Quiz.Answer.Any(move => move.Point == moves[note.Before].Point))
            {
                throw new DomainException(
                    $"Партия {id}: вопрос «{note.Title}» не принимает ход партии {moves[note.Before]}.");
            }
        }
    }
}
