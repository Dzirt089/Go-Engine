using GoEngine.App.ViewModels;

namespace GoEngine.App.Views;


/// <summary>Строки состояния партии для шапки, шторки и баннера итога.</summary>
/// <remarks>
/// Текст собирается чистыми функциями: правило «что показывать» проверяется тестами, а вид
/// только подставляет готовые строки. Главное правило здесь — пока идёт согласование мёртвых
/// групп (<c>IsCounting</c>), победитель не называется: иначе игрок видит приговор раньше,
/// чем подтвердит подсчёт. Итог партии и предварительный счёт показываются по-разному.
/// Согласование — обязательный шаг по правилам (D-064), но не ошибка: счёт, территория и пленные
/// посчитаны и показаны до него, а строки лишь приглашают проверить пометки и подтвердить их.
/// </remarks>
public static class GameStatusLines
{
    /// <summary>Состояние партии, пока подсчёт не подтверждён: название шага, а не приговор.</summary>
    /// <remarks>
    /// Прежнее «Подсчёт не подтверждён» читалось как сообщение об ошибке (жалоба 2026-10-02),
    /// хотя счёт уже посчитан и показан верно, а от игрока ждут лишь проверки пометок.
    /// </remarks>
    private const string CountingHeadline = "Согласование подсчёта";

    /// <summary>Приглашение проверить пометки мёртвых групп — вторая строка в режиме подсчёта.</summary>
    private const string CountingDetail = "Проверьте пометки мёртвых групп";

    /// <summary>Подсказка панели согласования: что делает щелчок и чего ждут от игрока дальше.</summary>
    /// <remarks>
    /// Текст вынесен из разметки, чтобы его проверяли тесты: прежняя подсказка обещала
    /// «окончательный счёт — по кнопке» и выглядела ошибкой (жалоба 2026-10-02).
    /// </remarks>
    public const string CountingInvitation =
        "Проверьте пометки мёртвых групп: щелчок по камню помечает или снимает пометку. Подтвердите подсчёт, когда пометки верны.";

    /// <summary>Та же подсказка короче — для узкой полосы на телефоне.</summary>
    public const string CountingInvitationShort =
        "Проверьте пометки мёртвых групп: клик по камню помечает или снимает пометку.";

    /// <summary>Название предварительного счёта: счёт показан, но пометки ещё можно изменить.</summary>
    private const string ProvisionalMark = "Предварительный счёт";

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

    /// <summary>Строка счёта: предварительная, пока итог не подтверждён.</summary>
    /// <param name="provisional">
    /// Счёт предварительный: партия идёт (позиция недоиграна) или идёт согласование мёртвых групп
    /// (пометки ещё можно изменить щелчком).
    /// </param>
    /// <param name="score">Счёт партии строкой.</param>
    /// <returns>Строку счёта.</returns>
    /// <remarks>
    /// Пометка — не украшение: во время партии мёртвые камни не сняты и дамэ не заполнены,
    /// а в согласовании счёт меняется от каждого щелчка. Без пометки панель называла недоигранную
    /// позицию окончательным счётом — жалоба пользователя 2026-10-02 «подсчёт ведётся некорректно».
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

    /// <summary>Подпись баннера итога: счёт и оговорка о предварительном подсчёте.</summary>
    /// <param name="isCounting">Идёт согласование мёртвых групп.</param>
    /// <param name="score">Счёт партии строкой.</param>
    /// <returns>Строку под заголовком баннера.</returns>
    /// <remarks>Перевес уже назван в заголовке, поэтому здесь только счёт — без повторов.</remarks>
    public static string ResultDetail(bool isCounting, string score) =>
        isCounting ? $"{ProvisionalMark}: {score}" : $"Счёт: {score}";

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
