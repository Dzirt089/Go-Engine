using System.Globalization;
using GoEngine.Core;

namespace GoEngine.App.Services;

/// <summary>Строка о состоянии моделей нейросети для экрана настроек.</summary>
/// <remarks>
/// Модели грузит голова платформы, а показывает их состояние общий вид. Без этой строки отказ
/// виден только по отсутствию уровней с сетью, и узнать причину на телефоне нечем: на Android
/// модели копируются из пакета, на настольных системах лежат рядом с приложением (D-038, D-039).
/// Тексты собираются здесь, а не в головах, поэтому их можно проверить тестами.
/// </remarks>
public static class ModelStatusText
{
    /// <summary>Голова ещё не сообщала о моделях.</summary>
    public const string Unknown = "Модели: состояние неизвестно";

    /// <summary>Моделей нет: играют уровни без сети.</summary>
    public const string Missing = "Модели не найдены: доступны только уровни без сети";

    /// <summary>Собирает строку о загруженных моделях.</summary>
    /// <param name="sizes">Стороны доски, для которых модель на месте.</param>
    /// <returns>Например, «Модели загружены: 9×9, 19×19».</returns>
    /// <exception cref="ArgumentNullException">Список размеров не задан.</exception>
    public static string Loaded(IEnumerable<int> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        var labels = sizes.Where(IsSupported).Distinct().Order().Select(Label).ToList();

        return labels.Count == 0 ? Missing : $"Модели загружены: {string.Join(", ", labels)}";
    }

    /// <summary>Собирает строку об отказе с причиной словами.</summary>
    /// <param name="reason">Причина отказа.</param>
    /// <returns>Например, «Модели не загружены: не хватило места на устройстве».</returns>
    /// <exception cref="ArgumentException">Причина не задана.</exception>
    public static string Failed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return $"Модели не загружены: {reason}";
    }

    /// <summary>Описывает исключение вместе с вложенными причинами.</summary>
    /// <param name="exception">Исключение, остановившее подготовку моделей.</param>
    /// <param name="depth">Сколько уровней причины показывать; по умолчанию три.</param>
    /// <returns>Например, «TypeInitializationException: … → DllNotFoundException: …».</returns>
    /// <exception cref="ArgumentNullException">Исключение не задано.</exception>
    /// <remarks>
    /// На телефоне настоящая причина отказа прячется во вложенном исключении: снаружи видно только
    /// <c>TypeInitializationException</c>, а внутри — <c>DllNotFoundException</c> или отказ загрузки
    /// нативной библиотеки. Без цепочки одна строка с телефона не отличает битую модель от
    /// несовместимой нативной библиотеки, и диагноз превращается в догадки.
    /// </remarks>
    public static string Chain(Exception exception, int depth = 3)
    {
        ArgumentNullException.ThrowIfNull(exception);

        List<string> parts = [];
        Exception? current = exception;

        for (var level = 0; current is not null && level < Math.Max(depth, 1); level++)
        {
            var message = current.Message?.Trim();

            parts.Add(string.IsNullOrEmpty(message)
                ? current.GetType().Name
                : $"{current.GetType().Name}: {message}");

            current = current.InnerException;
        }

        return string.Join(" → ", parts);
    }

    /// <summary>Обрезает длинный технический текст, чтобы он не разносил экран настроек.</summary>
    /// <param name="text">Текст причины.</param>
    /// <param name="limit">Предел длины; по умолчанию 400 символов.</param>
    /// <returns>Текст не длиннее предела; обрезанный заканчивается многоточием.</returns>
    /// <exception cref="ArgumentNullException">Текст не задан.</exception>
    /// <remarks>
    /// Предел поднят с 160 символов: цепочка из трёх исключений вместе со списком ABI устройства
    /// в 160 символов не помещается, а именно она отличает «нет библиотеки под этот телефон»
    /// от «модель повреждена».
    /// </remarks>
    public static string Limit(string text, int limit = 400)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length <= limit ? text : text[..limit] + "…";
    }

    /// <summary>Проверяет, что размер доски есть в списке приложения.</summary>
    /// <param name="size">Сторона доски.</param>
    /// <returns><c>true</c>, если такую доску игрок может выбрать.</returns>
    /// <remarks>
    /// Профиль модели покрывает весь диапазон (например, 13…19), и среди этих чисел есть размеры,
    /// которых в приложении нет. Создавать <see cref="BoardSize"/> для них нельзя: тип принимает
    /// только 9, 13 и 19 и на остальных бросает <see cref="DomainException"/>.
    /// </remarks>
    private static bool IsSupported(int size) => BoardSizes.All.Any(board => board.Value == size);

    /// <summary>Подпись стороны доски.</summary>
    /// <param name="size">Сторона доски из списка приложения.</param>
    /// <returns>Например, «9×9».</returns>
    private static string Label(int size)
    {
        foreach (var board in BoardSizes.All)
        {
            if (board.Value == size)
            {
                return BoardSizes.Label(board);
            }
        }

        // Недостижимо: вызывающий отсеивает неподдерживаемые размеры через IsSupported.
        return string.Create(CultureInfo.InvariantCulture, $"{size}×{size}");
    }
}
