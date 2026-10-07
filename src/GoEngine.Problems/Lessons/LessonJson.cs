namespace GoEngine.Problems;

using System.Globalization;
using System.Text.Json;
using GoEngine.Core;

/// <summary>Разбор урока из JSON.</summary>
/// <remarks>
/// <para>
/// Урок — данные, а не код: тексты, позиции и принимаемые ходы лежат в файле
/// (<c>Lessons/*.json</c>), а движок только исполняет сценарий. Так материал можно править
/// и дополнять, не трогая движок, и он же проверяется тестами как данные.
/// </para>
/// <para>
/// Точки записываются как в SGF: две буквы, первая — колонка слева направо, вторая — строка
/// <b>сверху вниз</b> (<c>aa</c> — левый верхний угол). Внутри приложения строка считается снизу
/// вверх, поэтому при разборе строка переворачивается. Свойства шага: <c>kind</c> (<c>explain</c>
/// или <c>task</c>), <c>title</c>, <c>text</c>, <c>position</c>, <c>answer</c> (принимаемые ходы),
/// <c>hint</c>, <c>success</c>, <c>failure</c>. Позиция: <c>toMove</c> (<c>B</c> или <c>W</c>),
/// <c>black</c>, <c>white</c> и <c>highlight</c> (что подсветить на доске).
/// </para>
/// </remarks>
public static class LessonJson
{
    /// <summary>Что сказать игроку после верного хода, если автор не задал свой текст.</summary>
    public const string DefaultSuccess = "Верно!";

    /// <summary>Что сказать игроку после неверного хода, если автор не задал свой текст.</summary>
    public const string DefaultFailure = "Пока не подходит: попробуйте другой ход.";

    /// <summary>Читает урок из текста JSON.</summary>
    /// <param name="text">Содержимое файла урока.</param>
    /// <param name="id">Идентификатор урока; <c>null</c> — взять из файла (<c>id</c>).</param>
    /// <returns>Урок или причина отказа на русском языке.</returns>
    /// <exception cref="ArgumentException">Текст пуст.</exception>
    public static Result<Lesson> Parse(string text, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Result<Lesson>.Fail($"Урок не разбирается как JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<Lesson>.Fail("Урок должен быть объектом JSON.");
            }

            var lessonId = string.IsNullOrWhiteSpace(id) ? Text(root, "id") : id;
            var title = Text(root, "title");
            var summary = Text(root, "summary");
            var size = Size(root);

            if (string.IsNullOrWhiteSpace(lessonId))
            {
                return Result<Lesson>.Fail("У урока нет идентификатора (id).");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Result<Lesson>.Fail($"Урок {lessonId}: нет названия (title).");
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return Result<Lesson>.Fail($"Урок {lessonId}: нет описания (summary).");
            }

            if (size is null)
            {
                return Result<Lesson>.Fail($"Урок {lessonId}: размер доски (size) должен быть 9, 13 или 19.");
            }

            if (!root.TryGetProperty("steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array
                || steps.GetArrayLength() == 0)
            {
                return Result<Lesson>.Fail($"Урок {lessonId}: нет шагов (steps).");
            }

            List<LessonStep> parsed = [];
            var number = 0;

            foreach (var element in steps.EnumerateArray())
            {
                number++;
                // BoardSize — значимый тип, поэтому Nullable разворачивается через Value: size!
                // для Nullable<BoardSize> ничего не значит (проверено сборкой).
                var step = Step(element, size.Value, lessonId!, number);

                if (!step.IsSuccess)
                {
                    return Result<Lesson>.Fail(step.Error!);
                }

                parsed.Add(step.Value!);
            }

            try
            {
                return Result<Lesson>.Ok(new Lesson(
                    lessonId!,
                    title!,
                    summary!,
                    Text(root, "source") ?? string.Empty,
                    parsed.AsReadOnly()));
            }
            catch (DomainException exception)
            {
                return Result<Lesson>.Fail(exception.Message);
            }
        }
    }

    /// <summary>Разбирает шаг урока.</summary>
    private static Result<LessonStep> Step(JsonElement element, BoardSize size, string lessonId, int number)
    {
        var where = $"Урок {lessonId}, шаг {number}";

        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result<LessonStep>.Fail($"{where}: шаг должен быть объектом JSON.");
        }

        var kind = Text(element, "kind") switch
        {
            "explain" => LessonStepKind.Explain,
            "task" => LessonStepKind.Task,
            _ => null
        };

        if (kind is null)
        {
            return Result<LessonStep>.Fail($"{where}: свойство kind должно быть explain или task.");
        }

        var title = Text(element, "title");
        var text = Text(element, "text");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
        {
            return Result<LessonStep>.Fail($"{where}: нужны заголовок (title) и текст (text).");
        }

        if (!element.TryGetProperty("position", out var positionElement))
        {
            return Result<LessonStep>.Fail($"{where}: нет позиции (position).");
        }

        var position = Position(positionElement, size, where);

        if (!position.IsSuccess)
        {
            return Result<LessonStep>.Fail(position.Error!);
        }

        var answer = Points(element, "answer", size, where);

        if (!answer.IsSuccess)
        {
            return Result<LessonStep>.Fail(answer.Error!);
        }

        var highlight = Points(element, "highlight", size, where);

        if (!highlight.IsSuccess)
        {
            return Result<LessonStep>.Fail(highlight.Error!);
        }

        var moves = answer.Value!.Select(point => Move.Play(point, position.Value!.ToMove)).ToList().AsReadOnly();
        Move? hint = null;

        if (Text(element, "hint") is { Length: > 0 } hintToken)
        {
            var hintPoint = PointOf(hintToken, size, where);

            if (!hintPoint.IsSuccess)
            {
                return Result<LessonStep>.Fail(hintPoint.Error!);
            }

            hint = Move.Play(hintPoint.Value, position.Value!.ToMove);
        }

        try
        {
            return Result<LessonStep>.Ok(new LessonStep(
                kind,
                title,
                text,
                position.Value!,
                moves,
                hint,
                Text(element, "success") ?? DefaultSuccess,
                Text(element, "failure") ?? DefaultFailure));
        }
        catch (DomainException exception)
        {
            return Result<LessonStep>.Fail(exception.Message);
        }
    }

    /// <summary>Разбирает позицию шага.</summary>
    private static Result<LessonPosition> Position(JsonElement element, BoardSize size, string where)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result<LessonPosition>.Fail($"{where}: позиция должна быть объектом JSON.");
        }

        var toMove = Text(element, "toMove") switch
        {
            "B" => StoneColor.Black,
            "W" => StoneColor.White,
            _ => null
        };

        if (toMove is null)
        {
            return Result<LessonPosition>.Fail($"{where}: свойство toMove должно быть B или W.");
        }

        var black = Points(element, "black", size, where);

        if (!black.IsSuccess)
        {
            return Result<LessonPosition>.Fail(black.Error!);
        }

        var white = Points(element, "white", size, where);

        if (!white.IsSuccess)
        {
            return Result<LessonPosition>.Fail(white.Error!);
        }

        var highlight = Points(element, "highlight", size, where);

        if (!highlight.IsSuccess)
        {
            return Result<LessonPosition>.Fail(highlight.Error!);
        }

        List<ProblemStone> stones = [];

        stones.AddRange(black.Value!.Select(point => new ProblemStone(point, StoneColor.Black)));
        stones.AddRange(white.Value!.Select(point => new ProblemStone(point, StoneColor.White)));

        try
        {
            return Result<LessonPosition>.Ok(new LessonPosition(size, stones.AsReadOnly(), toMove, highlight.Value!));
        }
        catch (DomainException exception)
        {
            return Result<LessonPosition>.Fail($"{where}: {exception.Message}");
        }
    }

    /// <summary>Читает список точек из свойства шага.</summary>
    /// <param name="element">Объект шага или позиции.</param>
    /// <param name="name">Имя свойства.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="where">Место в файле — для сообщения об отказе.</param>
    /// <returns>Точки или причина отказа; отсутствие свойства — пустой список.</returns>
    private static Result<IReadOnlyList<Point>> Points(JsonElement element, string name, BoardSize size, string where)
    {
        if (!element.TryGetProperty(name, out var array))
        {
            return Result<IReadOnlyList<Point>>.Ok(Array.Empty<Point>());
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            return Result<IReadOnlyList<Point>>.Fail($"{where}: свойство {name} должно быть списком точек.");
        }

        List<Point> points = [];

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return Result<IReadOnlyList<Point>>.Fail($"{where}: в списке {name} есть не строка.");
            }

            var point = PointOf(item.GetString()!, size, where);

            if (!point.IsSuccess)
            {
                return Result<IReadOnlyList<Point>>.Fail(point.Error!);
            }

            points.Add(point.Value);
        }

        return Result<IReadOnlyList<Point>>.Ok(points.AsReadOnly());
    }

    /// <summary>Переводит запись точки SGF в точку доски.</summary>
    /// <param name="token">Две буквы, например <c>dd</c>.</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="where">Место в файле — для сообщения об отказе.</param>
    /// <returns>Точка или причина отказа.</returns>
    /// <remarks>Строка переворачивается: в SGF <c>a</c> — верхняя строка, у доски — нижняя.</remarks>
    private static Result<Point> PointOf(string token, BoardSize size, string where)
    {
        var value = token.Trim().ToLowerInvariant();

        if (value.Length != 2)
        {
            return Result<Point>.Fail($"{where}: точка «{token}» записывается двумя буквами, например dd.");
        }

        var column = value[0] - 'a';
        var row = value[1] - 'a';

        if (column < 0 || column >= size.Value || row < 0 || row >= size.Value)
        {
            return Result<Point>.Fail($"{where}: точка «{token}» находится вне доски {size}.");
        }

        return Result<Point>.Ok(new Point((byte)column, (byte)(size.Value - 1 - row)));
    }

    /// <summary>Читает размер доски урока.</summary>
    /// <param name="root">Корень файла урока.</param>
    /// <returns>Размер доски или <c>null</c>, если его нет или он не поддерживается.</returns>
    private static BoardSize? Size(JsonElement root) =>
        root.TryGetProperty("size", out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
            ? number switch
            {
                9 => BoardSize.Size9,
                13 => BoardSize.Size13,
                19 => BoardSize.Size19,
                _ => null
            }
            : null;

    /// <summary>Читает строковое свойство.</summary>
    /// <param name="element">Объект JSON.</param>
    /// <param name="name">Имя свойства.</param>
    /// <returns>Строку или <c>null</c>, если свойства нет.</returns>
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
