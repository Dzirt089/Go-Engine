namespace GoEngine.Problems;

using GoEngine.Core;

/// <summary>Сборка начальной позиции задачи легальными ходами.</summary>
/// <remarks>
/// У доски нет публичного способа поставить камень в обход правил, поэтому расстановка собирается
/// последовательностью <see cref="Board.ApplyMove"/>: каждый камень ставится как ход и обязан быть
/// легальным в тот момент. Порядок подбирается детерминированным поиском с возвратом: сначала
/// пробуются камни в порядке схемы. Ход, который снимает чужие камни, пропускается — иначе
/// собранная позиция перестанет совпадать со схемой. Если легального порядка нет, задача
/// невалидна, и это должен показать <see cref="ProblemChecker"/>, а не «подгонка» позиции.
/// </remarks>
public static class ProblemSetup
{
    /// <summary>Предел числа проб при поиске порядка расстановки.</summary>
    /// <remarks>
    /// Позиции задач малы, поэтому предел нужен только как страховка от комбинаторного взрыва:
    /// при его достижении сборка честно сообщает об отказе.
    /// </remarks>
    public const int DefaultAttemptLimit = 200_000;

    /// <summary>Собирает позицию задачи из расстановки.</summary>
    /// <param name="size">Размер доски.</param>
    /// <param name="stones">Камни расстановки.</param>
    /// <param name="attemptLimit">Предел числа проб.</param>
    /// <returns>Доска без истории партии или причина отказа на русском языке.</returns>
    /// <remarks>Истории у доски нет: позиционное суперко в задачах не применяется.</remarks>
    public static Result<Board> Build(BoardSize size, IReadOnlyList<ProblemStone> stones, int attemptLimit = DefaultAttemptLimit)
    {
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(stones);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptLimit);

        if (stones.Count == 0)
        {
            return Result<Board>.Fail("Расстановка задачи пуста.");
        }

        foreach (var stone in stones)
        {
            if (!stone.Point.IsOnBoard(size))
            {
                return Result<Board>.Fail($"Камень {stone.Point} находится вне доски {size}.");
            }

            if (stone.Color == StoneColor.Empty)
            {
                return Result<Board>.Fail($"В расстановке есть пустая точка {stone.Point}: цвет камня обязателен.");
            }
        }

        for (var i = 0; i < stones.Count; i++)
        {
            for (var j = i + 1; j < stones.Count; j++)
            {
                if (stones[i].Point == stones[j].Point)
                {
                    return Result<Board>.Fail($"Точка {stones[i].Point} встречается в расстановке дважды.");
                }
            }
        }

        var placed = new bool[stones.Count];
        var attempts = 0;

        if (!Place(new Board(size), stones.Count, out var assembled) || assembled is null)
        {
            return Result<Board>.Fail(
                $"Не удалось расставить камни легальными ходами за {attemptLimit} проб: у части камней нет порядка, при котором ход легален.");
        }

        var schema = Verify(assembled, stones);

        return schema.IsSuccess
            ? Result<Board>.Ok(assembled)
            : Result<Board>.Fail($"Собранная позиция не совпала со схемой: {schema.Error}");

        bool Place(Board board, int left, out Board? result)
        {
            if (left == 0)
            {
                result = board;

                return true;
            }

            result = null;

            for (var index = 0; index < stones.Count; index++)
            {
                if (placed[index])
                {
                    continue;
                }

                if (++attempts > attemptLimit)
                {
                    return false;
                }

                var stone = stones[index];

                if (board.At(stone.Point) != StoneColor.Empty)
                {
                    continue;
                }

                var move = Move.Play(stone.Point, stone.Color);

                if (!board.IsLegal(move).IsSuccess)
                {
                    continue;
                }

                var after = board.ApplyMove(move);

                // Снятие камней при расстановке запрещено: позиция должна совпасть со схемой.
                if (after.CapturedStones.Count > 0)
                {
                    continue;
                }

                placed[index] = true;

                if (Place(after, left - 1, out result))
                {
                    return true;
                }

                placed[index] = false;
            }

            result = null;

            return false;
        }
    }

    /// <summary>Проверяет, что доска совпадает с расстановкой.</summary>
    /// <param name="board">Собранная доска.</param>
    /// <param name="stones">Ожидаемые камни.</param>
    /// <returns>Успех, если на доске ровно эти камни и ничего лишнего.</returns>
    public static Result<Unit> Verify(Board board, IReadOnlyList<ProblemStone> stones)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(stones);

        foreach (var point in board.AllPoints())
        {
            var expected = StoneColor.Empty;

            foreach (var stone in stones)
            {
                if (stone.Point == point)
                {
                    expected = stone.Color;
                    break;
                }
            }

            if (board.At(point) != expected)
            {
                return Result<Unit>.Fail($"Точка {point}: на доске {board.At(point).Name}, в расстановке {expected.Name}.");
            }
        }

        return Result<Unit>.Ok(Unit.Value);
    }
}
