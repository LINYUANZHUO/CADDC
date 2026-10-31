namespace YiZhiMao.Dc.Core;

public static class FrameContentProcessor
{
    public static IReadOnlyList<PrintFrame> Prepare(
        IEnumerable<PrintFrame> blockFrames,
        PrintFrame? otherFrame,
        double tolerance = 0.0001)
    {
        if (blockFrames is null)
        {
            throw new ArgumentNullException(nameof(blockFrames));
        }

        var candidates = blockFrames.ToList();
        if (otherFrame is not null)
        {
            candidates.Add(otherFrame.Value);
        }

        var merged = new List<PrintFrame>();
        foreach (var candidate in candidates.Where(frame =>
                     frame.IsFinite && frame.Width > tolerance && frame.Height > tolerance))
        {
            var combined = candidate;
            while (true)
            {
                var overlapIndex = merged.FindIndex(frame => HasAreaOverlap(frame, combined, tolerance));
                if (overlapIndex < 0)
                {
                    break;
                }

                combined = Union(merged[overlapIndex], combined);
                merged.RemoveAt(overlapIndex);
            }

            merged.Add(combined);
        }

        return PrintFrameProcessor.Prepare(merged, tolerance);
    }

    private static bool HasAreaOverlap(PrintFrame left, PrintFrame right, double tolerance) =>
        Math.Min(left.MaxX, right.MaxX) - Math.Max(left.MinX, right.MinX) > tolerance &&
        Math.Min(left.MaxY, right.MaxY) - Math.Max(left.MinY, right.MinY) > tolerance;

    private static PrintFrame Union(PrintFrame left, PrintFrame right) =>
        new(
            Math.Min(left.MinX, right.MinX),
            Math.Min(left.MinY, right.MinY),
            Math.Max(left.MaxX, right.MaxX),
            Math.Max(left.MaxY, right.MaxY));
}
