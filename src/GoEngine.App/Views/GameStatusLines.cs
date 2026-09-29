namespace GoEngine.App.Views;

/// <summary>Строки состояния партии для шапки и шторки.</summary>
/// <remarks>
/// Текст собирается чистыми функциями: правило «что показывать» проверяется тестами, а вид
/// только подставляет готовые строки. Главное правило здесь — пока идёт согласование мёртвых
/// групп (<c>IsCounting</c>), победитель не называется: иначе игрок видит приговор раньше,
/// чем подтвердит подсчёт. Итог партии и предварительный счёт показываются по-разному.
/// </remarks>
public static class GameStatusLines
{
    /// <summary>Состояние партии, пока подсчёт не подтверждён.</summary>
    private const string CountingHeadline = "Подсчёт не подтверждён";

    /// <summary>Приглашение проверить мёртвые группы — вторая строка в режиме подсчёта.</summary>
    private const string CountingDetail = "Проверьте мёртвые группы";

    /// <summary>Строка о состоянии партии: кто ходит, идёт подсчёт или партия окончена.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="hasOutcome">У партии есть итог (в том числе ещё не подтверждённый).</param>
    /// <param name="status">Состояние партии словами: «Идёт», «Завершена двумя пасами» и т. п.</param>
    /// <param name="toMove">Кто ходит: «Чёрные» или «Белые».</param>
    /// <returns>Строку для шапки вида.</returns>
    /// <remarks>
    /// Порядок проверок важен: <paramref name="hasOutcome"/> истинно и во время согласования,
    /// потому что счёт уже посчитан по доске. Поэтому подсчёт проверяется первым.
    /// </remarks>
    public static string Headline(bool isCounting, bool hasOutcome, string status, string toMove)
    {
        if (isCounting)
        {
            return CountingHeadline;
        }

        return hasOutcome ? status : $"Ход: {toMove}";
    }

    /// <summary>Вторая строка состояния: номер хода, приглашение проверить группы или итог.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="outcome">Итог партии словами: «Победили белые» и т. п.</param>
    /// <param name="moveNumber">Номер хода строкой.</param>
    /// <returns>Строку для второй строки шапки.</returns>
    public static string Detail(bool isCounting, bool hasOutcome, string outcome, string moveNumber)
    {
        if (isCounting)
        {
            return CountingDetail;
        }

        return hasOutcome ? outcome : $"Ход №{moveNumber}";
    }

    /// <summary>Строка счёта: в режиме подсчёта счёт предварительный.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="score">Счёт партии строкой.</param>
    /// <returns>Строку счёта.</returns>
    /// <remarks>
    /// Пометка «предварительный» — не украшение: до подтверждения счёт считается по доске
    /// без помеченных камней и меняется от каждого щелчка.
    /// </remarks>
    public static string ScoreLine(bool isCounting, string score) =>
        isCounting ? $"Счёт (предварительный): {score}" : $"Счёт: {score}";

    /// <summary>Показывать ли камень того, кто ходит.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <returns><c>true</c>, если партия ждёт хода.</returns>
    /// <remarks>Во время согласования и после конца партии хода нет: камень очереди не показывается.</remarks>
    public static bool ShowTurnStone(bool isCounting, bool hasOutcome) => !isCounting && !hasOutcome;

    /// <summary>Показывать ли итог партии.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <returns><c>true</c>, если итог можно показывать.</returns>
    /// <remarks>До подтверждения подсчёта итог не показывается: он ещё может измениться.</remarks>
    public static bool ShowOutcome(bool isCounting, bool hasOutcome) => !isCounting && hasOutcome;
}
