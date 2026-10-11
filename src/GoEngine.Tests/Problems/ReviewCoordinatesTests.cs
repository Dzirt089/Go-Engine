using System.Globalization;
using System.Text.RegularExpressions;
using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Координаты в разборе не спорят с доской: точки существуют, а цвет назван верно.</summary>
/// <remarks>
/// <para>
/// Жалоба пользователя 2026-10-11: «нашёл несоответствие в описании координат камней с настоящим
/// описанием (чёрные B5 и B4, по факту — B5 и E4), и такие ляпы встречаются везде». Текст разбора
/// пишет человек, позицию считает движок: расхождение обязана ловить проверка.
/// </para>
/// <para>
/// Правила проверки осторожные, чтобы не ругаться на верный текст. Точка обязана лежать на доске
/// — это проверяется всегда. Цвет проверяется только там, где слово о цвете стоит вплотную перед
/// точкой («чёрные B5»), и только когда на доске стоит камень <b>другого</b> цвета: пустая точка
/// означает ход из другой части партии (будущий или прошлый), и это не ошибка. Позиция берётся
/// дважды — до хода заметки и после него: заметка говорит и о ходе, и о том, что стоит вокруг.
/// </para>
/// </remarks>
public sealed class ReviewCoordinatesTests
{
    /// <summary>Подпись точки в тексте: буква столбца (без «I») и номер строки.</summary>
    private static readonly Regex PointPattern = new(@"(?<![A-Za-zА-Яа-я])(?<point>[A-HJ-T]\d{1,2})(?![0-9])", RegexOptions.Compiled);

    /// <summary>Слово о цвете вплотную перед точкой.</summary>
    private static readonly Regex ColourPattern = new(@"(?<colour>чёрн\w*|черн\w*|бел\w*)[\s:–—-]{0,3}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Все_Упомянутые_Точки_Лежат_На_Доске()
    {
        List<string> errors = [];

        foreach (var game in ReviewLibrary.All)
        {
            foreach (var note in game.Notes)
            {
                foreach (Match match in PointPattern.Matches(note.Text))
                {
                    if (Parse(match.Groups["point"].Value) is not { } point
                        || point.X >= game.Size.Value
                        || point.Y >= game.Size.Value)
                    {
                        errors.Add($"{game.Id}, заметка «{note.Title}»: точки {match.Groups["point"].Value} нет на доске {game.Size.Value}×{game.Size.Value}");
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void Цвет_Рядом_С_Точкой_Не_Противоречит_Доске()
    {
        List<string> errors = [];

        foreach (var game in ReviewLibrary.All)
        {
            var boards = Positions(game);

            foreach (var note in game.Notes)
            {
                var before = boards[note.Before];
                var after = boards[Math.Min(note.Before + 1, boards.Length - 1)];
                var text = note.Text + " " + note.Title;

                foreach (Match match in PointPattern.Matches(text))
                {
                    if (Parse(match.Groups["point"].Value) is not { } point
                        || point.X >= game.Size.Value
                        || point.Y >= game.Size.Value)
                    {
                        continue;
                    }

                    var colour = ColourBefore(text[..match.Index]);

                    if (colour is null)
                    {
                        continue;
                    }

                    var expected = colour == 'б' ? StoneColor.White : StoneColor.Black;
                    var stoneBefore = before.At(point);
                    var stoneAfter = after.At(point);

                    // Пустая точка — ход из другой части партии: это не ошибка.
                    // Спорен только случай, когда на доске стоит камень другого цвета.
                    var contradicts = (stoneBefore != StoneColor.Empty && stoneBefore != expected)
                        || (stoneAfter != StoneColor.Empty && stoneAfter != expected);

                    if (contradicts)
                    {
                        errors.Add(
                            $"{game.Id}, заметка «{note.Title}» (ход {note.Before}): сказано «{match.Groups["point"].Value}» как {(expected == StoneColor.Black ? "чёрный" : "белый")}, " +
                            $"а на доске там {(stoneBefore == StoneColor.Empty ? "пусто" : stoneBefore == StoneColor.Black ? "чёрный камень" : "белый камень")}");
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    /// <summary>Собирает позиции партии: индекс — число сыгранных ходов.</summary>
    /// <param name="game">Обучающая партия.</param>
    /// <returns>Позиции по ходам, включая начальную.</returns>
    private static Board[] Positions(ReviewGame game)
    {
        var boards = new Board[game.Moves.Count + 1];
        var board = game.StartPosition();
        boards[0] = board;

        for (var index = 0; index < game.Moves.Count; index++)
        {
            board = board.ApplyMove(game.Moves[index]);
            boards[index + 1] = board;
        }

        return boards;
    }

    /// <summary>Превращает подпись точки в координату.</summary>
    /// <param name="text">Подпись вида «D6».</param>
    /// <returns>Точка или <c>null</c>, если подпись не разбирается.</returns>
    private static Point? Parse(string text)
    {
        const string letters = "ABCDEFGHJKLMNOPQRST";
        var column = letters.IndexOf(text[0]);

        if (column < 0 || !int.TryParse(text[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
        {
            return null;
        }

        return new Point((byte)column, (byte)(row - 1));
    }

    /// <summary>Ищет слово о цвете вплотную перед точкой.</summary>
    /// <param name="text">Текст до точки.</param>
    /// <returns>«б» для белых, «ч» для чёрных или <c>null</c>.</returns>
    private static char? ColourBefore(string text)
    {
        var match = ColourPattern.Match(text);

        return match.Success ? match.Groups["colour"].Value[0] is 'б' or 'Б' ? 'б' : 'ч' : null;
    }
}
