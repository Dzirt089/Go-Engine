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

        Assert.Equal("20 кю · эвристики · без поиска", model.LevelDescription);
    }

    [Fact]
    public void Подпись_Уровня_Показывает_Нейросеть()
    {
        var model = Create(level: DifficultyLevel.Dan5, modelSizes: new HashSet<int> { 9 });

        Assert.Equal("5 дан · нейросеть 9×9 · 4 с/ход", model.LevelDescription);
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

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(19)]
    public void Выбор_Уровня_Создаёт_Партию_Именно_С_Этим_Уровнем(int side)
    {
        // Для каждого размера и каждого доступного уровня: выбор в модели представления
        // обязан создать партию ровно с этим уровнем, а подпись — назвать его движок и бюджет.
        var model = Create(size: new BoardSize((byte)side), modelSizes: new HashSet<int> { 9, 13, 19 });
        var options = model.LevelOptions;

        Assert.True(options.Count >= 10, $"уровней для {side}×{side}: {options.Count}");

        for (var index = 0; index < options.Count; index++)
        {
            model.SelectedLevelIndex = index;

            Assert.Equal(options[index], model.CurrentLevel);
            Assert.Equal(LevelChooser.Describe(options[index], model.Board.Size, true), model.LevelDescription);
        }
    }

    [Fact]
    public void Список_Уровней_Не_Зависит_От_Прежнего_Выбора()
    {
        var model = Create(modelSizes: new HashSet<int> { 9 });
        var before = model.LevelOptions.ToArray();

        model.SelectedLevelIndex = 3;
        var afterAnother = model.LevelOptions.ToArray();
        model.SelectedLevelIndex = 0;

        Assert.Equal(before, afterAnother);
        Assert.Equal(before, model.LevelOptions.ToArray());
    }

    [Fact]
    public void Повторный_Выбор_Того_Же_Уровня_Не_Начинает_Новую_Партию()
    {
        var model = Create(modelSizes: new HashSet<int> { 9 });
        _ = model.PlayMove(new Point(4, 4));
        var moves = model.MoveNumber;
        var index = model.SelectedLevelIndex;

        model.SelectedLevelIndex = index;

        Assert.Equal(moves, model.MoveNumber);
    }

    [Fact]
    public void Список_Подписей_Уровней_Не_Пересоздаётся_Без_Причин()
    {
        // Регрессия: подмена ItemsSource заставляла список выбора сбросить индекс и вернуть его
        // в модель представления — партия начиналась не с выбранным уровнем.
        var model = Create(modelSizes: new HashSet<int> { 9 });
        var labels = model.LevelLabels;

        _ = model.PlayMove(new Point(4, 4));

        Assert.Same(labels, model.LevelLabels);
    }

    [Fact]
    public void Лестница_Сети_Доступна_Для_Доски_С_Моделью()
    {
        var model = Create(modelSizes: new HashSet<int> { 9 });

        Assert.Contains(DifficultyLevel.Kyu1, model.LevelOptions);
        Assert.Contains(DifficultyLevel.Dan1, model.LevelOptions);
        Assert.Contains(DifficultyLevel.Dan5, model.LevelOptions);
    }

    [Fact]
    public void Без_Модели_Вся_Лестница_Сети_Скрыта()
    {
        var model = Create();

        Assert.DoesNotContain(DifficultyLevel.Kyu1, model.LevelOptions);
        Assert.DoesNotContain(DifficultyLevel.Dan1, model.LevelOptions);
        Assert.DoesNotContain(DifficultyLevel.Dan5, model.LevelOptions);
    }

    [Fact]
    public void Подписи_Уровней_Называют_Движок_И_Бюджет()
    {
        var model = Create(modelSizes: new HashSet<int> { 9 });
        var options = model.LevelOptions;

        Assert.Equal(options.Count, model.LevelLabels.Count);

        for (var index = 0; index < options.Count; index++)
        {
            var level = options[index];
            var label = model.LevelLabels[index];

            // Список выбора показывает ту же подпись, что и панель партии: движок, размер
            // доски и бюджет (D-045).
            Assert.Equal(LevelChooser.Describe(level, model.Board.Size, true), label);
            Assert.Contains(LevelChooser.Rank(level), label, StringComparison.Ordinal);

            // Бюджет в подписи: у уровня кю с сетью поиск идёт по бюджету сети, у остальных —
            // по бюджету уровня.
            var budget = level.NeuralBudget is { } neural && !level.NeedsNetwork
                ? $"{(int)neural.TotalMilliseconds} мс/ход"
                : LevelChooser.Budget(level);

            Assert.Contains(budget, label, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Смена_Доски_Сохраняет_Уровень_Если_Он_Доступен()
    {
        var model = Create(modelSizes: new HashSet<int> { 9, 13, 19 });
        var options = model.LevelOptions;
        model.SelectedLevelIndex = options.Count - 1;

        Assert.Equal(DifficultyLevel.Dan5, model.CurrentLevel);

        model.SelectedSizeIndex = 2;

        Assert.Equal(19, model.Board.Size.Value);
        Assert.Equal(DifficultyLevel.Dan5, model.CurrentLevel);
    }

    [Fact]
    public void Смена_Доски_Откатывает_Недоступный_Уровень()
    {
        // Модель есть только для 19×19: на 9×9 уровень «5 дан» играть не сможет (D-038).
        var model = Create(
            modelSizes: new HashSet<int> { 19 },
            size: BoardSize.Size19,
            level: DifficultyLevel.Dan5);

        model.SelectedSizeIndex = 0;

        Assert.Equal(9, model.Board.Size.Value);
        Assert.Equal(LevelChooser.Fallback, model.CurrentLevel);
    }

    [Fact]
    public void Сброс_Индекса_Списком_Не_Меняет_Уровень_Партии()
    {
        // Регрессия: подмена ItemsSource сбрасывала индекс, и сброс уходил в модель как выбор.
        var model = Create(modelSizes: new HashSet<int> { 9 });
        var options = model.LevelOptions;
        model.SelectedLevelIndex = options.Count - 1;
        var chosen = model.CurrentLevel;
        var state = new ComboState();

        _ = state.BeginUpdate(model.LevelLabels, model.SelectedLevelIndex);

        // Пока идёт обновление, список сообщает о сбросе индекса — в модель это не попадает.
        if (state.TryAccept(0))
        {
            model.SelectedLevelIndex = 0;
        }

        state.EndUpdate();

        Assert.Equal(chosen, model.CurrentLevel);
    }

    [Fact]
    public void Настройки_Из_Диалога_Применяются_Вместе_С_Уровнем()
    {
        // Сценарий жалобы: в диалоге выбрали «5 дан» и 19×19, а панель показывала другой уровень.
        var model = Create(modelSizes: new HashSet<int> { 9, 13, 19 });

        model.ApplySettings(AppSettings.From(BoardSize.Size19, DifficultyLevel.Dan5, StoneColor.Black, Komi.For19x19));

        Assert.Equal(19, model.Board.Size.Value);
        Assert.Equal(DifficultyLevel.Dan5, model.CurrentLevel);
        Assert.Equal("5 дан · нейросеть 19×19 · 4 с/ход", model.LevelDescription);
        Assert.Equal(model.SelectedLevelIndex, model.LevelOptions.ToList().IndexOf(DifficultyLevel.Dan5));
    }

    /// <summary>Создаёт модель представления для теста.</summary>
    /// <param name="color">Цвет игрока; по умолчанию чёрные.</param>
    /// <param name="level">Уровень AI; по умолчанию 20 кю — эвристики без поиска.</param>
    /// <param name="modelSizes">Стороны доски, для которых есть модель.</param>
    /// <returns>Модель представления на доске 9×9.</returns>
    private static MainViewModel Create(
        StoneColor? color = null,
        DifficultyLevel? level = null,
        IReadOnlySet<int>? modelSizes = null,
        BoardSize? size = null)
    {
        var settings = AppSettings.From(
            size ?? BoardSize.Size9,
            level ?? DifficultyLevel.Kyu20,
            color ?? StoneColor.Black,
            Komi.For(size ?? BoardSize.Size9));

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