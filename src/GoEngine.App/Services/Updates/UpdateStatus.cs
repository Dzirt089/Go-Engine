using GoEngine.Core;

namespace GoEngine.App.Services.Updates;

/// <summary>Итог проверки обновления.</summary>
/// <remarks>Тип-перечисление, а не <c>enum</c>: так требуют правила проекта (<c>AGENTS.md</c>, п. 2).</remarks>
public sealed class UpdateStatus : Enumeration
{
    /// <summary>Установлена самая свежая версия.</summary>
    public static readonly UpdateStatus UpToDate = new(0, "Актуальная версия");

    /// <summary>Доступна новая версия.</summary>
    public static readonly UpdateStatus Available = new(1, "Доступно обновление");

    /// <summary>Создаёт элемент перечисления.</summary>
    /// <param name="id">Числовой идентификатор.</param>
    /// <param name="name">Имя элемента.</param>
    private UpdateStatus(int id, string name) : base(id, name)
    {
    }
}
