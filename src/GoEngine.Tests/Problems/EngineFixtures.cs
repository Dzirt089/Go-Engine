using GoEngine.Core;
using GoEngine.Problems;

namespace GoEngine.Tests;

/// <summary>Образцы задач с доказанными целями: на них держатся проверки движка.</summary>
/// <remarks>
/// <para>
/// Прежняя библиотека состояла из таких задач, но её заменили задачами по решению источника
/// (<see cref="ProblemGoal.Reference"/>), у которых машинного критерия исхода нет. Чтобы проверки
/// доказанных целей (<see cref="ProblemGoal.Capture"/>, <see cref="ProblemGoal.Dead"/>,
/// <see cref="ProblemGoal.Live"/>) не ослабли, образцы перенесены сюда: те же позиции, деревья и
/// горизонты, что были в библиотеке. В поставку приложения они не входят.
/// </para>
/// <para>
/// Образцы — не «своя задача на глаз»: каждый был собран и проверен перебором
/// (<c>tools/problem-prover</c>), а здесь используется как эталон для независимых проверок.
/// </para>
/// </remarks>
internal static class EngineFixtures
{
    /// <summary>Захват двух камней в углу одним ходом: три принимаемых первых хода.</summary>
    public const string CaptureInCorner =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Захват в углу]"
        + "GC[Белые два камня в углу в атари: чёрные снимают их одним ходом.]"
        + "GE[capture]GD[30]GX[3]GW[3]PL[B]GT[ai]AB[ah]AB[bh]AW[ai]AW[bi]"
        + "(;B[ci])(;B[di];W[ci];B[ch])(;B[ch];W[ci];B[di]))";

    /// <summary>Два дамэ в углу: снятие группы за три полухода, единственный выигрышный ход.</summary>
    public const string CaptureTwoLiberties =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Два дамэ в углу]"
        + "GC[У белой группы из четырёх камней в углу два дамэ: снятие за три полухода.]"
        + "GE[capture]GD[22]GX[3]GW[3]PL[B]GT[ch]AB[ah]AB[dh]AB[di]AW[ch]AW[ai]AW[bi]AW[ci];B[cg];W[bh];B[bg])";

    /// <summary>Накаде: группа остаётся на доске, но спастись не может.</summary>
    public const string Nakade =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Накаде 1: три внутренних пункта]"
        + "GC[У группы белых три внутренних пункта: единственный убивающий ход.]"
        + "GE[dead]GD[16]GX[5]GW[3]PL[B]GT[bh]AB[ci]AB[ch]AW[bi]AW[bh];B[bg])";

    /// <summary>Два глаза на краю: жизнь подтверждается глазной проверкой.</summary>
    public const string TwoEyes =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Два глаза на краю]"
        + "GC[У чёрной группы три точки на первой линии: сыграйте середину.]"
        + "GE[live]GD[27]GX[1]GW[3]PL[B]GT[bh]AB[ai]AB[ah]AB[bh]AB[ch]AB[dh]AB[eh]AB[ei];B[ci])";

    /// <summary>Согнутая четвёрка: жизнь формой за три полухода.</summary>
    public const string BentFour =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Согнутая четвёрка]"
        + "GC[Глазное пространство из четырёх точек с изгибом: чёрные успевают построить два глаза.]"
        + "GE[live]GD[20]GX[3]GW[3]PL[B]GT[ch]AB[cg]AB[dg]AB[eg]AB[ah]AB[bh]AB[ch]AB[eh]AB[ai]AB[ei]"
        + ";B[ci];W[di];B[dh])";

    /// <summary>Накаде, вариант 2: три внутренних пункта, единственный убивающий ход.</summary>
    public const string NakadeSecond =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Накаде 2: три внутренних пункта]"
        + "GC[У группы белых три внутренних пункта: единственный убивающий ход.]"
        + "GE[dead]GD[16]GX[5]GW[3]PL[B]GT[bh]AB[ci]AB[bg]AW[bi]AW[bh];B[ch])";

    /// <summary>Накаде, вариант 3: три внутренних пункта, единственный убивающий ход.</summary>
    public const string NakadeThird =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Накаде 3: три внутренних пункта]"
        + "GC[У группы белых три внутренних пункта: единственный убивающий ход.]"
        + "GE[dead]GD[16]GX[5]GW[3]PL[B]GT[bh]AB[ah]AB[ch]AW[bi]AW[bh];B[bg])";

    /// <summary>Подкладной камень в углу: группа остаётся на доске, но спастись не может.</summary>
    public const string UnderStone =
        "(;GM[1]FF[4]CA[UTF-8]SZ[9]KM[5.5]GN[Подкладной камень в углу]"
        + "GC[Белая группа в углу стоит на одном дамэ: подкладной камень убивает её.]"
        + "GE[dead]GD[20]GX[3]GW[3]PL[B]GT[bh]AB[bf]AB[cg]AB[dh]AB[ai]AB[ci]"
        + "AW[ag]AW[bg]AW[ah]AW[bh]AW[ch];B[af])";

    /// <summary>Имена образцов с целью «мертва».</summary>
    public static IReadOnlyList<string> DeadNames { get; } = ["nakade", "nakade-second", "nakade-third", "under-stone"];

    /// <summary>Задача-образец по имени.</summary>
    /// <param name="name">Имя образца.</param>
    /// <returns>Разобранная задача.</returns>
    /// <exception cref="DomainException">Имя неизвестно или образец не разбирается.</exception>
    public static Problem Get(string name)
    {
        var text = name switch
        {
            "capture-in-corner" => CaptureInCorner,
            "capture-two-liberties" => CaptureTwoLiberties,
            "nakade" => Nakade,
            "nakade-second" => NakadeSecond,
            "nakade-third" => NakadeThird,
            "under-stone" => UnderStone,
            "two-eyes" => TwoEyes,
            "bent-four" => BentFour,
            _ => throw new DomainException($"Неизвестный образец: {name}.")
        };

        var parsed = ProblemSgf.Parse(text, name);

        if (!parsed.IsSuccess)
        {
            throw new DomainException($"Образец {name} не разбирается: {parsed.Error}");
        }

        return parsed.Value!;
    }
}
