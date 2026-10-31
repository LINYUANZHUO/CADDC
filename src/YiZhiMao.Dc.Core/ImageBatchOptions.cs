using System.Globalization;

namespace YiZhiMao.Dc.Core;

public sealed record ImageBatchOptions(double Gap, bool UseUnifiedHeight, int PixelHeight)
{
    public const double DefaultGap = 50.0;
    public const int DefaultPixelHeight = 1000;
    public const int MaximumPixelHeight = 10000;

    public static ImageBatchOptions Default { get; } = new(DefaultGap, true, DefaultPixelHeight);

    public static bool TryCreate(
        string? gapText,
        bool useUnifiedHeight,
        string? pixelHeightText,
        out ImageBatchOptions options,
        out string error)
    {
        options = Default;
        error = string.Empty;

        if (!TryParseDouble(gapText, out var gap) || !Compatibility.IsFinite(gap) || gap < 0)
        {
            error = "排列间距必须是大于或等于 0 的数字。";
            return false;
        }

        var pixelHeight = DefaultPixelHeight;
        if (useUnifiedHeight &&
            (!int.TryParse(pixelHeightText, NumberStyles.Integer, CultureInfo.CurrentCulture, out pixelHeight) ||
             pixelHeight is < 1 or > MaximumPixelHeight))
        {
            error = $"统一像素高度必须是 1 到 {MaximumPixelHeight} 之间的整数。";
            return false;
        }

        options = new ImageBatchOptions(gap, useUnifiedHeight, pixelHeight);
        return true;
    }

    public ImagePixelSize CalculateTargetSize(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "图片尺寸必须大于 0。");
        }

        if (!UseUnifiedHeight)
        {
            return new ImagePixelSize(sourceWidth, sourceHeight);
        }

        var width = Math.Max(1, (int)Math.Round(
            sourceWidth * (PixelHeight / (double)sourceHeight),
            MidpointRounding.AwayFromZero));
        return new ImagePixelSize(width, PixelHeight);
    }

    public double CalculateNextX(double currentX, double insertedWidth)
    {
        if (!Compatibility.IsFinite(insertedWidth) || insertedWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insertedWidth), "插入宽度必须大于 0。");
        }

        return currentX + insertedWidth + Gap;
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ||
               double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}

public readonly record struct ImagePixelSize(int Width, int Height);
