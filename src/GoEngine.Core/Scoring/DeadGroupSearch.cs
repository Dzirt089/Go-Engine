namespace GoEngine.Core;

/// <summary>Ограниченный перебор форсированного захвата одной группы.</summary>
/// <remarks>
/// <para>
/// Перебор отвечает на один вопрос: снимается ли группа, если защищающийся ходит первым?
/// Это и есть определение мёртвой группы при согласовании конца партии: мёртвая — та, которую
/// не спасает ни один ход защиты. Ответ «мертва» выдаётся только при доказанном захвате;
/// исчерпанный горизонт, упор в предел узлов или отсутствие ходов у атакующего означают
/// «не доказано», то есть группа остаётся живой. Ошибка в эту сторону безопасна: лишний камень
/// на доске игрок увидит и снимет сам, а ошибочно снятый живой камень испортит счёт молча.
/// </para>
/// <para>
/// Кандидаты ходов — только локальные: дамэ группы (их заполняет атакующий и в них же
/// продлевается защищающийся) плюс точки снятия прижатых атакующих групп. Дальний ход группу от
/// форсированного захвата не спасает: ко в этом переборе нет, а без ко угроза вдали ничего
/// не меняет. Окно ограничено самими дамэ, поэтому перебор не зависит от размера доски.
/// </para>
/// <para>
/// Пас защищающегося — полноправный ход. Без него группа с двумя глазами объявлялась бы мёртвой:
/// заполнить глаз противник не может, а защищающийся, вынужденный ходить только в свои глаза,
/// терял бы группу сам.
/// </para>
/// <para>
/// Ходы перебора не попадают в историю суперко: вызывающий обязан передать доску без истории
/// (<see cref="Board.WithoutHistory"/>), иначе примерочные ходы запретили бы игроку ход,
/// который он вправе сделать.
/// </para>
/// </remarks>
internal static class DeadGroupSearch
{
    /// <summary>Проверяет, форсированно ли снимается группа.</summary>
    /// <param name="board">Позиция без истории партии: перебор её не меняет.</param>
    /// <param name="group">Проверяемая группа: её камни и дамэ на момент вызова.</param>
    /// <param name="horizon">Горизонт в полуходах: дальше перебор не идёт.</param>
    /// <param name="maxNodes">Предел перебранных позиций: упор в предел отменяет приговор.</param>
    /// <returns><c>true</c>, если захват форсирован в пределах горизонта и предела узлов.</returns>
    /// <exception cref="ArgumentException">Группа не содержит камней.</exception>
    internal static bool IsForcedCapture(Board board, Group group, int horizon, int maxNodes)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(horizon);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

        if (group.Stones is null || group.Stones.Count == 0)
        {
            throw new ArgumentException("Группа без камней: перебору захвата не с чего начинать.", nameof(group));
        }

        return new Search(group.Color, group.Stones, maxNodes).DefenderToMove(board, horizon);
    }

    /// <summary>Состояние одного перебора: кто защищается, какие камни проверяются и сколько узлов пройдено.</summary>
    private sealed class Search
    {
        /// <summary>Цвет проверяемой группы: он же цвет защищающегося.</summary>
        private readonly StoneColor _color;

        /// <summary>Исходные камни группы: пока хоть один из них на доске, группа не снята.</summary>
        private readonly IReadOnlyList<Point> _stones;

        /// <summary>Предел перебранных позиций на одну группу.</summary>
        private readonly int _maxNodes;

        /// <summary>Сколько позиций уже перебрано.</summary>
        private int _nodes;

        /// <summary>Перебор упёрся в предел узлов: доказательства нет, приговор не выносится.</summary>
        private bool _limitReached;

        /// <summary>Создаёт перебор для группы.</summary>
        /// <param name="color">Цвет группы.</param>
        /// <param name="stones">Камни группы на момент вызова.</param>
        /// <param name="maxNodes">Предел перебранных позиций.</param>
        internal Search(StoneColor color, IReadOnlyList<Point> stones, int maxNodes)
        {
            _color = color;
            _stones = stones;
            _maxNodes = maxNodes;
        }

        /// <summary>Ход защищающегося: группа мертва, только если все его ответы проигрывают.</summary>
        /// <param name="board">Позиция.</param>
        /// <param name="depth">Остаток горизонта в полуходах.</param>
        /// <returns><c>true</c>, если захват форсирован.</returns>
        internal bool DefenderToMove(Board board, int depth)
        {
            if (Captured(board))
            {
                return true;
            }

            if (depth <= 0 || Exhausted())
            {
                return false;
            }

            // Пас проверяется первым: он дешёвый и сразу отсекает позиции, где защита и не нужна.
            if (!AttackerToMove(board, depth - 1))
            {
                return false;
            }

            foreach (var move in DefenseMoves(board))
            {
                if (!AttackerToMove(board.ApplyMove(move), depth - 1))
                {
                    return false;
                }

                if (_limitReached)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Ход атакующего: группа мертва, если хоть один его ход ведёт к захвату.</summary>
        /// <param name="board">Позиция.</param>
        /// <param name="depth">Остаток горизонта в полуходах.</param>
        /// <returns><c>true</c>, если захват форсирован.</returns>
        private bool AttackerToMove(Board board, int depth)
        {
            if (Captured(board))
            {
                return true;
            }

            if (depth <= 0 || Exhausted())
            {
                return false;
            }

            foreach (var move in AttackMoves(board))
            {
                if (DefenderToMove(board.ApplyMove(move), depth - 1))
                {
                    return true;
                }

                if (_limitReached)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Проверяет, снята ли группа с доски.</summary>
        /// <param name="board">Позиция.</param>
        /// <returns><c>true</c>, если хотя бы одного исходного камня на доске нет.</returns>
        /// <remarks>
        /// Достаточно одного камня: группа, к которой он принадлежал, снимается целиком
        /// (<c>GO_RULES.md</c>, п. 4). Проверка по цвету, а не по пустоте, ловит и подмену:
        /// на месте снятого камня мог оказаться камень другого цвета.
        /// </remarks>
        private bool Captured(Board board)
        {
            foreach (var stone in _stones)
            {
                if (board.At(stone) != _color)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Собирает ходы атакующего: заполнение дамэ группы.</summary>
        /// <param name="board">Позиция.</param>
        /// <returns>Легальные ходы, уменьшающие дамэ группы.</returns>
        /// <remarks>Нелегальный ход (самоубийство) в список не попадает: атакующий его не сделает.</remarks>
        private List<Move> AttackMoves(Board board)
        {
            List<Move> moves = [];

            foreach (var liberty in CurrentGroup(board).Liberties)
            {
                var move = Move.Play(liberty, _color.Opponent());

                if (board.IsLegal(move).IsSuccess)
                {
                    moves.Add(move);
                }
            }

            return moves;
        }

        /// <summary>Собирает ходы защищающегося: продление группы и снятие прижатого атакующего.</summary>
        /// <param name="board">Позиция.</param>
        /// <returns>Легальные ходы защиты.</returns>
        /// <remarks>
        /// Снятие атакующего камня входит в список отдельно: точка снятия — дамэ чужой группы,
        /// а не обязательно дамэ своей, поэтому одним продлением её не покрыть.
        /// </remarks>
        private List<Move> DefenseMoves(Board board)
        {
            var group = CurrentGroup(board);
            HashSet<Point> points = [.. group.Liberties];

            foreach (var stone in group.Stones)
            {
                foreach (var neighbor in stone.Neighbors(board.Size))
                {
                    if (board.At(neighbor) == _color.Opponent())
                    {
                        points.UnionWith(GroupTracker.FindGroup(board, neighbor).Liberties);
                    }
                }
            }

            List<Move> moves = [];

            foreach (var point in points)
            {
                var move = Move.Play(point, _color);

                if (board.IsLegal(move).IsSuccess)
                {
                    moves.Add(move);
                }
            }

            return moves;
        }

        /// <summary>Находит текущую группу: связную область, в которой остались исходные камни.</summary>
        /// <param name="board">Позиция; хотя бы один исходный камень на ней есть.</param>
        /// <returns>Камни группы и её дамэ.</returns>
        /// <remarks>
        /// Группа называется «текущей», потому что защита могла её продлить: камень, поставленный
        /// в дамэ, присоединяется к ней, и дамэ считаются уже у выросшей группы.
        /// </remarks>
        private Group CurrentGroup(Board board)
        {
            foreach (var stone in _stones)
            {
                if (board.At(stone) == _color)
                {
                    return GroupTracker.FindGroup(board, stone);
                }
            }

            // Сюда попасть нельзя: захват проверяется до каждого обращения к группе.
            throw new DomainException("Группа уже снята с доски: дамэ считают только у камней на доске.");
        }

        /// <summary>Считает пройденный узел перебора и проверяет предел.</summary>
        /// <returns><c>true</c>, если предел узлов достигнут.</returns>
        private bool Exhausted()
        {
            if (!_limitReached && ++_nodes > _maxNodes)
            {
                _limitReached = true;
            }

            return _limitReached;
        }
    }
}
