namespace YiZhiMao.Dc.Core;

public readonly record struct AxisAlignedSegment(double StartX, double StartY, double EndX, double EndY);

public readonly record struct RectangleEdgeMatch(
    int BottomIndex,
    int TopIndex,
    int LeftIndex,
    int RightIndex,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY);

public static class AxisAlignedRectangleDetector
{
    public static IReadOnlyList<RectangleEdgeMatch> Find(
        IReadOnlyList<AxisAlignedSegment> segments,
        double tolerance = 0.001)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (!double.IsFinite(tolerance) || tolerance <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be a finite positive number.");
        }

        var horizontal = new List<NormalizedSegment>();
        var horizontalLookup = new Dictionary<SpanKey, List<NormalizedSegment>>();
        var verticalLookup = new Dictionary<RectangleKey, List<NormalizedSegment>>();

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            if (Math.Abs(segment.StartY - segment.EndY) <= tolerance &&
                Math.Abs(segment.StartX - segment.EndX) > tolerance)
            {
                var normalized = new NormalizedSegment(
                    index,
                    (segment.StartY + segment.EndY) / 2,
                    Math.Min(segment.StartX, segment.EndX),
                    Math.Max(segment.StartX, segment.EndX));
                horizontal.Add(normalized);
                Add(horizontalLookup, CreateSpanKey(normalized.Min, normalized.Max, tolerance), normalized);
            }
            else if (Math.Abs(segment.StartX - segment.EndX) <= tolerance &&
                     Math.Abs(segment.StartY - segment.EndY) > tolerance)
            {
                var normalized = new NormalizedSegment(
                    index,
                    (segment.StartX + segment.EndX) / 2,
                    Math.Min(segment.StartY, segment.EndY),
                    Math.Max(segment.StartY, segment.EndY));
                Add(verticalLookup, CreateRectangleKey(normalized.Coordinate, normalized.Min, normalized.Max, tolerance), normalized);
            }
        }

        var matches = new List<RectangleEdgeMatch>();
        foreach (var first in horizontal)
        {
            foreach (var second in FindHorizontalCandidates(horizontalLookup, first, tolerance))
            {
                if (second.Index <= first.Index ||
                    Math.Abs(first.Min - second.Min) > tolerance ||
                    Math.Abs(first.Max - second.Max) > tolerance)
                {
                    continue;
                }

                var minY = Math.Min(first.Coordinate, second.Coordinate);
                var maxY = Math.Max(first.Coordinate, second.Coordinate);
                if (maxY - minY <= tolerance ||
                    !TryFindVertical(verticalLookup, first.Min, minY, maxY, tolerance, out var left) ||
                    !TryFindVertical(verticalLookup, first.Max, minY, maxY, tolerance, out var right))
                {
                    continue;
                }

                var bottom = first.Coordinate <= second.Coordinate ? first : second;
                var top = first.Coordinate <= second.Coordinate ? second : first;
                matches.Add(new RectangleEdgeMatch(
                    bottom.Index,
                    top.Index,
                    left.Index,
                    right.Index,
                    first.Min,
                    minY,
                    first.Max,
                    maxY));
            }
        }

        return matches;
    }

    private static IEnumerable<NormalizedSegment> FindHorizontalCandidates(
        IReadOnlyDictionary<SpanKey, List<NormalizedSegment>> lookup,
        NormalizedSegment segment,
        double tolerance)
    {
        var key = CreateSpanKey(segment.Min, segment.Max, tolerance);
        for (var minOffset = -1; minOffset <= 1; minOffset++)
        {
            for (var maxOffset = -1; maxOffset <= 1; maxOffset++)
            {
                if (lookup.TryGetValue(new SpanKey(key.Min + minOffset, key.Max + maxOffset), out var candidates))
                {
                    foreach (var candidate in candidates)
                    {
                        yield return candidate;
                    }
                }
            }
        }
    }

    private static bool TryFindVertical(
        IReadOnlyDictionary<RectangleKey, List<NormalizedSegment>> lookup,
        double x,
        double minY,
        double maxY,
        double tolerance,
        out NormalizedSegment result)
    {
        var key = CreateRectangleKey(x, minY, maxY, tolerance);
        for (var xOffset = -1; xOffset <= 1; xOffset++)
        {
            for (var minOffset = -1; minOffset <= 1; minOffset++)
            {
                for (var maxOffset = -1; maxOffset <= 1; maxOffset++)
                {
                    if (!lookup.TryGetValue(
                            new RectangleKey(key.Coordinate + xOffset, key.Min + minOffset, key.Max + maxOffset),
                            out var candidates))
                    {
                        continue;
                    }

                    foreach (var candidate in candidates)
                    {
                        if (Math.Abs(candidate.Coordinate - x) <= tolerance &&
                            Math.Abs(candidate.Min - minY) <= tolerance &&
                            Math.Abs(candidate.Max - maxY) <= tolerance)
                        {
                            result = candidate;
                            return true;
                        }
                    }
                }
            }
        }

        result = default;
        return false;
    }

    private static void Add<TKey>(Dictionary<TKey, List<NormalizedSegment>> lookup, TKey key, NormalizedSegment segment)
        where TKey : notnull
    {
        if (!lookup.TryGetValue(key, out var values))
        {
            values = [];
            lookup.Add(key, values);
        }

        values.Add(segment);
    }

    private static SpanKey CreateSpanKey(double min, double max, double tolerance) =>
        new(Quantize(min, tolerance), Quantize(max, tolerance));

    private static RectangleKey CreateRectangleKey(double coordinate, double min, double max, double tolerance) =>
        new(Quantize(coordinate, tolerance), Quantize(min, tolerance), Quantize(max, tolerance));

    private static long Quantize(double value, double tolerance) =>
        (long)Math.Round(value / tolerance, MidpointRounding.AwayFromZero);

    private readonly record struct NormalizedSegment(int Index, double Coordinate, double Min, double Max);

    private readonly record struct SpanKey(long Min, long Max);

    private readonly record struct RectangleKey(long Coordinate, long Min, long Max);
}
