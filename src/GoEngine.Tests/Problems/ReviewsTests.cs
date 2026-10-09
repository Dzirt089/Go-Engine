using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тесты обучающих партий: библиотека, запись, разбор и вопросы.</summary>
/// <remarks>
/// Партии — это данные, поэтому тесты проверяют не только движок разбора, но и сам материал:
/// каждый ход обязан быть легальным, партия — доигранной до конца, разбор — ссылаться
/// на существующие ходы, а вопрос — принимать ход, который в партии действительно сыгран.
/// Так ошибка в записи находится сборкой, а не игроком на середине разбора.
/// </remarks>
public sealed class ReviewsTests
{
    [Fact]
    public void Библиотека_Партий_Не_Пуста()
    {
        Assert.True(ReviewLibrary.Count >= 3, $"партий в библиотеке: {ReviewLibrary.Count}");
    }

    [Fact]
    public void Есть_Партии_На_Девяти_И_Тринадцати_Досках()
    {
        Assert.Contains(ReviewLibrary.All, game => game.Size.Value == 9);
        Assert.Contains(ReviewLibrary.All, game => game.Size.Value == 13);
    }

    [Fact]
    public void Идентификаторы_Партий_Уникальны()
    {
        var ids = ReviewLibrary.All.Select(game => game.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void У_Каждой_Партии_Есть_Источник_И_Итог()
    {
        // Источник называет, откуда взят материал: без него партию нельзя проверить.
        Assert.All(
            ReviewLibrary.All,
            game => Assert.True(
                game.Source.Contains("YouTube", StringComparison.Ordinal),
                $"{game.Id}: источник не назван — {game.Source}"));

        Assert.All(ReviewLibrary.All, game => Assert.False(string.IsNullOrWhiteSpace(game.Result)));
    }

    [Fact]
    public void Каждый_Ход_Партии_Легален()
    {
        foreach (var game in ReviewLibrary.All)
        {
            // Партия начинается со своей позиции: у партий с форой на доске уже стоят камни,
            // и пустая доска для них неверна.
            var board = game.StartPosition();
            var number = 0;

            foreach (var move in game.Moves)
            {
                number++;
                var legal = board.IsLegal(move);

                Assert.True(legal.IsSuccess, $"{game.Id}, ход {number} ({move}): {legal.Error}");

                board = board.ApplyMove(move);
            }
        }
    }

    [Fact]
    public void Партия_Доиграна_До_Конца()
    {
        foreach (var game in ReviewLibrary.All)
        {
            var state = GameState.NewGame(game.Size, game.Komi);

            foreach (var move in game.Moves)
            {
                _ = state.Play(move);
            }

            // Партия из библиотеки — целая игра: она заканчивается двумя пасами, а не обрывается
            // на середине и не упирается в лимит ходов.
            Assert.Equal(GameStatus.FinishedByTwoPasses.Name, state.Status.Name);
            Assert.True(state.Finish().IsSuccess, $"{game.Id}: итог партии не считается");
        }
    }

    [Fact]
    public void Итог_Партии_Совпадает_С_Подсчётом_Движка()
    {
        foreach (var game in ReviewLibrary.All)
        {
            var board = game.StartPosition();

            foreach (var move in game.Moves)
            {
                board = board.ApplyMove(move);
            }

            StoneColor? winnerColor;
            double margin;

            if (game.Handicap.Count == 0)
            {
                // Партия без форы: итог берётся у движка вместе со снятием мёртвых камней —
                // так же, как его считает завершение партии.
                var state = GameState.NewGame(game.Size, game.Komi);

                foreach (var move in game.Moves)
                {
                    _ = state.Play(move);
                }

                var result = state.Finish().Value;

                winnerColor = result.Winner;
                margin = Math.Round(result.Score?.Margin ?? 0, 1);
            }
            else
            {
                // Партия с форой: движок не умеет начинать с расстановки, поэтому итог считается
                // по площади финальной позиции — тем же подсчётом очков, что и в конце партии.
                var score = Scorer.Calculate(board, game.Komi);

                winnerColor = score.Winner;
                margin = Math.Round(score.Margin, 1);
            }

            var winner = winnerColor?.Name switch
            {
                "Black" => "Чёрные",
                "White" => "Белые",
                _ => "ничья"
            };

            Assert.Contains(winner, game.Result, StringComparison.Ordinal);

            var declared = DeclaredMargin(game.Result);

            if (game.Handicap.Count > 0)
            {
                // Партия с форой: итог считается по площади и совпадает точно.
                Assert.Equal(margin, declared);
            }
            else
            {
                // Партия движка: поиск мёртвых групп может пометить пару камней иначе, чем при
                // генерации, поэтому разница сверяется с допуском в три очка — грубая ошибка
                // в данных видна, а разница в мёртвых камнях тест не роняет.
                Assert.True(
                    Math.Abs(margin - declared) <= 3,
                    $"{game.Id}: в данных {declared}, подсчёт даёт {margin}");
            }
        }
    }

    [Fact]
    public void У_Каждой_Партии_Есть_Разбор_И_Вопросы()
    {
        foreach (var game in ReviewLibrary.All)
        {
            Assert.True(game.Notes.Count >= 5, $"{game.Id}: разбор короче пяти заметок");
            Assert.True(game.QuizCount >= 2, $"{game.Id}: в партии меньше двух вопросов");

            Assert.All(
                game.Notes,
                note => Assert.False(
                    string.IsNullOrWhiteSpace(note.Title) || string.IsNullOrWhiteSpace(note.Text),
                    $"{game.Id}: у заметки нет заголовка или текста"));
        }
    }

    [Fact]
    public void Разбор_Ссылается_На_Существующие_Ходы()
    {
        foreach (var game in ReviewLibrary.All)
        {
            foreach (var note in game.Notes)
            {
                Assert.InRange(note.Before, 0, game.MoveCount);
            }

            // Заметок на одну позицию быть не должно: игрок не поймёт, какую читать.
            var duplicates = game.Notes.GroupBy(note => note.Before).Where(group => group.Count() > 1);

            Assert.Empty(duplicates);
        }
    }

    [Fact]
    public void Вопрос_Принимает_Ход_Партии()
    {
        foreach (var game in ReviewLibrary.All)
        {
            foreach (var note in game.Notes.Where(note => note.Quiz is not null))
            {
                var quiz = note.Quiz!;
                var move = game.Moves[note.Before];

                Assert.Contains(quiz.Answer, answer => answer.Point == move.Point);
                Assert.Equal(move.Color.Name, quiz.Answer[0].Color.Name);

                // Позиция вопроса собирается заново до нужного хода — так же, как в проверке записи:
                // снимки делят историю партии, и её потеря меняла вердикт правил.
                var board = game.StartPosition();

                for (var step = 0; step < note.Before; step++)
                {
                    board = board.ApplyMove(game.Moves[step]);
                }

                foreach (var answer in quiz.Answer)
                {
                    var legal = board.IsLegal(answer);

                    Assert.True(legal.IsSuccess, $"{game.Id}, вопрос «{note.Title}»: ход {answer.Point} невозможен — {legal.Error}");
                }
            }
        }
    }

    [Fact]
    public void Партия_Проходится_С_Вопросами()
    {
        foreach (var game in ReviewLibrary.All)
        {
            var session = new ReviewSession(game);
            var guard = 0;

            while (!session.IsCompleted && guard++ < game.MoveCount + 10)
            {
                if (session.Note is { Quiz: not null } note)
                {
                    // Игрок ищет ход сам: сессия принимает ход партии и играет его.
                    // Заметка запоминается до ответа: после верного хода разбор идёт дальше,
                    // и текущей заметки может уже не быть.
                    var played = session.Play(game.Moves[session.MoveNumber]);

                    Assert.True(
                        played == LessonVerdict.Correct,
                        $"{game.Id}, вопрос «{note.Title}»: ход не принят — {session.Message}");
                }

                _ = session.Next();
            }

            Assert.True(session.IsCompleted, $"{game.Id}: разбор не дошёл до конца партии");
            Assert.Equal(game.MoveCount, session.MoveNumber);
        }
    }

    [Fact]
    public void Неверный_Ответ_Не_Меняет_Позицию()
    {
        foreach (var game in ReviewLibrary.All)
        {
            var note = game.Notes.FirstOrDefault(note => note.Quiz is not null);

            if (note is null)
            {
                continue;
            }

            var session = new ReviewSession(game);
            _ = session.GoTo(note.Before);

            var before = session.Board;
            var correct = game.Moves[note.Before].Point;
            var wrong = WrongPoint(session.Board, session.ToMove, note.Quiz!, correct);

            if (wrong is null)
            {
                continue;
            }

            var verdict = session.Play(Move.Play(wrong.Value, session.ToMove));

            Assert.True(verdict == LessonVerdict.Wrong, $"{game.Id}: неверный ответ принят");
            Assert.Same(before, session.Board);
            Assert.False(session.CanGoNext, $"{game.Id}: разбор пошёл дальше без верного ответа");
        }
    }

    [Fact]
    public void Ход_Без_Вопроса_Не_Принимается()
    {
        var game = ReviewLibrary.All[0];
        var session = new ReviewSession(game);

        // Доходим до позиции без вопроса: вопрос сначала решается, иначе разбор не идёт дальше.
        while (session.Note?.Quiz is not null && !session.IsCompleted)
        {
            _ = session.Play(game.Moves[session.MoveNumber]);
        }

        if (session.IsCompleted)
        {
            return;
        }

        var before = session.Board;
        var verdict = session.Play(Move.Play(FreePoint(before), session.ToMove));

        Assert.Equal(LessonVerdict.None, verdict);
        Assert.Same(before, session.Board);
    }

    [Fact]
    public void Возврат_И_Заново_Работают()
    {
        var game = ReviewLibrary.All[0];
        var session = new ReviewSession(game);

        Step(session);
        Step(session);

        Assert.Equal(2, session.MoveNumber);
        Assert.True(session.Back());
        Assert.Equal(1, session.MoveNumber);

        // Второй возврат доводит до начала партии, третий уже некуда.
        Assert.True(session.Back());
        Assert.Equal(0, session.MoveNumber);
        Assert.False(session.Back());

        session.Restart();

        Assert.Equal(0, session.MoveNumber);
        Assert.False(session.IsCompleted);
        Assert.Equal(game.Moves.Count, session.MoveCount);
    }

    [Fact]
    public void Тексты_Разбора_На_Русском()
    {
        Assert.All(
            ReviewLibrary.All,
            game =>
            {
                Assert.True(ContainsCyrillic(game.Title), $"{game.Id}: название без кириллицы");
                Assert.True(ContainsCyrillic(game.Summary), $"{game.Id}: описание без кириллицы");
                Assert.True(ContainsCyrillic(game.Players), $"{game.Id}: игроки без кириллицы");

                Assert.All(game.Notes, note => Assert.True(
                    ContainsCyrillic(note.Title) && ContainsCyrillic(note.Text),
                    $"{game.Id}: разбор без кириллицы — «{note.Title}»"));
            });
    }

    [Fact]
    public void Партия_С_Форой_Начинается_С_Хода_Белых()
    {
        // Фора — начальная расстановка: три камня чёрных уже стоят, первым ходит белый.
        var parsed = ReviewJson.Parse("""
            {"id":"h","title":"Фора","summary":"как играть с форой","source":"YouTube","players":"вы",
             "result":"Чёрные +5.5","size":9,"komi":0.5,"handicap":["cc","gc","gg"],
             "moves":["ee"],
             "notes":[{"before":0,"title":"Начало","text":"Белые начинают","quiz":{"answer":["ee"]}}]}
            """);

        Assert.True(parsed.IsSuccess, parsed.Error);

        var game = parsed.Value!;
        var session = new ReviewSession(game);

        Assert.Equal(3, game.Handicap.Count);
        Assert.Equal(StoneColor.White.Name, game.FirstColor.Name);
        Assert.Equal(StoneColor.White.Name, session.ToMove.Name);
        Assert.Equal(StoneColor.Black.Name, session.Board.At(new Point(2, 2)).Name);
        Assert.Equal(StoneColor.Black.Name, session.Board.At(new Point(6, 2)).Name);
        Assert.Equal(StoneColor.Black.Name, session.Board.At(new Point(6, 6)).Name);
        Assert.Equal(0, session.MoveNumber);

        // Вопрос по умолчанию задан тому, чей ход: в партии с форой — белым.
        Assert.Equal(StoneColor.White.Name, game.Notes[0].Quiz!.Answer[0].Color.Name);
        Assert.Equal(LessonVerdict.Correct, session.Play(game.Moves[0]));
        Assert.Equal(1, session.MoveNumber);
    }

    [Fact]
    public void Партия_Без_Форы_Начинается_С_Хода_Чёрных()
    {
        var game = ReviewLibrary.All[0];
        var session = new ReviewSession(game);

        Assert.Empty(game.Handicap);
        Assert.Equal(StoneColor.Black.Name, game.FirstColor.Name);
        Assert.Equal(StoneColor.Black.Name, session.ToMove.Name);
    }

    [Fact]
    public void Камень_Форы_Вне_Доски_Отклоняется()
    {
        var parsed = ReviewJson.Parse("""
            {"id":"h","title":"Фора","summary":"s","source":"YouTube","players":"вы","result":"ничья",
             "size":9,"handicap":["zz"],"moves":["ee"],"notes":[]}
            """);

        Assert.False(parsed.IsSuccess);
        Assert.Contains("вне доски", parsed.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Материалы_Обучения_Лежат_В_Сборке_Ресурсами()
    {
        // Одна и та же сборка едет на Windows, Linux, macOS и Android: если материал попал
        // в ресурсы, он доступен на всех платформах, и это проверяется здесь, а не сборкой под
        // каждую систему (замечание пользователя 2026-10-09 — проверить все платформы).
        var names = typeof(ReviewLibrary).Assembly.GetManifestResourceNames();

        Assert.Contains(names, name => name.EndsWith("Games.game-01.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.EndsWith("Games.game-08.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.EndsWith("Lessons.go-01.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.EndsWith("Lessons.go-12.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.EndsWith("Glossary.glossary.json", StringComparison.Ordinal));
        Assert.Contains(names, name => name.EndsWith(".sgf", StringComparison.Ordinal));
    }

    [Fact]
    public void Партия_Ищется_По_Идентификатору()
    {
        var first = ReviewLibrary.All[0];

        Assert.NotNull(ReviewLibrary.ById(first.Id));
        Assert.Null(ReviewLibrary.ById("нет-такой"));
    }

    [Fact]
    public void Пустой_Набор_Партий_Отклоняется()
    {
        var parsed = ReviewJson.Parse("""
            {"id":"x","title":"t","summary":"s","source":"YouTube","players":"вы","result":"ничья","size":9,"moves":[],"notes":[]}
            """);

        Assert.False(parsed.IsSuccess);
    }

    [Fact]
    public void Партия_С_Невозможным_Ходом_Отклоняется()
    {
        // Ход в занятую точку: запись обязана падать при разборе, а не в середине разбора.
        var parsed = ReviewJson.Parse("""
            {"id":"x","title":"t","summary":"s","source":"YouTube","players":"вы","result":"ничья","size":9,
             "moves":["ee","ee"],"notes":[]}
            """);

        Assert.False(parsed.IsSuccess);
        Assert.Contains("невозможен", parsed.Error ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Читает разницу очков из итога партии: «Чёрные +11.5» — это 11,5.</summary>
    /// <param name="result">Итог словами.</param>
    /// <returns>Разница очков.</returns>
    private static double DeclaredMargin(string result)
    {
        var plus = result.IndexOf('+', StringComparison.Ordinal);

        return plus >= 0
            ? double.Parse(result[(plus + 1)..].Trim(), System.Globalization.CultureInfo.InvariantCulture)
            : 0;
    }

    /// <summary>Делает один шаг разбора: отвечает на вопрос, если он есть, и играет ход партии.</summary>
    /// <param name="session">Сессия разбора.</param>
    /// <remarks>
    /// Партия начинается с вопроса (первый ход ищет игрок), поэтому шаг без ответа невозможен:
    /// разбор не идёт дальше нерешённого вопроса.
    /// </remarks>
    private static void Step(ReviewSession session)
    {
        if (session.Note?.Quiz is not null)
        {
            // Верный ответ сам играет ход партии: второй ход здесь был бы лишним.
            _ = session.Play(session.Game.Moves[session.MoveNumber]);

            return;
        }

        _ = session.Next();
    }

    /// <summary>Ищет точку, которой нет в принимаемых ходах вопроса и которая легальна.</summary>
    /// <param name="board">Позиция вопроса.</param>
    /// <param name="color">Чей ход в позиции.</param>
    /// <param name="quiz">Вопрос разбора.</param>
    /// <param name="correct">Ход партии: он верный и в пример не годится.</param>
    private static Point? WrongPoint(Board board, StoneColor color, ReviewQuiz quiz, Point correct)
    {
        for (var x = 0; x < board.Size.Value; x++)
        {
            for (var y = 0; y < board.Size.Value; y++)
            {
                var point = new Point((byte)x, (byte)y);

                if (point == correct || !board.IsEmpty(point))
                {
                    continue;
                }

                if (quiz.Answer.Any(answer => answer.Point == point))
                {
                    continue;
                }

                var legal = board.IsLegal(Move.Play(point, color));

                if (legal.IsSuccess)
                {
                    return point;
                }
            }
        }

        return null;
    }

    /// <summary>Ищет любую свободную точку, куда можно сыграть.</summary>
    private static Point FreePoint(Board board)
    {
        for (var x = 0; x < board.Size.Value; x++)
        {
            for (var y = 0; y < board.Size.Value; y++)
            {
                var point = new Point((byte)x, (byte)y);

                if (board.IsEmpty(point))
                {
                    return point;
                }
            }
        }

        return new Point(0, 0);
    }

    /// <summary>Есть ли в тексте кириллица: тексты обучения обязаны быть русскими.</summary>
    private static bool ContainsCyrillic(string text) =>
        text.Any(ch => ch is >= 'А' and <= 'я' or 'ё' or 'Ё');
}
