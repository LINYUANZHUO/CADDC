namespace YiZhiMao.Dc.Core;

public readonly record struct GrilleRectangle(double X, double Y, double Width, double Height);

public sealed record GrilleLayout(
    GrilleRectangle Frame,
    IReadOnlyList<GrilleRectangle> FrontStrips,
    IReadOnlyList<GrilleRectangle> PlanRectangles,
    IReadOnlyList<GrilleRectangle> SideRectangles);

public static class GrilleLayoutCalculator
{
    private const double AuxiliaryViewGap = 200;

    public static GrilleLayout Calculate(
        double left,
        double bottom,
        double width,
        double height,
        GrilleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateBounds(left, bottom, width, height, options);

        var frame = new GrilleRectangle(left, bottom, width, height);
        var frontStrips = CreateFrontStrips(left, bottom, width, height, options);
        var planRectangles = options.NeedPlanView
            ? CreatePlanView(left, bottom, width, options)
            : [];
        var sideRectangles = options.NeedSideView
            ? CreateSideView(left, bottom, width, height, options)
            : [];
        return new GrilleLayout(frame, frontStrips, planRectangles, sideRectangles);
    }

    private static IReadOnlyList<GrilleRectangle> CreateFrontStrips(
        double left,
        double bottom,
        double width,
        double height,
        GrilleOptions options)
    {
        var rectangles = new List<GrilleRectangle>();
        var span = options.IsVertical ? width : height;
        var step = options.StripWidth + options.StripGap;
        for (var offset = 0d; offset + options.StripWidth <= span; offset += step)
        {
            rectangles.Add(options.IsVertical
                ? new GrilleRectangle(left + offset, bottom, options.StripWidth, height)
                : new GrilleRectangle(left, bottom + offset, width, options.StripWidth));
        }

        return rectangles;
    }

    private static IReadOnlyList<GrilleRectangle> CreatePlanView(
        double left,
        double bottom,
        double width,
        GrilleOptions options)
    {
        var viewBottom = bottom - options.BackThickness - options.StripThickness - AuxiliaryViewGap;
        var rectangles = new List<GrilleRectangle>
        {
            new(left, viewBottom + options.StripThickness, width, options.BackThickness)
        };
        if (!options.IsVertical)
        {
            rectangles.Add(new GrilleRectangle(left, viewBottom, width, options.StripThickness));
            return rectangles;
        }

        var step = options.StripWidth + options.StripGap;
        for (var offset = 0d; offset + options.StripWidth <= width; offset += step)
        {
            rectangles.Add(new GrilleRectangle(
                left + offset,
                viewBottom,
                options.StripWidth,
                options.StripThickness));
        }

        return rectangles;
    }

    private static IReadOnlyList<GrilleRectangle> CreateSideView(
        double left,
        double bottom,
        double width,
        double height,
        GrilleOptions options)
    {
        var viewLeft = left + width + AuxiliaryViewGap;
        var rectangles = new List<GrilleRectangle>
        {
            new(viewLeft, bottom, options.BackThickness, height)
        };
        if (options.IsVertical)
        {
            rectangles.Add(new GrilleRectangle(
                viewLeft + options.BackThickness,
                bottom,
                options.StripThickness,
                height));
            return rectangles;
        }

        var step = options.StripWidth + options.StripGap;
        for (var offset = 0d; offset + options.StripWidth <= height; offset += step)
        {
            rectangles.Add(new GrilleRectangle(
                viewLeft + options.BackThickness,
                bottom + offset,
                options.StripThickness,
                options.StripWidth));
        }

        return rectangles;
    }

    private static void ValidateBounds(
        double left,
        double bottom,
        double width,
        double height,
        GrilleOptions options)
    {
        if (!double.IsFinite(left) || !double.IsFinite(bottom) ||
            !double.IsFinite(width) || !double.IsFinite(height) ||
            width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "格栅范围必须是有效的正尺寸矩形。");
        }

        var span = options.IsVertical ? width : height;
        if (options.StripWidth <= 0 || options.StripGap < 0 || options.StripWidth > span)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "格栅条宽或间距不适用于当前范围。");
        }
    }
}
