namespace GoEngine.Core;

using System.Globalization;
using System.Text;

/// <summary>Запись партии в SGF (FF[4]).</summary>
/// <remarks>
/// Поддерживаются свойства <c>GM</c>, <c>FF</c>, <c>CA</c>, <c>SZ</c>, <c>KM</c>, <c>C</c>
/// и ходы <c>B</c>/<c>W</c> — минимальный набор из <c>AGENTS.md</c>, п. 10.
/// Координаты — буквы <c>a</c>…<c>s</c> от левого верхнего угла; пас записывается пустым
/// значением <c>B[]</c>.
/// </remarks>
public static class SgfWriter
{
    /// <summary>Первый символ координатной буквы.</summary>
    private const char FirstCoordinateLetter = 'a';

    /// <summary>Записывает партию в текст SGF.</summary>
    /// <param name="game">Партия.</param>
    /// <returns>Текст SGF одной строкой.</returns>
    public static string Write(SgfGame game)
    {
        var builder = new StringBuilder();

        builder.Append("(;GM[1]FF[4]CA[UTF-8]");
        builder.Append(CultureInfo.InvariantCulture, $"SZ[{game.Size.Value}]");
        builder.Append(CultureInfo.InvariantCulture, $"KM[{game.Komi.Value}]");

        if (!string.IsNullOrEmpty(game.Comment))
        {
            builder.Append(CultureInfo.InvariantCulture, $"C[{Escape(game.Comment)}]");
        }

        foreach (var move in game.Moves)
        {
            builder.Append(CultureInfo.InvariantCulture, $";{Property(move)}[{Value(move)}]");
        }

        return builder.Append(')').ToString();
    }

    /// <summary>Возвращает свойство хода: <c>B</c> для чёрных, <c>W</c> для белых.</summary>
    /// <param name="move">Ход.</param>
    /// <returns>Имя свойства SGF.</returns>
    private static string Property(Move move) => move.Color == StoneColor.Black ? "B" : "W";

    /// <summary>Возвращает значение хода: координаты или пусто для паса.</summary>
    /// <param name="move">Ход.</param>
    /// <returns>Значение свойства SGF.</returns>
    private static string Value(Move move) => move.Type == MoveType.Play
        ? $"{(char)(FirstCoordinateLetter + move.Point.X)}{(char)(FirstCoordinateLetter + move.Point.Y)}"
        : string.Empty;

    /// <summary>Экранирует значение свойства SGF.</summary>
    /// <param name="value">Исходный текст.</param>
    /// <returns>Текст, безопасный внутри квадратных скобок.</returns>
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
}