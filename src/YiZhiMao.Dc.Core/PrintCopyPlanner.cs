namespace YiZhiMao.Dc.Core;

public readonly record struct PrintCopyStep(int JobPosition, int CopyIndex);

public static class PrintCopyPlanner
{
    public static IReadOnlyList<PrintCopyStep> Build(IReadOnlyList<int> copyCounts, bool collate)
    {
        var result = new List<PrintCopyStep>();
        if (collate)
        {
            var rounds = copyCounts.Count == 0 ? 0 : copyCounts.Max();
            for (var copyIndex = 0; copyIndex < rounds; copyIndex++)
            {
                for (var jobPosition = 0; jobPosition < copyCounts.Count; jobPosition++)
                {
                    if (copyCounts[jobPosition] > copyIndex)
                    {
                        result.Add(new PrintCopyStep(jobPosition, copyIndex));
                    }
                }
            }

            return result;
        }

        for (var jobPosition = 0; jobPosition < copyCounts.Count; jobPosition++)
        {
            for (var copyIndex = 0; copyIndex < copyCounts[jobPosition]; copyIndex++)
            {
                result.Add(new PrintCopyStep(jobPosition, copyIndex));
            }
        }

        return result;
    }
}
