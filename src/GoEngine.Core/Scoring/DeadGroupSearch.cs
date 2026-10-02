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
/// Работа идёт по <b>локальной копии позиции</b>: перебор не создаёт ни досок, ни множеств на
/// каждый узел. Ход ставится прямо в копию массива камней, снятые группы запоминаются, а после
/// возврата из ветки всё откатывается (<see cref="Searcher.Place"/> и <see cref="Searcher.Undo"/>).
/// Группы и дамэ считаются обходом по заранее построенной таблице соседей с отметками-поколениями
/// вместо <see cref="HashSet{T}"/>; ходы очередного узла лежат в буфере своей глубины. Раньше
/// каждый примерочный ход строил новую доску и пересобирал группы через
/// <see cref="GroupTracker"/>, из-за чего узел стоил сотни микросекунд, а оценка позиции
/// на плотной доске 19×19 — секунды (D-070). Приговор при этом не меняется: на корпусе из
/// 61 позиции списки предложенных мёртвых совпадают камень в камень до и после ускорения
/// (<c>Предложение_Совпадает_С_Корпусом</c>).
/// </para>
/// <para>
/// Ходы перебора не попадают в историю суперко: он идёт по копии позиции без истории, снятой
/// с доски-источника. Поэтому примерочные ходы не запрещают игроку законный ход.
/// </para>
/// </remarks>
internal static class DeadGroupSearch
{
    /// <summary>Приговор перебора одной группы вместе со стоимостью вопроса.</summary>
    /// <param name="Forced">Захват форсирован в пределах горизонта и предела узлов.</param>
    /// <param name="Nodes">Сколько позиций перебрано: упор в предел означает «не доказано».</param>
    internal readonly record struct CaptureVerdict(bool Forced, int Nodes);

    /// <summary>Перебор одной позиции: локальная копия камней, буферы и таблица соседей.</summary>
    /// <remarks>
    /// <para>
    /// Всё, что нужно узлу, выделено заранее и живёт столько же, сколько позиция: массивы камней,
    /// отметок, стека обхода, ходов по глубинам и снятых камней. Узел не создаёт объектов вовсе —
    /// иначе перебор, который считается на каждый ход партии, платил бы за сборку мусора больше,
    /// чем за сам поиск.
    /// </para>
    /// <para>
    /// Перебор создаётся на позицию, а не на группу: предложение проверяет все группы-кандидаты
    /// подряд, и общая настройка (копия позиции, таблица соседей, буферы) обязана делиться между
    /// ними. На группу остаётся только копирование исходных камней в рабочий массив.
    /// </para>
    /// </remarks>
    internal sealed class Searcher
    {
        /// <summary>Сторона доски и её площадь: индексы считаются как <c>y * side + x</c>.</summary>
        private readonly int _side;
        private readonly int _area;

        /// <summary>Исходная позиция: из неё перед каждой группой восстанавливается рабочая копия.</summary>
        private readonly StoneColor[] _initial;

        /// <summary>Рабочая копия позиции: перебор меняет её и откатывает ходы.</summary>
        private readonly StoneColor[] _stones;

        /// <summary>Индексы камней исходной группы: по ним видно, снята ли группа и где она.</summary>
        private readonly int[] _ownStones;

        /// <summary>Цвет группы (он же защищающийся) и цвет атакующего: задаются перед проверкой.</summary>
        private StoneColor _color = StoneColor.Empty;
        private StoneColor _opponent = StoneColor.Empty;

        /// <summary>Горизонт в полуходах: дальше перебор не идёт.</summary>
        private readonly int _horizon;

        /// <summary>Предел перебранных позиций для текущей группы.</summary>
        private int _maxNodes;

        /// <summary>Соседи каждой точки: <c>_neighbors[_neighborOffset[i].._neighborOffset[i + 1]]</c>.</summary>
        private readonly int[] _neighbors;
        private readonly int[] _neighborOffset;

        /// <summary>Отметки обхода: какие камни уже в текущей группе и какие дамэ уже учтены.</summary>
        /// <remarks>
        /// Поколение (<c>_stamp</c>) растёт на каждый обход, поэтому чистить массивы не нужно:
        /// старая отметка просто не совпадает с текущей.
        /// </remarks>
        private readonly int[] _visitStamp;
        private readonly int[] _libertyStamp;

        /// <summary>Стек обхода группы.</summary>
        private readonly int[] _stack;

        /// <summary>Камни и дамэ проверяемой группы.</summary>
        private readonly int[] _groupStones;
        private readonly int[] _groupLiberties;

        /// <summary>Камни и дамэ соседней (противника или своей) группы: вложенный обход.</summary>
        private readonly int[] _otherStones;
        private readonly int[] _otherLiberties;

        /// <summary>Ходы очередного узла: у каждой глубины свой буфер, иначе рекурсия их затрёт.</summary>
        private readonly int[][] _moves;

        /// <summary>Отметки точек в списке ходов узла и проверенных групп противника.</summary>
        private readonly int[] _moveStamp;
        private readonly int[] _enemyStamp;
        private readonly int[] _captureStamp;

        /// <summary>Снятые камни и указатель стека: откат возвращает их на доску.</summary>
        private readonly int[] _captured;

        /// <summary>Счётчики поколений: обходов, списков ходов, групп противника и снятий.</summary>
        private int _stamp;
        private int _moveGeneration;
        private int _enemyGeneration;
        private int _captureGeneration;

        /// <summary>Сколько камней снято и лежит в <see cref="_captured"/>.</summary>
        private int _capturedCount;

        /// <summary>Версия позиции: растёт на каждом ходе и откате.</summary>
        /// <remarks>
        /// По ней видно, что группа и её дамэ не изменились с прошлого обращения. Узел
        /// защищающегося спрашивает группу дважды — для хода атакующего (пас) и для своих ходов,
        /// — и второй раз она берётся готовой.
        /// </remarks>
        private int _boardVersion;

        /// <summary>Версия позиции, для которой собрана текущая группа.</summary>
        private int _groupVersion = -1;

        /// <summary>Сколько позиций перебрано.</summary>
        private int _nodes;

        /// <summary>Перебор упёрся в предел узлов: доказательства нет, приговор не выносится.</summary>
        private bool _limitReached;

        /// <summary>Создаёт перебор для позиции: копирует камни и строит таблицу соседей.</summary>
        /// <param name="board">Позиция без истории партии.</param>
        /// <param name="horizon">Горизонт в полуходах: по нему выделяются буферы ходов.</param>
        internal Searcher(Board board, int horizon)
        {
            ArgumentNullException.ThrowIfNull(board);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(horizon);

            _side = board.Size.Value;
            _area = board.Size.Area;
            _horizon = horizon;

            _initial = new StoneColor[_area];
            _stones = new StoneColor[_area];

            foreach (var point in board.AllPoints())
            {
                _initial[(point.Y * _side) + point.X] = board.At(point);
            }

            _ownStones = new int[_area];
            _neighbors = new int[_area * 4];
            _neighborOffset = new int[_area + 1];

            var offset = 0;

            for (var index = 0; index < _area; index++)
            {
                _neighborOffset[index] = offset;

                var x = index % _side;
                var y = index / _side;

                if (y > 0)
                {
                    _neighbors[offset++] = index - _side;
                }

                if (y < _side - 1)
                {
                    _neighbors[offset++] = index + _side;
                }

                if (x > 0)
                {
                    _neighbors[offset++] = index - 1;
                }

                if (x < _side - 1)
                {
                    _neighbors[offset++] = index + 1;
                }
            }

            _neighborOffset[_area] = offset;

            _visitStamp = new int[_area];
            _libertyStamp = new int[_area];
            _stack = new int[_area];
            _groupStones = new int[_area];
            _groupLiberties = new int[_area];
            _otherStones = new int[_area];
            _otherLiberties = new int[_area];
            _moveStamp = new int[_area];
            _enemyStamp = new int[_area];
            _captureStamp = new int[_area];
            _captured = new int[_area];
            _moves = new int[horizon + 1][];

            for (var depth = 0; depth <= horizon; depth++)
            {
                _moves[depth] = new int[_area];
            }
        }

        /// <summary>Сколько позиций перебрано: стоимость вопроса для общего бюджета.</summary>
        internal int Nodes => _nodes;

        /// <summary>Проверяет группу и сообщает приговор вместе со стоимостью ответа.</summary>
        /// <param name="group">Проверяемая группа: её камни и дамэ на момент вызова.</param>
        /// <param name="maxNodes">Предел перебранных позиций: упор в предел отменяет приговор.</param>
        /// <returns>Приговор и число перебранных позиций.</returns>
        /// <exception cref="ArgumentException">Группа не содержит камней.</exception>
        /// <remarks>
        /// Стоимость возвращается наружу, а не остаётся внутри: предложение мёртвых для всей доски
        /// ограничено общим бюджетом (<see cref="Endgame.MaxNodesPerPosition"/>), и решать, хватит ли
        /// его на следующую группу, можно только зная, сколько потратила проверенная.
        /// Перед проверкой рабочая копия восстанавливается из исходной позиции: перебор предыдущей
        /// группы не должен оставить в ней следов.
        /// </remarks>
        internal CaptureVerdict Judge(Group group, int maxNodes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodes);

            if (group.Stones is null || group.Stones.Count == 0)
            {
                throw new ArgumentException("Группа без камней: перебору захвата не с чего начинать.", nameof(group));
            }

            Array.Copy(_initial, _stones, _area);

            _color = group.Color;
            _opponent = group.Color.Opponent();
            _maxNodes = maxNodes;
            _nodes = 0;
            _limitReached = false;
            _capturedCount = 0;
            _boardVersion = 0;
            _groupVersion = -1;

            for (var index = 0; index < group.Stones.Count; index++)
            {
                var point = group.Stones[index];

                _ownStones[index] = (point.Y * _side) + point.X;
            }

            _ownCount = group.Stones.Count;

            return new CaptureVerdict(DefenderToMove(_horizon), _nodes);
        }

        /// <summary>Ход защищающегося: группа мертва, только если все его ответы проигрывают.</summary>
        /// <param name="depth">Остаток горизонта в полуходах.</param>
        /// <returns><c>true</c>, если захват форсирован.</returns>
        private bool DefenderToMove(int depth)
        {
            if (Captured())
            {
                return true;
            }

            if (depth <= 0 || Exhausted())
            {
                return false;
            }

            // Пас проверяется первым: он дешёвый и сразу отсекает позиции, где защита и не нужна.
            if (!AttackerToMove(depth - 1))
            {
                return false;
            }

            var moves = _moves[depth];
            var count = DefenseMoves(depth);

            for (var index = 0; index < count; index++)
            {
                var point = moves[index];
                var captureStart = Place(point, _color);

                var result = AttackerToMove(depth - 1);

                Undo(point, _color, captureStart);

                if (!result)
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
        /// <param name="depth">Остаток горизонта в полуходах.</param>
        /// <returns><c>true</c>, если захват форсирован.</returns>
        private bool AttackerToMove(int depth)
        {
            if (Captured())
            {
                return true;
            }

            if (depth <= 0 || Exhausted())
            {
                return false;
            }

            var moves = _moves[depth];
            var count = AttackMoves(depth);

            for (var index = 0; index < count; index++)
            {
                var point = moves[index];
                var captureStart = Place(point, _opponent);

                var result = DefenderToMove(depth - 1);

                Undo(point, _opponent, captureStart);

                if (result)
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
        /// <returns><c>true</c>, если хотя бы одного исходного камня на доске нет.</returns>
        /// <remarks>
        /// Достаточно одного камня: группа, к которой он принадлежал, снимается целиком
        /// (<c>GO_RULES.md</c>, п. 4). Проверка по цвету, а не по пустоте, ловит и подмену:
        /// на месте снятого камня мог оказаться камень другого цвета.
        /// </remarks>
        private bool Captured()
        {
            for (var index = 0; index < _ownCount; index++)
            {
                if (_stones[_ownStones[index]] != _color)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Собирает ходы атакующего: заполнение дамэ группы.</summary>
        /// <param name="depth">Глубина узла: её буфер принимает ходы.</param>
        /// <returns>Число ходов в буфере глубины.</returns>
        /// <remarks>Нелегальный ход (самоубийство) в список не попадает: атакующий его не сделает.</remarks>
        private int AttackMoves(int depth)
        {
            if (!CollectCurrentGroup(out var libertyCount))
            {
                return 0;
            }

            var moves = _moves[depth];
            var count = 0;

            for (var index = 0; index < libertyCount; index++)
            {
                var point = _groupLiberties[index];

                if (IsLegal(point, _opponent))
                {
                    moves[count++] = point;
                }
            }

            return count;
        }

        /// <summary>Собирает ходы защищающегося: продление группы и снятие прижатого атакующего.</summary>
        /// <param name="depth">Глубина узла: её буфер принимает ходы.</param>
        /// <returns>Число ходов в буфере глубины.</returns>
        /// <remarks>
        /// Снятие атакующего камня входит в список отдельно: точка снятия — дамэ чужой группы,
        /// а не обязательно дамэ своей, поэтому одним продлением её не покрыть. Группы противника
        /// обходятся по одной: соседние камни одной группы отмечаются, чтобы не считать её дважды.
        /// </remarks>
        private int DefenseMoves(int depth)
        {
            if (!CollectCurrentGroup(out var libertyCount))
            {
                return 0;
            }

            var moves = _moves[depth];
            var count = 0;
            var generation = ++_moveGeneration;

            for (var index = 0; index < libertyCount; index++)
            {
                count = AddMove(moves, count, _groupLiberties[index], generation);
            }

            var enemyGeneration = ++_enemyGeneration;
            var stoneCount = _groupStoneCount;

            for (var stoneIndex = 0; stoneIndex < stoneCount; stoneIndex++)
            {
                var stone = _groupStones[stoneIndex];

                for (var index = _neighborOffset[stone]; index < _neighborOffset[stone + 1]; index++)
                {
                    var neighbor = _neighbors[index];

                    if (_stones[neighbor] != _opponent || _enemyStamp[neighbor] == enemyGeneration)
                    {
                        continue;
                    }

                    var enemyStones = CollectGroup(neighbor, _otherStones, _otherLiberties, out var enemyLiberties);

                    for (var enemy = 0; enemy < enemyStones; enemy++)
                    {
                        _enemyStamp[_otherStones[enemy]] = enemyGeneration;
                    }

                    for (var liberty = 0; liberty < enemyLiberties; liberty++)
                    {
                        count = AddMove(moves, count, _otherLiberties[liberty], generation);
                    }
                }
            }

            return count;
        }

        /// <summary>Добавляет точку в список ходов узла, если она ещё не в нём и ход легален.</summary>
        /// <param name="moves">Буфер ходов глубины.</param>
        /// <param name="count">Сколько ходов уже в буфере.</param>
        /// <param name="point">Точка-кандидат.</param>
        /// <param name="generation">Поколение списка: по нему видно, что точка уже добавлена.</param>
        /// <returns>Новое число ходов.</returns>
        private int AddMove(int[] moves, int count, int point, int generation)
        {
            if (_moveStamp[point] == generation)
            {
                return count;
            }

            _moveStamp[point] = generation;

            if (IsLegal(point, _color))
            {
                moves[count++] = point;
            }

            return count;
        }

        /// <summary>Собирает текущую группу защищающегося: её камни и дамэ.</summary>
        /// <param name="libertyCount">Число дамэ группы.</param>
        /// <returns><c>false</c>, если группа уже снята: дамэ считать не у чего.</returns>
        /// <remarks>
        /// Группа называется «текущей», потому что защита могла её продлить: камень, поставленный
        /// в дамэ, присоединяется к ней, и дамэ считаются уже у выросшей группы.
        /// </remarks>
        private bool CollectCurrentGroup(out int libertyCount)
        {
            if (_groupVersion == _boardVersion)
            {
                libertyCount = _groupLibertyCount;

                return true;
            }

            for (var index = 0; index < _ownCount; index++)
            {
                if (_stones[_ownStones[index]] == _color)
                {
                    _groupStoneCount = CollectGroup(
                        _ownStones[index],
                        _groupStones,
                        _groupLiberties,
                        out libertyCount);

                    _groupLibertyCount = libertyCount;
                    _groupVersion = _boardVersion;

                    return true;
                }
            }

            // Сюда попасть нельзя: захват проверяется до каждого обращения к группе.
            libertyCount = 0;

            return false;
        }

        /// <summary>Считает камни и дамэ группы, начиная с указанного камня.</summary>
        /// <param name="start">Индекс камня, с которого идёт обход.</param>
        /// <param name="stones">Буфер камней группы.</param>
        /// <param name="liberties">Буфер дамэ группы.</param>
        /// <param name="libertyCount">Число дамэ группы.</param>
        /// <returns>Число камней группы.</returns>
        /// <remarks>
        /// Обход — по стороне, стеком по заранее выделенному массиву. Отметки-поколения заменяют
        /// множества: камень считается посещённым, если его отметка равна текущему поколению.
        /// </remarks>
        private int CollectGroup(int start, int[] stones, int[] liberties, out int libertyCount)
        {
            var stamp = ++_stamp;
            var stackTop = 0;

            _stack[stackTop++] = start;
            _visitStamp[start] = stamp;

            var color = _stones[start];
            var stoneCount = 0;

            libertyCount = 0;

            while (stackTop > 0)
            {
                var current = _stack[--stackTop];

                stones[stoneCount++] = current;

                for (var index = _neighborOffset[current]; index < _neighborOffset[current + 1]; index++)
                {
                    var neighbor = _neighbors[index];
                    var stone = _stones[neighbor];

                    if (stone == StoneColor.Empty)
                    {
                        if (_libertyStamp[neighbor] != stamp)
                        {
                            _libertyStamp[neighbor] = stamp;
                            liberties[libertyCount++] = neighbor;
                        }

                        continue;
                    }

                    if (stone == color && _visitStamp[neighbor] != stamp)
                    {
                        _visitStamp[neighbor] = stamp;
                        _stack[stackTop++] = neighbor;
                    }
                }
            }

            return stoneCount;
        }

        /// <summary>Проверяет, легален ли ход в точку: не самоубийство и точка пуста.</summary>
        /// <param name="point">Индекс точки.</param>
        /// <param name="color">Цвет хода.</param>
        /// <returns><c>true</c>, если ход разрешён.</returns>
        /// <remarks>
        /// Повторяет правила <see cref="Board.IsLegal"/> для позиции без истории: суперко не
        /// проверяется, потому что истории у копии нет. Считаются дамэ своей будущей группы
        /// (пустые соседи точки плюс дамэ соседних своих групп) и снятие соседней группы
        /// противника, у которой эта точка — последнее дамэ (<c>GO_RULES.md</c>, п. 5).
        /// </remarks>
        private bool IsLegal(int point, StoneColor color)
        {
            if (_stones[point] != StoneColor.Empty)
            {
                return false;
            }

            for (var index = _neighborOffset[point]; index < _neighborOffset[point + 1]; index++)
            {
                var neighbor = _neighbors[index];
                var stone = _stones[neighbor];

                // Пустой сосед — уже дамэ будущей группы: ход легален, считать больше нечего.
                if (stone == StoneColor.Empty)
                {
                    return true;
                }

                if (stone == color)
                {
                    // Своя соседняя группа: ход легален, если у неё есть дамэ помимо самой точки.
                    if (HasLibertyOtherThan(neighbor, point))
                    {
                        return true;
                    }

                    continue;
                }

                // Группа противника снимается, если точка хода — её единственное дамэ (GO_RULES.md, п. 5).
                if (!HasLibertyOtherThan(neighbor, point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Проверяет, есть ли у группы дамэ, отличное от указанной точки.</summary>
        /// <param name="start">Индекс камня группы.</param>
        /// <param name="excluded">Точка, которая дамэ не считается: её занимает проверяемый ход.</param>
        /// <returns><c>true</c>, если у группы есть другое дамэ.</returns>
        /// <remarks>
        /// Камни группы при этом не собираются: обход идёт до первого чужого дамэ и выходит.
        /// Так проверка хода не платит за обход всей группы противника — а именно она решает,
        /// попадёт ход в список или нет.
        /// </remarks>
        private bool HasLibertyOtherThan(int start, int excluded)
        {
            var stamp = ++_stamp;
            var stackTop = 0;

            _stack[stackTop++] = start;
            _visitStamp[start] = stamp;

            var color = _stones[start];

            while (stackTop > 0)
            {
                var current = _stack[--stackTop];

                for (var index = _neighborOffset[current]; index < _neighborOffset[current + 1]; index++)
                {
                    var neighbor = _neighbors[index];
                    var stone = _stones[neighbor];

                    if (stone == StoneColor.Empty)
                    {
                        if (neighbor != excluded)
                        {
                            return true;
                        }

                        continue;
                    }

                    if (stone == color && _visitStamp[neighbor] != stamp)
                    {
                        _visitStamp[neighbor] = stamp;
                        _stack[stackTop++] = neighbor;
                    }
                }
            }

            return false;
        }

        /// <summary>Ставит камень в копию позиции и снимает группы противника без дамэ.</summary>
        /// <param name="point">Индекс точки хода.</param>
        /// <param name="color">Цвет хода.</param>
        /// <returns>Указатель стека снятых камней до хода: по нему ход откатывается.</returns>
        /// <remarks>
        /// Порядок из <c>GO_RULES.md</c>, п. 4: сначала ставится камень, затем снимаются группы
        /// противника без дамэ. Перебираются только соседи хода: дамэ может потерять лишь группа,
        /// соседняя с новой точкой. Ход считается легальным заранее (<see cref="IsLegal"/>),
        /// поэтому здесь проверок нет — только изменение позиции.
        /// </remarks>
        private int Place(int point, StoneColor color)
        {
            _stones[point] = color;
            _boardVersion++;

            // Снимается цвет, противоположный ходу, а не «противник группы»: атакующий снимает
            // камни защищающегося, и наоборот.
            var victim = color.Opponent();
            var captureStart = _capturedCount;
            var generation = ++_captureGeneration;

            for (var index = _neighborOffset[point]; index < _neighborOffset[point + 1]; index++)
            {
                var neighbor = _neighbors[index];

                if (_stones[neighbor] != victim || _captureStamp[neighbor] == generation)
                {
                    continue;
                }

                var stoneCount = CollectGroup(neighbor, _otherStones, _otherLiberties, out var libertyCount);

                for (var stone = 0; stone < stoneCount; stone++)
                {
                    _captureStamp[_otherStones[stone]] = generation;
                }

                if (libertyCount != 0)
                {
                    continue;
                }

                for (var stone = 0; stone < stoneCount; stone++)
                {
                    var captured = _otherStones[stone];

                    _stones[captured] = StoneColor.Empty;
                    _captured[_capturedCount++] = captured;
                }
            }

            return captureStart;
        }

        /// <summary>Откатывает ход: возвращает снятые камни и убирает поставленный.</summary>
        /// <param name="point">Индекс точки хода.</param>
        /// <param name="color">Цвет хода.</param>
        /// <param name="captureStart">Указатель стека снятых камней до хода.</param>
        private void Undo(int point, StoneColor color, int captureStart)
        {
            _stones[point] = StoneColor.Empty;
            _boardVersion++;

            for (var index = captureStart; index < _capturedCount; index++)
            {
                _stones[_captured[index]] = color.Opponent();
            }

            _capturedCount = captureStart;
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

        /// <summary>Число камней проверяемой группы.</summary>
        private int _ownCount;

        /// <summary>Число камней текущей группы: заполняется <see cref="CollectCurrentGroup"/>.</summary>
        private int _groupStoneCount;

        /// <summary>Число дамэ текущей группы: вместе с версией образует кэш группы.</summary>
        private int _groupLibertyCount;
    }
}
