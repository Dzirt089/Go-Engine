using Microsoft.Extensions.Logging;

namespace GoEngine.App.Services.Logging;

/// <summary>Сообщения лога приложения: тексты собраны в одном месте через генератор.</summary>
/// <remarks>
/// Только <c>[LoggerMessage]</c> (<c>AGENTS.md</c>, п. 7): строки не склеиваются вручную, поэтому
/// и формат, и уровень видны рядом, а сам вызов не аллоцирует лишнего. Логи живут только в слое
/// <c>App</c>: в <c>Core</c> и <c>AI</c> их нет и быть не должно.
/// </remarks>
public static partial class AppLogMessages
{
    /// <summary>Запуск приложения: версия, система, среда и каталог логов.</summary>
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Запуск приложения: версия {Version}, система {Os}, среда {Runtime}, каталог логов {Directory}")]
    public static partial void AppStarting(ILogger logger, string version, string os, string runtime, string directory);

    /// <summary>Завершение приложения.</summary>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Приложение завершено, код возврата {ExitCode}")]
    public static partial void AppFinished(ILogger logger, int exitCode);

    /// <summary>Логирование в файл недоступно.</summary>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Логирование в файл недоступно: {Reason}")]
    public static partial void FileLoggingUnavailable(ILogger logger, string reason);

    /// <summary>Найдена запись о сбое прошлого запуска.</summary>
    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Прошлый запуск завершился сбоем {Moment}: {Reason}; лог: {CrashFile}")]
    public static partial void PreviousCrashFound(ILogger logger, string moment, string reason, string crashFile);

    /// <summary>Начата новая партия.</summary>
    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Новая партия: доска {Size}, уровень {Level}, цвет игрока {Color}, коми {Komi}")]
    public static partial void NewGame(ILogger logger, string size, string level, string color, string komi);

    /// <summary>Сделал ход игрок или соперник.</summary>
    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "Ход {Number}: {Color} {Point}")]
    public static partial void MovePlayed(ILogger logger, int number, string color, string point);

    /// <summary>Пропуск хода.</summary>
    [LoggerMessage(EventId = 1012, Level = LogLevel.Information, Message = "Ход {Number}: {Color} пас")]
    public static partial void MovePassed(ILogger logger, int number, string color);

    /// <summary>Ход отклонён правилами.</summary>
    [LoggerMessage(EventId = 1013, Level = LogLevel.Warning, Message = "Ход {Color} {Point} отклонён: {Reason}")]
    public static partial void MoveRejected(ILogger logger, string color, string point, string reason);

    /// <summary>Ход соперника: движок уровня и время поиска.</summary>
    [LoggerMessage(EventId = 1014, Level = LogLevel.Information, Message = "Соперник: {Color} {Point}, движок {Engine}, поиск {Milliseconds} мс")]
    public static partial void AiMovePlayed(ILogger logger, string color, string point, string engine, double milliseconds);

    /// <summary>Поиск хода соперника отменён.</summary>
    [LoggerMessage(EventId = 1015, Level = LogLevel.Information, Message = "Поиск хода соперника отменён, партия не изменилась")]
    public static partial void AiSearchCancelled(ILogger logger);

    /// <summary>Партия завершена.</summary>
    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "Партия завершена: {Status}, счёт {Score}, ходов {Moves}")]
    public static partial void GameFinished(ILogger logger, string status, string score, int moves);

    /// <summary>Смена настроек партии.</summary>
    [LoggerMessage(EventId = 1017, Level = LogLevel.Information, Message = "Настройки партии: доска {Size}, уровень {Level}, цвет {Color}, коми {Komi}")]
    public static partial void SettingsApplied(ILogger logger, string size, string level, string color, string komi);

    /// <summary>Партия сохранена в SGF.</summary>
    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "Партия сохранена: {Path}, ходов {Moves}")]
    public static partial void SgfSaved(ILogger logger, string path, int moves);

    /// <summary>Партия загружена из SGF.</summary>
    [LoggerMessage(EventId = 1021, Level = LogLevel.Information, Message = "Партия загружена: {Path}, ходов {Moves}")]
    public static partial void SgfLoaded(ILogger logger, string path, int moves);

    /// <summary>Сохранение или загрузка SGF не удались.</summary>
    [LoggerMessage(EventId = 1022, Level = LogLevel.Warning, Message = "{Operation}: {Reason}")]
    public static partial void SgfFailed(ILogger logger, string operation, string reason);

    /// <summary>Переключение режима приложения.</summary>
    [LoggerMessage(EventId = 1030, Level = LogLevel.Information, Message = "Режим приложения: {Mode}")]
    public static partial void ModeChanged(ILogger logger, string mode);

    /// <summary>Задача решена в режиме задач.</summary>
    [LoggerMessage(EventId = 1031, Level = LogLevel.Information, Message = "Задача {ProblemId} решена, ходов {Moves}")]
    public static partial void ProblemSolved(ILogger logger, string problemId, int moves);

    /// <summary>Задача не решена: ход не ведёт к цели.</summary>
    [LoggerMessage(EventId = 1032, Level = LogLevel.Information, Message = "Задача {ProblemId}: ход {Point} не решает задачу")]
    public static partial void ProblemMoveRejected(ILogger logger, string problemId, string point);

    /// <summary>Игрок выбрал вид обучения: уроки или разбор партий.</summary>
    [LoggerMessage(EventId = 1043, Level = LogLevel.Information, Message = "Обучение: {Study}")]
    public static partial void StudySelected(ILogger logger, string study);

    /// <summary>Прогресс обучения не записался: отметки потеряются после закрытия.</summary>
    [LoggerMessage(EventId = 1044, Level = LogLevel.Warning, Message = "Прогресс обучения не сохранён: {Reason}")]
    public static partial void ProgressSaveFailed(ILogger logger, string reason);

    /// <summary>Начат разбор обучающей партии.</summary>
    [LoggerMessage(EventId = 1038, Level = LogLevel.Information, Message = "Партия {GameId}: начало разбора")]
    public static partial void ReviewStarted(ILogger logger, string gameId);

    /// <summary>Игрок нашёл ход партии в вопросе разбора.</summary>
    [LoggerMessage(EventId = 1039, Level = LogLevel.Information, Message = "Партия {GameId}: ход {Number} найден")]
    public static partial void ReviewQuizPassed(ILogger logger, string gameId, int number);

    /// <summary>Партия просмотрена до конца.</summary>
    [LoggerMessage(EventId = 1042, Level = LogLevel.Information, Message = "Партия {GameId} просмотрена, ходов {Total}")]
    public static partial void ReviewCompleted(ILogger logger, string gameId, int total);

    /// <summary>Игрок сменил оформление.</summary>
    [LoggerMessage(EventId = 1034, Level = LogLevel.Information, Message = "Оформление: {Theme}")]
    public static partial void ThemeChanged(ILogger logger, string theme);

    /// <summary>Начат урок обучения.</summary>
    [LoggerMessage(EventId = 1035, Level = LogLevel.Information, Message = "Урок {LessonId}: начало")]
    public static partial void LessonStarted(ILogger logger, string lessonId);

    /// <summary>Шаг урока пройден верным ходом.</summary>
    [LoggerMessage(EventId = 1036, Level = LogLevel.Information, Message = "Урок {LessonId}: шаг {Step} пройден")]
    public static partial void LessonStepPassed(ILogger logger, string lessonId, int step);

    /// <summary>Урок пройден до конца.</summary>
    [LoggerMessage(EventId = 1037, Level = LogLevel.Information, Message = "Урок {LessonId} пройден, шагов {Steps}")]
    public static partial void LessonCompleted(ILogger logger, string lessonId, int steps);

    /// <summary>Модели нейросети загружены.</summary>
    [LoggerMessage(EventId = 1040, Level = LogLevel.Information, Message = "Модели загружены: {Directory}, доски {Sizes}")]
    public static partial void ModelsReady(ILogger logger, string directory, string sizes);

    /// <summary>Модели нейросети недоступны.</summary>
    [LoggerMessage(EventId = 1041, Level = LogLevel.Warning, Message = "Модели не загружены: {Reason}")]
    public static partial void ModelsUnavailable(ILogger logger, string reason);

    /// <summary>Проверка обновлений выполнена.</summary>
    [LoggerMessage(EventId = 1050, Level = LogLevel.Information, Message = "Проверка обновлений: установлена {Current}, результат: {Result}")]
    public static partial void UpdateChecked(ILogger logger, string current, string result);

    /// <summary>Найдено обновление.</summary>
    [LoggerMessage(EventId = 1051, Level = LogLevel.Information, Message = "Доступно обновление {Version} ({Size} байт)")]
    public static partial void UpdateAvailable(ILogger logger, string version, long size);

    /// <summary>Проверка обновлений не удалась.</summary>
    [LoggerMessage(EventId = 1052, Level = LogLevel.Warning, Message = "Проверка обновлений не удалась: {Reason}")]
    public static partial void UpdateFailed(ILogger logger, string reason);

    /// <summary>Обновление скачано и передано установщику.</summary>
    [LoggerMessage(EventId = 1053, Level = LogLevel.Information, Message = "Обновление {Version} скачано: {Path}")]
    public static partial void UpdateDownloaded(ILogger logger, string version, string path);

    /// <summary>Сбой в приложении.</summary>
    [LoggerMessage(EventId = 1090, Level = LogLevel.Critical, Message = "СБОЙ ({Source})")]
    public static partial void Crash(ILogger logger, string source, Exception exception);

    /// <summary>Копия лога, снятая при сбое: этот файл игрок и присылает.</summary>
    [LoggerMessage(EventId = 1091, Level = LogLevel.Critical, Message = "Копия лога для отправки: {CrashFile}")]
    public static partial void CrashCopyMade(ILogger logger, string crashFile);
}
