namespace YiZhiMao.Dc.Core;

public readonly record struct BoardLineEditParameters(double StartParameter, double EndParameter);

public readonly record struct BoardLineExtensionLayout(BoardPlacementPoint Start, BoardPlacementPoint End);

public enum BoardSplitOrientation
{
    Vertical,
    Horizontal
}

public sealed record BoardSplitLayout(
    BoardSplitOrientation Orientation,
    IReadOnlyList<double> Coordinates,
    IReadOnlyList<BoardSpaceBounds> Parts);

public sealed record BoardRectangleEditLayout(
    BoardRectangleSide Side,
    double BoundaryCoordinate,
    BoardSpaceBounds Bounds);

public enum BoardRectangleSide
{
    Left,
    Right,
    Bottom,
    Top
}

public enum BoardProjectionKind
{
    ShelfSideView,
    VerticalBoardPlanView
}

public sealed record BoardProjectionLayout(BoardProjectionKind Kind, BoardSpaceBounds Bounds);

public static class BoardGeometryCalculator
{
    private const double DefaultTolerance = 0.000001;

    public static IReadOnlyList<double> GetParallelLineOffsets(int lineCount, double thickness)
    {
        if (lineCount is not (2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(lineCount), "替换线数量只能是 2 或 3。");
        }

        if (!Compatibility.IsFinite(thickness) || thickness <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "偏移板厚必须大于 0。");
        }

        var half = thickness / 2;
        return lineCount == 2 ? [-half, half] : [-thickness, 0, thickness];
    }

    public static BoardLineExtensionLayout ExtendLineByDrawingAxes(
        BoardPlacementPoint start,
        BoardPlacementPoint end,
        double upperOrLeftExtension,
        double lowerOrRightExtension)
    {
        if (!IsFinitePoint(start) || !IsFinitePoint(end))
        {
            throw new ArgumentOutOfRangeException(nameof(start), "直线端点必须有效。");
        }

        if (!Compatibility.IsFinite(upperOrLeftExtension) || upperOrLeftExtension < 0 ||
            !Compatibility.IsFinite(lowerOrRightExtension) || lowerOrRightExtension < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(upperOrLeftExtension), "延伸值不能小于 0。");
        }

        var deltaX = end.X - start.X;
        var deltaY = end.Y - start.Y;
        var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        if (length <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "直线长度必须大于 0。");
        }

        var vertical = Math.Abs(deltaY) >= Math.Abs(deltaX);
        var startIsUpperOrLeft = vertical ? start.Y > end.Y : start.X < end.X;
        var startExtension = startIsUpperOrLeft ? upperOrLeftExtension : lowerOrRightExtension;
        var endExtension = startIsUpperOrLeft ? lowerOrRightExtension : upperOrLeftExtension;
        var unitX = deltaX / length;
        var unitY = deltaY / length;
        return new BoardLineExtensionLayout(
            new BoardPlacementPoint(start.X - unitX * startExtension, start.Y - unitY * startExtension),
            new BoardPlacementPoint(end.X + unitX * endExtension, end.Y + unitY * endExtension));
    }

    public static bool IsInteriorSplitCoordinate(
        double minimum,
        double maximum,
        double coordinate,
        double tolerance = DefaultTolerance)
    {
        return Compatibility.IsFinite(minimum) &&
               Compatibility.IsFinite(maximum) &&
               Compatibility.IsFinite(coordinate) &&
               Compatibility.IsFinite(tolerance) &&
               tolerance >= 0 &&
               maximum - minimum > tolerance * 2 &&
               coordinate > minimum + tolerance &&
               coordinate < maximum - tolerance;
    }

    public static BoardSplitLayout CalculateSplitLayoutFromTwoPoints(
        BoardSpaceBounds bounds,
        BoardPlacementPoint firstPoint,
        BoardPlacementPoint secondPoint,
        double tolerance = DefaultTolerance)
    {
        ValidateBounds(bounds, tolerance);
        if (!IsPointWithinBounds(bounds, firstPoint, tolerance) ||
            !IsPointWithinBounds(bounds, secondPoint, tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(secondPoint), "分割点必须位于所选矩形范围内。");
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Top - bounds.Bottom;
        var orientation = width >= height
            ? BoardSplitOrientation.Vertical
            : BoardSplitOrientation.Horizontal;
        var firstCoordinate = orientation == BoardSplitOrientation.Vertical
            ? firstPoint.X
            : firstPoint.Y;
        var secondCoordinate = orientation == BoardSplitOrientation.Vertical
            ? secondPoint.X
            : secondPoint.Y;
        var minimum = orientation == BoardSplitOrientation.Vertical ? bounds.Left : bounds.Bottom;
        var maximum = orientation == BoardSplitOrientation.Vertical ? bounds.Right : bounds.Top;
        if (!IsInteriorSplitCoordinate(minimum, maximum, firstCoordinate, tolerance) ||
            !IsInteriorSplitCoordinate(minimum, maximum, secondCoordinate, tolerance))
        {
            throw new InvalidOperationException("分割位置必须位于板件长向内部，不能落在板件两端。");
        }

        double[] coordinates = Math.Abs(firstCoordinate - secondCoordinate) <= tolerance
            ? [(firstCoordinate + secondCoordinate) / 2]
            : [Math.Min(firstCoordinate, secondCoordinate), Math.Max(firstCoordinate, secondCoordinate)];

        if (orientation == BoardSplitOrientation.Vertical)
        {
            if (coordinates.Length == 1)
            {
                var coordinate = coordinates[0];
                return new BoardSplitLayout(
                    orientation,
                    coordinates,
                    [bounds with { Right = coordinate }, bounds with { Left = coordinate }]);
            }

            return new BoardSplitLayout(
                orientation,
                coordinates,
                [
                    bounds with { Right = coordinates[0] },
                    bounds with { Left = coordinates[0], Right = coordinates[1] },
                    bounds with { Left = coordinates[1] }
                ]);
        }

        if (coordinates.Length == 1)
        {
            var coordinate = coordinates[0];
            return new BoardSplitLayout(
                orientation,
                coordinates,
                [bounds with { Top = coordinate }, bounds with { Bottom = coordinate }]);
        }

        return new BoardSplitLayout(
            orientation,
            coordinates,
            [
                bounds with { Top = coordinates[0] },
                bounds with { Bottom = coordinates[0], Top = coordinates[1] },
                bounds with { Bottom = coordinates[1] }
            ]);
    }

    public static BoardLineEditParameters CalculateLineTrim(
        double intersectionParameter,
        double pickedParameter,
        double tolerance = DefaultTolerance)
    {
        if (!Compatibility.IsFinite(intersectionParameter) ||
            !Compatibility.IsFinite(pickedParameter) ||
            !Compatibility.IsFinite(tolerance) ||
            tolerance < 0 ||
            intersectionParameter <= tolerance ||
            intersectionParameter >= 1 - tolerance ||
            pickedParameter < -tolerance ||
            pickedParameter > 1 + tolerance)
        {
            throw new InvalidOperationException("交点或点击位置不符合修剪条件。");
        }

        if (pickedParameter < intersectionParameter - tolerance)
        {
            return new BoardLineEditParameters(intersectionParameter, 1);
        }

        if (pickedParameter > intersectionParameter + tolerance)
        {
            return new BoardLineEditParameters(0, intersectionParameter);
        }

        throw new InvalidOperationException("请点击需要删除的线段，不要点击交点本身。");
    }

    public static BoardLineEditParameters CalculateNearestLineTrim(
        BoardPlacementPoint startPoint,
        BoardPlacementPoint endPoint,
        BoardPlacementPoint pickedPoint,
        IReadOnlyList<AxisAlignedSegment> segments,
        double tolerance = DefaultTolerance)
    {
        Compatibility.NotNull(segments, nameof(segments));
        if (!IsFinitePoint(startPoint) || !IsFinitePoint(endPoint) || !IsFinitePoint(pickedPoint) ||
            !Compatibility.IsFinite(tolerance) || tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pickedPoint));
        }

        var deltaX = endPoint.X - startPoint.X;
        var deltaY = endPoint.Y - startPoint.Y;
        var lengthSquared = deltaX * deltaX + deltaY * deltaY;
        if (lengthSquared <= tolerance * tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(endPoint), "被修剪直线长度必须大于 0。");
        }

        var pickedParameter = ((pickedPoint.X - startPoint.X) * deltaX +
                               (pickedPoint.Y - startPoint.Y) * deltaY) / lengthSquared;
        if (pickedParameter < -tolerance || pickedParameter > 1 + tolerance ||
            Math.Abs(pickedParameter - 0.5) <= tolerance)
        {
            throw new InvalidOperationException("请点击直线靠近需要修剪的一端。");
        }

        var trimStart = pickedParameter < 0.5;
        var bestDistance = double.MaxValue;
        double? intersectionParameter = null;
        foreach (var segment in segments)
        {
            if (!TryGetIntersectionParameter(startPoint, endPoint, segment, tolerance, out var candidate) ||
                !IsInteriorSplitCoordinate(0, 1, candidate, tolerance) ||
                (trimStart && candidate <= pickedParameter + tolerance) ||
                (!trimStart && candidate >= pickedParameter - tolerance))
            {
                continue;
            }

            var distance = Math.Abs(candidate - pickedParameter);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                intersectionParameter = candidate;
            }
        }

        if (!intersectionParameter.HasValue)
        {
            throw new InvalidOperationException("点击端内侧没有找到与直线实际相交的有效边界。");
        }

        return CalculateLineTrim(intersectionParameter.Value, pickedParameter, tolerance);
    }

    public static BoardSpaceBounds CalculateRectangleEdit(
        BoardSpaceBounds bounds,
        BoardRectangleSide side,
        double boundaryCoordinate,
        bool isExtend,
        double tolerance = DefaultTolerance)
    {
        ValidateBounds(bounds, tolerance);
        if (!Enum.IsDefined(typeof(BoardRectangleSide), side))
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        if (!Compatibility.IsFinite(boundaryCoordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(boundaryCoordinate));
        }

        var outside = side switch
        {
            BoardRectangleSide.Left => boundaryCoordinate < bounds.Left - tolerance,
            BoardRectangleSide.Right => boundaryCoordinate > bounds.Right + tolerance,
            BoardRectangleSide.Bottom => boundaryCoordinate < bounds.Bottom - tolerance,
            BoardRectangleSide.Top => boundaryCoordinate > bounds.Top + tolerance,
            _ => false
        };
        var inside = side switch
        {
            BoardRectangleSide.Left or BoardRectangleSide.Right =>
                boundaryCoordinate > bounds.Left + tolerance &&
                boundaryCoordinate < bounds.Right - tolerance,
            BoardRectangleSide.Bottom or BoardRectangleSide.Top =>
                boundaryCoordinate > bounds.Bottom + tolerance &&
                boundaryCoordinate < bounds.Top - tolerance,
            _ => false
        };
        if ((isExtend && !outside) || (!isExtend && !inside))
        {
            throw new InvalidOperationException("边界位置不符合当前延伸或修剪条件。");
        }

        return side switch
        {
            BoardRectangleSide.Left => bounds with { Left = boundaryCoordinate },
            BoardRectangleSide.Right => bounds with { Right = boundaryCoordinate },
            BoardRectangleSide.Bottom => bounds with { Bottom = boundaryCoordinate },
            BoardRectangleSide.Top => bounds with { Top = boundaryCoordinate },
            _ => throw new ArgumentOutOfRangeException(nameof(side))
        };
    }

    public static BoardRectangleSide SelectRectangleEditSide(
        BoardSpaceBounds bounds,
        BoardPlacementPoint pickedPoint,
        bool verticalBoundary)
    {
        ValidateBounds(bounds, DefaultTolerance);
        if (!Compatibility.IsFinite(pickedPoint.X) || !Compatibility.IsFinite(pickedPoint.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(pickedPoint));
        }

        if (verticalBoundary)
        {
            return Math.Abs(pickedPoint.X - bounds.Left) <= Math.Abs(pickedPoint.X - bounds.Right)
                ? BoardRectangleSide.Left
                : BoardRectangleSide.Right;
        }

        return Math.Abs(pickedPoint.Y - bounds.Bottom) <= Math.Abs(pickedPoint.Y - bounds.Top)
            ? BoardRectangleSide.Bottom
            : BoardRectangleSide.Top;
    }

    public static BoardRectangleEditLayout CalculateNearestRectangleExtension(
        BoardSpaceBounds bounds,
        BoardPlacementPoint pickedPoint,
        IReadOnlyList<AxisAlignedSegment> segments,
        double tolerance = DefaultTolerance)
    {
        ValidateBounds(bounds, tolerance);
        Compatibility.NotNull(segments, nameof(segments));
        var side = SelectLongAxisEnd(bounds, pickedPoint, tolerance);
        var verticalSide = side is BoardRectangleSide.Left or BoardRectangleSide.Right;
        var bestDistance = double.MaxValue;
        double? boundaryCoordinate = null;

        foreach (var segment in segments)
        {
            if (!Compatibility.IsFinite(segment.StartX) ||
                !Compatibility.IsFinite(segment.StartY) ||
                !Compatibility.IsFinite(segment.EndX) ||
                !Compatibility.IsFinite(segment.EndY))
            {
                continue;
            }

            var verticalSegment = Math.Abs(segment.StartX - segment.EndX) <= tolerance &&
                                  Math.Abs(segment.StartY - segment.EndY) > tolerance;
            var horizontalSegment = Math.Abs(segment.StartY - segment.EndY) <= tolerance &&
                                    Math.Abs(segment.StartX - segment.EndX) > tolerance;
            if ((verticalSide && !verticalSegment) || (!verticalSide && !horizontalSegment))
            {
                continue;
            }

            var coordinate = verticalSide
                ? (segment.StartX + segment.EndX) / 2
                : (segment.StartY + segment.EndY) / 2;
            var outward = side switch
            {
                BoardRectangleSide.Left => coordinate < bounds.Left - tolerance,
                BoardRectangleSide.Right => coordinate > bounds.Right + tolerance,
                BoardRectangleSide.Bottom => coordinate < bounds.Bottom - tolerance,
                BoardRectangleSide.Top => coordinate > bounds.Top + tolerance,
                _ => false
            };
            if (!outward)
            {
                continue;
            }

            var overlap = verticalSide
                ? Math.Min(Math.Max(segment.StartY, segment.EndY), bounds.Top) -
                  Math.Max(Math.Min(segment.StartY, segment.EndY), bounds.Bottom)
                : Math.Min(Math.Max(segment.StartX, segment.EndX), bounds.Right) -
                  Math.Max(Math.Min(segment.StartX, segment.EndX), bounds.Left);
            if (overlap <= tolerance)
            {
                continue;
            }

            var distance = side switch
            {
                BoardRectangleSide.Left => bounds.Left - coordinate,
                BoardRectangleSide.Right => coordinate - bounds.Right,
                BoardRectangleSide.Bottom => bounds.Bottom - coordinate,
                BoardRectangleSide.Top => coordinate - bounds.Top,
                _ => double.MaxValue
            };
            if (distance < bestDistance)
            {
                bestDistance = distance;
                boundaryCoordinate = coordinate;
            }
        }

        if (!boundaryCoordinate.HasValue)
        {
            throw new InvalidOperationException("点击方向没有找到与矩形边范围重叠的外侧平行线。" );
        }

        return new BoardRectangleEditLayout(
            side,
            boundaryCoordinate.Value,
            CalculateRectangleEdit(bounds, side, boundaryCoordinate.Value, isExtend: true, tolerance));
    }

    public static BoardRectangleEditLayout CalculateNearestRectangleTrim(
        BoardSpaceBounds bounds,
        BoardPlacementPoint pickedPoint,
        IReadOnlyList<AxisAlignedSegment> segments,
        double tolerance = DefaultTolerance)
    {
        ValidateBounds(bounds, tolerance);
        Compatibility.NotNull(segments, nameof(segments));
        var side = SelectLongAxisEnd(bounds, pickedPoint, tolerance);
        var verticalSide = side is BoardRectangleSide.Left or BoardRectangleSide.Right;
        var pickedCoordinate = verticalSide ? pickedPoint.X : pickedPoint.Y;
        var bestDistance = double.MaxValue;
        double? boundaryCoordinate = null;

        foreach (var segment in segments)
        {
            if (!IsFiniteSegment(segment))
            {
                continue;
            }

            var verticalSegment = Math.Abs(segment.StartX - segment.EndX) <= tolerance &&
                                  Math.Abs(segment.StartY - segment.EndY) > tolerance;
            var horizontalSegment = Math.Abs(segment.StartY - segment.EndY) <= tolerance &&
                                    Math.Abs(segment.StartX - segment.EndX) > tolerance;
            if ((verticalSide && !verticalSegment) || (!verticalSide && !horizontalSegment))
            {
                continue;
            }

            var coordinate = verticalSide
                ? (segment.StartX + segment.EndX) / 2
                : (segment.StartY + segment.EndY) / 2;
            var towardInterior = side switch
            {
                BoardRectangleSide.Left => coordinate > pickedCoordinate + tolerance,
                BoardRectangleSide.Right => coordinate < pickedCoordinate - tolerance,
                BoardRectangleSide.Bottom => coordinate > pickedCoordinate + tolerance,
                BoardRectangleSide.Top => coordinate < pickedCoordinate - tolerance,
                _ => false
            };
            var inside = verticalSide
                ? IsInteriorSplitCoordinate(bounds.Left, bounds.Right, coordinate, tolerance)
                : IsInteriorSplitCoordinate(bounds.Bottom, bounds.Top, coordinate, tolerance);
            if (!towardInterior || !inside)
            {
                continue;
            }

            var overlap = verticalSide
                ? Math.Min(Math.Max(segment.StartY, segment.EndY), bounds.Top) -
                  Math.Max(Math.Min(segment.StartY, segment.EndY), bounds.Bottom)
                : Math.Min(Math.Max(segment.StartX, segment.EndX), bounds.Right) -
                  Math.Max(Math.Min(segment.StartX, segment.EndX), bounds.Left);
            if (overlap <= tolerance)
            {
                continue;
            }

            var distance = Math.Abs(coordinate - pickedCoordinate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                boundaryCoordinate = coordinate;
            }
        }

        if (!boundaryCoordinate.HasValue)
        {
            throw new InvalidOperationException("点击端内侧没有找到与矩形厚度范围重叠的有效边界。");
        }

        return new BoardRectangleEditLayout(
            side,
            boundaryCoordinate.Value,
            CalculateRectangleEdit(bounds, side, boundaryCoordinate.Value, isExtend: false, tolerance));
    }

    public static bool TryMergeBounds(
        BoardSpaceBounds first,
        BoardSpaceBounds second,
        out BoardSpaceBounds merged,
        double tolerance = DefaultTolerance)
    {
        merged = default;
        if (!AreValidBounds(first, tolerance) || !AreValidBounds(second, tolerance))
        {
            return false;
        }

        var sameVerticalSpan = Math.Abs(first.Bottom - second.Bottom) <= tolerance &&
                               Math.Abs(first.Top - second.Top) <= tolerance;
        var touchesLeftOrRight = Math.Abs(first.Right - second.Left) <= tolerance ||
                                 Math.Abs(second.Right - first.Left) <= tolerance;
        var sameHorizontalSpan = Math.Abs(first.Left - second.Left) <= tolerance &&
                                 Math.Abs(first.Right - second.Right) <= tolerance;
        var touchesTopOrBottom = Math.Abs(first.Top - second.Bottom) <= tolerance ||
                                 Math.Abs(second.Top - first.Bottom) <= tolerance;
        if ((!sameVerticalSpan || !touchesLeftOrRight) &&
            (!sameHorizontalSpan || !touchesTopOrBottom))
        {
            return false;
        }

        merged = new BoardSpaceBounds(
            Math.Min(first.Left, second.Left),
            Math.Max(first.Right, second.Right),
            Math.Min(first.Bottom, second.Bottom),
            Math.Max(first.Top, second.Top));
        return true;
    }

    public static BoardProjectionLayout CalculateProjection(
        BoardSpaceBounds source,
        BoardPlacementPoint depthStart,
        BoardPlacementPoint depthEnd,
        double tolerance = DefaultTolerance)
    {
        ValidateBounds(source, tolerance);
        if (!Compatibility.IsFinite(depthStart.X) ||
            !Compatibility.IsFinite(depthStart.Y) ||
            !Compatibility.IsFinite(depthEnd.X) ||
            !Compatibility.IsFinite(depthEnd.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(depthEnd));
        }

        var width = source.Right - source.Left;
        var height = source.Top - source.Bottom;
        if (width >= height)
        {
            var left = Math.Min(depthStart.X, depthEnd.X);
            var right = Math.Max(depthStart.X, depthEnd.X);
            if (right - left <= tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(depthEnd), "层板侧视投影需要横向深度范围。");
            }

            return new BoardProjectionLayout(
                BoardProjectionKind.ShelfSideView,
                new BoardSpaceBounds(left, right, source.Bottom, source.Top));
        }

        var bottom = Math.Min(depthStart.Y, depthEnd.Y);
        var top = Math.Max(depthStart.Y, depthEnd.Y);
        if (top - bottom <= tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(depthEnd), "立板俯视投影需要竖向深度范围。");
        }

        return new BoardProjectionLayout(
            BoardProjectionKind.VerticalBoardPlanView,
            new BoardSpaceBounds(source.Left, source.Right, bottom, top));
    }

    public static bool HaveCompatibleMergeProperties(
        string firstLayer,
        string secondLayer,
        double firstThickness,
        double secondThickness,
        double firstElevation,
        double secondElevation,
        double tolerance = DefaultTolerance)
    {
        return !string.IsNullOrWhiteSpace(firstLayer) &&
               string.Equals(firstLayer, secondLayer, StringComparison.OrdinalIgnoreCase) &&
               Compatibility.IsFinite(firstThickness) &&
               Compatibility.IsFinite(secondThickness) &&
               Compatibility.IsFinite(firstElevation) &&
               Compatibility.IsFinite(secondElevation) &&
               Compatibility.IsFinite(tolerance) &&
               tolerance >= 0 &&
               Math.Abs(firstThickness - secondThickness) <= tolerance &&
               Math.Abs(firstElevation - secondElevation) <= tolerance;
    }

    private static void ValidateBounds(BoardSpaceBounds bounds, double tolerance)
    {
        if (!AreValidBounds(bounds, tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "板件边界必须是有效矩形。");
        }
    }

    private static BoardRectangleSide SelectLongAxisEnd(
        BoardSpaceBounds bounds,
        BoardPlacementPoint pickedPoint,
        double tolerance)
    {
        if (!Compatibility.IsFinite(pickedPoint.X) || !Compatibility.IsFinite(pickedPoint.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(pickedPoint));
        }

        var width = bounds.Right - bounds.Left;
        var height = bounds.Top - bounds.Bottom;
        if (width >= height)
        {
            var centerX = (bounds.Left + bounds.Right) / 2;
            if (Math.Abs(pickedPoint.X - centerX) <= tolerance)
            {
                throw new InvalidOperationException("请点击横向板件靠左或靠右的一端。");
            }

            return pickedPoint.X < centerX ? BoardRectangleSide.Left : BoardRectangleSide.Right;
        }

        var centerY = (bounds.Bottom + bounds.Top) / 2;
        if (Math.Abs(pickedPoint.Y - centerY) <= tolerance)
        {
            throw new InvalidOperationException("请点击竖向板件靠下或靠上的一端。");
        }

        return pickedPoint.Y < centerY ? BoardRectangleSide.Bottom : BoardRectangleSide.Top;
    }

    private static bool TryGetIntersectionParameter(
        BoardPlacementPoint startPoint,
        BoardPlacementPoint endPoint,
        AxisAlignedSegment segment,
        double tolerance,
        out double parameter)
    {
        parameter = 0;
        if (!IsFiniteSegment(segment))
        {
            return false;
        }

        var deltaX = endPoint.X - startPoint.X;
        var deltaY = endPoint.Y - startPoint.Y;
        var vertical = Math.Abs(segment.StartX - segment.EndX) <= tolerance &&
                       Math.Abs(segment.StartY - segment.EndY) > tolerance;
        if (vertical)
        {
            if (Math.Abs(deltaX) <= tolerance)
            {
                return false;
            }

            var x = (segment.StartX + segment.EndX) / 2;
            parameter = (x - startPoint.X) / deltaX;
            var y = startPoint.Y + parameter * deltaY;
            return y >= Math.Min(segment.StartY, segment.EndY) - tolerance &&
                   y <= Math.Max(segment.StartY, segment.EndY) + tolerance;
        }

        var horizontal = Math.Abs(segment.StartY - segment.EndY) <= tolerance &&
                         Math.Abs(segment.StartX - segment.EndX) > tolerance;
        if (!horizontal || Math.Abs(deltaY) <= tolerance)
        {
            return false;
        }

        var candidateY = (segment.StartY + segment.EndY) / 2;
        parameter = (candidateY - startPoint.Y) / deltaY;
        var candidateX = startPoint.X + parameter * deltaX;
        return candidateX >= Math.Min(segment.StartX, segment.EndX) - tolerance &&
               candidateX <= Math.Max(segment.StartX, segment.EndX) + tolerance;
    }

    private static bool IsFinitePoint(BoardPlacementPoint point) =>
        Compatibility.IsFinite(point.X) && Compatibility.IsFinite(point.Y);

    private static bool IsFiniteSegment(AxisAlignedSegment segment) =>
        Compatibility.IsFinite(segment.StartX) &&
        Compatibility.IsFinite(segment.StartY) &&
        Compatibility.IsFinite(segment.EndX) &&
        Compatibility.IsFinite(segment.EndY);

    private static bool IsPointWithinBounds(
        BoardSpaceBounds bounds,
        BoardPlacementPoint point,
        double tolerance) =>
        Compatibility.IsFinite(point.X) &&
        Compatibility.IsFinite(point.Y) &&
        point.X >= bounds.Left - tolerance &&
        point.X <= bounds.Right + tolerance &&
        point.Y >= bounds.Bottom - tolerance &&
        point.Y <= bounds.Top + tolerance;

    private static bool AreValidBounds(BoardSpaceBounds bounds, double tolerance) =>
        Compatibility.IsFinite(bounds.Left) &&
        Compatibility.IsFinite(bounds.Right) &&
        Compatibility.IsFinite(bounds.Bottom) &&
        Compatibility.IsFinite(bounds.Top) &&
        Compatibility.IsFinite(tolerance) &&
        tolerance >= 0 &&
        bounds.Right - bounds.Left > tolerance &&
        bounds.Top - bounds.Bottom > tolerance;
}
