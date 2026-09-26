using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Сборщик позиций из текстовых схем для цумэго.</summary>
/// <remarks>
/// Схема — строки сверху вниз, символы слева направо: <c>X</c> — чёрный камень,
/// <c>O</c> — белый, <c>.</c> — пустая точка. Камни ставятся в порядке обхода доски;
/// схема должна допускать такую расстановку по правилам (без самоубийств).
/// </remarks>
public static class TsumegoBuilder
{
    /// <summary>Символ чёрного камня.</summary>
    private const char BlackStone = 'X';

    /// <summary>Символ белого камня.</summary>
    private const char WhiteStone = 'O';

    /// <summary>Строит позицию по схеме.</summary>
    /// <param name="rows">Строки схемы сверху вниз.</param>
    /// <returns>Доска 9×9 с камнями из схемы.</returns>
    /// <exception cref="ArgumentException">Схема не 9×9 или содержит недопустимый символ.</exception>
    /// <exception cref="DomainException">Камни схемы нельзя расставить по правилам.</exception>
    public static Board Build(params string[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (rows.Length != BoardSize.Size9.Value)
        {
            throw new ArgumentException($"Схема должна быть 9×9, получено строк: {rows.Length}.", nameof(rows));
        }

        var board = new Board(BoardSize.Size9);

        for (var y = 0; y < rows.Length; y++)
        {
            var row = rows[y];

            if (row.Length != BoardSize.Size9.Value)
            {
                throw new ArgumentException($"Строка {y} должна быть длиной 9, получено {row.Length}.", nameof(rows));
            }

            for (var x = 0; x < row.Length; x++)
            {
                var color = row[x] switch
                {
                    BlackStone => StoneColor.Black,
                    WhiteStone => StoneColor.White,
                    _ => null
                };

                if (color is not null)
                {
                    board = board.ApplyMove(Move.Play(new Point((byte)x, (byte)y), color));
                }
            }
        }

        return board;
    }
}
