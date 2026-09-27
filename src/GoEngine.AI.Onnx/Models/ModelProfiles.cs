namespace GoEngine.AI.Onnx;

/// <summary>Профиль модели: какой файл использовать для какого диапазона размеров доски.</summary>
/// <param name="FileName">Имя файла в каталоге моделей.</param>
/// <param name="MinBoardSize">Наименьшая сторона доски, для которой модель годится.</param>
/// <param name="MaxBoardSize">Наибольшая сторона доски.</param>
/// <param name="Description">Что это за модель и для чего она.</param>
public sealed record ModelProfile(string FileName, int MinBoardSize, int MaxBoardSize, string Description);

/// <summary>Каталог профилей моделей: единственное место, где заданы имена файлов.</summary>
/// <remarks>
/// Профили — <c>DECISIONS.md</c>, D-039: одна сеть на все доски не работает, поэтому у каждого
/// размера своя модель. Файлы в репозиторий не коммитятся (десятки мегабайт), их кладут
/// в каталог <c>models</c>; что скачать — в <c>models/README.md</c>.
/// </remarks>
public static class ModelProfiles
{
    /// <summary>Основная модель KataGo: 19×19 и 13×13.</summary>
    /// <remarks>Обучена на 19×19; на 13×13 её оценка правдоподобна, на 9×9 — нет (D-034).</remarks>
    public static ModelProfile Main19x19 { get; } = new(
        "kata1-b28c512nbt-adam-s11165M-d5387M.uint8.onnx",
        13,
        19,
        "Основная модель KataGo (b28c512nbt): 19×19 и 13×13.");

    /// <summary>Специализированная модель для 9×9.</summary>
    /// <remarks>
    /// Файла в проекте пока нет: в репозитории <c>kaya-go/kaya</c> специализированных моделей
    /// не бывает. Профиль описан заранее, чтобы модель подхватилась без правок кода (D-039).
    /// </remarks>
    public static ModelProfile Finetuned9x9 { get; } = new(
        "kata9x9-finetuned.uint8.onnx",
        7,
        11,
        "Специализированная модель для 9×9. Файла в проекте пока нет (D-039).");

    /// <summary>Все профили: узкие идут первыми, чтобы 9×9 не достался основной модели.</summary>
    public static IReadOnlyList<ModelProfile> All { get; } = [Finetuned9x9, Main19x19];

    /// <summary>Находит профиль, подходящий для размера доски.</summary>
    /// <param name="boardSize">Сторона доски.</param>
    /// <returns>Профиль или <c>null</c>, если подходящего нет.</returns>
    public static ModelProfile? FindForBoardSize(int boardSize) =>
        All.FirstOrDefault(profile => boardSize >= profile.MinBoardSize && boardSize <= profile.MaxBoardSize);
}
