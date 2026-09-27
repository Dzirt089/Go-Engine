using GoEngine.AI;
using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты уровней с сетью и профилей моделей — T-034, T-035 (D-038, D-039).</summary>
/// <remarks>
/// Уровни Дан играют только с загруженной моделью, и модель эта зависит от размера доски:
/// для 13×13 и 19×19 — основная, для 9×9 — специализированная. Нет модели под размер —
/// уровень недоступен, и это ожидаемый отказ, а не ошибка программиста.
/// </remarks>
public sealed class NeuralLevelTests
{
    /// <summary>Каталог моделей для тестов: файлов в нём нет, наличие модели решает подстановка.</summary>
    private const string ModelsDirectory = "models";

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
    public void Уровень_Дан_Без_Модели_Отказ()
    {
        Assert.Throws<DomainException>(() =>
            DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size19, null, ModelsDirectory));
    }

    [Fact]
    public void Уровень_Дан_Без_Размера_Доски_Отказ()
    {
        Assert.Throws<DomainException>(() =>
            DifficultyLevel.Dan5.CreateSelector(new Random(1), null, new FakeEvaluator(true), ModelsDirectory));
    }

    [Fact]
    public void Уровень_Дан_Без_Каталога_Моделей_Отказ()
    {
        Assert.Throws<DomainException>(() =>
            DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size19, new FakeEvaluator(true)));
    }

    [Fact]
    public void Уровень_Дан_Без_Модели_Для_Размера_Отказ()
    {
        Assert.Throws<DomainException>(() =>
            DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size9, new FakeEvaluator(false), ModelsDirectory));
    }

    [Fact]
    public void Уровень_Дан_На_Доске_19x19_Создаётся()
    {
        var selector = DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size19, new FakeEvaluator(true), ModelsDirectory);

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Уровень_Дан_На_Доске_9x9_С_Моделью_Создаётся()
    {
        // Специализированная модель для 9×9 делает уровень доступным и на этой доске (D-039).
        var selector = DifficultyLevel.Dan5.CreateSelector(new Random(1), BoardSize.Size9, new FakeEvaluator(true), ModelsDirectory);

        Assert.IsType<MctsMoveSelector>(selector);
    }

    [Fact]
    public void Уровень_Кю_Работает_На_Доске_9x9_Без_Сети()
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
    /// <param name="modelAvailable">Есть ли модель для запрошенного размера доски.</param>
    private sealed class FakeEvaluator(bool modelAvailable) : IPositionEvaluator
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
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => modelAvailable;
    }
}
