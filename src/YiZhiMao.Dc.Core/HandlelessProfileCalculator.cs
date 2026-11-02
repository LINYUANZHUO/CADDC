namespace YiZhiMao.Dc.Core;

public enum HandlelessProfileMode
{
    Automatic,
    Middle,
    Top
}

public enum HandlelessProfileFrontSide
{
    Left,
    Right
}

public readonly record struct HandlelessProfileRectangle(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Top => Y + Height;
}

public sealed record HandlelessProfileAnchor(
    HandlelessProfileMode Type,
    double DoorEdgeMinX,
    double DoorEdgeMaxX,
    double UpperBoundaryY,
    double LowerBoundaryY);

public sealed record HandlelessProfileLayout(
    HandlelessProfileMode Type,
    HandlelessProfileFrontSide FrontSide,
    IReadOnlyList<HandlelessProfileRectangle> Boards,
    double DoorOverlap);

public static class HandlelessProfileCalculator
{
    private const double Tolerance = 0.000001;

    public static bool TryDetect(
        BoardPlacementPoint click,
        IReadOnlyList<AxisAlignedSegment> segments,
        HandlelessProfileMode requestedMode,
        double boardThickness,
        double profileSize,
        double handlelessGap,
        out HandlelessProfileAnchor anchor)
    {
        Compatibility.NotNull(segments, nameof(segments));
        ValidateParameters(click, requestedMode, boardThickness, profileSize, handlelessGap);

        var horizontal = segments
            .Where(IsFiniteHorizontal)
            .Select(NormalizeHorizontal)
            .ToArray();
        Candidate? best = null;
        if (requestedMode is HandlelessProfileMode.Automatic or HandlelessProfileMode.Middle)
        {
            FindMiddleCandidates(click, horizontal, boardThickness, profileSize, handlelessGap, ref best);
        }

        if (requestedMode is HandlelessProfileMode.Automatic or HandlelessProfileMode.Top)
        {
            FindTopCandidates(click, horizontal, boardThickness, profileSize, handlelessGap, ref best);
        }

        if (best is null || best.Value.ClickDistance > profileSize + Tolerance)
        {
            anchor = null!;
            return false;
        }

        anchor = best.Value.Anchor;
        return true;
    }

    public static HandlelessProfileLayout Calculate(
        HandlelessProfileAnchor anchor,
        HandlelessProfileFrontSide frontSide,
        double boardThickness,
        double profileSize)
    {
        Compatibility.NotNull(anchor, nameof(anchor));
        if (!Enum.IsDefined(typeof(HandlelessProfileMode), anchor.Type) ||
            anchor.Type == HandlelessProfileMode.Automatic ||
            !Enum.IsDefined(typeof(HandlelessProfileFrontSide), frontSide) ||
            !Compatibility.IsFinite(anchor.DoorEdgeMinX) ||
            !Compatibility.IsFinite(anchor.DoorEdgeMaxX) ||
            !Compatibility.IsFinite(anchor.UpperBoundaryY) ||
            !Compatibility.IsFinite(anchor.LowerBoundaryY) ||
            anchor.DoorEdgeMaxX < anchor.DoorEdgeMinX ||
            anchor.UpperBoundaryY <= anchor.LowerBoundaryY ||
            !Compatibility.IsFinite(boardThickness) || boardThickness <= Tolerance ||
            !Compatibility.IsFinite(profileSize) || profileSize <= boardThickness)
        {
            throw new ArgumentOutOfRangeException(nameof(anchor), "免拉手侧视图定位或尺寸无效。");
        }

        var boundaryDistance = anchor.UpperBoundaryY - anchor.LowerBoundaryY;
        var overlap = anchor.Type == HandlelessProfileMode.Middle
            ? (profileSize - boundaryDistance) / 2
            : profileSize - boundaryDistance;
        if (overlap < -Tolerance ||
            anchor.Type == HandlelessProfileMode.Middle && overlap > boardThickness + Tolerance ||
            anchor.Type == HandlelessProfileMode.Top && overlap >= profileSize - Tolerance)
        {
            throw new InvalidOperationException("门板边界与50挡板尺寸不匹配，无法形成有效免拉手搭接。");
        }

        overlap = Math.Max(0, overlap);
        var innerX = frontSide == HandlelessProfileFrontSide.Right
            ? anchor.DoorEdgeMinX
            : anchor.DoorEdgeMaxX;
        var horizontalX = frontSide == HandlelessProfileFrontSide.Right
            ? innerX - profileSize
            : innerX;
        var connectorX = frontSide == HandlelessProfileFrontSide.Right
            ? horizontalX
            : horizontalX + profileSize - boardThickness;
        var boards = new List<HandlelessProfileRectangle>(anchor.Type == HandlelessProfileMode.Middle ? 3 : 2);

        if (anchor.Type == HandlelessProfileMode.Middle)
        {
            var connectorBottom = anchor.LowerBoundaryY - overlap;
            var connectorTop = anchor.UpperBoundaryY + overlap;
            boards.Add(new HandlelessProfileRectangle(horizontalX, connectorTop, profileSize, boardThickness));
            boards.Add(new HandlelessProfileRectangle(connectorX, connectorBottom, boardThickness, profileSize));
            boards.Add(new HandlelessProfileRectangle(
                horizontalX,
                connectorBottom - boardThickness,
                profileSize,
                boardThickness));
        }
        else
        {
            var connectorBottom = anchor.LowerBoundaryY - overlap;
            boards.Add(new HandlelessProfileRectangle(connectorX, connectorBottom, boardThickness, profileSize));
            boards.Add(new HandlelessProfileRectangle(
                horizontalX,
                connectorBottom - boardThickness,
                profileSize,
                boardThickness));
        }

        return new HandlelessProfileLayout(anchor.Type, frontSide, boards, overlap);
    }

    private static void FindMiddleCandidates(
        BoardPlacementPoint click,
        IReadOnlyList<HorizontalSegment> horizontal,
        double boardThickness,
        double profileSize,
        double handlelessGap,
        ref Candidate? best)
    {
        var dimensionTolerance = Math.Max(3, boardThickness / 2);
        for (var firstIndex = 0; firstIndex < horizontal.Count; firstIndex++)
        {
            var first = horizontal[firstIndex];
            if (!IsDoorEdge(first, boardThickness))
            {
                continue;
            }

            for (var secondIndex = firstIndex + 1; secondIndex < horizontal.Count; secondIndex++)
            {
                var second = horizontal[secondIndex];
                if (!IsDoorEdge(second, boardThickness))
                {
                    continue;
                }

                var distance = Math.Abs(first.Y - second.Y);
                if (Math.Abs(distance - handlelessGap) > dimensionTolerance ||
                    !HaveMatchingDoorRange(first, second, boardThickness))
                {
                    continue;
                }

                var upper = first.Y >= second.Y ? first : second;
                var lower = first.Y >= second.Y ? second : first;
                var minX = (upper.MinX + lower.MinX) / 2;
                var maxX = (upper.MaxX + lower.MaxX) / 2;
                var clickDistance = DistanceToBox(click, minX, maxX, lower.Y, upper.Y);
                var score = clickDistance + Math.Abs(distance - handlelessGap) * 2 +
                            Math.Abs(upper.MinX - lower.MinX) + Math.Abs(upper.MaxX - lower.MaxX);
                Consider(
                    new Candidate(
                        new HandlelessProfileAnchor(
                            HandlelessProfileMode.Middle,
                            minX,
                            maxX,
                            upper.Y,
                            lower.Y),
                        clickDistance,
                        score),
                    ref best);
            }
        }
    }

    private static void FindTopCandidates(
        BoardPlacementPoint click,
        IReadOnlyList<HorizontalSegment> horizontal,
        double boardThickness,
        double profileSize,
        double handlelessGap,
        ref Candidate? best)
    {
        var expectedDistance = handlelessGap;
        var dimensionTolerance = Math.Max(3, boardThickness / 2);
        foreach (var door in horizontal.Where(segment => IsDoorEdge(segment, boardThickness)))
        {
            foreach (var structure in horizontal.Where(segment => segment.Length >= boardThickness * 2))
            {
                var distance = structure.Y - door.Y;
                if (distance <= Tolerance ||
                    Math.Abs(distance - expectedDistance) > dimensionTolerance ||
                    Overlap(structure, door) < Math.Min(door.Length, boardThickness) / 2)
                {
                    continue;
                }

                var clickDistance = DistanceToBox(click, door.MinX, door.MaxX, door.Y, structure.Y);
                var score = clickDistance + Math.Abs(distance - expectedDistance) * 2;
                Consider(
                    new Candidate(
                        new HandlelessProfileAnchor(
                            HandlelessProfileMode.Top,
                            door.MinX,
                            door.MaxX,
                            structure.Y,
                            door.Y),
                        clickDistance,
                        score),
                    ref best);
            }
        }
    }

    private static void Consider(Candidate candidate, ref Candidate? best)
    {
        if (best is null || candidate.Score < best.Value.Score)
        {
            best = candidate;
        }
    }

    private static bool IsDoorEdge(HorizontalSegment segment, double boardThickness) =>
        segment.Length >= boardThickness / 2 && segment.Length <= boardThickness * 2;

    private static bool HaveMatchingDoorRange(
        HorizontalSegment first,
        HorizontalSegment second,
        double boardThickness) =>
        Overlap(first, second) >= Math.Min(Math.Min(first.Length, second.Length), boardThickness) / 2 &&
        Math.Abs(first.MinX - second.MinX) <= boardThickness / 2 &&
        Math.Abs(first.MaxX - second.MaxX) <= boardThickness / 2;

    private static double Overlap(HorizontalSegment first, HorizontalSegment second) =>
        Math.Max(0, Math.Min(first.MaxX, second.MaxX) - Math.Max(first.MinX, second.MinX));

    private static double DistanceToBox(
        BoardPlacementPoint point,
        double minX,
        double maxX,
        double minY,
        double maxY)
    {
        var deltaX = point.X < minX ? minX - point.X : point.X > maxX ? point.X - maxX : 0;
        var deltaY = point.Y < minY ? minY - point.Y : point.Y > maxY ? point.Y - maxY : 0;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    private static bool IsFiniteHorizontal(AxisAlignedSegment segment) =>
        Compatibility.IsFinite(segment.StartX) &&
        Compatibility.IsFinite(segment.StartY) &&
        Compatibility.IsFinite(segment.EndX) &&
        Compatibility.IsFinite(segment.EndY) &&
        Math.Abs(segment.StartY - segment.EndY) <= Tolerance &&
        Math.Abs(segment.StartX - segment.EndX) > Tolerance;

    private static HorizontalSegment NormalizeHorizontal(AxisAlignedSegment segment) =>
        new(Math.Min(segment.StartX, segment.EndX), Math.Max(segment.StartX, segment.EndX),
            (segment.StartY + segment.EndY) / 2);

    private static void ValidateParameters(
        BoardPlacementPoint click,
        HandlelessProfileMode requestedMode,
        double boardThickness,
        double profileSize,
        double handlelessGap)
    {
        if (!Compatibility.IsFinite(click.X) || !Compatibility.IsFinite(click.Y) ||
            !Enum.IsDefined(typeof(HandlelessProfileMode), requestedMode) ||
            !Compatibility.IsFinite(boardThickness) || boardThickness <= Tolerance ||
            !Compatibility.IsFinite(profileSize) || profileSize <= boardThickness ||
            !Compatibility.IsFinite(handlelessGap) || handlelessGap <= Tolerance ||
            handlelessGap >= profileSize)
        {
            throw new ArgumentOutOfRangeException(nameof(profileSize), "免拉手侧视图参数无效。");
        }
    }

    private readonly record struct HorizontalSegment(double MinX, double MaxX, double Y)
    {
        public double Length => MaxX - MinX;
    }

    private readonly record struct Candidate(
        HandlelessProfileAnchor Anchor,
        double ClickDistance,
        double Score);
}
