namespace GoEngine.Core;

using System.Globalization;

/// <summary>Итог подсчёта партии: камни, территория, пленные, нейтральные точки и коми.</summary>
/// <param name="BlackStones">Чёрные камни на доске после снятия согласованных мёртвых.</param>
/// <param name="BlackTerritory">Пустые точки, окружённые только чёрными, на доске без мёртвых камней.</param>
/// <param name="WhiteStones">Белые камни на доске после снятия согласованных мёртвых.</param>
/// <param name="WhiteTerritory">Пустые точки, окружённые только белыми, на доске без мёртвых камней.</param>
/// <param name="Neutral">Точки, не принадлежащие никому: области на границе обоих цветов и пустая доска.</param>
/// <param name="BlackPrisoners">Камни, захваченные чёрными за партию, вместе со снятыми в конце мёртвыми.</param>
/// <param name="WhitePrisoners">Камни, захваченные белыми за партию, вместе со снятыми в конце мёртвыми.</param>
/// <param name="Komi">Коми партии: прибавляется белым при сравнении величин.</param>
/// <remarks>
/// <para>
/// Итог держит обе системы подсчёта рядом, потому что они отвечают на разные вопросы.
/// По площади (китайские правила, <c>GO_RULES.md</c>, п. 9) считается <see cref="BlackArea"/> и
/// <see cref="WhiteArea"/>: каждый камень и каждая окружённая точка приносят очко, пленные отдельно
/// не нужны — снятый камень уже вернул своё очко как территория. По территории с пленными
/// (японская система) считается <see cref="BlackTerritoryPoints"/> и <see cref="WhiteTerritoryPoints"/>:
/// здесь камень на доске очков не приносит, зато приносит снятый камень.
/// </para>
/// <para>
/// Пленные считаются только один раз: камни, снятые по ходам, приходят из <see cref="GameState"/>,
/// камни, снятые при согласовании мёртвых групп, добавляет <see cref="Endgame.Finalize(Board, IReadOnlyCollection{Point}, Komi, int, int)"/>.
/// Поэтому величину нельзя «доначислить» повторно: <see cref="Endgame.Finalize(Board, IReadOnlyCollection{Point}, Komi, int, int)"/> — чистая функция
/// от переданного списка мёртвых, а не накопитель.
/// </para>
/// <para>
/// Коми не входит ни в одну из величин: оно прибавляется белым в момент сравнения. Так обе
/// системы сравниваются одним и тем же правилом и ни одна из них не прячет коми внутри.
/// </para>
/// </remarks>
public readonly record struct FinalScore(
    int BlackStones,
    int BlackTerritory,
    int WhiteStones,
    int WhiteTerritory,
    int Neutral,
    int BlackPrisoners,
    int WhitePrisoners,
    Komi Komi)
{
    /// <summary>Площадь чёрных: камни плюс территория, без коми.</summary>
    public int BlackArea => BlackStones + BlackTerritory;

    /// <summary>Площадь белых: камни плюс территория, без коми.</summary>
    public int WhiteArea => WhiteStones + WhiteTerritory;

    /// <summary>Территориальные очки чёрных: территория плюс пленные, без коми.</summary>
    public int BlackTerritoryPoints => BlackTerritory + BlackPrisoners;

    /// <summary>Территориальные очки белых: территория плюс пленные, без коми.</summary>
    public int WhiteTerritoryPoints => WhiteTerritory + WhitePrisoners;

    /// <summary>Все точки доски: площадь обоих цветов плюс нейтральные точки.</summary>
    /// <remarks>Сумма сходится и на доске без мёртвых камней, и на доске после их снятия.</remarks>
    public int Total => BlackArea + WhiteArea + Neutral;

    /// <summary>Победитель по площади: канон проекта (<c>GO_RULES.md</c>, п. 9).</summary>
    /// <remarks>Коми прибавляется белым здесь, а не хранится внутри <see cref="WhiteArea"/>.</remarks>
    public StoneColor AreaWinner => Compare(BlackArea, WhiteArea + Komi.Value);

    /// <summary>Победитель по территории с пленными: японская система.</summary>
    /// <remarks>
    /// На доигранной позиции (дамэ заполнены, мёртвые сняты, камней поставлено поровну) эта
    /// величина расходится с площадью ровно на разницу пленных, поэтому победитель тот же.
    /// Расхождение возможно там, где партия не доиграна или камней поставлено не поровну.
    /// </remarks>
    public StoneColor TerritoryWinner => Compare(BlackTerritoryPoints, WhiteTerritoryPoints + Komi.Value);

    /// <summary>Система подсчёта, по которой назван победитель.</summary>
    /// <remarks>
    /// Свойство, а не параметр записи: восемь величин итога — фиксированный контракт, а система
    /// приходит из настроек. Значение по умолчанию — японская система, как в большинстве
    /// турниров РФ (<see cref="ScoringRule.Japanese"/>).
    /// </remarks>
    public ScoringRule Rule { get; init; } = ScoringRule.Japanese;

    /// <summary>Победитель по основной системе подсчёта: по умолчанию японской.</summary>
    /// <remarks>
    /// Основную систему выбирает регламент: <see cref="Rule"/>. У китайской это
    /// <see cref="AreaWinner"/>, у японской — <see cref="TerritoryWinner"/>. Обе величины
    /// посчитаны всегда, поэтому смена системы — это выбор одной из двух, а не пересчёт.
    /// </remarks>
    public StoneColor Winner => Rule == ScoringRule.Chinese ? AreaWinner : TerritoryWinner;

    /// <summary>Разница очков по площади без знака: на сколько победитель опередил соперника.</summary>
    public double AreaMargin => Math.Abs(BlackArea - (WhiteArea + Komi.Value));

    /// <summary>Разница территориальных очков без знака.</summary>
    public double TerritoryMargin => Math.Abs(BlackTerritoryPoints - (WhiteTerritoryPoints + Komi.Value));

    /// <summary>Возвращает итог строкой по основной системе: «Чёрные 14 : 11.5 Белые».</summary>
    /// <returns>Очки обеих сторон вместе с коми у белых по системе <see cref="Rule"/>.</returns>
    /// <remarks>
    /// <para>
    /// Строка показывает основную систему, а не всегда площадь: при японской в ней стоят
    /// территория с пленными, иначе панель партии показывала бы не тот счёт, по которому назван
    /// победитель. Разбор обеих систем целиком даёт интерфейс.
    /// </para>
    /// <para>
    /// Числа печатаются инвариантно: строка счёта уходит и в журнал, и в проверочные режимы,
    /// и в отчёт приёмки, а разделитель дроби не должен зависеть от культуры машины. Строка
    /// сведений панели печатает коми так же (приложение собирается с <c>InvariantGlobalization</c>,
    /// но полагаться на настройку сборки в формате счёта не стоит). Разделитель — точка: так коми
    /// записано в правилах и в файле настроек.
    /// </para>
    /// </remarks>
    public override string ToString()
    {
        var black = (Rule == ScoringRule.Chinese ? BlackArea : BlackTerritoryPoints)
            .ToString(CultureInfo.InvariantCulture);
        var white = (Rule == ScoringRule.Chinese ? WhiteArea + Komi.Value : WhiteTerritoryPoints + Komi.Value)
            .ToString(CultureInfo.InvariantCulture);

        return $"Чёрные {black} : {white} Белые";
    }

    /// <summary>Сравнивает две величины с коми и называет сторону.</summary>
    /// <param name="black">Величина чёрных.</param>
    /// <param name="white">Величина белых уже вместе с коми.</param>
    /// <returns>Победившая сторона или <see cref="StoneColor.Empty"/> при равенстве.</returns>
    private static StoneColor Compare(double black, double white) =>
        black > white ? StoneColor.Black :
        white > black ? StoneColor.White :
        StoneColor.Empty;
}
