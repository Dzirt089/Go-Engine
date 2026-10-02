using System.ComponentModel;
using GoEngine.App.Services.Logging;
using GoEngine.App.Services.Updates;
using GoEngine.Core;
using Microsoft.Extensions.Logging;

namespace GoEngine.App.ViewModels;

/// <summary>Этап работы с обновлением: что показывает приглашение и полоса хода.</summary>
/// <remarks>
/// Тип-перечисление, а не <c>enum</c>: состояния в проекте описываются так же, как
/// <see cref="UpdateStatus"/> (AGENTS.md, п. 2).
/// </remarks>
public sealed class UpdateStage : Enumeration
{
    /// <summary>Ничего не происходит: приглашения нет.</summary>
    public static readonly UpdateStage Idle = new(0, "Ничего");

    /// <summary>Идёт проверка версии.</summary>
    public static readonly UpdateStage Checking = new(1, "Проверка");

    /// <summary>Найдена новая версия: показано приглашение.</summary>
    public static readonly UpdateStage Available = new(2, "Доступно обновление");

    /// <summary>Идёт скачивание: видна полоса хода и проценты.</summary>
    public static readonly UpdateStage Downloading = new(3, "Скачивание");

    /// <summary>Файл скачан: его ставит платформа.</summary>
    public static readonly UpdateStage Installing = new(4, "Установка");

    /// <summary>Готово: установщик запущен или установка завершена.</summary>
    public static readonly UpdateStage Done = new(5, "Готово");

    /// <summary>Не удалось: игроку показывается причина.</summary>
    public static readonly UpdateStage Failed = new(6, "Отказ");

    /// <summary>Создаёт элемент перечисления.</summary>
    /// <param name="id">Числовой идентификатор.</param>
    /// <param name="name">Имя элемента.</param>
    private UpdateStage(int id, string name) : base(id, name)
    {
    }
}

/// <summary>Приглашение обновления и ход загрузки: одно состояние на оболочку и экран настроек.</summary>
/// <remarks>
/// <para>
/// Модель одна на приложение (<c>App.Updates</c>): приглашение в оболочке и блок обновлений
/// в настройках показывают одно и то же, и проверка не ходит в сеть дважды. Логика здесь чистая —
/// ни Avalonia, ни SkiaSharp: окно только показывает готовые строки и числа, поэтому состояния
/// и проценты проверяются тестами и проверочным режимом без экрана.
/// </para>
/// <para>
/// Проверка при запуске тихая: приложение offline-first, и отсутствие сети — обычное дело,
/// а не ошибка игрока. Отказ остаётся в логе (его пишет <see cref="UpdateService"/>), а на экране
/// появляется только тогда, когда обновление действительно есть. «Позже» убирает приглашение
/// до следующего запуска; явная проверка из настроек показывает его снова.
/// </para>
/// </remarks>
public sealed class UpdateViewModel : INotifyPropertyChanged
{
    /// <summary>Строка проверки: её видят и приглашение, и экран настроек.</summary>
    private const string CheckingText = "Проверяю обновление…";

    /// <summary>Подпись установки: измерить её шаги нечем, поэтому без процентов.</summary>
    private const string InstallingText = "Установка…";

    /// <summary>Итог успешной установки: показывается даже тогда, когда установщик молчит.</summary>
    private const string DoneText = "Готово";

    /// <summary>Отмена — не сбой: приглашение остаётся, и попробовать можно снова.</summary>
    private const string CancelledText = "Скачивание отменено.";

    /// <summary>Начало строки отказа: игрок должен видеть, что это отказ, а не ход дела.</summary>
    private const string FailedPrefix = "Не удалось: ";

    /// <summary>Сборка без установщика: проверка обновлений ей недоступна.</summary>
    private const string UnsupportedText = "Проверка обновлений в этой сборке недоступна.";

    /// <summary>Установка без проверки: неизвестно, что и с какой платформы скачивать.</summary>
    private const string NotCheckedText = "Сначала проверьте обновления.";

    /// <summary>Свойства, зависящие от состояния: о них сообщается виду одним списком.</summary>
    /// <remarks>
    /// Сообщаем обо всех сразу: состояние меняется целиком, а вычислять, что именно изменилось,
    /// здесь нечего — свойства выводятся из одного этапа.
    /// </remarks>
    private static readonly string[] PropertyNames =
    [
        nameof(Stage), nameof(IsSupported), nameof(Headline), nameof(StatusLine), nameof(StatusText),
        nameof(HasStatus), nameof(Progress), nameof(Percent), nameof(IsBannerVisible),
        nameof(IsProgressVisible), nameof(IsProgressIndeterminate), nameof(IsUpdateNowVisible),
        nameof(IsCancelVisible), nameof(IsDismissVisible), nameof(DismissLabel)
    ];

    private readonly UpdateService? _updates;

    /// <summary>Логгер обновления: сбои, которых не ждали, заметны только здесь.</summary>
    private readonly ILogger _log = AppLog.For<UpdateViewModel>();

    /// <summary>Найденное обновление: по нему работает кнопка «Обновить сейчас».</summary>
    private UpdateCheck? _available;

    /// <summary>Отмена идущего скачивания: игрок вправе передумать.</summary>
    private CancellationTokenSource? _cancellation;

    private UpdateStage _stage = UpdateStage.Idle;
    private string _headline = string.Empty;
    private string _statusLine = string.Empty;
    private double _progress;

    /// <summary>Создаёт модель обновления.</summary>
    /// <param name="updates">Служба обновления; <c>null</c> — голова её не настроила.</param>
    public UpdateViewModel(UpdateService? updates) => _updates = updates;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Текущий этап работы.</summary>
    public UpdateStage Stage => _stage;

    /// <summary>Проверка обновлений доступна: голова задала установщик и каталог.</summary>
    public bool IsSupported => _updates is not null;

    /// <summary>Заголовок приглашения: «Доступна версия 0.2.0 — у вас 0.1.0».</summary>
    public string Headline => _headline;

    /// <summary>Подробность под заголовком: «Скачивание… 42%», «Установка…», сообщение установщика.</summary>
    public string StatusLine => _statusLine;

    /// <summary>Строка для экрана настроек: подробность, а если её нет — заголовок.</summary>
    public string StatusText => _statusLine.Length > 0 ? _statusLine : _headline;

    /// <summary>Есть ли подробность под заголовком.</summary>
    public bool HasStatus => _statusLine.Length > 0;

    /// <summary>Ход загрузки от 0 до 1.</summary>
    public double Progress => _progress;

    /// <summary>Ход загрузки целыми процентами от 0 до 100.</summary>
    public int Percent => (int)Math.Round(_progress * 100, MidpointRounding.AwayFromZero);

    /// <summary>Приглашение или ход работы видны в оболочке.</summary>
    /// <remarks>
    /// Проверка и «ничего не происходит» приглашения не показывают: при запуске игра не должна
    /// получать карточку о том, что она просто сходила в сеть.
    /// </remarks>
    public bool IsBannerVisible => _stage != UpdateStage.Idle && _stage != UpdateStage.Checking;

    /// <summary>Видна полоса хода: скачивание или установка.</summary>
    public bool IsProgressVisible => _stage == UpdateStage.Downloading || _stage == UpdateStage.Installing;

    /// <summary>Полоса хода без числа: у установки нет измеримых шагов.</summary>
    public bool IsProgressIndeterminate => _stage == UpdateStage.Installing;

    /// <summary>Видна кнопка «Обновить сейчас».</summary>
    public bool IsUpdateNowVisible => _stage == UpdateStage.Available;

    /// <summary>Видна кнопка «Отмена»: только пока идёт скачивание.</summary>
    public bool IsCancelVisible => _stage == UpdateStage.Downloading;

    /// <summary>Видна кнопка, убирающая приглашение.</summary>
    public bool IsDismissVisible =>
        _stage == UpdateStage.Available || _stage == UpdateStage.Done || _stage == UpdateStage.Failed;

    /// <summary>Подпись кнопки, убирающей приглашение.</summary>
    /// <remarks>
    /// До обновления это «Позже» (игрок откладывает), после — «Скрыть»: «Позже» после установки
    /// читалось бы странно.
    /// </remarks>
    public string DismissLabel => _stage == UpdateStage.Available ? "Позже" : "Скрыть";

    /// <summary>Проверяет, есть ли новая версия.</summary>
    /// <param name="silent">Тихая проверка: отказ не показывается игроку (проверка при запуске).</param>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Результат проверки или причина отказа.</returns>
    /// <remarks>
    /// Отказ сети — ожидаемый исход, а не исключение: он приходит значением <see cref="Result{T}"/>.
    /// Тихая проверка оставляет экран как был и пишет причину только в лог.
    /// </remarks>
    public async Task<Result<UpdateCheck>> CheckAsync(bool silent = false, CancellationToken cancellationToken = default)
    {
        if (_updates is null)
        {
            if (!silent)
            {
                SetStage(UpdateStage.Idle, string.Empty, UnsupportedText);
            }

            return Result<UpdateCheck>.Fail(UnsupportedText);
        }

        SetStage(UpdateStage.Checking, CheckingText);

        Result<UpdateCheck> result;

        try
        {
            result = await _updates.CheckAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Отмена — ожидаемый исход: результат проверки больше не нужен.
            SetStage(UpdateStage.Idle, string.Empty);

            return Result<UpdateCheck>.Fail("Проверка обновления отменена.");
        }
        catch (Exception exception)
        {
            // Проверка идёт в фоне и при запуске: её непредвиденный отказ некому показать, поэтому
            // он остаётся в логе, а не падает необработанной задачей. Отказы сети приходят
            // значением Result и разбираются ниже — этот catch для того, чего не ждали.
            AppLogMessages.UpdateFailed(_log, exception.Message);
            SetFailure(exception.Message);

            return Result<UpdateCheck>.Fail($"Не удалось проверить обновление: {exception.Message}");
        }

        if (!result.IsSuccess)
        {
            _available = null;

            if (silent)
            {
                // Игрок не просил проверку: отсутствие сети — обычное дело, и приглашения нет.
                SetStage(UpdateStage.Idle, string.Empty);
            }
            else
            {
                SetFailure(result.Error!);
            }

            return result;
        }

        var check = result.Value;

        if (check.IsAvailable)
        {
            _available = check;
            SetAvailable(check);
        }
        else
        {
            _available = null;

            // Актуальная версия: приглашения нет, но экран настроек должен сказать это словами.
            SetStage(UpdateStage.Idle, string.Empty, $"Установлена самая свежая версия ({_updates.Current}).");
        }

        return result;
    }

    /// <summary>Скачивает обновление и передаёт его установщику платформы.</summary>
    /// <param name="cancellationToken">Признак отмены.</param>
    /// <returns>Сообщение игроку об исходе.</returns>
    /// <remarks>
    /// Загрузка и установка идут по отдельности, хотя служба умеет их вместе: только так между
    /// «Скачивание… 100%» и «Готово» успевает появиться подпись «Установка…». Пока идёт скачивание,
    /// игрок может нажать «Отмена» — это ожидаемый исход, приглашение возвращается на место.
    /// </remarks>
    public async Task<Result<string>> DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        if (_updates is not { } updates)
        {
            SetStage(UpdateStage.Idle, string.Empty, UnsupportedText);

            return Result<string>.Fail(UnsupportedText);
        }

        if (_available is not { } check)
        {
            SetStage(UpdateStage.Idle, string.Empty, NotCheckedText);

            return Result<string>.Fail(NotCheckedText);
        }

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellation = cancellation;
        _progress = 0;
        SetStage(UpdateStage.Downloading, _headline, DownloadingText());

        try
        {
            // Progress<T> переносит отчёты в поток, где начата загрузка: обновлять привязки
            // из чужого потока нельзя, а загрузчик читает поток с ConfigureAwait(false).
            var progress = new Progress<DownloadProgress>(OnProgress);
            var downloaded = await updates.DownloadAsync(check, progress, cancellation.Token).ConfigureAwait(true);

            if (!downloaded.IsSuccess)
            {
                return FinishWithFailure(downloaded.Error!);
            }

            // Загрузка кончилась: полоса доходит до конца до перехода к установке.
            _progress = 1;
            SetStage(UpdateStage.Installing, _headline, InstallingText);

            // Установка запускает чужой процесс (установщик Windows, системный установщик Android),
            // поэтому она идёт в фоне: поток интерфейса ждать её не должен.
            var installed = await Task.Run(() => updates.Install(downloaded.Value!), cancellation.Token).ConfigureAwait(true);

            if (installed.IsSuccess)
            {
                SetStage(UpdateStage.Done, DoneText, installed.Value ?? string.Empty);
            }
            else
            {
                SetFailure(installed.Error!);
            }

            return installed;
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception exception)
        {
            // Как и у проверки: непредвиденный сбой фоновой работы обязан остаться в логе,
            // а игроку — словами. Ожидаемые отказы приходят значением Result.
            AppLogMessages.UpdateFailed(_log, exception.Message);
            SetFailure(exception.Message);

            return Result<string>.Fail($"Не удалось обновить: {exception.Message}");
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation))
            {
                _cancellation = null;
            }
        }
    }

    /// <summary>Откладывает приглашение до следующего запуска.</summary>
    /// <remarks>
    /// Найденное обновление запоминается: следующая проверка (например, кнопкой в настройках)
    /// покажет приглашение снова, а сама проверка при запуске — дело следующего запуска.
    /// </remarks>
    public void Later()
    {
        if (_stage == UpdateStage.Downloading || _stage == UpdateStage.Installing)
        {
            return;
        }

        SetStage(UpdateStage.Idle, string.Empty);
    }

    /// <summary>Останавливает скачивание.</summary>
    /// <remarks>Отменять нечего, если загрузка не идёт: вызов ничего не меняет.</remarks>
    public void Cancel() => _cancellation?.Cancel();

    /// <summary>Применяет отчёт о ходе загрузки.</summary>
    /// <param name="value">Сколько байт уже записано.</param>
    /// <remarks>
    /// Отчёт может прийти и после того, как загрузка кончилась: он идёт через поток интерфейса
    /// и встаёт в очередь. Такой отчёт не должен затирать «Установка…» или «Готово».
    /// </remarks>
    private void OnProgress(DownloadProgress value)
    {
        if (_stage != UpdateStage.Downloading)
        {
            return;
        }

        _progress = value.Fraction;
        _statusLine = DownloadingText();

        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasStatus));
    }

    /// <summary>Показывает приглашение с найденной версией.</summary>
    /// <param name="check">Результат проверки.</param>
    private void SetAvailable(UpdateCheck check) =>
        SetStage(UpdateStage.Available, $"Доступна версия {check.Version} — у вас {_updates!.Current}");

    /// <summary>Возвращает приглашение после отменённой загрузки.</summary>
    /// <returns>Отказ с причиной «отменено».</returns>
    private Result<string> Cancelled()
    {
        SetStage(UpdateStage.Available, _headline, CancelledText);

        return Result<string>.Fail(CancelledText);
    }

    /// <summary>Разбирает отказ загрузки: отмена игроком — не сбой.</summary>
    /// <param name="reason">Причина отказа от загрузчика.</param>
    /// <returns>Отказ с причиной.</returns>
    private Result<string> FinishWithFailure(string reason)
    {
        if (_cancellation?.IsCancellationRequested == true)
        {
            return Cancelled();
        }

        SetFailure(reason);

        return Result<string>.Fail(reason);
    }

    /// <summary>Показывает отказ словами игрока.</summary>
    /// <param name="reason">Причина отказа.</param>
    private void SetFailure(string reason) => SetStage(UpdateStage.Failed, FailedPrefix + reason);

    /// <summary>Собирает подпись скачивания с процентами.</summary>
    /// <returns>Строка вида «Скачивание… 42%».</returns>
    private string DownloadingText() => $"Скачивание… {Percent}%";

    /// <summary>Переводит модель в новый этап и сообщает виду обо всех зависящих свойствах.</summary>
    /// <param name="stage">Новый этап.</param>
    /// <param name="headline">Заголовок приглашения.</param>
    /// <param name="statusLine">Подробность под заголовком; пустая — её нет.</param>
    private void SetStage(UpdateStage stage, string headline, string statusLine = "")
    {
        _stage = stage;
        _headline = headline;
        _statusLine = statusLine;

        foreach (var name in PropertyNames)
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>Сообщает об изменении свойства.</summary>
    /// <param name="name">Имя свойства.</param>
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
