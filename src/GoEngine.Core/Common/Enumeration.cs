using System.Reflection;

namespace GoEngine.Core;

/// <summary>
/// Базовый тип перечисления-значения: <c>StoneColor</c>, <c>MoveType</c>, <c>GameStatus</c>,
/// <c>DifficultyLevel</c>.
/// </summary>
/// <remarks>
/// Style Bible запрещает <c>enum</c> для статусов и типов домена. Наследник объявляет элементы
/// публичными статическими свойствами, идентификатор и имя задаёт в конструкторе. Поиск выполняется
/// методами <see cref="FromId{T}"/> и <see cref="FromName{T}"/>, сравнение — по типу и
/// <see cref="Id"/>, поэтому элементы можно сравнивать операторами <c>==</c> и <c>!=</c>.
/// </remarks>
public abstract class Enumeration : IEquatable<Enumeration>
{
    /// <summary>Создаёт элемент перечисления.</summary>
    /// <param name="id">Числовой идентификатор элемента.</param>
    /// <param name="name">Имя элемента.</param>
    /// <param name="descriptions">Необязательное описание для UI.</param>
    /// <exception cref="ArgumentException">Имя пустое или состоит из пробелов.</exception>
    protected Enumeration(int id, string name, string? descriptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name;
        Descriptions = descriptions;
    }

    /// <summary>Числовой идентификатор элемента.</summary>
    public int Id { get; }

    /// <summary>Имя элемента.</summary>
    public string Name { get; }

    /// <summary>Описание элемента для UI или <c>null</c>.</summary>
    public string? Descriptions { get; }

    /// <summary>Возвращает элемент перечисления по идентификатору.</summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    /// <param name="id">Искомый идентификатор.</param>
    /// <returns>Элемент перечисления с указанным идентификатором.</returns>
    /// <exception cref="DomainException">Элемента с таким идентификатором нет.</exception>
    public static T FromId<T>(int id) where T : Enumeration =>
        Registry<T>.ById.TryGetValue(id, out var item)
            ? item
            : throw new DomainException($"Элемент перечисления {typeof(T).Name} с идентификатором {id} не найден.");

    /// <summary>Возвращает элемент перечисления по идентификатору, если он есть.</summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    /// <param name="id">Искомый идентификатор.</param>
    /// <returns>Элемент перечисления или <c>null</c>, если такого идентификатора нет.</returns>
    /// <remarks>Нужен разбору внешних данных (файл настроек): неизвестное значение — не ошибка движка.</remarks>
    public static T? TryFromId<T>(int id) where T : Enumeration =>
        Registry<T>.ById.TryGetValue(id, out var item) ? item : null;

    /// <summary>Возвращает элемент перечисления по имени, если он есть.</summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    /// <param name="name">Искомое имя; <c>null</c> и пустая строка означают «не найдено».</param>
    /// <returns>Элемент перечисления или <c>null</c>, если такого имени нет.</returns>
    /// <remarks>Нужен разбору внешних данных (файл настроек): неизвестное имя — не ошибка движка.</remarks>
    public static T? TryFromName<T>(string? name) where T : Enumeration =>
        !string.IsNullOrWhiteSpace(name) && Registry<T>.ByName.TryGetValue(name, out var item) ? item : null;

    /// <summary>Возвращает элемент перечисления по имени. Регистр учитывается.</summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    /// <param name="name">Искомое имя.</param>
    /// <returns>Элемент перечисления с указанным именем.</returns>
    /// <exception cref="ArgumentException">Имя пустое или состоит из пробелов.</exception>
    /// <exception cref="DomainException">Элемента с таким именем нет.</exception>
    public static T FromName<T>(string name) where T : Enumeration
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Registry<T>.ByName.TryGetValue(name, out var item)
            ? item
            : throw new DomainException($"Элемент перечисления {typeof(T).Name} с именем «{name}» не найден.");
    }

    /// <summary>Сравнивает элементы по типу и идентификатору.</summary>
    /// <param name="other">Другой элемент перечисления.</param>
    /// <returns><c>true</c>, если элементы одного типа и с одним идентификатором.</returns>
    public bool Equals(Enumeration? other) =>
        other is not null && GetType() == other.GetType() && Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Enumeration);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Сравнивает элементы по типу и идентификатору.</summary>
    /// <param name="left">Первый элемент.</param>
    /// <param name="right">Второй элемент.</param>
    /// <returns><c>true</c>, если элементы одного типа и с одним идентификатором.</returns>
    public static bool operator ==(Enumeration? left, Enumeration? right) => Equals(left, right);

    /// <summary>Сравнивает элементы по типу и идентификатору.</summary>
    /// <param name="left">Первый элемент.</param>
    /// <param name="right">Второй элемент.</param>
    /// <returns><c>true</c>, если элементы разных типов или идентификаторов.</returns>
    public static bool operator !=(Enumeration? left, Enumeration? right) => !Equals(left, right);

    /// <summary>
    /// Кэш элементов одного типа перечисления. Строится по публичным статическим свойствам наследника,
    /// поэтому элементы не нужно регистрировать вручную.
    /// </summary>
    /// <typeparam name="T">Тип перечисления.</typeparam>
    private static class Registry<T> where T : Enumeration
    {
        internal static readonly IReadOnlyList<T> Items = LoadItems().AsReadOnly();

        internal static readonly IReadOnlyDictionary<int, T> ById = Items.ToDictionary(static item => item.Id);

        internal static readonly IReadOnlyDictionary<string, T> ByName =
            Items.ToDictionary(static item => item.Name, StringComparer.Ordinal);

        private static List<T> LoadItems() =>
            typeof(T)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(static property => property.PropertyType == typeof(T))
                .Select(static property => (T)property.GetValue(null)!)
                .ToList();
    }
}
