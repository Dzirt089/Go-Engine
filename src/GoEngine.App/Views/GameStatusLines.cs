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
    private const string ProvisionalMark = "Счёт пока примерный";

    /// <summary>Признак конца партии для шапки и панели.</summary>
    /// <remarks>
    /// Слова «Завершена двумя пасами» называют способ, а не состояние: игроку после диалога итога
    /// нужно видеть, что партия окончена, и кто в ней победил. Без этого признака шапка пустела,
    /// и результат было не посмотреть (дефект 2 QA 2026-10-17).
    /// </remarks>
    public const string FinishedMark = "Партия окончена";

    /// <summary>Строка о состоянии партии: кто ходит или чем партия окончена.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="status">Состояние партии словами: «Идёт», «Завершена двумя пасами» и т. п.</param>
    /// <param name="toMove">Кто ходит: «Чёрные» или «Белые».</param>
    /// <returns>Строку для шапки вида.</returns>
    public static string Headline(bool hasOutcome, string status, string toMove) =>
        hasOutcome ? status : $"Ход: {toMove}";

    /// <summary>Строка оконченной партии: признак конца и итог словами.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="outcome">Итог партии словами: «Победили белые», «Ничья».</param>
    /// <returns>Строку «Партия окончена · Победили белые»; пустую, пока итога нет.</returns>
    /// <remarks>
    /// Счёт в строку не входит: он стоит отдельной строкой счёта рядом, и его повтор читался бы
    /// как два разных числа. В шапке телефона строка обрезается многоточием, поэтому первым
    /// словом идёт признак конца, а итог — сразу за ним.
    /// </remarks>
    public static string FinishedLine(bool hasOutcome, string outcome) =>
        hasOutcome ? $"{FinishedMark} · {outcome}" : string.Empty;

    /// <summary>Признак закрытой, но недоигранной партии: счёт не принят.</summary>
    /// <remarks>
    /// Победитель в этом состоянии не называется: на почти пустой доске подсчёт отдал бы всю
    /// доску одному цвету, и итог был бы неправдой (N2 отчёта QA 2026-10-17). Игроку при этом
    /// важно видеть, что партия закрыта, а не идёт: иначе он ищет, чей ход.
    /// </remarks>
    public const string UnfinishedMark = "Партия недоиграна";

    /// <summary>Способ завершения партии одним коротким словом.</summary>
    /// <param name="status">Состояние партии словами: «Завершена сдачей» и т. п.</param>
    /// <returns>«сдача», «два паса», «лимит ходов», «по согласию» или пустую строку, пока партия идёт.</returns>
    /// <remarks>
    /// Слова состояния приходят из <see cref="MainViewModel.Status"/>. Совпадения проверяются
    /// тестом на настоящих состояниях партии: если подпись там изменится, тест укажет на это,
    /// и разбор итога не разойдётся с состоянием.
    /// </remarks>
    public static string FinishReason(string status) => status switch
    {
        "Завершена сдачей" => "сдача",
        "Завершена двумя пасами" => "два паса",
        "Завершена по лимиту ходов" => "лимит ходов",
        "Завершена по согласию сторон" => "по согласию",
        _ => string.Empty
    };

    /// <summary>Короткий итог одной фразой для свёрнутой шторки телефона.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="needsScoreConfirmation">Счёт не принят: позиция явно недоиграна.</param>
    /// <param name="outcome">Итог партии словами: «Победили белые», «Ничья».</param>
    /// <param name="status">Состояние партии словами: «Завершена сдачей» и т. п.</param>
    /// <returns>Строку «Победили белые · сдача»; пустую, пока итога нет.</returns>
    /// <remarks>
    /// Одна строка вместо трёх (состояние, способ завершения, победитель): ребёнку нужно понять
    /// исход, а не разобрать протокол. Пока счёт не принят, называть победителя нельзя — строка
    /// так и говорит: «Счёт не принят».
    /// </remarks>
    public static string FinalLine(bool hasOutcome, bool needsScoreConfirmation, string outcome, string status)
    {
        if (!hasOutcome)
        {
            return string.Empty;
        }

        if (needsScoreConfirmation)
        {
            return outcome;
        }

        var reason = FinishReason(status);

        return reason.Length == 0 ? outcome : $"{outcome} · {reason}";
    }

    /// <summary>Короткая шапка телефона: кто ходит или что партия закончена.</summary>
    /// <param name="hasOutcome">У партии есть итог.</param>
    /// <param name="needsScoreConfirmation">Счёт не принят: позиция явно недоиграна.</param>
    /// <param name="network">Партия сетевая.</param>
    /// <param name="plainStatus">Состояние словами: «Твой ход», «Ход Лиса», «Пауза».</param>
    /// <returns>Строку шапки телефона.</returns>
    /// <remarks>
    /// После финала шапка не повторяет итог из шторки: она говорит только «Партия окончена».
    /// Иначе «Партия окончена» и «Победили белые» стояли бы рядом дважды, и строка обрезалась бы
    /// многоточием на 360 точках (жалоба пользователя 2026-11-09: стена текста после партии).
    /// </remarks>
    public static string MobileHeadline(bool hasOutcome, bool needsScoreConfirmation, bool network, string plainStatus) =>
        hasOutcome
            ? (needsScoreConfirmation ? UnfinishedMark : FinishedMark)
            : $"{(network ? "С другом" : "С ИИ")} · {plainStatus}";

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

        // Сдача не имеет перевеса по доске: цифра из счёта только путала бы игрока.
        var suffix = string.IsNullOrEmpty(margin) ? string.Empty : $" · {(tone == GameTone.Win ? "+" : "−")}{margin}";

        return tone switch
        {
            GameTone.Draw => "Ничья",
            GameTone.Win => $"Вы победили{suffix}",
            GameTone.Loss => $"Вы проиграли{suffix}",
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
