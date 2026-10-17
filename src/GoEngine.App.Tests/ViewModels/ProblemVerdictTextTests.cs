using GoEngine.App.ViewModels;

namespace GoEngine.App.Tests;

/// <summary>Тексты разбора задачи: длинная служебная оговорка домена до игрока не доходит.</summary>
/// <remarks>
/// N6 отчёта QA 2026-10-17: новичок читал «Верно: D9 — …; дыхания не изменились — он из линии
/// решения источника, а цель достигается всей линией» и не понимал, правильный ли ход. Оговорка
/// нужна домену, но не игроку; она снимается на выходе, в слое интерфейса.
/// </remarks>
public sealed class ProblemVerdictTextTests
{
    [Fact]
    public void Оговорка_Про_Линию_Источника_Снимается()
    {
        const string Feedback =
            "D9 — разрезал группы соперника; у белой группы 2 дыхания: дыхания не изменились — он из линии решения источника, а цель достигается всей линией";

        Assert.DoesNotContain(
            "линии решения источника",
            ProblemVerdictText.Shorten(Feedback),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Полезная_Часть_Разбора_Остаётся()
    {
        const string Feedback =
            "D9 — разрезал группы соперника — он из линии решения источника, а цель достигается всей линией";

        Assert.Equal(
            "D9 — разрезал группы соперника",
            ProblemVerdictText.Shorten(Feedback));
    }
}
