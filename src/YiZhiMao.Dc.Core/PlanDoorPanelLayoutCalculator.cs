namespace YiZhiMao.Dc.Core;

public readonly record struct PlanDoorPanelPoint(double X, double Y);

public readonly record struct PlanDoorPanelLeaf(
    PlanDoorPanelPoint InnerStart,
    PlanDoorPanelPoint InnerEnd,
    PlanDoorPanelPoint OuterEnd,
    PlanDoorPanelPoint OuterStart);

public sealed record PlanDoorPanelLayout(
    IReadOnlyList<PlanDoorPanelLeaf> Panels,
    double ActualGap,
    PlanDoorPanelPoint OutwardNormal);

public static class PlanDoorPanelLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static PlanDoorPanelLayout Calculate(
        PlanDoorPanelPoint start,
        PlanDoorPanelPoint end,
        PlanDoorPanelPoint cursor,
        PlanDoorPanelOptions options,
        bool flipDirection)
    {
        Compatibility.NotNull(options, nameof(options));
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var baselineLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (!Compatibility.IsFinite(baselineLength) || baselineLength <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "柜体前沿长度必须大于 0。");
        }

        var directionX = deltaX / baselineLength;
        var directionY = deltaY / baselineLength;
        var normalX = directionY;
        var normalY = -directionX;
        var cursorSide = (cursor.X - start.X) * normalX + (cursor.Y - start.Y) * normalY;
        if (cursorSide < -Tolerance)
        {
            normalX = -normalX;
            normalY = -normalY;
        }

        if (flipDirection)
        {
            normalX = -normalX;
            normalY = -normalY;
        }

        var totalWidth = baselineLength + options.LeftExtension + options.RightExtension;
        var division = DoorPanelDivisionCalculator.CalculateWithGapCompensation(
            totalWidth,
            options.DoorCount,
            options.Gap,
            options.CalculationMode);
        var originX = start.X - directionX * options.LeftExtension;
        var originY = start.Y - directionY * options.LeftExtension;
        var panels = new List<PlanDoorPanelLeaf>(options.DoorCount);
        var distance = 0d;
        for (var index = 0; index < division.PanelWidths.Count; index++)
        {
            var panelStart = PointAt(originX, originY, directionX, directionY, distance);
            var panelEndDistance = index == division.PanelWidths.Count - 1
                ? totalWidth
                : distance + division.PanelWidths[index];
            var panelEnd = PointAt(originX, originY, directionX, directionY, panelEndDistance);
            var innerStart = Offset(panelStart, normalX, normalY, options.FrontClearance);
            var innerEnd = Offset(panelEnd, normalX, normalY, options.FrontClearance);
            var outerStart = Offset(innerStart, normalX, normalY, options.Thickness);
            var outerEnd = Offset(innerEnd, normalX, normalY, options.Thickness);
            panels.Add(new PlanDoorPanelLeaf(innerStart, innerEnd, outerEnd, outerStart));
            distance = panelEndDistance + division.ActualGap;
        }

        return new PlanDoorPanelLayout(
            panels,
            division.ActualGap,
            new PlanDoorPanelPoint(normalX, normalY));
    }

    private static PlanDoorPanelPoint PointAt(
        double originX,
        double originY,
        double directionX,
        double directionY,
        double distance)
    {
        return new PlanDoorPanelPoint(
            originX + directionX * distance,
            originY + directionY * distance);
    }

    private static PlanDoorPanelPoint Offset(
        PlanDoorPanelPoint point,
        double normalX,
        double normalY,
        double distance)
    {
        return new PlanDoorPanelPoint(
            point.X + normalX * distance,
            point.Y + normalY * distance);
    }
}
