using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты базового типа Enumeration.</summary>
public sealed class EnumerationTests
{
    [Fact]
    public void Enumeration_FromName_Находит_Элемент()
    {
        Assert.Equal(StoneColor.Black, Enumeration.FromName<StoneColor>("Black"));
    }

    [Fact]
    public void Enumeration_FromId_Находит_Элемент()
    {
        Assert.Equal(MoveType.Pass, Enumeration.FromId<MoveType>(1));
    }

    [Fact]
    public void Enumeration_FromName_Для_Неизвестного_Имени_Бросает()
    {
        Assert.Throws<DomainException>(() => Enumeration.FromName<StoneColor>("Red"));
    }

    [Fact]
    public void Enumeration_FromId_Для_Неизвестного_Идентификатора_Бросает()
    {
        Assert.Throws<DomainException>(() => Enumeration.FromId<StoneColor>(99));
    }

    [Fact]
    public void Enumeration_FromName_Для_Пустого_Имени_Бросает()
    {
        Assert.Throws<ArgumentException>(() => Enumeration.FromName<StoneColor>(" "));
    }

    [Fact]
    public void Enumeration_Разные_Типы_С_Одинаковым_Идентификатором_Не_Равны()
    {
        Assert.NotEqual<Enumeration>(StoneColor.Empty, MoveType.Play);
    }

    [Fact]
    public void Enumeration_TryFromId_Возвращает_Элемент()
    {
        Assert.Equal(StoneColor.White, Enumeration.TryFromId<StoneColor>(StoneColor.White.Id));
    }

    [Fact]
    public void Enumeration_TryFromId_Неизвестный_Идентификатор_Возвращает_Ноль()
    {
        Assert.Null(Enumeration.TryFromId<StoneColor>(999));
    }

    [Fact]
    public void Enumeration_ToString_Возвращает_Имя()
    {
        Assert.Equal("Empty", StoneColor.Empty.ToString());
    }
}
