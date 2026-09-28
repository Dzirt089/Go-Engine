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
