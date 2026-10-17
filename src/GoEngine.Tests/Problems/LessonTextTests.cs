using System.Text.RegularExpressions;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Тексты уроков: одна и та же форма называется одинаково и подписями доски.</summary>
/// <remarks>
/// Ревизия содержания 2026-10-17 нашла места, где текст урока расходился с доской: линия C7
/// называлась то третьей, то четвёртой; урок 9 говорил, что косуми разрезать нельзя, а урок 12
/// показывал разрез; в объяснении ошибки урока 11 стояли внутренние индексы вместо подписей.
/// Проверки держат согласие текстов между уроками: правка одного урока не разойдётся с другим.
/// </remarks>
public sealed class LessonTextTests
{
    /// <summary>Внутренние координаты вида «(4,5)»: индексы массива, а не подписи доски.</summary>
    private static readonly Regex InternalCoordinates = new(@"\(\s*\d{1,2}\s*,\s*\d{1,2}\s*\)", RegexOptions.Compiled);

    [Fact]
    public void Урок6_СчитаетУгловыеТочкиТретьейЛинией()
    {
        var lesson = LessonLibrary.ById("go-06")!;
        var corner = Step(lesson, "Порядок большой");
        var firstLine = Step(lesson, "Первая линия");

        // Подсвеченные углы C7, C3, G7 и G3 стоят на третьей линии от края — это считается
        // по точкам позиции, а не берётся из текста.
        Assert.All(
            corner.Position.Highlight,
            point => Assert.Equal(3, MoveDescriber.LineOf(point, corner.Position.Size.Value)));

        Assert.Contains("C7 — третий столбец и третья строка от нижнего края", corner.Text, StringComparison.Ordinal);
        Assert.Contains("чёрный C7 стоит на третьей линии", firstLine.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Урок9_ГоворитЧтоКосумиМожноРазрезать()
    {
        var step = Step(LessonLibrary.ById("go-09")!, "Косуми");

        Assert.Contains("соперник может разрезать", step.Text, StringComparison.Ordinal);
        Assert.Contains("E5", step.Text, StringComparison.Ordinal);
        Assert.Contains("D6", step.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("разрезать их нельзя", step.Text, StringComparison.Ordinal);

        // Позиция подтверждает разрез: D5 и E6 стоят по диагонали, а E5 и D6 — пустые точки разрезания.
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(3, 4) && stone.Color == StoneColor.Black);
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(4, 5) && stone.Color == StoneColor.Black);
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(4, 4));
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(3, 5));
    }

    [Fact]
    public void Урок12_ПоказываетПримерРазрезаКосуми()
    {
        var lesson = LessonLibrary.ById("go-12")!;
        var step = Step(lesson, "Соедините косуми");

        Assert.Contains("белые могут разрезать", step.Text, StringComparison.Ordinal);
        Assert.Contains("E5", step.Text, StringComparison.Ordinal);
        Assert.Contains("D6", step.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("нельзя разрезать", lesson.Summary, StringComparison.Ordinal);

        // Разрезать можно только эти две точки: у косуми D5–E6 их ровно две.
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(3, 4) && stone.Color == StoneColor.Black);
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(4, 5) && stone.Color == StoneColor.Black);
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(4, 4));
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(3, 5));
    }

    [Fact]
    public void Урок11_ВОбъясненииОшибки_ПодписиДоски()
    {
        var step = Step(LessonLibrary.ById("go-11")!, "Сыграйте легко");

        foreach (var label in new[] { "E4", "E5", "F4", "F5" })
        {
            Assert.Contains(label, step.Failure, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("(", step.Failure, StringComparison.Ordinal);

        // Названные точки стоят там, где сказано: E4 и E5 — камни, F4 и F5 — пустые.
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(4, 3) && stone.Color == StoneColor.Black);
        Assert.Contains(step.Position.Stones, stone => stone.Point == new Point(4, 4) && stone.Color == StoneColor.Black);
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(5, 3));
        Assert.DoesNotContain(step.Position.Stones, stone => stone.Point == new Point(5, 4));
    }

    [Fact]
    public void Словарь_СогласенСУрокамиПроКосуми()
    {
        // Словарь — тоже текст для игрока: определение косуми не может говорить «нельзя»,
        // когда уроки показывают разрез (находка живого прогона 2026-10-17).
        var kosumi = GlossaryLibrary.All.Single(term => term.Name == "Косуми");

        Assert.Contains("может разрезать", kosumi.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("нельзя", kosumi.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ТекстыУроков_НеПоказываютВнутренниеКоординаты()
    {
        List<string> errors = [];

        foreach (var lesson in LessonLibrary.All)
        {
            foreach (var step in lesson.Steps)
            {
                var text = string.Join(" ", new[] { step.Title, step.Text, step.Success, step.Failure });

                if (InternalCoordinates.IsMatch(text))
                {
                    errors.Add($"{lesson.Id} «{step.Title}»: {InternalCoordinates.Match(text).Value}");
                }
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Шаг урока по части заголовка.</summary>
    /// <param name="lesson">Урок.</param>
    /// <param name="titlePart">Часть заголовка.</param>
    /// <returns>Найденный шаг.</returns>
    /// <exception cref="InvalidOperationException">Шага с таким заголовком нет.</exception>
    private static LessonStep Step(Lesson lesson, string titlePart)
    {
        foreach (var step in lesson.Steps)
        {
            if (step.Title.Contains(titlePart, StringComparison.Ordinal))
            {
                return step;
            }
        }

        throw new InvalidOperationException($"В уроке {lesson.Id} нет шага «{titlePart}».");
    }
}
