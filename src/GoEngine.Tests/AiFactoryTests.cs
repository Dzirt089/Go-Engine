using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты фабрики селекторов — T-018.</summary>
public sealed class AiFactoryTests
{
    private const int Seed = 20260926;

    [Fact]
    public void Factory_Создаёт_Корректный_Селектор()
    {
        Assert.IsType<HeuristicMoveSelector>(AiFactory.Create(DifficultyLevel.Kyu20, new Random(Seed)));
    }

    [Fact]
    public void Factory_Создаёт_Случайный_Селектор_Для_30_Кю()
    {
        Assert.IsType<RandomMoveSelector>(AiFactory.Create(DifficultyLevel.Kyu30, new Random(Seed)));
    }

    [Fact]
    public void Factory_Создаёт_Mcts_Селектор_Для_5_Кю()
    {
        Assert.IsType<MctsMoveSelector>(AiFactory.Create(DifficultyLevel.Kyu5, new Random(Seed)));
    }

    [Fact]
    public void Factory_Недопустимый_Уровень_Бросает()
    {
        Assert.Throws<ArgumentNullException>(() => AiFactory.Create(null!, new Random(Seed)));
    }

    [Fact]
    public void Factory_Без_Источника_Случайности_Бросает()
    {
        Assert.Throws<ArgumentNullException>(() => AiFactory.Create(DifficultyLevel.Kyu20, null!));
    }

    [Fact]
    public void Factory_Вид_Селектора_Соответствует_Уровню()
    {
        var selector = AiFactory.Create(DifficultyLevel.Kyu10, new Random(Seed));

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Factory_Селектор_Играет_Легальный_Ход()
    {
        var game = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var selector = AiFactory.Create(DifficultyLevel.Kyu20, new Random(Seed));

        Assert.True(game.Board.IsLegal(selector.SelectMove(game)).IsSuccess);
    }

    [Fact]
    public void Factory_Имя_Селектора_Заполнено()
    {
        Assert.False(string.IsNullOrWhiteSpace(AiFactory.Create(DifficultyLevel.Kyu25, new Random(Seed)).Name));
    }
}