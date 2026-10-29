namespace YiZhiMao.Dc.Core;

public readonly record struct DoorOpeningSegment(
    double StartX,
    double StartY,
    double EndX,
    double EndY);

public static class DoorOpeningDetector
{
    private const double Tolerance = 0.000001;

    public static bool TryDetect(
        double clickX,
        double clickY,
        IEnumerable<DoorOpeningSegment> segments,
        out DoorPanelBounds bounds)
    {
        bounds = default;
        if (!double.IsFinite(clickX) || !double.IsFinite(clickY) || segments is null)
        {
            return false;
        }

        var verticals = segments
            .Where(IsFinite)
            .Where(segment => Math.Abs(segment.StartX - segment.EndX) <= Tolerance)
            .Select(segment => new VerticalSpan(
                (segment.StartX + segment.EndX) / 2,
                Math.Min(segment.StartY, segment.EndY),
                Math.Max(segment.StartY, segment.EndY)))
            .Where(span => span.Bottom <= clickY + Tolerance && span.Top >= clickY - Tolerance)
            .ToArray();
        var left = verticals
            .Where(span => span.X < clickX - Tolerance)
            .OrderByDescending(span => span.X)
            .FirstOrDefault();
        var right = verticals
            .Where(span => span.X > clickX + Tolerance)
            .OrderBy(span => span.X)
            .FirstOrDefault();
        if (left == default || right == default || right.X - left.X <= Tolerance)
        {
            return false;
        }

        var horizontals = segments
            .Where(IsFinite)
            .Where(segment => Math.Abs(segment.StartY - segment.EndY) <= Tolerance)
            .Select(segment => new HorizontalSpan(
                Math.Min(segment.StartX, segment.EndX),
                Math.Max(segment.StartX, segment.EndX),
                (segment.StartY + segment.EndY) / 2))
            .Where(span =>
                span.Left <= left.X + Tolerance &&
                span.Right >= right.X - Tolerance)
            .ToArray();
        var bottom = horizontals
            .Where(span => span.Y < clickY - Tolerance)
            .OrderByDescending(span => span.Y)
            .FirstOrDefault();
        var top = horizontals
            .Where(span => span.Y > clickY + Tolerance)
            .OrderBy(span => span.Y)
            .FirstOrDefault();
        if (bottom == default || top == default || top.Y - bottom.Y <= Tolerance)
        {
            return false;
        }

        if (left.Bottom > bottom.Y + Tolerance || left.Top < top.Y - Tolerance ||
            right.Bottom > bottom.Y + Tolerance || right.Top < top.Y - Tolerance)
        {
            return false;
        }

        bounds = new DoorPanelBounds(left.X, right.X, bottom.Y, top.Y);
        return bounds.Width > Tolerance && bounds.Height > Tolerance;
    }

    private static bool IsFinite(DoorOpeningSegment segment)
    {
        return double.IsFinite(segment.StartX) && double.IsFinite(segment.StartY) &&
               double.IsFinite(segment.EndX) && double.IsFinite(segment.EndY);
    }

    private readonly record struct VerticalSpan(double X, double Bottom, double Top);

    private readonly record struct HorizontalSpan(double Left, double Right, double Y);
}
