using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Согласование подсчёта: пометки мёртвых групп и итог партии в любой позиции.</summary>
/// <remarks>
/// <para>
/// Оценка постоянная (D-070): предложение мёртвых считается для любой позиции, а шага подтверждения
/// нет — партия, завершённая двумя пасами, называет итог сразу. Клик по камню остаётся правом
/// игрока: он правит пометку в любой момент, и счёт пересчитывается тут же.
/// </para>
/// <para>
/// Проверки идут по самой службе, а не через модель представления: так видно, что согласование
/// живёт отдельно от панели партии и его можно проверить на одной позиции. Те же сценарии целиком,
/// через окно и панель, держат <c>EndgameScoreTests</c> и <c>TerritoryTests</c>.
/// </para>
/// </remarks>
public sealed class ScoringAgreementTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Пометки_В_Идущей_Партии_Есть_Сразу()
    {
        // Жалоба пользователя 2026-10-02: мёртвую группу определяет программа, а не человек, —
        // и не только после двух пасов. Игрок ничего не нажимает: пометки стоят с первого чтения.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        Assert.Contains(new Point(0, 0), agreement.DeadPoints);
    }

    [Fact]
    public void Предложение_Считается_Для_Идущей_Партии()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        _ = agreement.DeadPoints;

        Assert.Equal(1, agreement.DeadProposalCount);
    }

    [Fact]
    public void Предложение_Совпадает_С_Перебором_Ядра()
    {
        // Своего «облегчённого» поиска для игры нет: тот же перебор, что и после двух пасов.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.InProgress);

        Assert.Equal(Endgame.ProposeDead(board), agreement.DeadPoints);
    }

    [Fact]
    public void Предложение_Считается_Один_Раз_На_Позицию()
    {
        // Регрессия жалобы 2026-10-02: перебор дорог, и он обязан считаться один раз на позицию,
        // а не на каждое чтение свойства.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        for (var index = 0; index < 50; index++)
        {
            _ = agreement.CurrentScore;
            _ = agreement.DeadPoints;
            _ = agreement.ScoringBoard;
        }

        Assert.Equal(1, agreement.DeadProposalCount);
    }

    [Fact]
    public void Повторная_Синхронизация_Не_Повторяет_Перебор()
    {
        // Позиция та же: доска неизменяема, и её экземпляр — ключ кэша предложения.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);
        _ = agreement.DeadPoints;

        agreement.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(1, agreement.DeadProposalCount);
    }

    [Fact]
    public void Смена_Позиции_Считает_Предложение_Заново()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);
        _ = agreement.DeadPoints;

        agreement.Sync(BoardWithCenterStone(), GameStatus.FinishedByTwoPasses);
        _ = agreement.DeadPoints;

        Assert.Equal(2, agreement.DeadProposalCount);
    }

    [Fact]
    public void Смена_Состояния_Считает_Предложение_Заново()
    {
        // Ключ кэша — пара «доска + состояние»: отмена хода возвращает тот же экземпляр доски,
        // но партия снова идёт, и предложение пересчитывается для нового состояния.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);
        _ = agreement.DeadPoints;

        agreement.Sync(board, GameStatus.InProgress);
        _ = agreement.DeadPoints;

        Assert.Equal(2, agreement.DeadProposalCount);
    }

    [Fact]
    public void Оценочный_Бюджет_Действует_В_Идущей_Партии()
    {
        // Пока партия идёт, предложение — оценка перспективы: предохранитель у неё дешёвый,
        // чтобы перебор на каждом ходу не тормозил игру.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        Assert.Equal(Endgame.EstimateNodesPerPosition, agreement.DeadProposalBudget);
    }

    [Fact]
    public void Окончательный_Бюджет_Действует_В_Завершённой_Партии()
    {
        // После конца партии предложение решает судьбу счёта: предохранитель полный.
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

        Assert.Equal(Endgame.MaxNodesPerPosition, agreement.DeadProposalBudget);
    }

    [Fact]
    public void Итог_После_Двух_Пасов_Считается_Сразу()
    {
        // Мёртвые группы определены программой, и счёт уже включает их пленные: 4 точки территории
        // и 2 снятых белых камня против коми 5.5.
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

        Assert.Equal("Чёрные 6 : 5.5 Белые", agreement.CurrentScore.ToString());
    }

    [Fact]
    public void Клик_После_Конца_Партии_Меняет_Пометку()
    {
        // Правка пометки после конца партии — право игрока: итог уже назван, но его можно уточнить.
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

        Assert.True(agreement.ToggleDeadAt(new Point(8, 8)));
    }

    [Fact]
    public void Клик_После_Конца_Партии_Пересчитывает_Итог()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);
        _ = agreement.CurrentScore;
        var before = agreement.CurrentScore;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.NotEqual(before, agreement.CurrentScore);
    }

    [Fact]
    public void Клик_Снятия_Пометки_После_Конца_Партии_Меняет_Итог()
    {
        // Обратная правка: игрок снимает программную пометку, и счёт возвращается к живому камню.
        var agreement = AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);
        var withDead = agreement.CurrentScore;

        _ = agreement.ToggleDeadAt(new Point(0, 0));

        Assert.NotEqual(withDead, agreement.CurrentScore);
    }

    [Fact]
    public void Снятая_Кликом_Программная_Пометка_Не_Возвращается_Пересчётом()
    {
        // Клик игрока — истина в последней инстанции: снятая пометка не возвращается ни синхронизацией,
        // ни повторным чтением. Без набора снятых камней перебор предложил бы группу снова.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.InProgress);
        _ = agreement.ToggleDeadAt(new Point(0, 0));

        agreement.Sync(board, GameStatus.InProgress);

        Assert.DoesNotContain(new Point(0, 0), agreement.DeadPoints);
    }

    [Fact]
    public void Ручная_Пометка_Добавляется_К_Программной()
    {
        // Игрок вправе помечать и то, чего перебор не доказал: программа и человек работают
        // в одном наборе пометок, и счёт учитывает обе.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.DeadPoints;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Contains(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Ручная_Правка_Переживает_Синхронизацию()
    {
        // Игрок вправе спорить с перебором: живая группа в дальнем углу помечена мёртвой вручную,
        // и пересчёт предложения (та же позиция, то же состояние) обязан оставить пометку в силе.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        agreement.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.Contains(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Клик_Помечает_Всю_Группу()
    {
        // Живая группа белых из двух камней: клик по одному помечает и второй.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Contains(new Point(8, 7), agreement.DeadPoints);
    }

    [Fact]
    public void Повторный_Клик_Снимает_Пометку()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.DoesNotContain(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Клик_По_Пустой_Точке_Ничего_Не_Меняет()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        Assert.False(agreement.ToggleDeadAt(new Point(4, 4)));
    }

    [Fact]
    public void Пометка_Увеличивает_Счётчик_Ревизии()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.DeadPoints;
        var before = agreement.DeadRevision;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(before + 1, agreement.DeadRevision);
    }

    [Fact]
    public void Пометка_Не_Запускает_Перебор_Заново()
    {
        // Позиция от клика не меняется: перебор предложения не повторяется, счёт пересчитывается
        // по уже готовому списку.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.DeadPoints;
        var proposals = agreement.DeadProposalCount;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(proposals, agreement.DeadProposalCount);
    }

    [Fact]
    public void Пометка_Поднимает_Уведомление()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        var raised = 0;
        agreement.Changed += (_, _) => raised++;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Неудачная_Пометка_Не_Поднимает_Уведомление()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        var raised = 0;
        agreement.Changed += (_, _) => raised++;

        _ = agreement.ToggleDeadAt(new Point(4, 4));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Синхронизация_Другой_Позиции_Сбрасывает_Пометки()
    {
        // Мёртвая группа одной позиции ничего не значит в другой: согласование начинается заново.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        agreement.Sync(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.DoesNotContain(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Доска_Подсчёта_Снимает_Помеченные_Камни()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(StoneColor.Empty, agreement.ScoringBoard.At(new Point(8, 8)));
    }

    [Fact]
    public void Пометки_Не_Снимают_Камни_С_Доски_Партии()
    {
        // Доска подсчёта — отдельная доска: партия от согласования не меняется.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.InProgress);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(StoneColor.White, board.At(new Point(8, 8)));
    }

    [Fact]
    public void Доска_Подсчёта_Переиспользуется_Для_Той_Же_Позиции()
    {
        // Оценка постоянная, и панель читает разметку на каждое изменение: сборка доски подсчёта
        // кэшируется по позиции и ревизии пометок, иначе каждое чтение обходило бы группы заново.
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);

        Assert.Same(agreement.ScoringBoard, agreement.ScoringBoard);
    }

    [Fact]
    public void Смена_Пометки_Меняет_Доску_Подсчёта()
    {
        var agreement = AgreementFor(CountingBoard(), GameStatus.InProgress);
        var before = agreement.ScoringBoard;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.NotSame(before, agreement.ScoringBoard);
    }

    [Fact]
    public void Предварительный_Счёт_Меняется_От_Пометок()
    {
        // Счёт считается по пометкам: игрок видит, что именно правит.
        var agreement = AgreementFor(BoardWithCenterStone(), GameStatus.InProgress);
        var before = agreement.CurrentScore;

        _ = agreement.ToggleDeadAt(new Point(4, 4));

        Assert.NotEqual(before, agreement.CurrentScore);
    }

    /// <summary>Создаёт согласование для позиции и состояния партии.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="status">Состояние партии.</param>
    /// <returns>Согласование с расчётом итога без пленных за партию: у этих позиций их нет.</returns>
    private static ScoringAgreement AgreementFor(Board board, GameStatus status) =>
        new(board, status, dead => Endgame.Finalize(board, dead, Komi.For9x9, 0, 0));

    /// <summary>Доска идущей партии с одним камнем чёрных в центре.</summary>
    /// <returns>Позиция, где мёртвых групп нет, но камень для клика есть.</returns>
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
    /// Живая белая группа в дальнем углу — (8,8) и (8,7): по ней проверяется правка кликами.
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
