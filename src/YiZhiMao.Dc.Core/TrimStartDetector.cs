namespace YiZhiMao.Dc.Core;

public enum TrimStartLineSide
{
    Left,
    Right
}

public enum TrimStartLineKind
{
    Single,
    Double
}

public readonly record struct TrimStartContext(
    BoardPlacementPoint Anchor,
    TrimStartLineSide LineSide,
    TrimStartLineKind LineKind);

public static class TrimStartDetector
{
    public static bool TryDetect(
        BoardPlacementPoint click,
        IReadOnlyList<AxisAlignedSegment> segments,
        double boardThickness,
        out TrimStartContext context,
        double tolerance = 0.5)
    {
        context = default;
        if (!Compatibility.IsFinite(click.X) || !Compatibility.IsFinite(click.Y) || segments is null)
        {
            return false;
        }

        if (!Compatibility.IsFinite(boardThickness) || boardThickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardThickness));
        }

        if (!Compatibility.IsFinite(tolerance) || tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        var candidates = new List<EndpointCandidate>();
        foreach (var segment in segments)
        {
            if (!IsFinite(segment) || Math.Abs(segment.StartY - segment.EndY) > tolerance)
            {
                continue;
            }

            var minX = Math.Min(segment.StartX, segment.EndX);
            var maxX = Math.Max(segment.StartX, segment.EndX);
            var y = (segment.StartY + segment.EndY) / 2;
            var verticalDistance = Math.Abs(y - click.Y);
            if (verticalDistance > boardThickness + tolerance)
            {
                continue;
            }

            if (Math.Abs(maxX - click.X) <= tolerance && minX < click.X - tolerance)
            {
                candidates.Add(new EndpointCandidate(
                    new BoardPlacementPoint(maxX, y),
                    TrimStartLineSide.Left,
                    verticalDistance));
            }

            if (Math.Abs(minX - click.X) <= tolerance && maxX > click.X + tolerance)
            {
                candidates.Add(new EndpointCandidate(
                    new BoardPlacementPoint(minX, y),
                    TrimStartLineSide.Right,
                    verticalDistance));
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        var selected = candidates
            .OrderBy(candidate => candidate.DistanceToClick)
            .ThenBy(candidate => candidate.LineSide)
            .First();
        var sameSideCoordinates = candidates
            .Where(candidate => candidate.LineSide == selected.LineSide)
            .Select(candidate => candidate.Anchor.Y)
            .Distinct()
            .ToArray();
        var pairTolerance = Math.Max(tolerance, boardThickness * 0.1);
        var isDouble = sameSideCoordinates.Any(first =>
            sameSideCoordinates.Any(second =>
                Math.Abs(Math.Abs(first - second) - boardThickness) <= pairTolerance));

        context = new TrimStartContext(
            selected.Anchor,
            selected.LineSide,
            isDouble ? TrimStartLineKind.Double : TrimStartLineKind.Single);
        return true;
    }

    private static bool IsFinite(AxisAlignedSegment segment) =>
        Compatibility.IsFinite(segment.StartX) && Compatibility.IsFinite(segment.StartY) &&
        Compatibility.IsFinite(segment.EndX) && Compatibility.IsFinite(segment.EndY);

    private readonly record struct EndpointCandidate(
        BoardPlacementPoint Anchor,
        TrimStartLineSide LineSide,
        double DistanceToClick);
}
