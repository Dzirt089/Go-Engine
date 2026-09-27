using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты разделения зон ответственности AI по размеру доски — T-034, D-038.</summary>
/// <remarks>
/// Сеть обучалась на 19×19, на 9×9 её оценка неправдоподобна (D-034), а обычный MCTS на 19×19
/// слаб из-за бюджета (D-021). Поэтому уровни Дан играют только на больших досках и только
/// с загруженной моделью, а уровни кю — на любой доске.
/// </remarks>
public sealed class NeuralLevelTests
{
    [Fact]
    public void Уровень_Дан_Требует_Сети()
    {
        Assert.True(DifficultyLevel.Dan5.NeedsNetwork);
    }

    [Fact]
    public void Уровень_Кю_Не_Требует_Сети()
    {
        Assert.False(DifficultyLevel.Kyu10.NeedsNetwork);
    }

    [Fact]
    public void Уровень_Дан_На_Доске_9x9_Отказ()
    {
        Assert.Throws<DomainException>(() => DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size9, new FakeEvaluator()));
    }

    [Fact]
    public void Уровень_Дан_Без_Размера_Доски_Отказ()
    {
        Assert.Throws<DomainException>(() => DifficultyLevel.Dan5.CreateSelector(new Random(1), null, new FakeEvaluator()));
    }

    [Fact]
    public void Уровень_Дан_Без_Модели_Отказ()
    {
        Assert.Throws<DomainException>(() => DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size19, null));
    }

    [Fact]
    public void Уровень_Дан_На_Доске_19x19_Создаётся()
    {
        var selector = DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size19, new FakeEvaluator());

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Уровень_Дан_На_Доске_13x13_Создаётся()
    {
        var selector = DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size13, new FakeEvaluator());

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Уровень_Кю_Работает_На_Доске_9x9()
    {
        var selector = DifficultyLevel.Kyu10.CreateSelector(new Random(1), BoardSize.Size9);

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Уровни_Дан_Идут_После_Кю_По_Силе()
    {
        // Ранг отрицательный: Дан выше любого кю.
        Assert.True(DifficultyLevel.Dan5.RankKyu < DifficultyLevel.Kyu5.RankKyu);
    }

    /// <summary>Подставная оценка позиции: тестам не нужна модель.</summary>
    private sealed class FakeEvaluator : IPositionEvaluator
    {
        /// <inheritdoc />
        public PositionEvaluation Evaluate(Board board, StoneColor toMove, Komi komi, IReadOnlyList<Move> moves)
        {
            var area = board.Size.Area;
            var policy = new double[area + 1];
            policy[0] = 1.0;

            return new PositionEvaluation(policy, 0.5, area);
        }

        /// <inheritdoc />
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => false;
    }
}
