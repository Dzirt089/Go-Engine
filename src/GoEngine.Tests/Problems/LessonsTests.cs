using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тесты уроков: библиотека, прохождение и проверка заданий.</summary>
/// <remarks>
/// Уроки — это данные, поэтому тесты проверяют не только движок, но и сам материал: каждый
/// принимаемый ход обязан быть легальным в своей позиции, каждая позиция — собираться, а урок —
/// проходиться до конца. Так ошибка в координате задачи находится сборкой, а не игроком.
/// </remarks>
public sealed class LessonsTests
{
    [Fact]
    public void Библиотека_Уроков_Не_Пуста()
    {
        Assert.True(LessonLibrary.Count >= 4, $"уроков в библиотеке: {LessonLibrary.Count}");
    }

    [Fact]
    public void Идентификаторы_Уроков_Уникальны()
    {
        var ids = LessonLibrary.All.Select(lesson => lesson.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void У_Каждого_Урока_Есть_Источник()
    {
        // Материал урока берётся из названного источника: разобранной партии с YouTube или плаката
        // с формами. Безымянный урок нельзя проверить — непонятно, чему он учит.
        Assert.All(
            LessonLibrary.All,
            lesson => Assert.True(
                lesson.Source.Contains("YouTube", StringComparison.Ordinal)
                || lesson.Source.Contains("Плакат", StringComparison.Ordinal),
                $"{lesson.Id}: источник не назван — {lesson.Source}"));
    }

    [Fact]
    public void У_Каждого_Шага_Есть_Заголовок_И_Текст()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            Assert.All(
                lesson.Steps,
                step => Assert.False(
                    string.IsNullOrWhiteSpace(step.Title) || string.IsNullOrWhiteSpace(step.Text),
                    $"{lesson.Id}: у шага нет заголовка или текста"));
        }
    }

    [Fact]
    public void Позиции_Всех_Шагов_Собираются()
    {
        Assert.All(LessonLibrary.All, lesson => Assert.NotNull(new LessonSession(lesson)));
    }

    [Fact]
    public void Каждый_Урок_Проходится_До_Конца()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            var session = new LessonSession(lesson);
            var guard = 0;

            while (!session.IsCompleted && guard++ < 100)
            {
                if (session.Step.IsTask)
                {
                    var played = session.Play(session.Step.Answer[0]);
                    Assert.True(
                        played == LessonVerdict.Correct,
                        $"{lesson.Id}, шаг «{session.Step.Title}»: ход {session.Step.Answer[0].Point} не принят — {session.Message}");
                }

                session.Next();
            }

            Assert.True(session.IsCompleted, $"{lesson.Id}: урок не дошёл до конца");
        }
    }

    [Fact]
    public void Каждое_Задание_Принимает_Все_Свои_Ходы()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            for (var index = 0; index < lesson.StepCount; index++)
            {
                var step = lesson.Steps[index];

                if (!step.IsTask)
                {
                    continue;
                }

                foreach (var move in step.Answer)
                {
                    var session = AtStep(lesson, index);
                    var verdict = session.Play(move);

                    Assert.True(
                        verdict == LessonVerdict.Correct,
                        $"{lesson.Id}, шаг «{step.Title}»: ход {move.Point} не принят — {session.Message}");
                }
            }
        }
    }

    [Fact]
    public void Каждый_Принимаемый_Ход_Легален()
    {
        // Урок учит ходу, который можно сыграть. Отдельная проверка нужна потому, что принятый,
        // но нелегальный ход движок зачитывает (приём «ход в глаз», LessonSession.Play), и опечатка
        // в координате иначе прошла бы молча. Исключение — шаги, где запрет и есть урок.
        foreach (var lesson in LessonLibrary.All)
        {
            for (var index = 0; index < lesson.StepCount; index++)
            {
                var step = lesson.Steps[index];

                if (!step.IsTask || TeachesForbiddenMove(lesson.Id, step.Title))
                {
                    continue;
                }

                var board = AtStep(lesson, index).Board;

                foreach (var move in step.Answer)
                {
                    var legal = board.IsLegal(move);

                    Assert.True(
                        legal.IsSuccess,
                        $"{lesson.Id}, шаг «{step.Title}»: принимаемый ход {move.Point} нелегален — {legal.Error}");
                }

                if (step.Hint is { } hint)
                {
                    var legal = board.IsLegal(hint);

                    Assert.True(
                        legal.IsSuccess,
                        $"{lesson.Id}, шаг «{step.Title}»: подсказка {hint.Point} нелегальна — {legal.Error}");
                }
            }
        }
    }

    [Fact]
    public void Неверный_Ход_Не_Меняет_Позицию()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            for (var index = 0; index < lesson.StepCount; index++)
            {
                var step = lesson.Steps[index];

                if (!step.IsTask || step.Answer.Count == 0)
                {
                    continue;
                }

                var session = AtStep(lesson, index);
                var before = session.Board;
                var wrong = WrongMove(session);

                if (wrong is null)
                {
                    continue;
                }

                var verdict = session.Play(wrong.Value);

                Assert.True(verdict == LessonVerdict.Wrong, $"{lesson.Id}, шаг «{step.Title}»: неверный ход принят");
                Assert.Same(before, session.Board);
            }
        }
    }

    [Fact]
    public void Задание_Не_Пропускается_Без_Верного_Хода()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            for (var index = 0; index < lesson.StepCount; index++)
            {
                if (!lesson.Steps[index].IsTask)
                {
                    continue;
                }

                var session = AtStep(lesson, index);

                Assert.False(session.CanGoNext, $"{lesson.Id}, шаг «{session.Step.Title}»: задание пропускается");
            }
        }
    }

    [Fact]
    public void Подсказка_Ведёт_К_Принимаемому_Ходу()
    {
        foreach (var lesson in LessonLibrary.All)
        {
            for (var index = 0; index < lesson.StepCount; index++)
            {
                if (!lesson.Steps[index].IsTask)
                {
                    continue;
                }

                var session = AtStep(lesson, index);
                var hint = session.ShowHint();

                Assert.True(hint is not null, $"{lesson.Id}, шаг «{session.Step.Title}»: подсказки нет");
                Assert.Contains(session.Step.Answer, move => move.Point == hint);
            }
        }
    }

    [Fact]
    public void Верный_Ход_Снимает_Подсказку()
    {
        var lesson = LessonLibrary.ById("go-01")!;
        var session = new LessonSession(lesson);

        session.Next();
        session.ShowHint();
        session.Play(session.Step.Answer[0]);

        Assert.Null(session.HintPoint);
    }

    [Fact]
    public void Возврат_К_Шагу_Начинает_Его_Заново()
    {
        var lesson = LessonLibrary.ById("go-01")!;
        var session = new LessonSession(lesson);
        session.Next();
        session.Play(session.Step.Answer[0]);

        session.Next();
        session.Back();

        Assert.False(session.IsStepAnswered);
        Assert.Equal(2, session.StepNumber);
    }

    [Fact]
    public void Урок_Начинается_Заново_Командой_Заново()
    {
        var lesson = LessonLibrary.ById("go-01")!;
        var session = new LessonSession(lesson);
        session.Next();
        session.Play(session.Step.Answer[0]);
        session.Next();

        session.Restart();

        Assert.Equal(1, session.StepNumber);
        Assert.False(session.IsCompleted);
    }

    [Fact]
    public void Ход_В_Глаз_Показывает_Запрет()
    {
        // Шаг урока о глазах: принимаемый ход запрещён правилами, и урок именно это и показывает.
        // Такой ход зачитывается, а доска остаётся прежней.
        var lesson = LessonLibrary.ById("go-02")!;
        var index = IndexOfStep(lesson, "Ход в глаз запрещён");
        var session = AtStep(lesson, index);
        var before = session.Board;

        var verdict = session.Play(session.Step.Answer[0]);

        Assert.Equal(LessonVerdict.Correct, verdict);
        Assert.Same(before, session.Board);
    }

    [Fact]
    public void Объяснение_Не_Принимает_Ходов()
    {
        var lesson = LessonLibrary.ById("go-01")!;
        var session = new LessonSession(lesson);

        var verdict = session.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(LessonVerdict.None, verdict);
    }

    [Fact]
    public void Урок_Ищется_По_Идентификатору()
    {
        Assert.NotNull(LessonLibrary.ById("go-03"));
    }

    [Fact]
    public void Неизвестный_Урок_Не_Находится()
    {
        Assert.Null(LessonLibrary.ById("нет-такого"));
    }

    [Fact]
    public void Урок_Без_Шагов_Отклоняется()
    {
        var parsed = LessonJson.Parse("""{"id":"x","title":"t","summary":"s","size":9,"steps":[]}""");

        Assert.False(parsed.IsSuccess);
    }

    [Fact]
    public void Точка_Вне_Доски_Отклоняется()
    {
        var parsed = LessonJson.Parse("""
            {"id":"x","title":"t","summary":"s","size":9,"steps":[
              {"kind":"task","title":"t","text":"t","position":{"toMove":"B","black":[],"white":[]},"answer":["kk"]}]}
            """);

        Assert.False(parsed.IsSuccess);
        Assert.Contains("вне доски", parsed.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Разбор_Точки_Читает_Строку_Как_В_SGF()
    {
        // «aa» — левый верхний угол: строка не переворачивается — так же читает SGF игра
        // (SgfReader) и так же подписаны строки на доске (строка 1 сверху, D-009).
        var parsed = LessonJson.Parse("""
            {"id":"x","title":"t","summary":"s","size":9,"steps":[
              {"kind":"task","title":"t","text":"t","position":{"toMove":"B","black":[],"white":[]},"answer":["aa"]}]}
            """);

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(new Point(0, 0), parsed.Value!.Steps[0].Answer[0].Point);
    }

    /// <summary>Шаг, где нелегальный ход и есть урок: игра внутрь глаза запрещена правилами.</summary>
    /// <param name="lessonId">Идентификатор урока.</param>
    /// <param name="title">Заголовок шага.</param>
    /// <returns>Шаг объясняет запрет правил, а не показывает возможный ход.</returns>
    private static bool TeachesForbiddenMove(string lessonId, string title) =>
        lessonId == "go-02" && title == "Ход в глаз запрещён";

    /// <summary>Ставит сессию на шаг с номером, проходя предыдущие.</summary>
    /// <param name="lesson">Урок.</param>
    /// <param name="index">Номер шага с нуля.</param>
    /// <returns>Сессия на нужном шаге.</returns>
    private static LessonSession AtStep(Lesson lesson, int index)
    {
        var session = new LessonSession(lesson);

        for (var i = 0; i < index; i++)
        {
            if (session.Step.IsTask)
            {
                session.Play(session.Step.Answer[0]);
            }

            session.Next();
        }

        return session;
    }

    /// <summary>Возвращает номер шага по заголовку.</summary>
    /// <param name="lesson">Урок.</param>
    /// <param name="title">Заголовок шага.</param>
    /// <returns>Номер шага с нуля.</returns>
    private static int IndexOfStep(Lesson lesson, string title)
    {
        for (var index = 0; index < lesson.StepCount; index++)
        {
            if (string.Equals(lesson.Steps[index].Title, title, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException($"В уроке {lesson.Id} нет шага «{title}».");
    }

    /// <summary>Ищет легальный ход, которого нет среди принимаемых.</summary>
    /// <param name="session">Сессия на задании.</param>
    /// <returns>Ход или <c>null</c>, если такого хода в позиции нет.</returns>
    private static Move? WrongMove(LessonSession session)
    {
        foreach (var point in session.Board.AllPoints())
        {
            var move = Move.Play(point, session.ToMove);

            if (session.Step.Answer.Contains(move) || !session.Board.IsLegal(move).IsSuccess)
            {
                continue;
            }

            return move;
        }

        return null;
    }
}
