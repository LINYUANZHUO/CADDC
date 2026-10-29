using System.Globalization;

namespace YiZhiMao.Dc.Core;

public enum DoorPanelCalculationMode
{
    GapFixedDoorDistributed,
    DoorRoundedGapDistributed,
    GapRoundedDoorDistributed,
    DoorAndGapRoundedRemainderRight
}

public sealed record DoorPanelOptions(
    DoorPanelCalculationMode CalculationMode,
    int DoorCount,
    double Gap,
    double TopExtension,
    double BottomExtension,
    double LeftExtension,
    double RightExtension,
    bool GenerateOpeningLines,
    bool GenerateHinges,
    bool AvoidShelves)
{
    public short LineColorIndex { get; init; } = 3;

    public bool GenerateMask { get; init; }

    public static DoorPanelOptions Default { get; } = new(
        DoorPanelCalculationMode.DoorRoundedGapDistributed,
        2,
        3,
        0,
        0,
        0,
        0,
        true,
        true,
        true);

    public static bool TryCreate(
        DoorPanelCalculationMode calculationMode,
        string? doorCount,
        string? gap,
        string? topExtension,
        string? bottomExtension,
        string? leftExtension,
        string? rightExtension,
        bool generateOpeningLines,
        bool generateHinges,
        bool avoidShelves,
        out DoorPanelOptions options,
        out string error)
    {
        options = Default;
        error = string.Empty;

        if (!Enum.IsDefined(calculationMode))
        {
            error = "请选择有效的门板尺寸计算模式。";
            return false;
        }

        if (!int.TryParse(doorCount, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count) || count <= 0)
        {
            error = "门板数必须是大于 0 的整数。";
            return false;
        }

        if (!TryNonNegative(gap, "门缝", out var parsedGap, out error) ||
            !TryNonNegative(topExtension, "上盖延伸值", out var parsedTop, out error) ||
            !TryNonNegative(bottomExtension, "下盖延伸值", out var parsedBottom, out error) ||
            !TryNonNegative(leftExtension, "左盖延伸值", out var parsedLeft, out error) ||
            !TryNonNegative(rightExtension, "右盖延伸值", out var parsedRight, out error))
        {
            return false;
        }

        options = new DoorPanelOptions(
            calculationMode,
            count,
            parsedGap,
            parsedTop,
            parsedBottom,
            parsedLeft,
            parsedRight,
            generateOpeningLines,
            generateHinges,
            avoidShelves);
        return true;
    }

    private static bool TryNonNegative(string? value, string name, out double result, out string error)
    {
        if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) &&
             !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) ||
            !double.IsFinite(result) ||
            result < 0)
        {
            error = $"{name}必须是大于或等于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
