using System.Globalization;

namespace YiZhiMao.Dc.Core;

public enum DrawerFaceOrientation
{
    Vertical,
    Horizontal
}

public sealed record DrawerFaceOptions(
    DrawerFaceOrientation Orientation,
    int Count,
    double Gap,
    double TopExtension,
    double BottomExtension,
    double LeftExtension,
    double RightExtension,
    bool GenerateMask,
    bool GenerateHandle)
{
    public DoorPanelCalculationMode CalculationMode { get; init; } =
        DoorPanelCalculationMode.GapRoundedDoorDistributed;

    public short LineColorIndex { get; init; } = 3;

    public static DrawerFaceOptions Default { get; } = new(
        DrawerFaceOrientation.Vertical,
        2,
        3,
        0,
        0,
        0,
        0,
        false,
        false);

    public static bool TryCreate(
        DrawerFaceOrientation orientation,
        string? count,
        string? gap,
        string? topExtension,
        string? bottomExtension,
        string? leftExtension,
        string? rightExtension,
        bool generateMask,
        bool generateHandle,
        out DrawerFaceOptions options,
        out string error)
    {
        options = Default;
        if (!Enum.IsDefined(orientation))
        {
            error = "请选择有效的抽面排列方向。";
            return false;
        }

        if (!int.TryParse(count, NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsedCount) ||
            parsedCount <= 0)
        {
            error = "抽面数量必须是大于 0 的整数。";
            return false;
        }

        if (!TryNonNegative(gap, "抽面缝隙", out var parsedGap, out error) ||
            !TryNonNegative(topExtension, "上盖", out var parsedTop, out error) ||
            !TryNonNegative(bottomExtension, "下盖", out var parsedBottom, out error) ||
            !TryNonNegative(leftExtension, "左盖", out var parsedLeft, out error) ||
            !TryNonNegative(rightExtension, "右盖", out var parsedRight, out error))
        {
            return false;
        }

        options = new DrawerFaceOptions(
            orientation,
            parsedCount,
            parsedGap,
            parsedTop,
            parsedBottom,
            parsedLeft,
            parsedRight,
            generateMask,
            generateHandle);
        error = string.Empty;
        return true;
    }

    private static bool TryNonNegative(string? value, string name, out double result, out string error)
    {
        if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) &&
             !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) ||
            !double.IsFinite(result) || result < 0)
        {
            error = $"{name}必须是大于或等于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
