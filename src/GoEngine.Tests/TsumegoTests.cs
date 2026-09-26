using GoEngine.AI;
using GoEngine.AI.Mcts;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Цумэго на 9×9: MCTS должен находить решающий ход.</summary>
/// <remarks>
/// Позиции — гонки захвата: доска заполнена почти целиком, у одной стороны одно дамэ,
/// у другой — два. Решающий ход один: он снимает группу противника; второй легальный ход
/// проигрывает, потому что после него противник снимает уже свою группу. Исход решает
/// не подсчёт территории, а единственный выигрывающий ход — это и проверяет тест.
/// </remarks>
public sealed class TsumegoTests
{
    private const int Seed = 20260926;

    /// <summary>Бюджет playout'ов: легальных ходов в позиции мало, хватает и десятка.</summary>
    private const int Budget = 10;

    /// <summary>Чёрные (x = 0…4) против белых (x = 5…8): у белых одно дамэ (4,4), у чёрных — (1,1).</summary>
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

    /// <summary>Чёрные (x = 0…2) против белых (x = 3…8): дамэ белых (2,4), дамэ чёрных (0,1).</summary>
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

    /// <summary>Чёрные (x = 0…5) против белых (x = 6…8): дамэ белых (5,4), дамэ чёрных (1,1).</summary>
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

    /// <summary>Белые (x = 4…8) против чёрных (x = 0…3): у чёрных одно дамэ (4,4), у белых — (7,7).</summary>
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

    [Fact]
    public void Цумэго_Чёрные_Снимают_Белую_Группу()
    {
        var board = TsumegoBuilder.Build(BlackCaptures);

        var move = Selector().SelectMove(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(new Point(4, 4), move.Point);
    }

    [Fact]
    public void Цумэго_Чёрные_Снимают_Большую_Белую_Группу()
    {
        var board = TsumegoBuilder.Build(SmallBlackCapturesBigWhite);

        var move = Selector().SelectMove(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(new Point(2, 4), move.Point);
    }

    [Fact]
    public void Цумэго_Чёрные_Снимают_Малую_Белую_Группу()
    {
        var board = TsumegoBuilder.Build(BigBlackCapturesSmallWhite);

        var move = Selector().SelectMove(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(new Point(5, 4), move.Point);
    }

    [Fact]
    public void Цумэго_Белые_Снимают_Чёрную_Группу()
    {
        var board = TsumegoBuilder.Build(WhiteCaptures);

        var move = Selector().SelectMove(board, StoneColor.White, Komi.For9x9);

        Assert.Equal(new Point(4, 4), move.Point);
    }

    [Fact]
    public void Цумэго_Гонка_Захвата_Решается_Первым_Ходом()
    {
        // Гонка захвата: у обеих сторон единственное дамэ (4,4); кто первый, тот и снимает.
        var board = TestPositions.CapturingRace();

        var move = Selector().SelectMove(board, StoneColor.Black, Komi.For9x9);

        Assert.Equal(new Point(4, 4), move.Point);
    }

    [Fact]
    public void Цумэго_Ошибочный_Ход_Проигрывает()
    {
        // Проверка самой проверки: второй легальный ход чёрных отдаёт партию —
        // белые снимают чёрную группу.
        var board = TsumegoBuilder.Build(BlackCaptures);
        var wrong = board.ApplyMove(Move.Play(new Point(1, 1), StoneColor.Black));
        var answer = wrong.ApplyMove(Move.Play(new Point(4, 4), StoneColor.White));

        Assert.Empty(answer.OccupiedPoints(StoneColor.Black));
    }

    [Fact]
    public void Цумэго_Решающий_Ход_Снимает_Всю_Группу()
    {
        var board = TsumegoBuilder.Build(BlackCaptures);

        var afterCapture = board.ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black));

        Assert.Equal(36, afterCapture.CapturedStones.Count);
    }

    [Fact]
    public void Цумэго_Схема_Строит_Позицию()
    {
        var board = TsumegoBuilder.Build(BlackCaptures);

        // Доска заполнена целиком, кроме двух дамэ: 43 чёрных камня и 36 белых.
        Assert.Equal(79, board.OccupiedPoints(StoneColor.Black).Count() + board.OccupiedPoints(StoneColor.White).Count());
    }

    /// <summary>Создаёт селектор MCTS для цумэго.</summary>
    /// <returns>Селектор с фиксированным зерном и малым бюджетом.</returns>
    private static MctsMoveSelector Selector() =>
        new(new Random(Seed), new PlayoutPolicy(), new MctsConfig(Budget, MctsConfig.DefaultUcb1C));
}
