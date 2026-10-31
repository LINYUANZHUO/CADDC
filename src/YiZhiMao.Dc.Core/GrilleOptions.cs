using System.Globalization;

namespace YiZhiMao.Dc.Core;

public sealed record GrilleOptions(
    bool IsVertical,
    bool NeedPlanView,
    bool NeedSideView,
    double Width,
    double Height,
    double BackThickness,
    double StripThickness,
    double StripWidth,
    double StripGap,
    string HatchPattern)
{
    public static GrilleOptions Default { get; } = new(
        true,
        false,
        false,
        1000,
        2400,
        9,
        15,
        30,
        20,
        "ANSI31");

    public static bool TryCreate(
        bool isVertical,
        bool needPlanView,
        bool needSideView,
        string? width,
        string? height,
        string? backThickness,
        string? stripThickness,
        string? stripWidth,
        string? stripGap,
        string? hatchPattern,
        out GrilleOptions options,
        out string error)
    {
        options = Default;
        if (!TryPositive(width, "总宽度", out var parsedWidth, out error) ||
            !TryPositive(height, "总高度", out var parsedHeight, out error) ||
            !TryPositive(backThickness, "底板厚", out var parsedBackThickness, out error) ||
            !TryPositive(stripThickness, "条厚", out var parsedStripThickness, out error) ||
            !TryPositive(stripWidth, "条宽", out var parsedStripWidth, out error) ||
            !TryNonNegative(stripGap, "条间距", out var parsedStripGap, out error))
        {
            return false;
        }

        var span = isVertical ? parsedWidth : parsedHeight;
        if (parsedStripWidth > span)
        {
            error = "条宽不能大于格栅排列方向的总尺寸。";
            return false;
        }

        options = new GrilleOptions(
            isVertical,
            needPlanView,
            needSideView,
            parsedWidth,
            parsedHeight,
            parsedBackThickness,
            parsedStripThickness,
            parsedStripWidth,
            parsedStripGap,
            string.IsNullOrWhiteSpace(hatchPattern) ? "无填充" : hatchPattern!.Trim());
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
               Compatibility.IsFinite(result);
    }
}
