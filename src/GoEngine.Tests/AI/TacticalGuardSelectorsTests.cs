using GoEngine.AI;
using GoEngine.Core;

using static GoEngine.Tests.TestPositions;

namespace GoEngine.Tests;

/// <summary>Тесты тактического предохранителя в селекторах: жалоба «кю подставляются под съедание».</summary>
/// <remarks>
/// Предохранитель ищет ходы в поиске, но слабость уровней кю — в случайной ветке: «наугад»
/// раньше брался любой легальный ход, включая самоатари. Здесь проверяется, что случайный ход
/// уровней кю выбирается из тактически безопасных, а партия с историей не ломается суперко.
/// </remarks>
public sealed class TacticalGuardSelectorsTests
{
    /// <summary>Сколько seeds проверяется: случайная ветка должна отсекать подстановку устойчиво.</summary>
    private const int Seeds = 30;


    [Fact]
    public void Случайный_Ход_Mcts_Не_Подставляет_Группу()
    {
        var board = SelfAtariPosition();

        for (var seed = 0; seed < Seeds; seed++)
        {
            var selector = new MctsMoveSelector(
                new Random(seed),
                new PlayoutPolicy(),
                new MctsConfig(1, MctsConfig.DefaultUcb1C) { TacticalGuard = true },
                randomnessPercent: 100);

            var move = selector.SelectMove(board, StoneColor.Black, Komi.For9x9);

            Assert.True(TacticalGuard.IsSafeMove(board, move), $"seed {seed}: ход {move.Point} подставляет группу.");
        }
    }

    [Fact]
    public void Случайный_Ход_Mcts_Подставлял_Группу_Без_Предохранителя()
    {
        // Тест фиксирует сам изъян: без предохранителя случайная ветка играет самоатари.
        var board = SelfAtariPosition();
        var unsafePicks = 0;

        for (var seed = 0; seed < Seeds; seed++)
        {
            var selector = new MctsMoveSelector(
                new Random(seed),
                new PlayoutPolicy(),
                new MctsConfig(1, MctsConfig.DefaultUcb1C) { TacticalGuard = false },
                randomnessPercent: 100);

            if (!TacticalGuard.IsSafeMove(board, selector.SelectMove(board, StoneColor.Black, Komi.For9x9)))
            {
                unsafePicks++;
            }
        }

        Assert.True(unsafePicks > 0, "Без предохранителя случайная ветка ни разу не сыграла самоатари: тест перестал ловить изъян.");
    }

    [Fact]
    public void Случайный_Ход_Эвристики_Не_Подставляет_Группу()
    {
        var board = SelfAtariPosition();
        var unsafePicks = 0;

        for (var seed = 0; seed < Seeds; seed++)
        {
            var selector = new HeuristicMoveSelector(new Random(seed), randomnessPercent: 100, tacticalGuard: true);

            if (!TacticalGuard.IsSafeMove(board, selector.SelectMove(board, StoneColor.Black)))
            {
                unsafePicks++;
            }
        }

        Assert.Equal(0, unsafePicks);
    }

    [Fact]
    public void Случайный_Ход_Эвристики_Подставлял_Группу_Без_Предохранителя()
    {
        var board = SelfAtariPosition();
        var unsafePicks = 0;

        for (var seed = 0; seed < Seeds; seed++)
        {
            var selector = new HeuristicMoveSelector(new Random(seed), randomnessPercent: 100, tacticalGuard: false);

            if (!TacticalGuard.IsSafeMove(board, selector.SelectMove(board, StoneColor.Black)))
            {
                unsafePicks++;
            }
        }

        Assert.True(unsafePicks > 0, "Без предохранителя случайная ветка эвристики ни разу не сыграла самоатари.");
    }

    [Fact]
    public void Партия_Уровня_Кю_Идёт_Без_Отклонённых_Ходов()
    {
        // Регрессия T-037: предохранитель пополнял историю партии, и GameState отклонял ходы
        // движка по суперко — на 15 кю партия обрывалась на втором ходу.
        var level = DifficultyLevel.FromName<DifficultyLevel>(nameof(DifficultyLevel.Kyu15));
        var selector = AiFactory.Create(level, new Random(20260926), BoardSize.Size9);
        var state = GameState.NewGame(BoardSize.Size9, Komi.For9x9);
        var moves = 0;

        while (moves < 40 && !state.Status.IsFinished)
        {
            var move = selector.SelectMove(state);

            Assert.True(state.Play(move).IsSuccess, $"Ход {move.Point} номер {moves + 1} отклонён партией.");
            moves++;
        }

        Assert.Equal(40, moves);
    }
}
