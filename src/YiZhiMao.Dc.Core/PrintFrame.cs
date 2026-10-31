namespace YiZhiMao.Dc.Core;

public readonly record struct PrintFrame(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    string SourceKey = "")
{
    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;

    public bool IsFinite => Compatibility.IsFinite(MinX) &&
                            Compatibility.IsFinite(MinY) &&
                            Compatibility.IsFinite(MaxX) &&
                            Compatibility.IsFinite(MaxY);

    public bool Contains(PrintFrame other, double tolerance = 0.0001)
    {
        return MinX <= other.MinX + tolerance &&
               MinY <= other.MinY + tolerance &&
               MaxX >= other.MaxX - tolerance &&
               MaxY >= other.MaxY - tolerance;
    }
}

public static class PrintFrameProcessor
{
    public static IReadOnlyList<PrintFrame> Prepare(IEnumerable<PrintFrame> frames, double tolerance = 0.0001)
    {
        var cleaned = frames
            .Where(frame => frame.IsFinite && frame.Width > tolerance && frame.Height > tolerance)
            .ToList();

        var unique = new List<PrintFrame>();
        foreach (var frame in cleaned)
        {
            if (!unique.Any(existing => IsSame(existing, frame, tolerance)))
            {
                unique.Add(frame);
            }
        }

        var outermost = unique
            .Where((frame, index) => !unique.Where((_, otherIndex) => otherIndex != index)
                .Any(other => other.Contains(frame, tolerance) && !IsSame(other, frame, tolerance)))
            .ToList();

        var rowTolerance = outermost.Count == 0
            ? tolerance
            : Math.Max(tolerance, outermost.Select(frame => frame.Height).OrderBy(height => height).ElementAt(outermost.Count / 2) * 0.25);

        var rows = new List<List<PrintFrame>>();
        foreach (var frame in outermost.OrderByDescending(frame => frame.MaxY))
        {
            var row = rows.FirstOrDefault(candidate => Math.Abs(candidate[0].MaxY - frame.MaxY) <= rowTolerance);
            if (row is null)
            {
                rows.Add([frame]);
            }
            else
            {
                row.Add(frame);
            }
        }

        return rows
            .OrderByDescending(row => row.Max(frame => frame.MaxY))
            .SelectMany(row => row.OrderBy(frame => frame.MinX))
            .ToList();
    }

    private static bool IsSame(PrintFrame left, PrintFrame right, double tolerance)
    {
        return Math.Abs(left.MinX - right.MinX) <= tolerance &&
               Math.Abs(left.MinY - right.MinY) <= tolerance &&
               Math.Abs(left.MaxX - right.MaxX) <= tolerance &&
               Math.Abs(left.MaxY - right.MaxY) <= tolerance;
    }
}
