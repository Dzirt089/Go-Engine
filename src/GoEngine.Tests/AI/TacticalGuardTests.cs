using GoEngine.AI;
using GoEngine.Core;

using static GoEngine.Tests.TestPositions;

namespace GoEngine.Tests;

/// <summary>Тесты тактического предохранителя — жалоба на «подставляется под съедание».</summary>
/// <remarks>
/// Предохранитель отсекает ходы, оставляющие своей группе одно дамэ, но не мешает захватам,
/// вынужденной защите и пасу: без исключений он запретил бы единственные осмысленные ходы
/// в атари.
/// </remarks>
public sealed class TacticalGuardTests
{

    [Fact]
    public void Самоатари_Отклоняется()
    {
        var board = SelfAtariPosition();

        Assert.False(TacticalGuard.IsSafeMove(board, Move.Play(new Point(3, 3), StoneColor.Black)));
    }

    [Fact]
    public void Безопасное_Продление_Принимается()
    {
        var board = SelfAtariPosition();

        Assert.True(TacticalGuard.IsSafeMove(board, Move.Play(new Point(2, 3), StoneColor.Black)));
    }

    [Fact]
    public void Захват_Принимается_Даже_Если_Группа_Худеет()
    {
        // Угловой белый камень (0,0) в атари: чёрный ход (0,1) снимает его и это оправдано.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(0, 0), StoneColor.White))
            .ApplyMove(Move.Play(new Point(1, 0), StoneColor.Black));

        Assert.True(TacticalGuard.IsSafeMove(board, Move.Play(new Point(0, 1), StoneColor.Black)));
    }

    [Fact]
    public void Защита_Своей_Группы_В_Атари_Принимается()
    {
        // Чёрная группа (4,4) в атари: ход в последнее дамэ — вынужденная защита.
        var board = new Board(BoardSize.Size9)
            .ApplyMove(Move.Play(new Point(4, 4), StoneColor.Black))
            .ApplyMove(Move.Play(new Point(3, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(5, 4), StoneColor.White))
            .ApplyMove(Move.Play(new Point(4, 3), StoneColor.White));

        Assert.True(TacticalGuard.IsSafeMove(board, Move.Play(new Point(4, 5), StoneColor.Black)));
    }

    [Fact]
    public void Пас_Не_Трогаем()
    {
        Assert.True(TacticalGuard.IsSafeMove(new Board(BoardSize.Size9), Move.Pass(StoneColor.Black)));
    }

    [Fact]
    public void Проверка_Не_Пополняет_Историю_Партии()
    {
        // Регрессия T-037: предохранитель проверял ход через ApplyMove на доске с историей,
        // а Board.PlaceStone дописывает позицию в PositionHistory. После проверки ход становился
        // «уже встречавшимся», и партия на 15 кю обрывалась на втором ходу: GameState отклонял
        // ходы движка по суперко.
        var state = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var move = Move.Play(new Point(4, 4), StoneColor.Black);
        var board = state.Board;
        var guarded = new List<Move>();

        foreach (var point in board.EmptyPoints())
        {
            guarded.Add(Move.Play(point, StoneColor.Black));
        }

        foreach (var candidate in guarded)
        {
            _ = TacticalGuard.IsSafeMove(board, candidate);
        }

        Assert.True(board.IsLegal(move).IsSuccess);
    }

    [Fact]
    public void Безопасные_Ходы_Фильтруются()
    {
        var board = SelfAtariPosition();
        List<Move> moves =
        [
            Move.Play(new Point(3, 3), StoneColor.Black),
            Move.Play(new Point(2, 3), StoneColor.Black)
        ];

        var safe = TacticalGuard.SafeMoves(board, moves);

        Assert.Single(safe);
        Assert.Equal(new Point(2, 3), safe[0].Point);
    }
}
