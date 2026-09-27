using GoEngine.AI;
using GoEngine.App.Services;
using GoEngine.App.ViewModels;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Тесты модели представления партии: пас, снятые камни, выбор доски и уровня.</summary>
/// <remarks>
/// Проверяется то, что видит игрок на панели: статус и итог партии, счёт, снятые камни,
/// подпись уровня и движка, доступность уровней Дан и смена партии при выборе доски или уровня.
/// </remarks>
public sealed class MainViewModelTests
{
    /// <summary>Зерно проверок: партия должна повторяться от запуска к запуску.</summary>
    private const int Seed = 20260926;

    [Fact]
    public void Пас_Передаёт_Ход_И_Получает_Ответ_AI()
    {
        var model = Create();

        Assert.True(model.Pass());
        Assert.Equal("2", model.MoveNumber);
        Assert.Equal("Чёрные", model.ToMove);
    }

    [Fact]
    public void Партия_Завершается_Двумя_Пасами()
    {
        var model = Create();

        var loaded = model.LoadGame(TwoPasses());

        Assert.True(loaded.IsSuccess);
        Assert.Equal("Завершена двумя пасами", model.Status);
    }

    [Fact]
    public void У_Завершённой_Партии_Есть_Итог()
    {
        var model = Create();

        _ = model.LoadGame(TwoPasses());

        Assert.True(model.HasOutcome);
        Assert.NotEmpty(model.Outcome);
    }

    [Fact]
    public void Снятые_Камни_Считаются_За_Партию()
    {
        // Белый камень в углу окружён чёрными: пятый ход снимает его с доски.
        List<Move> moves =
        [
            Move.Play(new Point(1, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(8, 8), StoneColor.Black),
            Move.Play(new Point(8, 0), StoneColor.White),
            Move.Play(new Point(0, 1), StoneColor.Black)
        ];
        var model = Create(StoneColor.White);

        var loaded = model.LoadGame(new SgfGame(BoardSize.Size9, Komi.For9x9, moves, null));

        Assert.True(loaded.IsSuccess);
        Assert.Equal(1, model.CapturedWhite);
        Assert.Equal(0, model.CapturedBlack);
    }

    [Fact]
    public void Подпись_Уровня_Показывает_Движок_Без_Сети()
    {
        var model = Create(level: DifficultyLevel.Kyu20);

        Assert.Equal("20 кю · эвристики", model.LevelDescription);
    }

    [Fact]
    public void Подпись_Уровня_Показывает_Нейросеть()
    {
        var model = Create(level: DifficultyLevel.Dan5, modelSizes: new HashSet<int> { 9 });

        Assert.Equal("5 дан · нейросеть 9×9", model.LevelDescription);
    }

    [Fact]
    public void Уровень_Дан_Доступен_Для_Доски_С_Моделью()
    {
        var model = Create(modelSizes: new HashSet<int> { 9 });

        Assert.Contains(DifficultyLevel.Dan5, model.LevelOptions);
        Assert.False(model.HasLevelHint);
    }

    [Fact]
    public void Без_Модели_Уровень_Дан_Скрыт()
    {
        var model = Create();

        Assert.DoesNotContain(DifficultyLevel.Dan5, model.LevelOptions);
    }

    [Fact]
    public void Без_Модели_Есть_Подсказка_Почему()
    {
        var model = Create();

        Assert.True(model.HasLevelHint);
        Assert.Contains("уровни кю", model.LevelHint!);
    }

    [Fact]
    public void Для_Доски_Без_Модели_Подсказка_Называет_Размер()
    {
        var model = Create(modelSizes: new HashSet<int> { 19 });

        Assert.Contains("9×9", model.LevelHint!);
    }

    [Fact]
    public void Смена_Размер_Доски_Начинает_Новую_Партию()
    {
        var model = Create();
        _ = model.PlayMove(new Point(4, 4));

        model.SelectedSizeIndex = 2;

        Assert.Equal(19, model.Board.Size.Value);
        Assert.Equal("0", model.MoveNumber);
        Assert.Equal(Komi.For19x19.ToString(), model.Komi);
    }

    [Fact]
    public void Смена_Уровня_Начинает_Новую_Партию()
    {
        var model = Create();
        _ = model.PlayMove(new Point(4, 4));

        model.SelectedLevelIndex = 0;

        Assert.Equal("30 кю", model.Level);
        Assert.Equal("0", model.MoveNumber);
    }

    /// <summary>Создаёт модель представления для теста.</summary>
    /// <param name="color">Цвет игрока; по умолчанию чёрные.</param>
    /// <param name="level">Уровень AI; по умолчанию 20 кю — эвристики без поиска.</param>
    /// <param name="modelSizes">Стороны доски, для которых есть модель.</param>
    /// <returns>Модель представления на доске 9×9.</returns>
    private static MainViewModel Create(
        StoneColor? color = null,
        DifficultyLevel? level = null,
        IReadOnlySet<int>? modelSizes = null)
    {
        var settings = AppSettings.From(
            BoardSize.Size9,
            level ?? DifficultyLevel.Kyu20,
            color ?? StoneColor.Black,
            Komi.For9x9);

        return modelSizes is null
            ? new MainViewModel(settings, new Random(Seed))
            : new MainViewModel(settings, new Random(Seed), new FakeEvaluator(), modelSizes);
    }

    /// <summary>Строит партию из двух пасов: по правилам она завершена.</summary>
    /// <returns>Партия с двумя пасами.</returns>
    private static SgfGame TwoPasses() =>
        new(BoardSize.Size9, Komi.For9x9, [Move.Pass(StoneColor.Black), Move.Pass(StoneColor.White)], null);

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
        public bool LoadModelForBoardSize(int boardSize, string modelsDirectory) => true;
    }
}