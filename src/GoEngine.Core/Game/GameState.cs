namespace GoEngine.Core;

/// <summary>Партия: доска, очередь хода, коми, счётчик ходов и состояние.</summary>
/// <remarks>
/// Состояние меняется ходами: <see cref="Play"/> проверяет ход, применяет его и обновляет партию.
/// Партия завершается двумя пасами, сдачей или лимитом ходов (<c>GO_RULES.md</c>, п. 7–8).
/// История позиций ведётся с самого начала: начальная позиция тоже входит в партию.
/// </remarks>
public sealed class GameState
{
    /// <summary>Сколько пасов подряд завершают партию.</summary>
    private const int PassesToFinish = 2;

    /// <summary>Причина завершения партии сдачей.</summary>
    private const string ResignReason = "Соперник сдался.";

    private readonly List<Move> _moves = [];
    private readonly Stack<Snapshot> _undo = [];
    private readonly Stack<Snapshot> _redo = [];
    private int _consecutivePasses;
    private GameResult? _result;

    private GameState(Board board, StoneColor toMove, Komi komi, int moveLimit, PositionHistory history)
    {
        Board = board;
        ToMove = toMove;
        Komi = komi;
        MoveLimit = moveLimit;
        History = history;
    }

    /// <summary>Текущая позиция.</summary>
    public Board Board { get; private set; }

    /// <summary>Цвет, который ходит следующим.</summary>
    public StoneColor ToMove { get; private set; }

    /// <summary>Коми партии: прибавляется белым при подсчёте.</summary>
    public Komi Komi { get; }

    /// <summary>Номер последнего сделанного хода; в начале партии — 0.</summary>
    public int MoveNumber { get; private set; }

    /// <summary>Лимит ходов партии; 0 — без ограничения.</summary>
    public int MoveLimit { get; }

    /// <summary>Состояние партии.</summary>
    public GameStatus Status { get; private set; } = GameStatus.InProgress;

    /// <summary>Сделанные ходы в порядке игры.</summary>
    public IReadOnlyList<Move> Moves => _moves.AsReadOnly();

    /// <summary>История позиций партии: нужна позиционному суперко.</summary>
    public PositionHistory History { get; private set; }

    /// <summary>Можно ли отменить последний ход.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Можно ли вернуть отменённый ход.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Начинает новую партию.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="komi">Коми партии.</param>
    /// <param name="moveLimit">Лимит ходов; 0 — взять <see cref="BoardSize.DefaultMoveLimit"/>.</param>
    /// <returns>Партия, в которой ходят чёрные, а история уже содержит начальную позицию.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Лимит ходов отрицательный.</exception>
    public static GameState NewGame(BoardSize size, Komi komi, int moveLimit = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(moveLimit);

        var limit = moveLimit == 0 ? size.DefaultMoveLimit : moveLimit;
        var board = new Board(size);
        var history = new PositionHistory();

        // Начальная позиция — часть партии: иначе первый ход в любую точку выглядел бы повторением.
        history.Add(board);

        return new GameState(board.WithHistory(history), StoneColor.Black, komi, limit, history);
    }

    /// <summary>Делает ход.</summary>
    /// <param name="move">Ход того цвета, который ходит; пас и сдача разрешены всегда.</param>
    /// <returns>Итог хода или причина отказа.</returns>
    /// <remarks>
    /// Нелегальный ход партию не меняет: ни доска, ни номер хода, ни очередь хода не обновляются.
    /// Отказ возвращается значением, а не исключением (<c>AGENTS.md</c>, п. 4).
    /// </remarks>
    public Result<MoveResult> Play(Move move)
    {
        if (Status.IsFinished)
        {
            return Result<MoveResult>.Fail($"Партия завершена ({Status.Name}): ходы больше не принимаются.");
        }

        if (move.IsNone)
        {
            return Result<MoveResult>.Fail("Ход не задан.");
        }

        if (move.Color != ToMove)
        {
            return Result<MoveResult>.Fail($"Сейчас ход {ToMove.Name}, а ход сделан за {move.Color.Name}.");
        }

        var legality = Board.IsLegal(move);

        if (!legality.IsSuccess)
        {
            return Result<MoveResult>.Fail(legality.Error!);
        }

        // Снимок берётся до хода: откат возвращает и позицию, и историю суперко.
        _undo.Push(TakeSnapshot());
        _redo.Clear();

        Board = Board.ApplyMove(move);
        _moves.Add(move);
        MoveNumber++;

        ApplyOutcome(move);

        return Result<MoveResult>.Ok(new MoveResult(true, move, Board.CapturedStones, null));
    }

    /// <summary>Завершает партию и возвращает её итог.</summary>
    /// <returns>Итог партии: способ завершения, счёт и победитель.</returns>
    /// <remarks>
    /// Если партия ещё идёт, текущая позиция считается окончательной и подсчитывается
    /// по китайским правилам (<c>GO_RULES.md</c>, п. 9). Мёртвые камни при этом не снимаются.
    /// </remarks>
    public Result<GameResult> Finish()
    {
        var result = _result ?? FinishByScore(GameStatus.FinishedByTwoPasses);

        return Result<GameResult>.Ok(result);
    }

    /// <summary>Отменяет последний ход.</summary>
    /// <returns>Успех или причина отказа, если отменять нечего.</returns>
    public Result Undo()
    {
        if (_undo.Count == 0)
        {
            return Result.Fail("Отменять нечего: ходов не было.");
        }

        _redo.Push(TakeSnapshot());
        Restore(_undo.Pop());

        return Result.Ok();
    }

    /// <summary>Возвращает последний отменённый ход.</summary>
    /// <returns>Успех или причина отказа, если возвращать нечего.</returns>
    public Result Redo()
    {
        if (_redo.Count == 0)
        {
            return Result.Fail("Возвращать нечего: ход не отменялся.");
        }

        _undo.Push(TakeSnapshot());
        Restore(_redo.Pop());

        return Result.Ok();
    }

    /// <summary>Снимает текущее состояние партии.</summary>
    /// <returns>Снимок для отката.</returns>
    private Snapshot TakeSnapshot() => new(
        Board.WithHistory(History.CreateCopy()),
        ToMove,
        MoveNumber,
        Status,
        _consecutivePasses,
        _result,
        _moves.Count);

    /// <summary>Восстанавливает партию из снимка.</summary>
    /// <param name="snapshot">Снимок состояния.</param>
    private void Restore(Snapshot snapshot)
    {
        Board = snapshot.Board;
        History = snapshot.Board.History!;
        ToMove = snapshot.ToMove;
        MoveNumber = snapshot.MoveNumber;
        Status = snapshot.Status;
        _consecutivePasses = snapshot.ConsecutivePasses;
        _result = snapshot.Result;

        if (_moves.Count > snapshot.MoveCount)
        {
            _moves.RemoveRange(snapshot.MoveCount, _moves.Count - snapshot.MoveCount);
        }
    }

    /// <summary>Состояние партии в момент снимка.</summary>
    /// <param name="Board">Позиция вместе с историей суперко.</param>
    /// <param name="ToMove">Чей был ход.</param>
    /// <param name="MoveNumber">Номер последнего хода.</param>
    /// <param name="Status">Состояние партии.</param>
    /// <param name="ConsecutivePasses">Сколько пасов подряд было сделано.</param>
    /// <param name="Result">Итог партии, если она была завершена.</param>
    /// <param name="MoveCount">Сколько ходов было в списке.</param>
    private readonly record struct Snapshot(
        Board Board,
        StoneColor ToMove,
        int MoveNumber,
        GameStatus Status,
        int ConsecutivePasses,
        GameResult? Result,
        int MoveCount);

    /// <summary>Обновляет партию после успешного хода.</summary>
    /// <param name="move">Сделанный ход.</param>
    private void ApplyOutcome(Move move)
    {
        if (move.Type == MoveType.Resign)
        {
            // Сдавшийся проигрывает: победитель — соперник, счёт не считается.
            ToMove = ToMove.Opponent();
            Status = GameStatus.FinishedByResign;
            _result = new GameResult(Status, null, ToMove, ResignReason);
            return;
        }

        _consecutivePasses = move.Type == MoveType.Pass ? _consecutivePasses + 1 : 0;
        ToMove = ToMove.Opponent();

        if (_consecutivePasses >= PassesToFinish)
        {
            FinishByScore(GameStatus.FinishedByTwoPasses);
            return;
        }

        if (MoveLimit > 0 && MoveNumber >= MoveLimit)
        {
            FinishByScore(GameStatus.FinishedByMoveLimit);
        }
    }

    /// <summary>Завершает партию подсчётом очков.</summary>
    /// <param name="status">Способ завершения: два паса или лимит ходов.</param>
    /// <returns>Итог партии со счётом.</returns>
    private GameResult FinishByScore(GameStatus status)
    {
        Status = status;

        var score = Scorer.Calculate(Board, Komi);
        _result = new GameResult(status, score, score.Winner, null);

        return _result.Value;
    }
}