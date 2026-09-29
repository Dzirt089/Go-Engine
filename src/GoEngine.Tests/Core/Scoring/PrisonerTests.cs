using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Пленные: сколько камней снял каждый цвет за партию.</summary>
/// <remarks>
/// Жалоба пользователя 1: захваченные камни не участвовали в счёте. Партия обязана помнить,
/// сколько камней снял каждый цвет, — из этого складывается японская величина итога
/// (<see cref="FinalScore.BlackTerritoryPoints"/>) и строка «пленные» в панели партии.
/// Пленные называются по тому, кто их взял: <see cref="GameState.BlackPrisoners"/> — снятые
/// чёрными белые камни. Отмена и возврат хода возвращают счёт вместе с позицией.
/// </remarks>
public sealed class PrisonerTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Пленные_Захват_Увеличивает_Счёт_Захватившего()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));

        // Ход в (0,1) закрывает единственное дамэ белого камня: он снимается и становится пленным.
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        Assert.Equal(1, game.BlackPrisoners);
    }

    [Fact]
    public void Пленные_Захват_Не_Считается_Сопернику()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        Assert.Equal(0, game.WhitePrisoners);
    }

    [Fact]
    public void Пленные_Новая_Партия_Начинает_С_Нуля()
    {
        var game = NewGame();

        Assert.Equal(0, game.BlackPrisoners + game.WhitePrisoners);
    }

    [Fact]
    public void Пленные_Пас_Не_Меняет_Счёт()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        _ = game.Play(Move.Pass(StoneColor.White));

        Assert.Equal(1, game.BlackPrisoners);
    }

    [Fact]
    public void Пленные_Отмена_Хода_Возвращает_Счёт()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        _ = game.Undo();

        Assert.Equal(0, game.BlackPrisoners);
    }

    [Fact]
    public void Пленные_Возврат_Хода_Восстанавливает_Счёт()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));
        _ = game.Undo();

        _ = game.Redo();

        Assert.Equal(1, game.BlackPrisoners);
    }

    [Fact]
    public void Пленные_Оба_Цвета_Считаются_Раздельно()
    {
        // Сначала чёрные снимают белый камень в углу, затем белые снимают чёрный, зажатый
        // в том же углу: (0,0) после первого снятия пуста, поэтому чёрному мало закрыть (2,0).
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(1, 1), StoneColor.White));
        _ = game.Play(Move.Play(new Point(5, 5), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(2, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(5, 6), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));

        Assert.Equal(1, game.WhitePrisoners);
    }

    [Fact]
    public void PrisonersOf_ПоЦвету_Возвращает_Счёт_Цвета()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        Assert.Equal(game.BlackPrisoners, game.PrisonersOf(StoneColor.Black));
    }

    [Fact]
    public void PrisonersOf_Пустой_Цвет_Отказ()
    {
        var game = NewGame();

        Assert.Throws<DomainException>(() => game.PrisonersOf(StoneColor.Empty));
    }

    [Fact]
    public void Пленные_Входят_В_Итог_Партии()
    {
        var game = NewGame();
        _ = game.Play(Move.Play(new Point(1, 0), StoneColor.Black));
        _ = game.Play(Move.Play(new Point(0, 0), StoneColor.White));
        _ = game.Play(Move.Play(new Point(0, 1), StoneColor.Black));

        Assert.Equal(1, game.FinalScore([]).BlackPrisoners);
    }

    /// <summary>Начинает партию 9×9 со стандартным коми.</summary>
    /// <returns>Партия, в которой ходят чёрные.</returns>
    private static GameState NewGame() => GameState.NewGame(Size, Komi.For9x9);
}
