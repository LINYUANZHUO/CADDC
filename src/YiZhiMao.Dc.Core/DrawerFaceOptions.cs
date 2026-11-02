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
    public int RowCount { get; init; } = 2;

    public int ColumnCount { get; init; } = 1;

    public bool Handleless { get; init; } = true;

    public double HandlelessGap { get; init; } = 26;

    public HandlelessProfileMode HandlelessProfileMode { get; init; } = HandlelessProfileMode.Automatic;

    public double HandlelessProfileSize { get; init; } = 50;

    public bool GenerateOpeningLines { get; init; } = true;

    public DoorPanelCalculationMode CalculationMode { get; init; } =
        DoorPanelCalculationMode.GapFixedDoorDistributed;

    public short LineColorIndex { get; init; } = 3;

    public static DrawerFaceOptions Default { get; } = new(
        DrawerFaceOrientation.Vertical,
        2,
        2,
        0,
        0,
        0,
        0,
        true,
        false)
    {
        RowCount = 2,
        ColumnCount = 1,
        Handleless = true,
        HandlelessGap = 26,
        HandlelessProfileMode = HandlelessProfileMode.Automatic,
        HandlelessProfileSize = 50,
        GenerateOpeningLines = true
    };

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
        if (!Enum.IsDefined(typeof(DrawerFaceOrientation), orientation))
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
            generateHandle)
        {
            RowCount = orientation == DrawerFaceOrientation.Vertical ? parsedCount : 1,
            ColumnCount = orientation == DrawerFaceOrientation.Horizontal ? parsedCount : 1,
            Handleless = false,
            HandlelessGap = 26,
            GenerateOpeningLines = true
        };
        error = string.Empty;
        return true;
    }

    public static bool TryCreateGrid(
        string? rowCount,
        string? columnCount,
        string? gap,
        string? handlelessGap,
        string? topExtension,
        string? bottomExtension,
        string? leftExtension,
        string? rightExtension,
        bool handleless,
        bool generateOpeningLines,
        bool generateMask,
        out DrawerFaceOptions options,
        out string error)
    {
        options = Default;
        if (!TryPositiveInteger(rowCount, "抽面行数", out var parsedRows, out error) ||
            !TryPositiveInteger(columnCount, "抽面列数", out var parsedColumns, out error) ||
            !TryNonNegative(gap, "普通抽面缝", out var parsedGap, out error) ||
            !TryNonNegative(handlelessGap, "免拉手上下缝", out var parsedHandlelessGap, out error) ||
            !TryNonNegative(topExtension, "上盖", out var parsedTop, out error) ||
            !TryNonNegative(bottomExtension, "下盖", out var parsedBottom, out error) ||
            !TryNonNegative(leftExtension, "左盖", out var parsedLeft, out error) ||
            !TryNonNegative(rightExtension, "右盖", out var parsedRight, out error))
        {
            return false;
        }

        options = new DrawerFaceOptions(
            DrawerFaceOrientation.Vertical,
            parsedRows,
            parsedGap,
            parsedTop,
            parsedBottom,
            parsedLeft,
            parsedRight,
            generateMask,
            false)
        {
            RowCount = parsedRows,
            ColumnCount = parsedColumns,
            Handleless = handleless,
            HandlelessGap = parsedHandlelessGap,
            GenerateOpeningLines = generateOpeningLines,
            CalculationMode = DoorPanelCalculationMode.GapFixedDoorDistributed
        };
        error = string.Empty;
        return true;
    }

    private static bool TryPositiveInteger(string? value, string name, out int result, out string error)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result) || result <= 0)
        {
            error = $"{name}必须是大于 0 的整数。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryNonNegative(string? value, string name, out double result, out string error)
    {
        if ((!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) &&
             !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) ||
            !Compatibility.IsFinite(result) || result < 0)
        {
            error = $"{name}必须是大于或等于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
