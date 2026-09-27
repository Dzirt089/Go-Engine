namespace GoEngine.Problems;

/// <summary>Отчёт проверки задачи.</summary>
/// <param name="ProblemId">Идентификатор задачи.</param>
/// <param name="Issues">Замечания по-русски; пустой список — задача доказана.</param>
public readonly record struct ProblemReport(string ProblemId, IReadOnlyList<string> Issues)
{
    /// <summary>Задача прошла проверку: замечаний нет.</summary>
    public bool IsValid => Issues.Count == 0;
}
