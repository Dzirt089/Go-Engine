using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;


/// <summary>Строки состояния партии для шапки, шторки и баннера итога.</summary>
/// <remarks>
/// Текст собирается чистыми функциями: правило «что показывать» проверяется тестами, а вид
/// только подставляет готовые строки. Пока партия идёт, показывается очередь хода и номер хода,
/// а счёт называется предварительным; после конца партии называются состояние и итог (D-070:
/// подтверждения нет, итог называется сразу). Шага согласования, во время которого победитель
/// молчал, больше не существует.
/// </remarks>
public static class GameStatusLines
{
    /// <summary>Название предварительного счёта: счёт показан, но пометки ещё можно изменить.</summary>
    private const string ProvisionalMark = "Предварительный счёт";

    /// <summary>Строка о состоянии партии: кто ходит или чем партия окончена.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="status">Состояние партии словами: «Идёт», «Завершена двумя пасами» и т. п.</param>
    /// <param name="toMove">Кто ходит: «Чёрные» или «Белые».</param>
    /// <returns>Строку для шапки вида.</returns>
    public static string Headline(bool hasOutcome, string status, string toMove) =>
        hasOutcome ? status : $"Ход: {toMove}";

    /// <summary>Вторая строка состояния: номер хода или итог.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="outcome">Итог партии словами: «Победили белые» и т. п.</param>
    /// <param name="moveNumber">Номер хода строкой.</param>
    /// <returns>Строку для второй строки шапки.</returns>
    public static string Detail(bool hasOutcome, string outcome, string moveNumber) =>
        hasOutcome ? outcome : $"Ход №{moveNumber}";

    /// <summary>Строка счёта: предварительная, пока партия идёт.</summary>
    /// <param name="provisional">Счёт предварительный: позиция недоиграна.</param>
    /// <param name="score">Счёт партии строкой.</param>
    /// <returns>Строку счёта.</returns>
    /// <remarks>
    /// Пометка — не украшение: во время партии мёртвые камни не сняты и дамэ не заполнены,
    /// и число ещё изменится. Без пометки панель называла недоигранную позицию окончательным
    /// счётом — жалоба пользователя 2026-10-02 «подсчёт ведётся некорректно».
    /// </remarks>
    public static string ScoreLine(bool provisional, string score) =>
        provisional ? $"{ProvisionalMark}: {score}" : $"Счёт: {score}";

    /// <summary>Строка сведений о партии для панели: соперник, цвет игрока, ход и коми.</summary>
    /// <param name="level">Подпись уровня соперника: «10 кю · перебор».</param>
    /// <param name="playerColor">Цвет игрока: «чёрные» или «белые».</param>
    /// <param name="moveNumber">Номер хода строкой.</param>
    /// <param name="komi">Коми партии строкой.</param>
    /// <returns>Одну короткую строку вместо трёх отдельных.</returns>
    /// <remarks>
    /// Три отдельные строки («Соперник», «Номер хода», «Коми») занимали полпанели и повторяли
    /// то, что уже сказано в шапке доски. Игроку нужна одна строка сведений, а не столбик;
    /// слова выбраны короткие, чтобы строка не переносилась в узкой панели (проверено живым
    /// прогоном 2026-09-30: с «Мой цвет:» строка занимала две строки).
    /// </remarks>
    public static string StatsLine(string level, string playerColor, string moveNumber, string komi) =>
        $"Соперник: {level} · вы: {playerColor} · ход №{moveNumber} · коми {komi}";

    /// <summary>Заголовок баннера итога: выигрыш, проигрыш или ничья — и с каким перевесом.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="outcome">Итог словами: «Победили чёрные».</param>
    /// <param name="margin">Перевес победителя строкой без знака: «7.5».</param>
    /// <param name="tone">Итог глазами игрока: выиграл, проиграл или ничья.</param>
    /// <returns>Крупную строку баннера.</returns>
    /// <remarks>
    /// Строка называется глазами игрока, а не языком протокола: «Победили чёрные» ничего не
    /// говорит тому, кто играет белыми, и заставляет пересчитывать в уме. Поэтому первым словом
    /// идёт «Вы победили» или «Вы проиграли», а цвет победителя назван ниже, в счёте.
    /// </remarks>
    public static string ResultHeadline(bool hasOutcome, string outcome, string margin, GameTone tone)
    {
        _ = outcome;

        if (!hasOutcome)
        {
            return string.Empty;
        }

        return tone switch
        {
            GameTone.Draw => "Ничья",
            GameTone.Win => $"Вы победили · +{margin}",
            GameTone.Loss => $"Вы проиграли · −{margin}",
            _ => string.Empty
        };
    }

    /// <summary>Подпись баннера итога: только исход словами, без счёта.</summary>
    /// <param name="outcome">Итог словами: «Победили белые», «Ничья».</param>
    /// <returns>Строку под заголовком баннера; пустую, пока итог не назван.</returns>
    /// <remarks>
    /// Счёт в подписи убран: строка счёта стоит рядом, и её повторение читалось как два разных
    /// числа (замечание 3 пользователя 2026-10-02). Здесь остаётся исход словами — то, чего
    /// в строке счёта нет.
    /// </remarks>
    public static string ResultDetail(string outcome) => outcome;

    /// <summary>Показывать ли камень того, кто ходит.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <returns><c>true</c>, если партия ждёт хода.</returns>
    /// <remarks>После конца партии хода нет: камень очереди не показывается.</remarks>
    public static bool ShowTurnStone(bool hasOutcome) => !hasOutcome;

    /// <summary>Показывать ли итог партии.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <returns><c>true</c>, если итог можно показывать.</returns>
    /// <remarks>Итог есть — его и показываем: подтверждать его не нужно (D-070).</remarks>
    public static bool ShowOutcome(bool hasOutcome) => hasOutcome;
}
