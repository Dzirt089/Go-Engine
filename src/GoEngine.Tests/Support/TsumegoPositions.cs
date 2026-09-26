namespace GoEngine.Tests;

using GoEngine.Core;

/// <summary>Цумэго серии T-019: позиция, цвет хода и решающая точка.</summary>
/// <remarks>Используются стендом замера силы (<see cref="StrengthBenchmark"/>).</remarks>
internal static class TsumegoPositions
{
    private static readonly string[] BlackCaptures =
    [
        "XXXXXOOOO",
        "X.XXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXX.OOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO"
    ];

    private static readonly string[] SmallBlackCapturesBigWhite =
    [
        "XXXOOOOOO",
        ".XXOOOOOO",
        "XXXOOOOOO",
        "XXXOOOOOO",
        "XX.OOOOOO",
        "XXXOOOOOO",
        "XXXOOOOOO",
        "XXXOOOOOO",
        "XXXOOOOOO"
    ];

    private static readonly string[] BigBlackCapturesSmallWhite =
    [
        "XXXXXXOOO",
        "X.XXXXOOO",
        "XXXXXXOOO",
        "XXXXXXOOO",
        "XXXXX.OOO",
        "XXXXXXOOO",
        "XXXXXXOOO",
        "XXXXXXOOO",
        "XXXXXXOOO"
    ];

    private static readonly string[] WhiteCaptures =
    [
        "XXXXOOOOO",
        "XXXXOOOOO",
        "XXXXOOOOO",
        "XXXXOOOOO",
        "XXXX.OOOO",
        "XXXXOOOOO",
        "XXXXOOOOO",
        "XXXXOOO.O",
        "XXXXOOOOO"
    ];

    private static readonly string[] CapturingRace =
    [
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXX.OOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO",
        "XXXXXOOOO"
    ];

    /// <summary>Позиции для замера: схема, цвет хода, решающий ход.</summary>
    internal static readonly (string[] Rows, StoneColor Color, Point Expected)[] All =
    [
        (BlackCaptures, StoneColor.Black, new Point(4, 4)),
        (SmallBlackCapturesBigWhite, StoneColor.Black, new Point(2, 4)),
        (BigBlackCapturesSmallWhite, StoneColor.Black, new Point(5, 4)),
        (WhiteCaptures, StoneColor.White, new Point(4, 4)),
        (CapturingRace, StoneColor.Black, new Point(4, 4))
    ];
}
