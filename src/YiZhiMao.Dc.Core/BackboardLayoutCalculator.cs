namespace YiZhiMao.Dc.Core;

public sealed record BackboardLayoutOptions(
    double Thickness,
    double Inset,
    double LeftExtension,
    double RightExtension,
    double TopExtension,
    double BottomExtension);

public sealed record BackboardLayout(
    BoardOffsetSide RearSide,
    BoardPlacementPolygon PlanPolygon,
    BoardPlacementPolygon ElevationPolygon);

public static class BackboardLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static BackboardLayout Calculate(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        BackboardLayoutOptions options)
    {
        Validate(bounds, cursor, options);

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var deltaX = cursor.X - centerX;
        var deltaY = cursor.Y - centerY;
        if (Math.Abs(deltaX) <= Tolerance && Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cursor),
                "请把鼠标移到内空中心的上、下、左或右侧。" );
        }

        BoardOffsetSide rearSide;
        BoardPlacementPolygon planPolygon;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            if (options.Inset + options.Thickness > bounds.Right - bounds.Left + Tolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.Inset),
                    "缩进距离与背板厚度之和不能大于内空宽度。" );
            }

            if (deltaX < 0)
            {
                rearSide = BoardOffsetSide.Left;
                var outer = bounds.Left + options.Inset;
                planPolygon = Polygon(
                    outer,
                    bounds.Bottom - options.BottomExtension,
                    outer + options.Thickness,
                    bounds.Top + options.TopExtension);
            }
            else
            {
                rearSide = BoardOffsetSide.Right;
                var outer = bounds.Right - options.Inset;
                planPolygon = Polygon(
                    outer - options.Thickness,
                    bounds.Bottom - options.BottomExtension,
                    outer,
                    bounds.Top + options.TopExtension);
            }
        }
        else
        {
            if (options.Inset + options.Thickness > bounds.Top - bounds.Bottom + Tolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.Inset),
                    "缩进距离与背板厚度之和不能大于内空高度。" );
            }

            if (deltaY < 0)
            {
                rearSide = BoardOffsetSide.Bottom;
                var outer = bounds.Bottom + options.Inset;
                planPolygon = Polygon(
                    bounds.Left - options.LeftExtension,
                    outer,
                    bounds.Right + options.RightExtension,
                    outer + options.Thickness);
            }
            else
            {
                rearSide = BoardOffsetSide.Top;
                var outer = bounds.Top - options.Inset;
                planPolygon = Polygon(
                    bounds.Left - options.LeftExtension,
                    outer - options.Thickness,
                    bounds.Right + options.RightExtension,
                    outer);
            }
        }

        var elevationPolygon = Polygon(
            bounds.Left - options.LeftExtension,
            bounds.Bottom - options.BottomExtension,
            bounds.Right + options.RightExtension,
            bounds.Top + options.TopExtension);
        return new BackboardLayout(rearSide, planPolygon, elevationPolygon);
    }

    private static void Validate(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        BackboardLayoutOptions options)
    {
        if (!Compatibility.IsFinite(bounds.Left) || !Compatibility.IsFinite(bounds.Right) ||
            !Compatibility.IsFinite(bounds.Bottom) || !Compatibility.IsFinite(bounds.Top) ||
            bounds.Right - bounds.Left <= Tolerance || bounds.Top - bounds.Bottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "内空边界必须形成有效矩形。" );
        }

        if (!Compatibility.IsFinite(cursor.X) || !Compatibility.IsFinite(cursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标位置必须是有效坐标。" );
        }

        if (!Compatibility.IsFinite(options.Thickness) || options.Thickness <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Thickness), "背板厚度必须大于 0。" );
        }

        double[] nonNegativeValues =
        [
            options.Inset,
            options.LeftExtension,
            options.RightExtension,
            options.TopExtension,
            options.BottomExtension
        ];
        if (nonNegativeValues.Any(value => !Compatibility.IsFinite(value) || value < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "背板缩进与延伸距离不能小于 0。" );
        }
    }

    private static BoardPlacementPolygon Polygon(
        double left,
        double bottom,
        double right,
        double top) => new(
        [
            new BoardPlacementPoint(left, bottom),
            new BoardPlacementPoint(right, bottom),
            new BoardPlacementPoint(right, top),
            new BoardPlacementPoint(left, top)
        ]);
}
