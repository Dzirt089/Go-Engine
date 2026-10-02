using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Предохранители перебора: цена предложения мёртвых и границы двух бюджетов.</summary>
/// <remarks>
/// <para>
/// Бюджеты (<see cref="Endgame.EstimateNodesPerPosition"/> для оценки идущей партии и
/// <see cref="Endgame.MaxNodesPerPosition"/> для окончательного предложения) — предохранители
/// от патологической доски, а не ограничители качества: без общего предела стоимость предложения
/// равна сумме по всем группам-кандидатам.
/// </para>
/// <para>
/// Здесь проверяются обе стороны: при исчерпанном бюджете предложение пусто (не доказано —
/// значит, не помечено), а на корпусе обоих бюджетов не срабатывает вовсе: предложение с бюджетом
/// и без него совпадает камень в камень.
/// </para>
/// <para>
/// Запас проверяется по корпусу тестовых позиций и по измеренному максимуму: самая дорогая позиция
/// реальной партии 19×19 стоит 7 761 узел (D-068). Измерять её здесь заново значило бы платить
/// две секунды за тест; число записано как доказанный максимум, и тест держит запас относительно
/// него.
/// </para>
/// </remarks>
public sealed class DeadProposalBudgetTests
{
    /// <summary>Бюджет, которого не хватает даже на первый узел доказательства.</summary>
    private const int ExhaustedBudget = 1;

    /// <summary>Самая дорогая измеренная позиция корпуса: партия 19×19 после 360 ходов (D-068).</summary>
    private const int MeasuredCorpusMaxNodes = 7_761;

    [Fact]
    public void Предохранитель_При_Исчерпанном_Бюджете_Ничего_Не_Предлагает()
    {
        // Упор в бюджет — «не доказано», а не догадка: пустой список безопаснее ложного приговора.
        Assert.Empty(Endgame.ProposeDead(DeadCornerBoard(), ExhaustedBudget));
    }

    [Fact]
    public void Предохранитель_Сообщает_Потраченные_Узлы()
    {
        _ = Endgame.ProposeDead(DeadCornerBoard(), Endgame.MaxNodesPerPosition, out var nodes);

        Assert.True(nodes > 0);
    }

    [Fact]
    public void Оценочный_Предохранитель_Не_Срабатывает_На_Корпусе_Задач()
    {
        // Совпадение камень в камень: если бы бюджет срезал проверку хоть одной группы,
        // списки разошлись бы, и доказанно мёртвая группа осталась бы на доске.
        Assert.All(
            Corpus(),
            item => Assert.Equal(
                Endgame.ProposeDead(item.Board, int.MaxValue),
                Endgame.ProposeDead(item.Board, Endgame.EstimateNodesPerPosition)));
    }

    [Fact]
    public void Окончательный_Предохранитель_Не_Срабатывает_На_Корпусе_Задач()
    {
        Assert.All(
            Corpus(),
            item => Assert.Equal(
                Endgame.ProposeDead(item.Board, int.MaxValue),
                Endgame.ProposeDead(item.Board, Endgame.MaxNodesPerPosition)));
    }

    [Fact]
    public void Оценочный_Предохранитель_Имеет_Пятикратный_Запас()
    {
        // Запас — условие того, что предохранитель не подменяет собой доказательство:
        // измеренный максимум обязан стоить впятеро меньше самого бюджета.
        Assert.True(
            MostExpensiveNodes() * 5 <= Endgame.EstimateNodesPerPosition,
            $"Самая дорогая позиция стоит {MostExpensiveNodes()} узлов при бюджете оценки {Endgame.EstimateNodesPerPosition}.");
    }

    [Fact]
    public void Окончательный_Предохранитель_Имеет_Десятикратный_Запас()
    {
        Assert.True(
            MostExpensiveNodes() * 10 <= Endgame.MaxNodesPerPosition,
            $"Самая дорогая позиция стоит {MostExpensiveNodes()} узлов при окончательном бюджете {Endgame.MaxNodesPerPosition}.");
    }

    /// <summary>Возвращает стоимость самой дорогой позиции корпуса в узлах.</summary>
    /// <returns>Наибольшее из измеренного максимума 19×19 и живого максимума корпуса.</returns>
    private static int MostExpensiveNodes()
    {
        var most = MeasuredCorpusMaxNodes;

        foreach (var item in Corpus())
        {
            _ = Endgame.ProposeDead(item.Board, Endgame.MaxNodesPerPosition, out var nodes);

            if (nodes > most)
            {
                most = nodes;
            }
        }

        return most;
    }

    /// <summary>Собирает позиции корпуса: тестовые, цумэго и задачи библиотеки.</summary>
    /// <returns>Пары «имя позиции + доска».</returns>
    /// <remarks>
    /// Позиции берутся из тех же источников, что и остальные тесты: расстановки правил
    /// (<see cref="TestPositions"/>), цумэго (<see cref="TsumegoPositions"/>) и 36 задач библиотеки.
    /// </remarks>
    private static IReadOnlyList<(string Name, Board Board)> Corpus()
    {
        List<(string, Board)> corpus =
        [
            ("Seki", TestPositions.Seki()),
            ("BlockInAtari", TestPositions.BlockInAtari()),
            ("CapturingRace", TestPositions.CapturingRace()),
            ("SelfAtariPosition", TestPositions.SelfAtariPosition()),
            ("SuicideAtCorner", TestPositions.SuicideAtCorner()),
            ("SuicideWithCapture", TestPositions.SuicideWithCapture()),
            ("BoardWithoutLegalMoves", TestPositions.BoardWithoutLegalMoves()),
            ("DeadCorner", DeadCornerBoard())
        ];

        foreach (var (rows, color, expected) in TsumegoPositions.All)
        {
            corpus.Add(($"Цумэго {color.Name} ход {expected}", TsumegoBuilder.Build(rows)));
        }

        foreach (var problem in ProblemLibrary.All)
        {
            var board = ProblemSetup.Build(problem.Size, problem.Stones);

            if (board.IsSuccess)
            {
                corpus.Add(($"Задача {problem.Id}", board.Value!));
            }
        }

        return corpus.AsReadOnly();
    }

    /// <summary>Строит позицию: белая группа в углу в атари, остальные камни — чёрная стена.</summary>
    /// <returns>Доска 9×9 с доказанно мёртвыми белыми камнями (0,0) и (1,0).</returns>
    private static Board DeadCornerBoard()
    {
        var board = new Board(BoardSize.Size9);

        foreach (var (point, color) in new (Point Point, StoneColor Color)[]
        {
            (new Point(0, 0), StoneColor.White),
            (new Point(1, 0), StoneColor.White),
            (new Point(0, 1), StoneColor.Black),
            (new Point(1, 1), StoneColor.Black),
            (new Point(2, 1), StoneColor.Black),
            (new Point(3, 1), StoneColor.Black),
            (new Point(4, 1), StoneColor.Black),
            (new Point(3, 0), StoneColor.Black),
            (new Point(4, 0), StoneColor.Black)
        })
        {
            board = board.ApplyMove(Move.Play(point, color));
        }

        return board;
    }
}
