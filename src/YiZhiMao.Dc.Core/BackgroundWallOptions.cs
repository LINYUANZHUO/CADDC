using System.Globalization;

namespace YiZhiMao.Dc.Core;

public readonly record struct BackgroundWallRowInput(string? ColumnCount, string? Height);

public readonly record struct BackgroundWallRowOptions(int ColumnCount, double Height);

public sealed record BackgroundWallOptions(
    bool NeedPlanView,
    bool NeedSideView,
    bool UseRandomWidths,
    bool LeaveVerticalGap,
    bool GenerateAllPlanRows,
    double TotalWidth,
    double Depth,
    double Gap,
    bool CreateSlots,
    IReadOnlyList<BackgroundWallRowOptions> Rows)
{
    public const double MinimumPanelWidth = 100;

    public static BackgroundWallOptions Default { get; } = new(
        true,
        true,
        true,
        false,
        false,
        2400,
        18,
        3,
        true,
        [new BackgroundWallRowOptions(4, 700)]);

    public static bool TryCreate(
        bool needPlanView,
        bool needSideView,
        bool useRandomWidths,
        bool leaveVerticalGap,
        bool generateAllPlanRows,
        string? totalWidth,
        string? depth,
        string? gap,
        bool createSlots,
        string? rowCount,
        IReadOnlyList<BackgroundWallRowInput> rowInputs,
        out BackgroundWallOptions options,
        out string error)
    {
        options = Default;
        if (!TryPositive(totalWidth, "总宽度", out var parsedWidth, out error) ||
            !TryPositive(depth, "厚/深", out var parsedDepth, out error) ||
            !TryNonNegative(gap, "缝隙", out var parsedGap, out error) ||
            !TryPositiveInteger(rowCount, "横向排数", out var parsedRowCount, out error))
        {
            return false;
        }

        if (parsedRowCount > 5)
        {
            error = "横向排数最多为 5。";
            return false;
        }

        if (rowInputs.Count < parsedRowCount)
        {
            error = "请补全每排的列数和高度。";
            return false;
        }

        var rows = new List<BackgroundWallRowOptions>(parsedRowCount);
        for (var index = 0; index < parsedRowCount; index++)
        {
            var input = rowInputs[index];
            if (!TryPositiveInteger(input.ColumnCount, $"第 {index + 1} 排列数", out var columnCount, out error) ||
                !TryPositive(input.Height, $"第 {index + 1} 排高度", out var height, out error))
            {
                return false;
            }

            var usableWidth = parsedWidth - parsedGap * (columnCount - 1);
            if (useRandomWidths && usableWidth < columnCount * MinimumPanelWidth)
            {
                error = $"第 {index + 1} 排无法满足最小板宽 {MinimumPanelWidth:0}mm。";
                return false;
            }

            rows.Add(new BackgroundWallRowOptions(columnCount, height));
        }

        options = new BackgroundWallOptions(
            needPlanView,
            needSideView,
            useRandomWidths,
            leaveVerticalGap,
            generateAllPlanRows,
            parsedWidth,
            parsedDepth,
            parsedGap,
            createSlots,
            rows);
        error = string.Empty;
        return true;
    }

    private static bool TryPositive(string? value, string name, out double result, out string error)
    {
        if (!TryNumber(value, out result) || result <= 0)
        {
            error = $"{name}必须是大于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryNonNegative(string? value, string name, out double result, out string error)
    {
        if (!TryNumber(value, out result) || result < 0)
        {
            error = $"{name}必须是大于或等于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryPositiveInteger(string? value, string name, out int result, out string error)
    {
        if ((!int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result) &&
             !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)) ||
            result <= 0)
        {
            error = $"{name}必须是大于 0 的整数。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryNumber(string? value, out double result)
    {
        return (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) &&
               Compatibility.IsFinite(result);
    }
}
