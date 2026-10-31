using System.Globalization;

namespace YiZhiMao.Dc.Core;

public sealed record PerforatedPanelOptions(
    bool NeedPlanView,
    bool NeedSideView,
    double Width,
    double Height,
    double Depth,
    double InnerOffset,
    double HoleDiameter,
    int HorizontalCount,
    int VerticalCount)
{
    public static PerforatedPanelOptions Default { get; } = new(
        false,
        false,
        600,
        800,
        18,
        50,
        10,
        6,
        10);

    public static bool TryCreate(
        bool needPlanView,
        bool needSideView,
        string? width,
        string? height,
        string? depth,
        string? innerOffset,
        string? holeDiameter,
        string? horizontalCount,
        string? verticalCount,
        out PerforatedPanelOptions options,
        out string error)
    {
        options = Default;
        if (!TryPositive(width, "板宽度", out var parsedWidth, out error) ||
            !TryPositive(height, "板高度", out var parsedHeight, out error) ||
            !TryPositive(depth, "板深度", out var parsedDepth, out error) ||
            !TryNonNegative(innerOffset, "内偏移", out var parsedInnerOffset, out error) ||
            !TryPositive(holeDiameter, "洞直径", out var parsedHoleDiameter, out error) ||
            !TryPositiveInteger(horizontalCount, "横向孔数", out var parsedHorizontalCount, out error) ||
            !TryPositiveInteger(verticalCount, "纵向孔数", out var parsedVerticalCount, out error))
        {
            return false;
        }

        if (parsedInnerOffset < parsedHoleDiameter / 2)
        {
            error = "内偏移不能小于洞半径。";
            return false;
        }

        if (!CanPlaceAxis(parsedWidth, parsedInnerOffset, parsedHoleDiameter, parsedHorizontalCount) ||
            !CanPlaceAxis(parsedHeight, parsedInnerOffset, parsedHoleDiameter, parsedVerticalCount))
        {
            error = "板尺寸、内偏移和孔数量无法形成有效孔阵列。";
            return false;
        }

        options = new PerforatedPanelOptions(
            needPlanView,
            needSideView,
            parsedWidth,
            parsedHeight,
            parsedDepth,
            parsedInnerOffset,
            parsedHoleDiameter,
            parsedHorizontalCount,
            parsedVerticalCount);
        error = string.Empty;
        return true;
    }

    internal static bool CanPlaceAxis(double length, double offset, double diameter, int count)
    {
        return count == 1
            ? length >= diameter
            : length > offset * 2;
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
