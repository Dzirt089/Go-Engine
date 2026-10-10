using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Подсчёт партии: мёртвые группы, найденные программой, и итог по ним.</summary>
/// <remarks>
/// <para>
/// Мёртвые группы определяет <b>только программа</b> (решение 2026-10-10, D-084): перебор
/// <see cref="Endgame.ProposeDead(Board)"/> отвечает на вопрос по правилам, а игрок по доске
/// их не помечает. Жалоба пользователя: «нажмёшь на свои — свои помечаются, чужие — чужие,
/// хоть все можно пометить… это подтасовка фактов». Здесь сторожатся оба следствия этого решения:
/// пометки совпадают с перебором и никакой щелчок по доске их не меняет.
/// </para>
/// <para>
/// Проверки идут по самой службе, а не через модель представления: так видно, что подсчёт живёт
/// отдельно от панели партии и его можно проверить на одной позиции. Те же сценарии целиком,
/// через окно и панель, держат <c>EndgameScoreTests</c> и <c>TerritoryTests</c>.
/// </para>
/// </remarks>
public sealed class ScoringStateTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Пометки_В_Идущей_Партии_Есть_Сразу()
    {
        // Жалоба пользователя 2026-10-02: мёртвую группу определяет программа, а не человек, —
        // и не только после двух пасов. Игрок ничего не нажимает: пометки стоят с первого чтения.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        Assert.Contains(new Point(0, 0), state.DeadPoints);
    }

    [Fact]
    public void Предложение_Считается_Для_Идущей_Партии()
    {
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        _ = state.DeadPoints;

        Assert.Equal(1, state.DeadProposalCount);
    }

    [Fact]
    public void Пометки_Совпадают_С_Перебором_Ядра()
    {
        // Своего «облегчённого» поиска для игры нет: тот же перебор, что и после двух пасов.
        var board = CountingBoard();
        var state = StateFor(board, GameStatus.InProgress);

        Assert.Equal(Endgame.ProposeDead(board), state.DeadPoints);
    }

    [Fact]
    public void Пометки_Одной_Позиции_Не_Зависят_От_Состояния_Партии()
    {
        // Пометки — свойство доски, а не того, идёт партия или кончилась: предохранитель перебора
        // у оценки дешевле, но на этой позиции оба перебора доказывают одно и то же.
        var board = CountingBoard();

        Assert.Equal(
            StateFor(board, GameStatus.InProgress).DeadPoints,
            StateFor(board, GameStatus.FinishedByTwoPasses).DeadPoints);
    }

    [Fact]
    public void Предложение_Считается_Один_Раз_На_Позицию()
    {
        // Регрессия жалобы 2026-10-02: перебор дорог, и он обязан считаться один раз на позицию,
        // а не на каждое чтение свойства.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        for (var index = 0; index < 50; index++)
        {
            _ = state.CurrentScore;
            _ = state.DeadPoints;
            _ = state.ScoringBoard;
        }

        Assert.Equal(1, state.DeadProposalCount);
    }

    [Fact]
    public void Повторная_Синхронизация_Не_Повторяет_Перебор()
    {
        // Позиция та же: доска неизменяема, и её экземпляр — ключ кэша предложения.
        var board = CountingBoard();
        var state = StateFor(board, GameStatus.FinishedByTwoPasses);
        _ = state.DeadPoints;

        state.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(1, state.DeadProposalCount);
    }

    [Fact]
    public void Смена_Позиции_Считает_Предложение_Заново()
    {
        var state = StateFor(CountingBoard(), GameStatus.FinishedByTwoPasses);
        _ = state.DeadPoints;

        state.Sync(BoardWithCenterStone(), GameStatus.FinishedByTwoPasses);
        _ = state.DeadPoints;

        Assert.Equal(2, state.DeadProposalCount);
    }

    [Fact]
    public void Смена_Состояния_Считает_Предложение_Заново()
    {
        // Ключ кэша — пара «доска + состояние»: отмена хода возвращает тот же экземпляр доски,
        // но партия снова идёт, и предложение пересчитывается для нового состояния.
        var board = CountingBoard();
        var state = StateFor(board, GameStatus.FinishedByTwoPasses);
        _ = state.DeadPoints;

        state.Sync(board, GameStatus.InProgress);
        _ = state.DeadPoints;

        Assert.Equal(2, state.DeadProposalCount);
    }

    [Fact]
    public void Оценочный_Бюджет_Действует_В_Идущей_Партии()
    {
        // Пока партия идёт, предложение — оценка перспективы: предохранитель у неё дешёвый,
        // чтобы перебор на каждом ходу не тормозил игру.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        Assert.Equal(Endgame.EstimateNodesPerPosition, state.DeadProposalBudget);
    }

    [Fact]
    public void Окончательный_Бюджет_Действует_В_Завершённой_Партии()
    {
        // После конца партии предложение решает судьбу счёта: предохранитель полный.
        var state = StateFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

        Assert.Equal(Endgame.MaxNodesPerPosition, state.DeadProposalBudget);
    }

    [Fact]
    public void Итог_После_Двух_Пасов_Считается_Сразу()
    {
        // Мёртвые группы определены программой, и счёт уже включает их пленные: 4 точки территории
        // и 2 снятых белых камня против коми 5.5.
        var state = StateFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

        Assert.Equal("Чёрные 6 : 5.5 Белые", state.CurrentScore.ToString());
    }

    [Fact]
    public void Синхронизация_Другой_Позиции_Считает_Пометки_Заново()
    {
        // Мёртвая группа одной позиции ничего не значит в другой: пометки считаются по новой доске.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);
        var withDead = state.DeadPoints;

        state.Sync(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.NotEqual(withDead, state.DeadPoints);
        Assert.Empty(state.DeadPoints);
    }

    [Fact]
    public void Доска_Подсчёта_Снимает_Найденные_Камни()
    {
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        Assert.Equal(StoneColor.Empty, state.ScoringBoard.At(new Point(0, 0)));
    }

    [Fact]
    public void Пометки_Не_Снимают_Камни_С_Доски_Партии()
    {
        // Доска подсчёта — отдельная доска: партия от подсчёта не меняется.
        var board = CountingBoard();
        var state = StateFor(board, GameStatus.InProgress);
        _ = state.DeadPoints;

        Assert.Equal(StoneColor.White, board.At(new Point(0, 0)));
    }

    [Fact]
    public void Доска_Подсчёта_Переиспользуется_Для_Той_Же_Позиции()
    {
        // Оценка постоянная, и панель читает разметку на каждое изменение: сборка доски подсчёта
        // кэшируется по позиции и ревизии пометок, иначе каждое чтение обходило бы группы заново.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);

        Assert.Same(state.ScoringBoard, state.ScoringBoard);
    }

    [Fact]
    public void Смена_Позиции_Меняет_Доску_Подсчёта()
    {
        var state = StateFor(CountingBoard(), GameStatus.InProgress);
        var before = state.ScoringBoard;

        state.Sync(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.NotSame(before, state.ScoringBoard);
    }

    [Fact]
    public void Ревизия_Пометок_Растёт_При_Смене_Позиции()
    {
        // Вид перерисовывает разметку по ревизии: смена позиции обязана её поднять.
        var state = StateFor(CountingBoard(), GameStatus.InProgress);
        _ = state.DeadPoints;
        var before = state.DeadRevision;

        state.Sync(BoardWithCenterStone(), GameStatus.InProgress);
        _ = state.DeadPoints;

        Assert.True(state.DeadRevision > before, $"ревизия {before} → {state.DeadRevision}");
    }

    /// <summary>Создаёт подсчёт для позиции и состояния партии.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="status">Состояние партии.</param>
    /// <returns>Подсчёт с расчётом итога без пленных за партию: у этих позиций их нет.</returns>
    private static ScoringState StateFor(Board board, GameStatus status) =>
        new(board, status, dead => Endgame.Finalize(board, dead, Komi.For9x9, 0, 0));

    /// <summary>Доска идущей партии с одним камнем чёрных в центре.</summary>
    /// <returns>Позиция, где мёртвых групп нет.</returns>
    private static Board BoardWithCenterStone()
    {
        var game = GameState.NewGame(Size, Komi.For9x9);
        _ = game.Play(Move.Play(new Point(4, 4), StoneColor.Black));

        return game.Board;
    }

    /// <summary>Доска партии с мёртвой белой группой в углу, завершённой двумя пасами.</summary>
    /// <returns>Позиция, на которой перебор доказывает смерть группы.</returns>
    private static Board CountingBoard() => DeadCornerGame().ToGameState().Value!.Board;

    /// <summary>Строит партию 9×9 с мёртвой белой группой в углу, завершённую двумя пасами.</summary>
    /// <returns>Прочитанная партия: чёрные закрыли угол, внутри остались два белых камня.</returns>
    /// <remarks>
    /// Позиция: чёрные закрыли угол (2,0), (2,1), (0,2), (1,2), внутри остались два белых камня.
    /// Ход белых в любую из двух пустых точек угла проигрывает: чёрные отвечают и снимают группу.
    /// Живая белая группа в дальнем углу — (8,8) и (8,7).
    /// </remarks>
    private static SgfGame DeadCornerGame() => new(
        Size,
        Komi.For9x9,
        [
            Move.Play(new Point(2, 0), StoneColor.Black),
            Move.Play(new Point(0, 0), StoneColor.White),
            Move.Play(new Point(2, 1), StoneColor.Black),
            Move.Play(new Point(1, 0), StoneColor.White),
            Move.Play(new Point(0, 2), StoneColor.Black),
            Move.Play(new Point(8, 8), StoneColor.White),
            Move.Play(new Point(1, 2), StoneColor.Black),
            Move.Play(new Point(8, 7), StoneColor.White),
            Move.Pass(StoneColor.Black),
            Move.Pass(StoneColor.White)
        ],
        null);
}
