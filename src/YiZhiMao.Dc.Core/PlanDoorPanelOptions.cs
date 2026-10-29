using System.Globalization;

namespace YiZhiMao.Dc.Core;

public sealed record PlanDoorPanelOptions(
    DoorPanelCalculationMode CalculationMode,
    int DoorCount,
    double Thickness,
    double FrontClearance,
    double Gap,
    double LeftExtension,
    double RightExtension)
{
    public short LineColorIndex { get; init; } = 249;

    public static PlanDoorPanelOptions Default { get; } = new(
        DoorPanelCalculationMode.GapFixedDoorDistributed,
        2,
        18,
        2,
        3,
        0,
        0);

    public static bool TryCreate(
        DoorPanelCalculationMode calculationMode,
        string? doorCount,
        string? thickness,
        string? frontClearance,
        string? gap,
        string? leftExtension,
        string? rightExtension,
        out PlanDoorPanelOptions options,
        out string error)
    {
        options = Default;
        if (!Enum.IsDefined(calculationMode))
        {
            error = "请选择有效的门板尺寸计算模式。";
            return false;
        }

        if (!int.TryParse(doorCount, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsedCount) ||
            parsedCount <= 0)
        {
            error = "门板数必须是大于 0 的整数。";
            return false;
        }

        if (!TryPositive(thickness, "门板厚度", out var parsedThickness, out error) ||
            !TryNonNegative(frontClearance, "离柜体前沿", out var parsedFrontClearance, out error) ||
            !TryNonNegative(gap, "门缝", out var parsedGap, out error) ||
            !TryNonNegative(leftExtension, "左盖", out var parsedLeft, out error) ||
            !TryNonNegative(rightExtension, "右盖", out var parsedRight, out error))
        {
            return false;
        }

        options = new PlanDoorPanelOptions(
            calculationMode,
            parsedCount,
            parsedThickness,
            parsedFrontClearance,
            parsedGap,
            parsedLeft,
            parsedRight);
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

    private static bool TryNumber(string? value, out double result)
    {
        return (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) &&
               double.IsFinite(result);
    }
}
