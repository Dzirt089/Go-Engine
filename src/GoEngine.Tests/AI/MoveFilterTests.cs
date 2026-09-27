using GoEngine.AI;
using GoEngine.Core;

using static GoEngine.Tests.TestPositions;

namespace GoEngine.Tests;

/// <summary>Тесты фильтра ходов: тактический предохранитель вынесен из селекторов — D-051.</summary>
/// <remarks>
/// Правило «безопасных ходов нет — играй любой» живёт в фильтре, поэтому проверяется здесь,
/// а селекторы только спрашивают фильтр и играют разрешённое.
/// </remarks>
public sealed class MoveFilterTests
{

    [Fact]
    public void Фильтр_Отсекает_Подстановку_И_Оставляет_Безопасный_Ход()
    {
        var board = SelfAtariPosition();
        Move[] candidates = [Move.Play(new Point(3, 3), StoneColor.Black), Move.Play(new Point(2, 3), StoneColor.Black)];

        var allowed = TacticalMoveFilter.Instance.Apply(board, candidates);

        Assert.Equal([new Point(2, 3)], allowed.Moves.Select(move => move.Point));
    }

    [Fact]
    public void Отсечение_Не_Считается_Отсутствием_Ограничений()
    {
        var board = SelfAtariPosition();
        Move[] candidates = [Move.Play(new Point(3, 3), StoneColor.Black), Move.Play(new Point(2, 3), StoneColor.Black)];

        Assert.False(TacticalMoveFilter.Instance.Apply(board, candidates).EverythingAllowed);
    }

    [Fact]
    public void Из_Одних_Подстановок_Фильтр_Разрешает_Всё()
    {
        // Список кандидатов — это то, что селектор реально может сыграть: если безопасного среди
        // них нет, вынужденный пас хуже подстановки, поэтому ограничений не остаётся.
        var board = SelfAtariPosition();
        Move[] candidates = [Move.Play(new Point(3, 3), StoneColor.Black)];

        var allowed = TacticalMoveFilter.Instance.Apply(board, candidates);

        Assert.True(allowed.EverythingAllowed);
    }

    [Fact]
    public void Без_Предохранителя_Ограничений_Нет()
    {
        var board = SelfAtariPosition();
        Move[] candidates = [Move.Play(new Point(3, 3), StoneColor.Black)];

        var allowed = MoveFilters.Guarded(false).Apply(board, candidates);

        Assert.True(allowed.EverythingAllowed);
    }

    [Fact]
    public void Фильтр_Без_Ограничений_Возвращает_Тот_Же_Список()
    {
        var board = new Board(BoardSize.Size9);
        Move[] candidates = [Move.Play(new Point(3, 3), StoneColor.Black)];

        var allowed = MoveFilters.None.Apply(board, candidates);

        Assert.Same(candidates, allowed.Moves);
    }

    [Fact]
    public void Фильтр_По_Признаку_Выбирает_Тактический()
    {
        Assert.Same(TacticalMoveFilter.Instance, MoveFilters.Guarded(enabled: true));
    }

    [Fact]
    public void Фильтр_По_Признаку_Выбирает_Пустой()
    {
        Assert.Same(MoveFilters.None, MoveFilters.Guarded(enabled: false));
    }

    [Fact]
    public void Фильтр_Требует_Позицию()
    {
        Assert.Throws<ArgumentNullException>(() => TacticalMoveFilter.Instance.Apply(null!, []));
    }
}
