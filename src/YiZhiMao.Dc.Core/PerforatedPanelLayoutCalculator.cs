namespace YiZhiMao.Dc.Core;

public readonly record struct PerforatedPanelRectangle(double X, double Y, double Width, double Height);

public readonly record struct PerforatedHole(double X, double Y, double Radius);

public readonly record struct PerforatedLinearDimension(
    double StartX,
    double StartY,
    double EndX,
    double EndY,
    double LineX,
    double LineY);

public readonly record struct PerforatedDiameterDimension(double CenterX, double CenterY, double Radius);

public sealed record PerforatedPanelLayout(
    PerforatedPanelRectangle Frame,
    IReadOnlyList<PerforatedHole> Holes,
    IReadOnlyList<PerforatedPanelRectangle> PlanRectangles,
    IReadOnlyList<PerforatedPanelRectangle> SideRectangles,
    IReadOnlyList<PerforatedLinearDimension> LinearDimensions,
    PerforatedDiameterDimension DiameterDimension);

public static class PerforatedPanelLayoutCalculator
{
    private const double AuxiliaryViewGap = 200;

    public static PerforatedPanelLayout Calculate(
        double left,
        double bottom,
        double width,
        double height,
        PerforatedPanelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateBounds(left, bottom, width, height, options);

        var frame = new PerforatedPanelRectangle(left, bottom, width, height);
        var xCoordinates = CreateAxisCoordinates(left, width, options.InnerOffset, options.HorizontalCount);
        var yCoordinates = CreateAxisCoordinates(bottom, height, options.InnerOffset, options.VerticalCount);
        var holes = CreateHoles(xCoordinates, yCoordinates, options.HoleDiameter / 2);
        var planRectangles = options.NeedPlanView
            ? new[] { new PerforatedPanelRectangle(left, bottom - options.Depth - AuxiliaryViewGap, width, options.Depth) }
            : [];
        var sideRectangles = options.NeedSideView
            ? new[] { new PerforatedPanelRectangle(left + width + AuxiliaryViewGap, bottom, options.Depth, height) }
            : [];

        var linearDimensions = CreateLinearDimensions(left, bottom, xCoordinates, yCoordinates);
        var diameterDimension = new PerforatedDiameterDimension(
            xCoordinates[0],
            yCoordinates[0],
            options.HoleDiameter / 2);

        return new PerforatedPanelLayout(
            frame,
            holes,
            planRectangles,
            sideRectangles,
            linearDimensions,
            diameterDimension);
    }

    private static IReadOnlyList<PerforatedHole> CreateHoles(
        IReadOnlyList<double> xCoordinates,
        IReadOnlyList<double> yCoordinates,
        double radius)
    {
        var holes = new List<PerforatedHole>();
        foreach (var y in yCoordinates)
        {
            foreach (var x in xCoordinates)
            {
                holes.Add(new PerforatedHole(x, y, radius));
            }
        }

        return holes;
    }

    private static IReadOnlyList<PerforatedLinearDimension> CreateLinearDimensions(
        double left,
        double bottom,
        IReadOnlyList<double> xCoordinates,
        IReadOnlyList<double> yCoordinates)
    {
        const double dimensionOffset = 30;
        var dimensions = new List<PerforatedLinearDimension>
        {
            new(
                left,
                bottom,
                xCoordinates[0],
                bottom,
                (left + xCoordinates[0]) / 2,
                bottom - dimensionOffset)
        };
        if (xCoordinates.Count > 1)
        {
            dimensions.Add(new PerforatedLinearDimension(
                xCoordinates[0],
                bottom,
                xCoordinates[1],
                bottom,
                (xCoordinates[0] + xCoordinates[1]) / 2,
                bottom - dimensionOffset));
        }

        dimensions.Add(new PerforatedLinearDimension(
            left,
            bottom,
            left,
            yCoordinates[0],
            left - dimensionOffset,
            (bottom + yCoordinates[0]) / 2));
        if (yCoordinates.Count > 1)
        {
            dimensions.Add(new PerforatedLinearDimension(
                left,
                yCoordinates[0],
                left,
                yCoordinates[1],
                left - dimensionOffset,
                (yCoordinates[0] + yCoordinates[1]) / 2));
        }

        return dimensions;
    }

    private static IReadOnlyList<double> CreateAxisCoordinates(
        double start,
        double length,
        double offset,
        int count)
    {
        if (count == 1)
        {
            return [start + length / 2];
        }

        var step = (length - offset * 2) / (count - 1);
        return Enumerable.Range(0, count)
            .Select(index => start + offset + step * index)
            .ToArray();
    }

    private static void ValidateBounds(
        double left,
        double bottom,
        double width,
        double height,
        PerforatedPanelOptions options)
    {
        if (!double.IsFinite(left) || !double.IsFinite(bottom) ||
            !double.IsFinite(width) || !double.IsFinite(height) ||
            width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "洞洞板范围必须是有效的正尺寸矩形。");
        }

        if (!PerforatedPanelOptions.CanPlaceAxis(
                width, options.InnerOffset, options.HoleDiameter, options.HorizontalCount) ||
            !PerforatedPanelOptions.CanPlaceAxis(
                height, options.InnerOffset, options.HoleDiameter, options.VerticalCount))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "当前范围无法容纳设置的内偏移和孔阵列。");
        }
    }
}
