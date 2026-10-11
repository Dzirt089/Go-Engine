using System.Text.RegularExpressions;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Уроки называют точки: где текст говорит о позиции, там есть координата.</summary>
/// <remarks>
/// <para>
/// Замечание пользователя 2026-10-10: «пояснения во всех задачах, уроках и играх должны называть
/// координаты точно — о каком угле, стороне и линии речь». Проверка идёт по всем двенадцати урокам:
/// шаг, который показывает камни или подсвеченные точки, обязан назвать их координатами.
/// </para>
/// <para>
/// Общие правила игры исключение: «у камня в углу два дыхания» — это правило, а не место на доске,
/// и координата в нём только запутала бы. Поэтому проверяется связь с позицией: нет камней
/// и подсветки — нет и требования.
/// </para>
/// </remarks>
public sealed class LessonCoordinatesTests
{
    /// <summary>Подпись точки в тексте: буква столбца (без «I») и номер строки.</summary>
    private static readonly Regex PointPattern = new(@"(?<![A-Za-zА-Яа-я])(?<point>[A-HJ-T]\d{1,2})(?![0-9])", RegexOptions.Compiled);

    /// <summary>Слово о цвете вплотную перед точкой.</summary>
    private static readonly Regex ColourPattern = new(@"(?<colour>чёрн\w*|черн\w*|бел\w*)[\s:–—-]{0,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Шаг_С_Позицией_Называет_Координаты()
    {
        List<string> errors = [];

        foreach (var lesson in LessonLibrary.All)
        {
            foreach (var step in lesson.Steps)
            {
                if (!ShowsPosition(step))
                {
                    continue;
                }

                var text = Text(step);

                if (Points(text).Count == 0)
                {
                    errors.Add($"{lesson.Id} «{step.Title}»: в шаге нет ни одной координаты — «{step.Text}»");
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Цвет_Рядом_С_Точкой_Не_Противоречит_Позиции()
    {
        List<string> errors = [];

        foreach (var lesson in LessonLibrary.All)
        {
            foreach (var step in lesson.Steps)
            {
                var text = Text(step);

                foreach (Match match in PointPattern.Matches(text))
                {
                    var size = step.Position.Size.Value;

                    if (!TryParse(match.Groups["point"].Value, size, out var point))
                    {
                        errors.Add($"{lesson.Id} «{step.Title}»: точки {match.Groups["point"].Value} нет на доске {size}×{size}");
                        continue;
                    }

                    var colour = ColourBefore(text[..match.Index]);

                    if (colour is null)
                    {
                        continue;
                    }

                    var expected = colour == 'б' ? StoneColor.White : StoneColor.Black;
                    var stone = StoneAt(step, point);

                    // Пустая точка — ход из объяснения или будущий ход: спорен только чужой камень.
                    if (stone != StoneColor.Empty && stone != expected)
                    {
                        errors.Add(
                            $"{lesson.Id} «{step.Title}»: сказано «{match.Groups["point"].Value}» как {(expected == StoneColor.Black ? "чёрный" : "белый")}, "
                            + $"а в позиции там {(stone == StoneColor.Black ? "чёрный камень" : "белый камень")}");
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Показывает ли шаг конкретную позицию: камни или подсвеченные точки.</summary>
    /// <param name="step">Шаг урока.</param>
    /// <returns><c>true</c>, если позиция не пуста.</returns>
    private static bool ShowsPosition(LessonStep step) =>
        step.Position.Stones.Count > 0 || step.Position.Highlight.Count > 0;

    /// <summary>Весь текст шага: пояснение, похвала и отказ.</summary>
    /// <param name="step">Шаг урока.</param>
    /// <returns>Текст одной строкой.</returns>
    private static string Text(LessonStep step) =>
        string.Join(" ", new[] { step.Title, step.Text, step.Success, step.Failure }.Where(part => part.Length > 0));

    /// <summary>Камень в точке позиции шага.</summary>
    /// <param name="step">Шаг урока.</param>
    /// <param name="point">Точка.</param>
    /// <returns>Цвет камня или <see cref="StoneColor.Empty"/>.</returns>
    private static StoneColor StoneAt(LessonStep step, Point point)
    {
        foreach (var stone in step.Position.Stones)
        {
            if (stone.Point == point)
            {
                return stone.Color;
            }
        }

        return StoneColor.Empty;
    }

    /// <summary>Разбирает подпись точки.</summary>
    /// <param name="text">Подпись вида «D6».</param>
    /// <param name="size">Размер доски.</param>
    /// <param name="point">Разобранная точка.</param>
    /// <returns><c>true</c>, если подпись разобрана и точка на доске.</returns>
    private static bool TryParse(string text, int size, out Point point)
    {
        const string letters = "ABCDEFGHJKLMNOPQRST";
        var column = letters.IndexOf(text[0]);
        point = default;

        if (column < 0 || !int.TryParse(text[1..], out var row) || column >= size || row < 1 || row > size)
        {
            return false;
        }

        point = new Point((byte)column, (byte)(row - 1));

        return true;
    }

    /// <summary>Ищет слово о цвете вплотную перед точкой.</summary>
    /// <param name="text">Текст до точки.</param>
    /// <returns>«б» для белых, «ч» для чёрных или <c>null</c>.</returns>
    private static char? ColourBefore(string text)
    {
        var match = ColourPattern.Match(text);

        return match.Success ? match.Groups["colour"].Value[0] is 'б' or 'Б' ? 'б' : 'ч' : null;
    }

    /// <summary>Находит подписи точек в тексте.</summary>
    /// <param name="text">Текст.</param>
    /// <returns>Подписи.</returns>
    private static IReadOnlyList<string> Points(string text) =>
        PointPattern.Matches(text).Select(match => match.Groups["point"].Value).ToList();
}
