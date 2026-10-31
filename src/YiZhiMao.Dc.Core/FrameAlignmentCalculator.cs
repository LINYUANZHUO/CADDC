namespace YiZhiMao.Dc.Core;

public readonly record struct FrameAlignmentMove(
    PrintFrame Source,
    double DeltaX,
    double DeltaY);

public static class FrameAlignmentCalculator
{
    public static IReadOnlyList<FrameAlignmentMove> ArrangeFromLeft(
        IEnumerable<PrintFrame> frames,
        double baselineX,
        double gap)
    {
        var ordered = Validate(frames, baselineX, gap)
            .OrderBy(frame => frame.MinX)
            .ToList();
        var bottom = ordered.Min(frame => frame.MinY);
        var currentX = baselineX;
        var moves = new List<FrameAlignmentMove>(ordered.Count);
        foreach (var frame in ordered)
        {
            moves.Add(new FrameAlignmentMove(frame, currentX - frame.MinX, bottom - frame.MinY));
            currentX += frame.Width + gap;
        }

        return moves;
    }

    public static IReadOnlyList<FrameAlignmentMove> ArrangeFromBottom(
        IEnumerable<PrintFrame> frames,
        double baselineY,
        double gap)
    {
        var ordered = Validate(frames, baselineY, gap)
            .OrderBy(frame => frame.MinY)
            .ToList();
        var left = ordered.Min(frame => frame.MinX);
        var currentY = baselineY;
        var moves = new List<FrameAlignmentMove>(ordered.Count);
        foreach (var frame in ordered)
        {
            moves.Add(new FrameAlignmentMove(frame, left - frame.MinX, currentY - frame.MinY));
            currentY += frame.Height + gap;
        }

        return moves;
    }

    public static FrameAlignmentMove? FindContentMove(
        PrintFrame contentBounds,
        IReadOnlyList<FrameAlignmentMove> moves,
        double tolerance = 0.0001)
    {
        if (!contentBounds.IsFinite || contentBounds.Width < 0 || contentBounds.Height < 0)
        {
            return null;
        }

        var centerX = (contentBounds.MinX + contentBounds.MaxX) / 2;
        var centerY = (contentBounds.MinY + contentBounds.MaxY) / 2;
        return moves
            .Where(move => centerX >= move.Source.MinX - tolerance &&
                           centerX <= move.Source.MaxX + tolerance &&
                           centerY >= move.Source.MinY - tolerance &&
                           centerY <= move.Source.MaxY + tolerance)
            .OrderBy(move => move.Source.Width * move.Source.Height)
            .Cast<FrameAlignmentMove?>()
            .FirstOrDefault();
    }

    private static IReadOnlyList<PrintFrame> Validate(
        IEnumerable<PrintFrame> frames,
        double baseline,
        double gap)
    {
        if (frames is null)
        {
            throw new ArgumentNullException(nameof(frames));
        }
        var values = frames.ToList();
        if (values.Count == 0 || values.Any(frame => !frame.IsFinite || frame.Width <= 0 || frame.Height <= 0))
        {
            throw new ArgumentException("图框范围必须有效。", nameof(frames));
        }

        if (!Compatibility.IsFinite(baseline) || !Compatibility.IsFinite(gap) || gap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gap), "图框基线和间距必须有效。");
        }

        return values;
    }
}
