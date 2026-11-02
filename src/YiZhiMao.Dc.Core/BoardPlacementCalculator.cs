namespace YiZhiMao.Dc.Core;

public readonly record struct BoardPlacementPoint(double X, double Y);

public sealed record BoardPlacementPolygon(IReadOnlyList<BoardPlacementPoint> Points);

public sealed record BoardPlacementLayout(IReadOnlyList<BoardPlacementPolygon> Polygons);

public readonly record struct BoardSpaceBounds(
    double Left,
    double Right,
    double Bottom,
    double Top);

public enum BoardCenterOrientation
{
    Horizontal,
    Vertical
}

public enum BoardOffsetSide
{
    Left,
    Right,
    Bottom,
    Top
}

public sealed record BoardOffsetPlacement(
    BoardOffsetSide Side,
    BoardPlacementLayout Layout);

public sealed record BoardOffsetLinePlacement(
    BoardOffsetSide Side,
    BoardCenterOrientation Orientation,
    IReadOnlyList<double> Coordinates);

public enum BoardPlacementDirection
{
    East,
    South,
    West,
    North
}

public enum BoardTrimSectionType
{
    L,
    T,
    I
}

public static class BoardPlacementCalculator
{
    private const double Tolerance = 0.000001;

    public static BoardPlacementLayout CalculateEdgeBoard(
        BoardPlacementPoint start,
        BoardPlacementPoint end,
        BoardPlacementPoint cursor,
        double width)
    {
        ValidatePositive(width, nameof(width));
        var (directionX, directionY) = GetDirection(start, end);
        var normalX = directionY;
        var normalY = -directionX;
        if (Dot(cursor.X - start.X, cursor.Y - start.Y, normalX, normalY) < 0)
        {
            normalX = -normalX;
            normalY = -normalY;
        }

        return new BoardPlacementLayout(
        [
            Polygon(
                start,
                end,
                Offset(end, normalX, normalY, width),
                Offset(start, normalX, normalY, width))
        ]);
    }

    public static BoardPlacementLayout CalculateCenterBoard(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        double thickness,
        BoardCenterOrientation? orientation = null)
    {
        ValidatePositive(thickness, nameof(thickness));
        if (!Compatibility.IsFinite(bounds.Left) || !Compatibility.IsFinite(bounds.Right) ||
            !Compatibility.IsFinite(bounds.Bottom) || !Compatibility.IsFinite(bounds.Top) ||
            bounds.Right - bounds.Left <= Tolerance || bounds.Top - bounds.Bottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "内空边界必须形成有效矩形。");
        }

        if (!Compatibility.IsFinite(cursor.X) || !Compatibility.IsFinite(cursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标位置必须是有效坐标。");
        }

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var selectedOrientation = orientation;
        if (selectedOrientation is null)
        {
            var deltaX = Math.Abs(cursor.X - centerX);
            var deltaY = Math.Abs(cursor.Y - centerY);
            if (deltaX <= Tolerance && deltaY <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(cursor), "请把鼠标移到空间中心的水平或竖直方向。");
            }

            selectedOrientation = deltaX >= deltaY
                ? BoardCenterOrientation.Horizontal
                : BoardCenterOrientation.Vertical;
        }

        if (!Enum.IsDefined(typeof(BoardCenterOrientation), selectedOrientation.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(orientation));
        }

        var halfThickness = thickness / 2;
        if (selectedOrientation == BoardCenterOrientation.Horizontal)
        {
            if (bounds.Top - bounds.Bottom < thickness - Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(thickness), "板厚不能大于空间高度。");
            }

            return new BoardPlacementLayout(
            [
                Polygon(
                    new BoardPlacementPoint(bounds.Left, centerY - halfThickness),
                    new BoardPlacementPoint(bounds.Right, centerY - halfThickness),
                    new BoardPlacementPoint(bounds.Right, centerY + halfThickness),
                    new BoardPlacementPoint(bounds.Left, centerY + halfThickness))
            ]);
        }

        if (bounds.Right - bounds.Left < thickness - Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "板厚不能大于空间宽度。");
        }

        return new BoardPlacementLayout(
        [
            Polygon(
                new BoardPlacementPoint(centerX - halfThickness, bounds.Bottom),
                new BoardPlacementPoint(centerX + halfThickness, bounds.Bottom),
                new BoardPlacementPoint(centerX + halfThickness, bounds.Top),
                new BoardPlacementPoint(centerX - halfThickness, bounds.Top))
        ]);
    }

    public static BoardOffsetPlacement CalculateOffsetBoard(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        double clearance,
        double thickness)
    {
        ValidatePositive(clearance, nameof(clearance));
        ValidatePositive(thickness, nameof(thickness));
        if (!Compatibility.IsFinite(bounds.Left) || !Compatibility.IsFinite(bounds.Right) ||
            !Compatibility.IsFinite(bounds.Bottom) || !Compatibility.IsFinite(bounds.Top) ||
            bounds.Right - bounds.Left <= Tolerance || bounds.Top - bounds.Bottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "内空边界必须形成有效矩形。");
        }

        if (!Compatibility.IsFinite(cursor.X) || !Compatibility.IsFinite(cursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标位置必须是有效坐标。");
        }

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var deltaX = cursor.X - centerX;
        var deltaY = cursor.Y - centerY;
        if (Math.Abs(deltaX) <= Tolerance && Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "请把鼠标移到空间中心的上、下、左或右侧。");
        }

        BoardOffsetSide side;
        BoardPlacementPolygon polygon;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            if (clearance + thickness > bounds.Right - bounds.Left + Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(clearance), "位移净距与板厚之和不能大于空间宽度。");
            }

            if (deltaX < 0)
            {
                side = BoardOffsetSide.Left;
                var left = bounds.Left + clearance;
                polygon = Polygon(
                    new BoardPlacementPoint(left, bounds.Bottom),
                    new BoardPlacementPoint(left + thickness, bounds.Bottom),
                    new BoardPlacementPoint(left + thickness, bounds.Top),
                    new BoardPlacementPoint(left, bounds.Top));
            }
            else
            {
                side = BoardOffsetSide.Right;
                var right = bounds.Right - clearance;
                polygon = Polygon(
                    new BoardPlacementPoint(right - thickness, bounds.Bottom),
                    new BoardPlacementPoint(right, bounds.Bottom),
                    new BoardPlacementPoint(right, bounds.Top),
                    new BoardPlacementPoint(right - thickness, bounds.Top));
            }
        }
        else
        {
            if (clearance + thickness > bounds.Top - bounds.Bottom + Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(clearance), "位移净距与板厚之和不能大于空间高度。");
            }

            if (deltaY < 0)
            {
                side = BoardOffsetSide.Bottom;
                var bottom = bounds.Bottom + clearance;
                polygon = Polygon(
                    new BoardPlacementPoint(bounds.Left, bottom),
                    new BoardPlacementPoint(bounds.Right, bottom),
                    new BoardPlacementPoint(bounds.Right, bottom + thickness),
                    new BoardPlacementPoint(bounds.Left, bottom + thickness));
            }
            else
            {
                side = BoardOffsetSide.Top;
                var top = bounds.Top - clearance;
                polygon = Polygon(
                    new BoardPlacementPoint(bounds.Left, top - thickness),
                    new BoardPlacementPoint(bounds.Right, top - thickness),
                    new BoardPlacementPoint(bounds.Right, top),
                    new BoardPlacementPoint(bounds.Left, top));
            }
        }

        return new BoardOffsetPlacement(side, new BoardPlacementLayout([polygon]));
    }

    public static BoardOffsetLinePlacement CalculateOffsetLines(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        double clearance,
        double thickness,
        int lineCount)
    {
        ValidatePositive(clearance, nameof(clearance));
        ValidatePositive(thickness, nameof(thickness));
        if (lineCount is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(lineCount), "放线数量只能是 1、2 或 3。");
        }

        if (!Compatibility.IsFinite(bounds.Left) || !Compatibility.IsFinite(bounds.Right) ||
            !Compatibility.IsFinite(bounds.Bottom) || !Compatibility.IsFinite(bounds.Top) ||
            bounds.Right - bounds.Left <= Tolerance || bounds.Top - bounds.Bottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "内空边界必须形成有效矩形。");
        }

        if (!Compatibility.IsFinite(cursor.X) || !Compatibility.IsFinite(cursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标位置必须是有效坐标。");
        }

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var deltaX = cursor.X - centerX;
        var deltaY = cursor.Y - centerY;
        if (Math.Abs(deltaX) <= Tolerance && Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "请把鼠标移到空间中心的上、下、左或右侧。");
        }

        var requiredThickness = lineCount > 1 ? thickness * (lineCount - 1) : 0;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            var width = bounds.Right - bounds.Left;
            if (clearance + requiredThickness > width + Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(clearance), "偏移距离与板厚不能大于空间宽度。");
            }

            if (deltaX < 0)
            {
                var outer = bounds.Left + clearance;
                return new BoardOffsetLinePlacement(
                    BoardOffsetSide.Left,
                    BoardCenterOrientation.Vertical,
                    CreateOffsetLineCoordinates(outer, outer + thickness, lineCount));
            }

            var rightOuter = bounds.Right - clearance;
            return new BoardOffsetLinePlacement(
                BoardOffsetSide.Right,
                BoardCenterOrientation.Vertical,
                CreateOffsetLineCoordinates(rightOuter, rightOuter - thickness, lineCount));
        }

        var height = bounds.Top - bounds.Bottom;
        if (clearance + requiredThickness > height + Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(clearance), "偏移距离与板厚不能大于空间高度。");
        }

        if (deltaY < 0)
        {
            var outer = bounds.Bottom + clearance;
            return new BoardOffsetLinePlacement(
                BoardOffsetSide.Bottom,
                BoardCenterOrientation.Horizontal,
                CreateOffsetLineCoordinates(outer, outer + thickness, lineCount));
        }

        var topOuter = bounds.Top - clearance;
        return new BoardOffsetLinePlacement(
            BoardOffsetSide.Top,
            BoardCenterOrientation.Horizontal,
            CreateOffsetLineCoordinates(topOuter, topOuter - thickness, lineCount));
    }

    public static BoardPlacementLayout CalculatePlanSideTrim(
        BoardPlacementPoint anchor,
        BoardPlacementPoint cursor,
        double faceWidth,
        double auxiliaryDepth,
        double thickness,
        BoardTrimSectionType sectionType = BoardTrimSectionType.L)
    {
        var deltaX = cursor.X - anchor.X;
        var deltaY = cursor.Y - anchor.Y;
        if (Math.Abs(deltaX) <= Tolerance || Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标必须位于定位角点十字线的四个象限之一。");
        }

        var faceDirection = deltaX > 0
            ? BoardPlacementDirection.East
            : BoardPlacementDirection.West;
        var auxiliaryDirection = deltaY > 0
            ? BoardPlacementDirection.North
            : BoardPlacementDirection.South;
        return CalculateTrimSection(
            anchor,
            faceDirection,
            auxiliaryDirection,
            faceWidth,
            auxiliaryDepth,
            thickness,
            sectionType);
    }

    public static BoardPlacementLayout CalculatePlanTopTrim(
        BoardPlacementPoint anchor,
        BoardPlacementPoint cursor,
        double faceWidth,
        double auxiliaryDepth,
        double thickness,
        BoardTrimSectionType sectionType = BoardTrimSectionType.L)
    {
        var deltaX = cursor.X - anchor.X;
        var deltaY = cursor.Y - anchor.Y;
        if (Math.Abs(deltaX) <= Tolerance || Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标必须位于定位角点十字线的四个象限之一。");
        }

        var faceDirection = deltaY > 0
            ? BoardPlacementDirection.North
            : BoardPlacementDirection.South;
        var auxiliaryDirection = deltaX > 0
            ? BoardPlacementDirection.East
            : BoardPlacementDirection.West;
        return CalculateTrimSection(
            anchor,
            faceDirection,
            auxiliaryDirection,
            faceWidth,
            auxiliaryDepth,
            thickness,
            sectionType);
    }

    public static BoardPlacementLayout CalculateTrimSection(
        BoardPlacementPoint anchor,
        BoardPlacementDirection faceDirection,
        BoardPlacementDirection auxiliaryDirection,
        double faceWidth,
        double auxiliaryDepth,
        double thickness,
        BoardTrimSectionType sectionType)
    {
        ValidatePositive(faceWidth, nameof(faceWidth));
        ValidatePositive(thickness, nameof(thickness));
        if (!Enum.IsDefined(typeof(BoardTrimSectionType), sectionType))
        {
            throw new ArgumentOutOfRangeException(nameof(sectionType));
        }

        if (sectionType == BoardTrimSectionType.T && faceWidth < thickness)
        {
            throw new ArgumentOutOfRangeException(
                nameof(faceWidth),
                faceWidth,
                "T型收口正面宽度不能小于板厚。");
        }

        var (faceX, faceY) = GetDirectionVector(faceDirection);
        var (depthX, depthY) = GetDirectionVector(auxiliaryDirection);
        if (Math.Abs(Dot(faceX, faceY, depthX, depthY)) > Tolerance)
        {
            throw new ArgumentException("正面板与辅助板方向必须互相垂直。", nameof(auxiliaryDirection));
        }

        var faceEnd = Offset(anchor, faceX, faceY, faceWidth);
        var faceBackStart = Offset(anchor, depthX, depthY, thickness);
        var faceBackEnd = Offset(faceEnd, depthX, depthY, thickness);
        var facePolygon = Polygon(anchor, faceEnd, faceBackEnd, faceBackStart);
        if (sectionType == BoardTrimSectionType.I)
        {
            return new BoardPlacementLayout([facePolygon]);
        }

        ValidatePositive(auxiliaryDepth, nameof(auxiliaryDepth));
        var auxiliaryOffset = sectionType == BoardTrimSectionType.T
            ? (faceWidth - thickness) / 2
            : 0;
        var auxiliaryStart = Offset(faceBackStart, faceX, faceY, auxiliaryOffset);
        var auxiliaryEnd = Offset(auxiliaryStart, depthX, depthY, auxiliaryDepth);
        var auxiliaryOuterEnd = Offset(auxiliaryEnd, faceX, faceY, thickness);
        var auxiliaryOuterStart = Offset(auxiliaryStart, faceX, faceY, thickness);

        return new BoardPlacementLayout(
        [
            facePolygon,
            Polygon(auxiliaryStart, auxiliaryEnd, auxiliaryOuterEnd, auxiliaryOuterStart)
        ]);
    }

    public static BoardPlacementLayout CalculatePlanVisiblePanel(
        BoardPlacementPoint anchor,
        BoardPlacementPoint frontDirection,
        BoardPlacementPoint cursor,
        double depth,
        double thickness)
    {
        ValidatePositive(depth, nameof(depth));
        ValidatePositive(thickness, nameof(thickness));
        var (frontX, frontY) = GetDirection(anchor, frontDirection);
        var normalX = frontY;
        var normalY = -frontX;
        if (Dot(cursor.X - anchor.X, cursor.Y - anchor.Y, normalX, normalY) < 0)
        {
            normalX = -normalX;
            normalY = -normalY;
        }

        var frontEnd = Offset(anchor, frontX, frontY, thickness);
        var backStart = Offset(anchor, normalX, normalY, depth);
        var backEnd = Offset(frontEnd, normalX, normalY, depth);
        return new BoardPlacementLayout([Polygon(anchor, frontEnd, backEnd, backStart)]);
    }

    private static IReadOnlyList<double> CreateOffsetLineCoordinates(
        double outerCoordinate,
        double nextCoordinate,
        int lineCount)
    {
        if (lineCount == 1)
        {
            return [outerCoordinate];
        }

        var step = nextCoordinate - outerCoordinate;
        var lastCoordinate = outerCoordinate + step * (lineCount - 1);
        var minimum = Math.Min(outerCoordinate, lastCoordinate);
        var maximum = Math.Max(outerCoordinate, lastCoordinate);
        return lineCount == 2
            ? [minimum, maximum]
            : [minimum, minimum + Math.Abs(step), maximum];
    }

    private static BoardPlacementPolygon Polygon(params BoardPlacementPoint[] points) => new(points);

    private static (double X, double Y) GetDirectionVector(BoardPlacementDirection direction) => direction switch
    {
        BoardPlacementDirection.East => (1, 0),
        BoardPlacementDirection.South => (0, -1),
        BoardPlacementDirection.West => (-1, 0),
        BoardPlacementDirection.North => (0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    private static BoardPlacementPoint Offset(
        BoardPlacementPoint point,
        double directionX,
        double directionY,
        double distance) =>
        new(point.X + directionX * distance, point.Y + directionY * distance);

    private static (double X, double Y) GetDirection(BoardPlacementPoint start, BoardPlacementPoint end)
    {
        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (!Compatibility.IsFinite(length) || length <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "定位方向长度必须大于0。");
        }

        return (deltaX / length, deltaY / length);
    }

    private static double Dot(double firstX, double firstY, double secondX, double secondY) =>
        firstX * secondX + firstY * secondY;

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!Compatibility.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "尺寸必须大于0。");
        }
    }
}
