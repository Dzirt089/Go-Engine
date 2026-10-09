namespace GoEngine.Problems;

using System.Globalization;
using System.Text.Json;
using GoEngine.Core;

/// <summary>Разбор обучающей партии из JSON.</summary>
/// <remarks>
/// <para>
/// Партия — данные, а не код: ходы и разбор лежат в файле (<c>Games/*.json</c>), а движок только
/// проводит её по записи. Так материал можно править и дополнять, не трогая движок, и он же
/// проверяется тестами как данные.
/// </para>
/// <para>
/// Свойства файла: <c>id</c>, <c>title</c>, <c>summary</c>, <c>source</c>, <c>players</c>,
/// <c>result</c>, <c>size</c> (9, 13 или 19), <c>komi</c>, <c>moves</c> (ходы партии подряд),
/// <c>notes</c> (разбор). Ход записывается как точка SGF: две буквы, первая — колонка слева
/// направо, вторая — строка сверху вниз (<c>aa</c> — левый верхний угол); строка не
/// переворачивается — так же читает SGF игра (<c>SgfReader</c>) и так же подписаны строки
/// на доске (D-009). Пас записывается пустой строкой, <c>-</c> или <c>pass</c>.
/// Заметка разбора: <c>before</c> (перед каким ходом), <c>title</c>, <c>text</c> и необязательный
/// <c>quiz</c> с принимаемыми ходами (<c>answer</c>), подсказкой (<c>hint</c>) и текстами
/// <c>success</c> и <c>failure</c>.
/// </para>
/// </remarks>
public static class ReviewJson
{
    /// <summary>Что сказать игроку после верного хода, если автор не задал свой текст.</summary>
    public const string DefaultSuccess = "Верно!";

    /// <summary>Что сказать игроку после неверного хода, если автор не задал свой текст.</summary>
    public const string DefaultFailure = "Пока не подходит: попробуйте другой ход.";

    /// <summary>Читает обучающую партию из текста JSON.</summary>
    /// <param name="text">Содержимое файла партии.</param>
    /// <param name="id">Идентификатор партии; <c>null</c> — взять из файла (<c>id</c>).</param>
    /// <returns>Партия или причина отказа на русском языке.</returns>
    /// <exception cref="ArgumentException">Текст пуст.</exception>
    public static Result<ReviewGame> Parse(string text, string? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            return Result<ReviewGame>.Fail($"Партия не разбирается как JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<ReviewGame>.Fail("Партия должна быть объектом JSON.");
            }

            var gameId = string.IsNullOrWhiteSpace(id) ? Text(root, "id") : id;
            var title = Text(root, "title");
            var summary = Text(root, "summary");
            var source = Text(root, "source");
            var players = Text(root, "players");
            var result = Text(root, "result");
            var size = Size(root);

            if (string.IsNullOrWhiteSpace(gameId))
            {
                return Result<ReviewGame>.Fail("У партии нет идентификатора (id).");
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: нет названия (title).");
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: нет описания (summary).");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: нет источника (source).");
            }

            if (string.IsNullOrWhiteSpace(players))
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: не сказано, кто с кем играет (players).");
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: нет итога (result).");
            }

            if (size is null)
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: размер доски (size) должен быть 9, 13 или 19.");
            }

            if (!root.TryGetProperty("moves", out var moves)
                || moves.ValueKind != JsonValueKind.Array
                || moves.GetArrayLength() == 0)
            {
                return Result<ReviewGame>.Fail($"Партия {gameId}: нет ходов (moves).");
            }

            // Фора — это начальная расстановка: с двумя камнями и больше первым ходит белый,
            // и от этого цвета считаются цвета всех ходов партии.
            List<Point> handicap = [];

            if (root.TryGetProperty("handicap", out var handicapElement))
            {
                if (handicapElement.ValueKind != JsonValueKind.Array)
                {
                    return Result<ReviewGame>.Fail($"Партия {gameId}: фора (handicap) должна быть списком точек.");
                }

                foreach (var element in handicapElement.EnumerateArray())
                {
                    var point = MoveOf(element.GetString(), size.Value, StoneColor.Black, gameId!, handicap.Count + 1);

                    if (!point.IsSuccess)
                    {
                        return Result<ReviewGame>.Fail($"{point.Error} (камень форы)");
                    }

                    handicap.Add(point.Value!.Point);
                }
            }

            var firstColor = handicap.Count >= 2 ? StoneColor.White : StoneColor.Black;
            var parsedMoves = new List<Move>(moves.GetArrayLength());
            var index = 0;

            foreach (var element in moves.EnumerateArray())
            {
                var color = index % 2 == 0 ? firstColor : firstColor.Opponent();
                var move = MoveOf(element.GetString(), size.Value, color, gameId!, index + 1);

                if (!move.IsSuccess)
                {
                    return Result<ReviewGame>.Fail(move.Error!);
                }

                parsedMoves.Add(move.Value!);
                index++;
            }

            List<ReviewNote> notes = [];

            if (root.TryGetProperty("notes", out var noteElements))
            {
                if (noteElements.ValueKind != JsonValueKind.Array)
                {
                    return Result<ReviewGame>.Fail($"Партия {gameId}: разбор (notes) должен быть списком.");
                }

                foreach (var element in noteElements.EnumerateArray())
                {
                    var note = Note(element, size.Value, firstColor, gameId!, index: notes.Count + 1);

                    if (!note.IsSuccess)
                    {
                        return Result<ReviewGame>.Fail(note.Error!);
                    }

                    notes.Add(note.Value!);
                }
            }

            var komi = KomiOf(root, size.Value);

            try
            {
                return Result<ReviewGame>.Ok(new ReviewGame(
                    gameId!,
                    title!,
                    summary!,
                    source!,
                    players!,
                    result!,
                    size.Value,
                    komi,
                    parsedMoves.AsReadOnly(),
                    notes.AsReadOnly(),
                    handicap.AsReadOnly()));
            }
            catch (DomainException exception)
            {
                return Result<ReviewGame>.Fail(exception.Message);
            }
        }
    }

    /// <summary>Разбирает заметку разбора.</summary>
    private static Result<ReviewNote> Note(JsonElement element, BoardSize size, StoneColor firstColor, string gameId, int index)
    {
        var where = $"Партия {gameId}, заметка {index}";

        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result<ReviewNote>.Fail($"{where}: заметка должна быть объектом JSON.");
        }

        var title = Text(element, "title");
        var body = Text(element, "text");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
        {
            return Result<ReviewNote>.Fail($"{where}: у заметки нет заголовка (title) или текста (text).");
        }

        if (!element.TryGetProperty("before", out var beforeElement) || !beforeElement.TryGetInt32(out var before))
        {
            return Result<ReviewNote>.Fail($"{where}: не указано, перед каким ходом стоит разбор (before).");
        }

        ReviewQuiz? quiz = null;

        if (element.TryGetProperty("quiz", out var quizElement))
        {
            // В позиции перед ходом <c>before</c> ходит та сторона, чей это ход по счёту:
            // явный toMove нужен только там, где вопрос задан не ей (в партии такого нет).
            var sideToMove = before % 2 == 0 ? firstColor : firstColor.Opponent();
            var parsed = Quiz(quizElement, size, sideToMove, where);

            if (!parsed.IsSuccess)
            {
                return Result<ReviewNote>.Fail(parsed.Error!);
            }

            quiz = parsed.Value;
        }

        try
        {
            return Result<ReviewNote>.Ok(new ReviewNote(before, title!, body!, quiz));
        }
        catch (DomainException exception)
        {
            return Result<ReviewNote>.Fail($"{where}: {exception.Message}");
        }
    }

    /// <summary>Разбирает вопрос разбора.</summary>
    private static Result<ReviewQuiz> Quiz(JsonElement element, BoardSize size, StoneColor firstColor, string where)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result<ReviewQuiz>.Fail($"{where}: вопрос (quiz) должен быть объектом JSON.");
        }

        if (!element.TryGetProperty("answer", out var answers)
            || answers.ValueKind != JsonValueKind.Array
            || answers.GetArrayLength() == 0)
        {
            return Result<ReviewQuiz>.Fail($"{where}: у вопроса нет принимаемых ходов (answer).");
        }

        // Цвет хода задаёт позиция: в записи партии цвета чередуются от первого цвета, поэтому
        // в файле его указывают только там, где вопрос задан не первому ходившему.
        List<Move> parsed = [];
        var color = element.TryGetProperty("toMove", out var toMove) && toMove.GetString() is { Length: > 0 } name
            ? StoneColor.FromName<StoneColor>(name == "W" ? nameof(StoneColor.White) : nameof(StoneColor.Black))
            : firstColor;

        var number = 0;

        foreach (var answer in answers.EnumerateArray())
        {
            number++;
            var move = MoveOf(answer.GetString(), size, color, where, number);

            if (!move.IsSuccess)
            {
                return Result<ReviewQuiz>.Fail(move.Error!);
            }

            parsed.Add(move.Value!);
        }

        Move? hint = null;

        if (element.TryGetProperty("hint", out var hintElement) && hintElement.ValueKind == JsonValueKind.String)
        {
            var parsedHint = MoveOf(hintElement.GetString(), size, color, where, number);

            if (!parsedHint.IsSuccess)
            {
                return Result<ReviewQuiz>.Fail(parsedHint.Error!);
            }

            hint = parsedHint.Value;
        }

        return Result<ReviewQuiz>.Ok(new ReviewQuiz(
            parsed.AsReadOnly(),
            hint,
            Text(element, "success") ?? DefaultSuccess,
            Text(element, "failure") ?? DefaultFailure));
    }

    /// <summary>Разбирает ход: точку SGF, пас или пустую строку.</summary>
    private static Result<Move> MoveOf(string? token, BoardSize size, StoneColor color, string where, int number)
    {
        if (string.IsNullOrWhiteSpace(token) || token == "-" || token == "pass" || token == "пас")
        {
            return Result<Move>.Ok(Move.Pass(color));
        }

        if (token.Length != 2)
        {
            return Result<Move>.Fail($"{where}, ход {number}: ход «{token}» должен быть двумя буквами.");
        }

        var column = token[0] - 'a';
        var row = token[1] - 'a';

        if (column < 0 || row < 0 || column >= size.Value || row >= size.Value)
        {
            return Result<Move>.Fail($"{where}, ход {number}: точка «{token}» вне доски {size}.");
        }

        return Result<Move>.Ok(Move.Play(new Point((byte)column, (byte)row), color));
    }

    /// <summary>Читает размер доски.</summary>
    private static BoardSize? Size(JsonElement root) =>
        root.TryGetProperty("size", out var element) && element.TryGetInt32(out var value)
            ? value switch
            {
                9 => BoardSize.Size9,
                13 => BoardSize.Size13,
                19 => BoardSize.Size19,
                _ => null
            }
            : null;

    /// <summary>Читает коми партии; без него — коми по размеру доски.</summary>
    private static Komi KomiOf(JsonElement root, BoardSize size) =>
        root.TryGetProperty("komi", out var element) && element.TryGetDouble(out var value)
            ? new Komi(value)
            : Komi.For(size);

    /// <summary>Читает строковое свойство.</summary>
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
