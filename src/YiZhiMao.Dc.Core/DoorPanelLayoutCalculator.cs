namespace YiZhiMao.Dc.Core;

public readonly record struct DoorPanelBounds(double Left, double Right, double Bottom, double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;
}

public readonly record struct DoorPanelLeaf(double Left, double Right, double Bottom, double Top, bool HingeOnLeft)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;
}

public sealed record DoorPanelLayout(DoorPanelBounds Bounds, IReadOnlyList<DoorPanelLeaf> Panels);

public static class DoorPanelLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static DoorPanelLayout Calculate(
        double innerLeft,
        double innerRight,
        double innerBottom,
        double innerTop,
        DoorPanelOptions options,
        bool singleDoorHingeOnLeft = true)
    {
        Compatibility.NotNull(options, nameof(options));
        if (!Compatibility.IsFinite(innerLeft) || !Compatibility.IsFinite(innerRight) ||
            !Compatibility.IsFinite(innerBottom) || !Compatibility.IsFinite(innerTop) ||
            innerRight - innerLeft <= Tolerance || innerTop - innerBottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRight), "柜体内空的宽度和高度必须大于 0。");
        }

        var bounds = new DoorPanelBounds(
            innerLeft - options.LeftExtension,
            innerRight + options.RightExtension,
            innerBottom - options.BottomExtension,
            innerTop + options.TopExtension);
        var division = DoorPanelDivisionCalculator.CalculateWithGapCompensation(
            bounds.Width,
            options.DoorCount,
            options.Gap,
            options.CalculationMode);
        var panels = new List<DoorPanelLeaf>(options.DoorCount);
        var left = bounds.Left;
        for (var index = 0; index < division.PanelWidths.Count; index++)
        {
            var right = index == division.PanelWidths.Count - 1
                ? bounds.Right
                : left + division.PanelWidths[index];
            panels.Add(new DoorPanelLeaf(
                left,
                right,
                bounds.Bottom,
                bounds.Top,
                HingeOnLeft(index, division.PanelWidths.Count, singleDoorHingeOnLeft)));
            left = right + division.ActualGap;
        }

        return new DoorPanelLayout(bounds, panels);
    }

    private static bool HingeOnLeft(int index, int count, bool singleDoorHingeOnLeft)
    {
        if (count == 1)
        {
            return singleDoorHingeOnLeft;
        }

        return index < count / 2 || (count % 2 == 1 && index == count / 2);
    }
}
