namespace GoEngine.App.Services;

/// <summary>Источник файлов моделей: откуда их брать при установке в каталог приложения.</summary>
/// <remarks>
/// Абстракция нужна, чтобы логика установки была одна и проверялась тестами: на настольных
/// системах файлы лежат рядом с приложением, на Android — внутри пакета (`assets/models`),
/// а в тестах источник подставляется в памяти.
/// </remarks>
public interface IModelSource
{
    /// <summary>Есть ли файл в источнике.</summary>
    /// <param name="fileName">Имя файла модели.</param>
    /// <returns><c>true</c>, если файл доступен.</returns>
    bool Contains(string fileName);

    /// <summary>Открывает файл модели на чтение.</summary>
    /// <param name="fileName">Имя файла модели.</param>
    /// <returns>Поток с содержимым файла.</returns>
    /// <exception cref="FileNotFoundException">Файла в источнике нет.</exception>
    Stream Open(string fileName);

    /// <summary>Возвращает длину файла, если источник её знает.</summary>
    /// <param name="fileName">Имя файла модели.</param>
    /// <returns>Длина в байтах или <c>-1</c>, если длину узнать нельзя.</returns>
    /// <remarks>
    /// Длина нужна, чтобы не копировать 97 МБ при каждом запуске: если файл уже установлен
    /// и его размер совпадает, копирование пропускается. Неизвестная длина означает
    /// «копировать всегда» — это медленнее, но никогда не оставит битую модель.
    /// </remarks>
    long Length(string fileName);
}
