using GoEngine.Core;

namespace GoEngine.Tests;

/// <summary>Тесты результата операции — <c>AGENTS.md</c>, п. 4.</summary>
public sealed class ResultTests
{
    [Fact]
    public void Result_Ok_IsSuccess()
    {
        Assert.True(Result.Ok().IsSuccess);
    }

    [Fact]
    public void Result_Fail_HasError()
    {
        Assert.Equal("Ход в ко запрещён.", Result.Fail("Ход в ко запрещён.").Error);
    }

    [Fact]
    public void Result_Fail_Без_Причины_Бросает()
    {
        Assert.Throws<ArgumentException>(() => Result.Fail(" "));
    }

    [Fact]
    public void ResultT_Ok_HasValue()
    {
        Assert.Equal(7, Result<int>.Ok(7).Value);
    }

    [Fact]
    public void ResultT_Ok_IsSuccess()
    {
        Assert.True(Result<int>.Ok(7).IsSuccess);
    }

    [Fact]
    public void ResultT_Fail_NoValue()
    {
        Assert.Equal(0, Result<int>.Fail("Нет значения.").Value);
    }

    [Fact]
    public void ResultT_Fail_HasError()
    {
        Assert.False(Result<int>.Fail("Нет значения.").IsSuccess);
    }
}
