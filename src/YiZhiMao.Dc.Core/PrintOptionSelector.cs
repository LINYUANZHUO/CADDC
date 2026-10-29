namespace YiZhiMao.Dc.Core;

public static class PrintOptionSelector
{
    public static string SelectRequired(
        IEnumerable<string?> candidates,
        IEnumerable<string> availableValues,
        string optionName)
    {
        var available = availableValues
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (available.Count == 0)
        {
            throw new InvalidOperationException($"当前打印设备没有可用的{optionName}。");
        }

        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var match = available.FirstOrDefault(value => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return available[0];
    }

    public static bool IsPlotStyleCompatible(string styleName, bool usesColorDependentStyles)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            return true;
        }

        var expectedExtension = usesColorDependentStyles ? ".ctb" : ".stb";
        return Path.GetExtension(styleName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase);
    }
}
