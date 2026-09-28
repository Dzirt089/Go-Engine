namespace GoEngine.Problems;

using System.Globalization;
using System.Text;
using GoEngine.Core;

/// <summary>Разбор и запись файла задачи в SGF с вариантами.</summary>
/// <remarks>
/// <para>
/// Игровой <c>SgfReader</c> в <c>Core</c> варианты не читает, поэтому у слоя задач свой разбор.
/// Формат: свойства <c>SZ</c>, <c>KM</c>, <c>PL</c>, <c>AB</c>, <c>AW</c>, <c>C</c> и аннотации
/// <c>GN</c> (название), <c>GC</c> (условие для игрока), <c>GE</c> (цель: <c>capture</c> или
/// <c>live</c>), <c>GD</c> (сложность в кю), <c>GT</c> (точка целевой группы), <c>GX</c> (заявленный
/// горизонт), <c>GW</c> (объявленный радиус окна поиска). Дерево решения —
/// вложенные варианты: <c>;B[..]</c> и <c>;W[..]</c> с <c>( ... )</c>. Пустое значение
/// (<c>B[]</c> или <c>tt</c>) — пас.
/// </para>
/// <para>
/// Координаты SGF (<c>aa</c> — левый верхний угол) переводятся в <see cref="Point"/>: колонка
/// слева направо, строка снизу вверх, как на доске приложения.
/// </para>
/// </remarks>
public static class ProblemSgf
{
    private const string PassMarker = "tt";

    /// <summary>Имя цели в файле задачи.</summary>
    /// <param name="goal">Цель задачи.</param>
    /// <returns>capture, live или dead.</returns>
    private static string GoalLabel(ProblemGoal goal) => goal == ProblemGoal.Capture
        ? "capture"
        : goal == ProblemGoal.Live ? "live" : "dead";

    /// <summary>Читает задачу из текста SGF.</summary>
    /// <param name="text">Содержимое файла задачи.</param>
    /// <param name="id">Идентификатор задачи; <c>null</c> — взять из названия (GN).</param>
    /// <returns>Задача или причина отказа на русском языке.</returns>
    public static Result<Problem> Parse(string text, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var parsed = ParseTree(text);

        if (!parsed.IsSuccess)
        {
            return Result<Problem>.Fail(parsed.Error!);
        }

        var root = parsed.Value!;

        if (!TrySize(root, out var size, out var sizeError))
        {
            return Result<Problem>.Fail(sizeError);
        }

        var name = Property(root, "GN");

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Problem>.Fail("В задаче нет названия (GN).");
        }

        var identifier = string.IsNullOrWhiteSpace(id) ? name : id;

        var goal = Property(root, "GE") switch
        {
            "capture" => ProblemGoal.Capture,
            "live" => ProblemGoal.Live,
            "dead" => ProblemGoal.Dead,
            var other => null
        };

        if (goal is null)
        {
            return Result<Problem>.Fail("Свойство GE должно быть capture, live или dead.");
        }

        var solver = Property(root, "PL") switch
        {
            "B" => StoneColor.Black,
            "W" => StoneColor.White,
            _ => null
        };

        if (solver is null)
        {
            return Result<Problem>.Fail("Свойство PL должно быть B или W: неизвестно, кто решает задачу.");
        }

        var stones = Stones(root, size);
        var target = ParsePoint(Property(root, "GT"), size);

        if (!target.IsSuccess)
        {
            return Result<Problem>.Fail($"Целевая точка (GT): {target.Error}");
        }

        var rank = int.TryParse(Property(root, "GD"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRank)
            ? parsedRank
            : 30;
        // GX — заявленный горизонт проверки в полуходах.
        var checkDepth = int.TryParse(Property(root, "GX"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDepth)
            ? parsedDepth
            : 0;
        // GW — объявленный радиус окна поиска; без свойства берётся объявленный по умолчанию.
        var windowRadius = int.TryParse(Property(root, "GW"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedWindow)
            ? parsedWindow
            : Problem.DefaultWindowRadius;
        var description = Property(root, "GC");

        if (string.IsNullOrWhiteSpace(description))
        {
            description = Property(root, "C");
        }

        var solution = Solution(root, size);

        if (!solution.IsSuccess)
        {
            return Result<Problem>.Fail(solution.Error!);
        }

        try
        {
            return Result<Problem>.Ok(new Problem(
                identifier,
                name,
                size,
                KomiOf(root, size),
                solver,
                goal,
                stones,
                target.Value,
                rank,
                description ?? string.Empty,
                solution.Value!,
                checkDepth,
                windowRadius));
        }
        catch (DomainException exception)
        {
            return Result<Problem>.Fail(exception.Message);
        }
    }

    /// <summary>Записывает задачу в текст SGF.</summary>
    /// <param name="problem">Задача.</param>
    /// <returns>Содержимое файла задачи.</returns>
    public static string Write(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);

        StringBuilder builder = new();
        builder.Append("(;GM[1]FF[4]CA[UTF-8]");
        builder.Append(CultureInfo.InvariantCulture, $"SZ[{problem.Size.Value}]");
        builder.Append(CultureInfo.InvariantCulture, $"KM[{problem.Komi.Value.ToString(CultureInfo.InvariantCulture)}]");
        builder.Append(CultureInfo.InvariantCulture, $"GN[{problem.Name}]");
        builder.Append(CultureInfo.InvariantCulture, $"GC[{problem.Description}]");
        builder.Append(CultureInfo.InvariantCulture, $"GE[{GoalLabel(problem.Goal)}]");
        builder.Append(CultureInfo.InvariantCulture, $"GD[{problem.Rank}]");
        builder.Append(CultureInfo.InvariantCulture, $"GX[{problem.CheckDepth}]");
        builder.Append(CultureInfo.InvariantCulture, $"GW[{problem.WindowRadius}]");
        builder.Append(CultureInfo.InvariantCulture, $"PL[{(problem.SolverColor == StoneColor.Black ? "B" : "W")}]");
        builder.Append(CultureInfo.InvariantCulture, $"GT[{ToSgf(problem.Target, problem.Size)}]");

        foreach (var colour in new[] { StoneColor.Black, StoneColor.White })
        {
            var label = colour == StoneColor.Black ? "AB" : "AW";

            foreach (var stone in problem.Stones)
            {
                if (stone.Color == colour)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"{label}[{ToSgf(stone.Point, problem.Size)}]");
                }
            }
        }

        WriteMoves(builder, problem.Solution, problem.Size);
        builder.Append(')');

        return builder.ToString();
    }

    private static void WriteMoves(StringBuilder builder, ProblemNode node, BoardSize size)
    {
        // Несколько принимаемых ходов в узле — это ветви, а не последовательность: игрок выбирает
        // одну из них. Без скобок парсер склеивал бы ветви в цепочку ходов (регрессия: задача
        // многоходовая, и «ход за ту же сторону дважды» ломал обход дерева).
        var branches = node.Moves.Count > 1;

        foreach (var accepted in node.Moves)
        {
            var label = accepted.Move.Type == MoveType.Play && accepted.Move.Color == StoneColor.White ? "W" : "B";
            var value = accepted.Move.Type == MoveType.Play ? ToSgf(accepted.Move.Point, size) : string.Empty;

            if (branches)
            {
                builder.Append('(');
            }

            builder.Append(CultureInfo.InvariantCulture, $";{label}[{value}]");

            if (!accepted.Next.IsLeaf)
            {
                WriteMoves(builder, accepted.Next, size);
            }

            if (branches)
            {
                builder.Append(')');
            }
        }
    }

    private static Result<ProblemNode> Solution(SgfNode root, BoardSize size)
    {
        if (root.Children.Count == 0)
        {
            return Result<ProblemNode>.Fail("В задаче нет дерева решения: после расстановки нет ходов.");
        }

        List<ProblemMove> moves = [];

        foreach (var child in root.Children)
        {
            var move = MoveOf(child, size);

            if (!move.IsSuccess)
            {
                return Result<ProblemNode>.Fail(move.Error!);
            }

            var next = child.Children.Count == 0
                ? ProblemNode.Leaf
                : Solution(child, size).Value!;

            moves.Add(new ProblemMove(move.Value, next));
        }

        return Result<ProblemNode>.Ok(new ProblemNode(moves.AsReadOnly()));
    }

    private static Result<Move> MoveOf(SgfNode node, BoardSize size)
    {
        var black = Property(node, "B");
        var white = Property(node, "W");

        if (black is null && white is null)
        {
            return Result<Move>.Fail("Узел дерева решения не содержит хода (B или W).");
        }

        var color = black is not null ? StoneColor.Black : StoneColor.White;
        var value = black ?? white!;

        if (string.IsNullOrEmpty(value) || value == PassMarker)
        {
            return Result<Move>.Ok(Move.Pass(color));
        }

        var point = ParsePoint(value, size);

        return point.IsSuccess ? Result<Move>.Ok(Move.Play(point.Value, color)) : Result<Move>.Fail(point.Error!);
    }

    private static IReadOnlyList<ProblemStone> Stones(SgfNode root, BoardSize size)
    {
        List<ProblemStone> stones = [];

        foreach (var (label, color) in new[] { ("AB", StoneColor.Black), ("AW", StoneColor.White) })
        {
            foreach (var value in Properties(root, label))
            {
                var point = ParsePoint(value, size);

                if (point.IsSuccess)
                {
                    stones.Add(new ProblemStone(point.Value, color));
                }
            }
        }

        return stones.AsReadOnly();
    }

    private static bool TrySize(SgfNode root, out BoardSize size, out string error)
    {
        size = BoardSize.Size9;
        error = string.Empty;

        if (!int.TryParse(Property(root, "SZ"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value is not (9 or 13 or 19))
        {
            error = "Свойство SZ должно быть 9, 13 или 19.";

            return false;
        }

        size = new BoardSize((byte)value);

        return true;
    }

    private static Komi KomiOf(SgfNode root, BoardSize size) =>
        double.TryParse(Property(root, "KM"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? new Komi(value)
            : Komi.For(size);

    /// <summary>Переводит точку доски в координаты SGF.</summary>
    /// <param name="point">Точка доски.</param>
    /// <param name="size">Размер доски.</param>
    /// <returns>Две буквы координат SGF.</returns>
    public static string ToSgf(Point point, BoardSize size) =>
        string.Create(CultureInfo.InvariantCulture, $"{(char)('a' + point.X)}{(char)('a' + size.Value - 1 - point.Y)}");

    private static Result<Point> ParsePoint(string? value, BoardSize size)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 2)
        {
            return Result<Point>.Fail("координата должна состоять из двух букв");
        }

        var x = value[0] - 'a';
        var y = size.Value - 1 - (value[1] - 'a');

        if (x < 0 || y < 0 || x >= size.Value || y >= size.Value)
        {
            return Result<Point>.Fail($"координата {value} вне доски {size}");
        }

        return Result<Point>.Ok(new Point((byte)x, (byte)y));
    }

    private static string? Property(SgfNode node, string name) =>
        node.Properties.TryGetValue(name, out var values) && values.Count > 0 ? values[0] : null;

    private static IReadOnlyList<string> Properties(SgfNode node, string name) =>
        node.Properties.TryGetValue(name, out var values) ? values : [];

    private static Result<SgfNode> ParseTree(string text)
    {
        var index = 0;

        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length || text[index] != '(')
        {
            return Result<SgfNode>.Fail("Файл задачи должен начинаться с «(».");
        }

        var node = ParseNode(text, ref index);

        if (node is null)
        {
            return Result<SgfNode>.Fail("Не удалось разобрать дерево задачи.");
        }

        return Result<SgfNode>.Ok(node);
    }

    private static SgfNode? ParseNode(string text, ref int index)
    {
        Skip(text, ref index);

        if (index >= text.Length || text[index] != '(')
        {
            return null;
        }

        index++;

        var root = ParseSingleNode(text, ref index);

        if (root is null)
        {
            return null;
        }

        var tail = root;

        // Последовательность «;a;b» — цепочка: каждый следующий узел становится единственным ребёнком.
        while (true)
        {
            Skip(text, ref index);

            if (index >= text.Length || text[index] != ';')
            {
                break;
            }

            var next = ParseSingleNode(text, ref index);

            if (next is null)
            {
                break;
            }

            tail.Children.Add(next);
            tail = next;
        }

        while (true)
        {
            Skip(text, ref index);

            if (index >= text.Length)
            {
                break;
            }

            if (text[index] == '(')
            {
                var child = ParseNode(text, ref index);

                if (child is null)
                {
                    break;
                }

                tail.Children.Add(child);

                continue;
            }

            if (text[index] == ')')
            {
                index++;

                break;
            }

            break;
        }

        return root;
    }

    private static SgfNode? ParseSingleNode(string text, ref int index)
    {
        Skip(text, ref index);

        if (index >= text.Length || text[index] != ';')
        {
            return null;
        }

        index++;
        var node = new SgfNode();
        ParseProperties(text, ref index, node);

        return node;
    }

    private static void ParseProperties(string text, ref int index, SgfNode node)
    {
        while (index < text.Length)
        {
            Skip(text, ref index);

            if (index >= text.Length || !char.IsLetter(text[index]))
            {
                return;
            }

            var start = index;

            while (index < text.Length && char.IsLetter(text[index]))
            {
                index++;
            }

            var name = text[start..index].ToUpperInvariant();
            List<string> values = [];

            while (index < text.Length && text[index] == '[')
            {
                index++;
                StringBuilder value = new();

                while (index < text.Length && text[index] != ']')
                {
                    if (text[index] == '\\' && index + 1 < text.Length)
                    {
                        index++;
                    }

                    value.Append(text[index]);
                    index++;
                }

                index++;
                values.Add(value.ToString());
            }

            if (values.Count == 0)
            {
                continue;
            }

            // Свойство может повторяться (AB[..]AB[..]): значения накапливаются, а не перезаписываются.
            if (node.Properties.TryGetValue(name, out var existing))
            {
                existing.AddRange(values);
            }
            else
            {
                node.Properties[name] = values;
            }
        }
    }

    private static void Skip(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    /// <summary>Узел SGF: свойства и дочерние варианты.</summary>
    private sealed class SgfNode
    {
        public Dictionary<string, List<string>> Properties { get; } = [];

        public List<SgfNode> Children { get; } = [];
    }
}
