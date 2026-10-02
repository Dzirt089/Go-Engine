using GoEngine.App.Services;
using GoEngine.Core;

namespace GoEngine.App.Tests;

/// <summary>Согласование подсчёта: пометки мёртвых групп, предложение перебора и подтверждённый итог.</summary>
/// <remarks>
/// Проверки идут по самой службе, а не через модель представления: так видно, что согласование
/// живёт отдельно от панели партии и его можно проверить на одной позиции. Те же сценарии целиком,
/// через окно и панель, держат <c>EndgameScoreTests</c> и <c>TerritoryTests</c>.
/// </remarks>
public sealed class ScoringAgreementTests
{
    /// <summary>Размер доски проверок.</summary>
    private static readonly BoardSize Size = BoardSize.Size9;

    [Fact]
    public void Идущая_Партия_Не_Начинает_Согласование()
    {
        var agreement = AgreementFor(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.False(agreement.IsCounting);
    }

    [Fact]
    public void Идущая_Партия_Не_Считает_Предложение()
    {
        var agreement = AgreementFor(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.Equal(0, agreement.DeadProposalCount);
    }

    [Fact]
    public void Идущая_Партия_Не_Помечает_Камни()
    {
        // Камень в центре есть, но партия не завершена: помечать мёртвых нечего.
        var agreement = AgreementFor(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.False(agreement.ToggleDeadAt(new Point(4, 4)));
    }

    [Fact]
    public void Согласование_Начинается_После_Двух_Пасов()
    {
        var agreement = Counting();

        Assert.True(agreement.IsCounting);
    }

    [Fact]
    public void Возврат_К_Идущей_Партии_Снимает_Пометки_На_Той_Же_Доске()
    {
        // Регрессия рефакторинга 2026-10-02: отмена хода возвращает тот же экземпляр доски,
        // но партия снова идёт. Кэш предложения, сверявший только доску, возвращал предложение
        // завершённой партии — и пометки мёртвых оставались на играющей доске.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);

        Assert.NotEmpty(agreement.DeadPoints);

        agreement.Sync(board, GameStatus.InProgress);

        Assert.False(agreement.IsCounting);
        Assert.Empty(agreement.DeadPoints);
    }

    [Fact]
    public void Возврат_В_Игру_Не_Считает_Предложение_Заново()
    {
        // Ключ кэша — пара «доска + состояние», поэтому возврат в игру пересчитывает предложение
        // (оно пустое), но счётчик перебора не растёт: перебор идёт только для завершённой партии.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(1, agreement.DeadProposalCount);

        agreement.Sync(board, GameStatus.InProgress);

        Assert.Equal(1, agreement.DeadProposalCount);
    }

    [Fact]
    public void Подтверждение_Доступно_Сразу()
    {
        var agreement = Counting();

        Assert.True(agreement.CanConfirmScore);
    }

    [Fact]
    public void Предложение_Совпадает_С_Перебором_Ядра()
    {
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(Endgame.ProposeDead(board), agreement.DeadPoints);
    }

    [Fact]
    public void Предложение_Считается_Один_Раз_На_Позицию()
    {
        // Регрессия жалобы 2026-10-02: Endgame.ProposeDead перебирает до 20 000 позиций на группу,
        // и он не должен запускаться на каждое чтение свойства — только на смену позиции.
        var agreement = Counting();

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

        agreement.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(1, agreement.DeadProposalCount);
    }

    [Fact]
    public void Смена_Позиции_Считает_Предложение_Заново()
    {
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);

        agreement.Sync(BoardWithCenterStone(), GameStatus.InProgress);
        agreement.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.Equal(2, agreement.DeadProposalCount);
    }

    [Fact]
    public void Клик_Помечает_Всю_Группу()
    {
        // Живая группа белых из двух камней: клик по одному помечает и второй.
        var agreement = Counting();

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Contains(new Point(8, 7), agreement.DeadPoints);
    }

    [Fact]
    public void Повторный_Клик_Снимает_Пометку()
    {
        var agreement = Counting();
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.DoesNotContain(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Клик_По_Пустой_Точке_Ничего_Не_Меняет()
    {
        var agreement = Counting();

        Assert.False(agreement.ToggleDeadAt(new Point(4, 4)));
    }

    [Fact]
    public void Пометка_Увеличивает_Счётчик_Ревизии()
    {
        var agreement = Counting();
        var before = agreement.DeadRevision;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(before + 1, agreement.DeadRevision);
    }

    [Fact]
    public void Пометка_Не_Запускает_Перебор_Заново()
    {
        // Позиция от клика не меняется: перебор предложения не повторяется, счёт пересчитывается
        // по уже готовому списку.
        var agreement = Counting();
        var proposals = agreement.DeadProposalCount;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(proposals, agreement.DeadProposalCount);
    }

    [Fact]
    public void Пометка_Поднимает_Уведомление()
    {
        var agreement = Counting();
        var raised = 0;
        agreement.Changed += (_, _) => raised++;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Неудачная_Пометка_Не_Поднимает_Уведомление()
    {
        var agreement = Counting();
        var raised = 0;
        agreement.Changed += (_, _) => raised++;

        _ = agreement.ToggleDeadAt(new Point(4, 4));

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Синхронизация_Другой_Позиции_Сбрасывает_Пометки()
    {
        // Мёртвая группа одной позиции ничего не значит в другой: согласование начинается заново.
        var agreement = Counting();
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        agreement.Sync(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.Empty(agreement.DeadPoints);
    }

    [Fact]
    public void Правка_Кликов_Не_Закрепляется_Повторной_Синхронизацией()
    {
        // Синхронизация возвращает пометки к предложению перебора: кэш предложения тот же,
        // но список мёртвых собирается из него, а не накапливает клики.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        agreement.Sync(board, GameStatus.FinishedByTwoPasses);

        Assert.DoesNotContain(new Point(8, 8), agreement.DeadPoints);
    }

    [Fact]
    public void Доска_Подсчёта_Снимает_Помеченные_Камни()
    {
        var agreement = Counting();
        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(StoneColor.Empty, agreement.ScoringBoard.At(new Point(8, 8)));
    }

    [Fact]
    public void Пометки_Не_Снимают_Камни_С_Доски_Партии()
    {
        // Доска подсчёта — отдельная доска: партия от согласования не меняется.
        var board = CountingBoard();
        var agreement = AgreementFor(board, GameStatus.FinishedByTwoPasses);

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.Equal(StoneColor.White, board.At(new Point(8, 8)));
    }

    [Fact]
    public void Предварительный_Счёт_Меняется_От_Пометок()
    {
        // До подтверждения счёт считается по пометкам: игрок видит, что именно подтверждает.
        var agreement = Counting();
        var before = agreement.CurrentScore;

        _ = agreement.ToggleDeadAt(new Point(8, 8));

        Assert.NotEqual(before, agreement.CurrentScore);
    }

    [Fact]
    public void Подтверждение_Завершает_Согласование()
    {
        var agreement = Counting();

        _ = agreement.ConfirmScore();

        Assert.False(agreement.IsCounting);
    }

    [Fact]
    public void После_Подтверждения_Пометки_Не_Меняются()
    {
        var agreement = Counting();
        _ = agreement.ConfirmScore();

        Assert.False(agreement.ToggleDeadAt(new Point(0, 0)));
    }

    [Fact]
    public void После_Подтверждения_Пометки_Не_Меняют_Итог()
    {
        var agreement = Counting();
        _ = agreement.ConfirmScore();
        var score = agreement.CurrentScore;

        _ = agreement.ToggleDeadAt(new Point(0, 0));

        Assert.Equal(score, agreement.CurrentScore);
    }

    [Fact]
    public void Повторное_Подтверждение_Ничего_Не_Меняет()
    {
        var agreement = Counting();
        _ = agreement.ConfirmScore();

        Assert.False(agreement.ConfirmScore());
    }

    [Fact]
    public void Идущая_Партия_Не_Подтверждает_Итог()
    {
        var agreement = AgreementFor(BoardWithCenterStone(), GameStatus.InProgress);

        Assert.False(agreement.ConfirmScore());
    }

    [Fact]
    public void Подтверждение_Спрашивает_Итог_Один_Раз()
    {
        // Итог считает партия: подтверждение — единственный вызов расчёта, а не чтение свойства.
        var board = CountingBoard();
        var calls = 0;
        var agreement = new ScoringAgreement(
            board,
            GameStatus.FinishedByTwoPasses,
            dead =>
            {
                calls++;
                return Endgame.Finalize(board, dead, Komi.For9x9, 0, 0);
            });

        _ = agreement.ConfirmScore();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Смена_Системы_Переносит_Подтверждённый_Итог()
    {
        // Величины в подтверждённом итоге уже посчитаны: система выбирает, по какой называть
        // победителя, и без переноса счёт остался бы назван по прежней.
        var agreement = Counting();
        _ = agreement.ConfirmScore();

        agreement.ApplyRule(ScoringRule.Chinese);

        Assert.Equal(ScoringRule.Chinese, agreement.CurrentScore.Rule);
    }

    /// <summary>Создаёт согласование завершённой двумя пасами партии с мёртвой белой группой в углу.</summary>
    /// <returns>Согласование, в котором перебор уже предложил мёртвые камни.</returns>
    private static ScoringAgreement Counting() => AgreementFor(CountingBoard(), GameStatus.FinishedByTwoPasses);

    /// <summary>Создаёт согласование для позиции и состояния партии.</summary>
    /// <param name="board">Позиция.</param>
    /// <param name="status">Состояние партии.</param>
    /// <returns>Согласование с расчётом итога без пленных за партию: у этих позиций их нет.</returns>
    private static ScoringAgreement AgreementFor(Board board, GameStatus status) =>
        new(board, status, dead => Endgame.Finalize(board, dead, Komi.For9x9, 0, 0));

    /// <summary>Доска идущей партии с одним камнем чёрных в центре.</summary>
    /// <returns>Позиция, где согласование ещё не началось, но камень для клика есть.</returns>
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
