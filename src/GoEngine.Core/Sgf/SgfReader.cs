namespace GoEngine.Core.Sgf;

using System.Globalization;
using System.Text;
using GoEngine.Core;

/// <summary>Чтение партии из SGF (FF[4]).</summary>
/// <remarks>
/// Разбирается минимальный набор свойств: <c>SZ</c>, <c>KM</c>, <c>C</c> и ходы <c>B</c>/<c>W</c>.
/// Варианты (<c>(…)</c> внутри узла) пропускаются: в v1 партия линейна. Неизвестные свойства
/// игнорируются — так открываются файлы других программ. Любая непонятная или недопустимая
/// часть файла даёт <see cref="Result{T}"/> с причиной, а не исключение.
/// </remarks>
public static class SgfReader
{
    /// <summary>Первый символ координатной буквы.</summary>
    private const char FirstCoordinateLetter = 'a';

    /// <summary>Читает партию из текста SGF.</summary>
    /// <param name="sgf">Текст файла SGF.</param>
    /// <returns>Партия или причина отказа.</returns>
    /// <exception cref="ArgumentException">Текст пуст или состоит из пробелов.</exception>
    public static Result<SgfGame> Read(string sgf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sgf);

        var reader = new Cursor(sgf);

        if (!reader.TrySkipToNodeStart())
        {
            return Result<SgfGame>.Fail("В файле SGF нет ни одного узла: ожидался символ ';'.");
        }

        var size = 0;
        double? komi = null;
        string? comment = null;
        List<Move> moves = [];

        while (reader.TryReadNode(out var properties))
        {
            foreach (var (name, value) in properties)
            {
                switch (name)
                {
                    case "SZ":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out size))
                        {
                            return Result<SgfGame>.Fail($"Свойство SZ не разобрано: «{value}».");
                        }

                        break;

                    case "KM":
                        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedKomi))
                        {
                            return Result<SgfGame>.Fail($"Свойство KM не разобрано: «{value}».");
                        }

                        komi = parsedKomi;
                        break;

                    case "C" when comment is null:
                        comment = value;
                        break;

                    case "B":
                    case "W":
                        var move = ParseMove(name, value);

                        if (move is null)
                        {
                            return Result<SgfGame>.Fail($"Ход «{name}[{value}]» не разобран.");
                        }

                        moves.Add(move.Value);
                        break;

                    default:
                        // Неизвестные свойства пропускаем: файл мог быть записан другой программой.
                        break;
                }
            }
        }

        if (size == 0)
        {
            return Result<SgfGame>.Fail("В файле SGF нет свойства SZ: размер доски неизвестен.");
        }

        if (size != BoardSize.Size9.Value && size != BoardSize.Size13.Value && size != BoardSize.Size19.Value)
        {
            return Result<SgfGame>.Fail($"Недопустимый размер доски в SGF: {size}.");
        }

        var komiValue = komi ?? Komi.For(new BoardSize((byte)size)).Value;

        if (double.IsNaN(komiValue) || komiValue < 0)
        {
            return Result<SgfGame>.Fail($"Недопустимое коми в SGF: {komiValue}.");
        }

        return Result<SgfGame>.Ok(new SgfGame(
            new BoardSize((byte)size),
            new Komi(komiValue),
            moves.AsReadOnly(),
            comment));
    }

    /// <summary>Разбирает ход из свойства SGF.</summary>
    /// <param name="name">Имя свойства: <c>B</c> или <c>W</c>.</param>
    /// <param name="value">Значение свойства.</param>
    /// <returns>Ход или <c>null</c>, если значение не разобрано.</returns>
    private static Move? ParseMove(string name, string value)
    {
        var color = name == "B" ? StoneColor.Black : StoneColor.White;

        if (value.Length == 0)
        {
            return Move.Pass(color);
        }

        if (value.Length != 2)
        {
            return null;
        }

        var x = value[0] - FirstCoordinateLetter;
        var y = value[1] - FirstCoordinateLetter;

        return x is < 0 or > 24 || y is < 0 or > 24
            ? null
            : Move.Play(new Point((byte)x, (byte)y), color);
    }

    /// <summary>Курсор по тексту SGF: читает узлы и их свойства.</summary>
    private sealed class Cursor
    {
        private readonly string _text;
        private int _position;

        internal Cursor(string text) => _text = text;

        /// <summary>Пропускает пробелы и открывающие скобки до первого узла.</summary>
        /// <returns><c>true</c>, если найден символ начала узла.</returns>
        internal bool TrySkipToNodeStart()
        {
            while (_position < _text.Length)
            {
                if (_text[_position] == ';')
                {
                    return true;
                }

                _position++;
            }

            return false;
        }

        /// <summary>Читает один узел со свойствами.</summary>
        /// <param name="properties">Свойства узла в порядке записи.</param>
        /// <returns><c>true</c>, если узел прочитан.</returns>
        internal bool TryReadNode(out List<(string Name, string Value)> properties)
        {
            properties = [];

            if (!TrySkipToNodeStart())
            {
                return false;
            }

            _position++;

            while (_position < _text.Length)
            {
                SkipWhitespace();

                if (_position >= _text.Length || !char.IsAsciiLetterUpper(_text[_position]))
                {
                    break;
                }

                var name = ReadIdentifier();

                while (_position < _text.Length && _text[_position] == '[')
                {
                    properties.Add((name, ReadValue()));
                }
            }

            // Варианты партии не разбираются: в v1 партия линейна, ветки пропускаются целиком.
            while (SkipVariation())
            {
            }

            return true;
        }

        /// <summary>Пропускает вариант партии вместе с его ходами.</summary>
        /// <returns><c>true</c>, если вариант был пропущен.</returns>
        private bool SkipVariation()
        {
            SkipWhitespace();

            if (_position >= _text.Length || _text[_position] != '(')
            {
                return false;
            }

            var depth = 0;

            while (_position < _text.Length)
            {
                var symbol = _text[_position];

                if (symbol == '[')
                {
                    // Значения в квадратных скобках могут содержать скобки: их нужно пропустить.
                    SkipBracketValue();
                    continue;
                }

                if (symbol == '(')
                {
                    depth++;
                }
                else if (symbol == ')')
                {
                    depth--;

                    if (depth == 0)
                    {
                        _position++;

                        return true;
                    }
                }

                _position++;
            }

            return true;
        }

        /// <summary>Пропускает значение свойства в квадратных скобках.</summary>
        private void SkipBracketValue()
        {
            _position++;

            while (_position < _text.Length && _text[_position] != ']')
            {
                if (_text[_position] == '\\' && _position + 1 < _text.Length)
                {
                    _position++;
                }

                _position++;
            }

            if (_position < _text.Length)
            {
                _position++;
            }
        }

        /// <summary>Пропускает пробельные символы.</summary>
        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        /// <summary>Читает имя свойства из заглавных букв.</summary>
        /// <returns>Имя свойства.</returns>
        private string ReadIdentifier()
        {
            var start = _position;

            while (_position < _text.Length && char.IsAsciiLetterUpper(_text[_position]))
            {
                _position++;
            }

            return _text[start.._position];
        }

        /// <summary>Читает значение свойства в квадратных скобках.</summary>
        /// <returns>Значение без экранирования.</returns>
        private string ReadValue()
        {
            _position++;

            var builder = new StringBuilder();

            while (_position < _text.Length && _text[_position] != ']')
            {
                if (_text[_position] == '\\' && _position + 1 < _text.Length)
                {
                    _position++;
                }

                _ = builder.Append(_text[_position]);
                _position++;
            }

            if (_position < _text.Length)
            {
                _position++;
            }

            return builder.ToString();
        }
    }
}
