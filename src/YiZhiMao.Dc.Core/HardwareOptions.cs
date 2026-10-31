using System.Globalization;

namespace YiZhiMao.Dc.Core;

public enum HingeDimensionMode
{
    Continuous,
    SplitFromEnds
}

public sealed record HardwareOptions(
    double HangingRodTopOffset,
    double LightStripTopOffset,
    double LightStripSideOpening,
    double HingeTopOffset,
    double HingeBottomOffset,
    int HingeCountBelow900,
    int HingeCount900To1500,
    int HingeCount1500To2000,
    int HingeCount2000To2500,
    int HingeCountAtLeast2500,
    bool UseFixedHingeSpacing,
    double FixedHingeSpacing,
    bool GenerateHingeDimensions,
    HingeDimensionMode HingeDimensionMode,
    double HingeDimensionOffset,
    double HingeDimensionLineSpacing,
    double HingeDimensionTextHeight,
    double HingeTypeTextHeight,
    double HingeTypeTextSpacing)
{
    public static HardwareOptions Default { get; } = new(
        100, 10, 100, 100, 100, 2, 3, 4, 5, 6, false, 280, true,
        HingeDimensionMode.SplitFromEnds, 60, 80, 50, 35, 35);

    public int GetHingeCount(double panelHeight)
    {
        if (!Compatibility.IsFinite(panelHeight) || panelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(panelHeight), "门板高度必须大于 0。");
        }

        if (panelHeight < 900)
        {
            return HingeCountBelow900;
        }

        if (panelHeight < 1500)
        {
            return HingeCount900To1500;
        }

        if (panelHeight < 2000)
        {
            return HingeCount1500To2000;
        }

        if (panelHeight < 2500)
        {
            return HingeCount2000To2500;
        }

        return HingeCountAtLeast2500;
    }

    public static bool TryCreate(
        string? hangingRodTopOffset,
        string? lightStripTopOffset,
        string? lightStripSideOpening,
        string? hingeTopOffset,
        string? hingeBottomOffset,
        string? hingeCountBelow900,
        string? hingeCount900To1500,
        string? hingeCount1500To2000,
        string? hingeCount2000To2500,
        string? hingeCountAtLeast2500,
        bool useFixedHingeSpacing,
        string? fixedHingeSpacing,
        bool generateHingeDimensions,
        HingeDimensionMode hingeDimensionMode,
        string? hingeDimensionOffset,
        string? hingeDimensionLineSpacing,
        string? hingeDimensionTextHeight,
        string? hingeTypeTextHeight,
        string? hingeTypeTextSpacing,
        out HardwareOptions options,
        out string error)
    {
        options = Default;
        error = string.Empty;

        if (!TryNonNegative(hangingRodTopOffset, "挂衣杆离顶高度", out var rodTop, out error) ||
            !TryNonNegative(lightStripTopOffset, "灯带距顶深度", out var lightTop, out error) ||
            !TryNonNegative(lightStripSideOpening, "灯带侧视图距前口", out var lightOpening, out error) ||
            !TryNonNegative(hingeTopOffset, "铰链距门顶", out var hingeTop, out error) ||
            !TryNonNegative(hingeBottomOffset, "铰链距门底", out var hingeBottom, out error) ||
            !TryPositiveInt(hingeCountBelow900, "H<0.9m铰链数", out var count1, out error) ||
            !TryPositiveInt(hingeCount900To1500, "0.9-1.5m铰链数", out var count2, out error) ||
            !TryPositiveInt(hingeCount1500To2000, "1.5-2.0m铰链数", out var count3, out error) ||
            !TryPositiveInt(hingeCount2000To2500, "2.0-2.5m铰链数", out var count4, out error) ||
            !TryPositiveInt(hingeCountAtLeast2500, "H>=2.5m铰链数", out var count5, out error) ||
            !TryPositive(fixedHingeSpacing, "固定铰链间距", out var spacing, out error) ||
            !TryNonNegative(hingeDimensionOffset, "首层标注距门边", out var dimensionOffset, out error) ||
            !TryNonNegative(hingeDimensionLineSpacing, "多层标注间距", out var dimensionLineSpacing, out error) ||
            !TryPositive(hingeDimensionTextHeight, "标注文字高度", out var dimensionTextHeight, out error) ||
            !TryPositive(hingeTypeTextHeight, "铰链类型文字高度", out var typeTextHeight, out error) ||
            !TryNonNegative(hingeTypeTextSpacing, "铰链类型文字间距", out var typeTextSpacing, out error))
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(HingeDimensionMode), hingeDimensionMode))
        {
            error = "请选择有效的铰链标注模式。";
            return false;
        }

        options = new HardwareOptions(
            rodTop,
            lightTop,
            lightOpening,
            hingeTop,
            hingeBottom,
            count1,
            count2,
            count3,
            count4,
            count5,
            useFixedHingeSpacing,
            spacing,
            generateHingeDimensions,
            hingeDimensionMode,
            dimensionOffset,
            dimensionLineSpacing,
            dimensionTextHeight,
            typeTextHeight,
            typeTextSpacing);
        return true;
    }

    private static bool TryNonNegative(string? value, string name, out double result, out string error)
    {
        if (!TryDouble(value, out result) || !Compatibility.IsFinite(result) || result < 0)
        {
            error = $"{name}必须是大于或等于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryPositive(string? value, string name, out double result, out string error)
    {
        if (!TryDouble(value, out result) || !Compatibility.IsFinite(result) || result <= 0)
        {
            error = $"{name}必须是大于 0 的数字。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryPositiveInt(string? value, string name, out int result, out string error)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result) || result <= 0)
        {
            error = $"{name}必须是大于 0 的整数。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
