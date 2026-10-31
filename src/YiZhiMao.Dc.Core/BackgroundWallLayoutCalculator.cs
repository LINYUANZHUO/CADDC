namespace YiZhiMao.Dc.Core;

public readonly record struct BackgroundWallRectangle(double X, double Y, double Width, double Height);

public readonly record struct BackgroundWallPanel(
    int RowIndex,
    int ColumnIndex,
    double X,
    double Y,
    double Width,
    double Height);

public sealed record BackgroundWallLayout(
    BackgroundWallRectangle Frame,
    IReadOnlyList<BackgroundWallPanel> FrontPanels,
    IReadOnlyList<BackgroundWallRectangle> PlanRectangles,
    IReadOnlyList<BackgroundWallRectangle> PlanSlots,
    IReadOnlyList<BackgroundWallRectangle> SideRectangles);

public static class BackgroundWallLayoutCalculator
{
    private const double AuxiliaryViewGap = 200;
    private const double PlanRowGap = 40;
    private const double PlanSlotWidth = 25;
    private const double PlanSlotHeight = 5;

    public static BackgroundWallLayout Calculate(double left, double bottom, BackgroundWallOptions options)
    {
        Compatibility.NotNull(options, nameof(options));
        ValidateOptions(left, bottom, options);

        var panels = new List<BackgroundWallPanel>();
        var verticalGap = options.LeaveVerticalGap ? options.Gap : 0;
        var currentBottom = bottom;
        for (var rowIndex = 0; rowIndex < options.Rows.Count; rowIndex++)
        {
            var row = options.Rows[rowIndex];
            var widths = CreateWidths(options.TotalWidth, options.Gap, row.ColumnCount, options.UseRandomWidths, rowIndex);
            var currentLeft = left;
            for (var columnIndex = 0; columnIndex < row.ColumnCount; columnIndex++)
            {
                var panel = new BackgroundWallPanel(
                    rowIndex,
                    columnIndex,
                    currentLeft,
                    currentBottom,
                    widths[columnIndex],
                    row.Height);
                panels.Add(panel);
                currentLeft += widths[columnIndex] + options.Gap;
            }

            currentBottom += row.Height + verticalGap;
        }

        var totalHeight = options.Rows.Sum(row => row.Height) + verticalGap * (options.Rows.Count - 1);
        var frame = new BackgroundWallRectangle(left, bottom, options.TotalWidth, totalHeight);
        var planRectangles = new List<BackgroundWallRectangle>();
        var planSlots = new List<BackgroundWallRectangle>();
        if (options.NeedPlanView)
        {
            var planBottom = bottom - options.Depth - AuxiliaryViewGap;
            var planRowCount = options.GenerateAllPlanRows ? options.Rows.Count : 1;
            for (var rowIndex = 0; rowIndex < planRowCount; rowIndex++)
            {
                var rowPanels = panels.Where(panel => panel.RowIndex == rowIndex).ToArray();
                var planY = planBottom - rowIndex * (options.Depth + PlanRowGap);
                for (var columnIndex = 0; columnIndex < rowPanels.Length; columnIndex++)
                {
                    var panel = rowPanels[columnIndex];
                    planRectangles.Add(new BackgroundWallRectangle(panel.X, planY, panel.Width, options.Depth));

                    if (options.CreateSlots && columnIndex < rowPanels.Length - 1)
                    {
                        var seamCenter = panel.X + panel.Width + options.Gap / 2;
                        planSlots.Add(new BackgroundWallRectangle(
                            seamCenter - PlanSlotWidth / 2,
                            planY + (options.Depth - PlanSlotHeight) / 2,
                            PlanSlotWidth,
                            PlanSlotHeight));
                    }
                }
            }
        }

        var sideRectangles = options.NeedSideView
            ? new[] { new BackgroundWallRectangle(left + options.TotalWidth + AuxiliaryViewGap, bottom, options.Depth, totalHeight) }
            : [];
        return new BackgroundWallLayout(frame, panels, planRectangles, planSlots, sideRectangles);
    }

    private static double[] CreateWidths(
        double totalWidth,
        double gap,
        int count,
        bool useRandomWidths,
        int rowIndex)
    {
        var usableWidth = totalWidth - gap * (count - 1);
        if (!useRandomWidths)
        {
            return Enumerable.Repeat(usableWidth / count, count).ToArray();
        }

        var remainingWidth = usableWidth - BackgroundWallOptions.MinimumPanelWidth * count;
        var weights = Enumerable.Range(0, count)
            .Select(columnIndex => 1d + ((rowIndex * 37 + columnIndex * 17) % 31) / 100d)
            .ToArray();
        var totalWeight = weights.Sum();
        return weights
            .Select(weight => BackgroundWallOptions.MinimumPanelWidth + remainingWidth * weight / totalWeight)
            .ToArray();
    }

    private static void ValidateOptions(double left, double bottom, BackgroundWallOptions options)
    {
        if (!Compatibility.IsFinite(left) || !Compatibility.IsFinite(bottom) ||
            options.TotalWidth <= 0 || options.Depth <= 0 || options.Gap < 0 || options.Rows.Count is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "背景墙参数无效。");
        }

        foreach (var row in options.Rows)
        {
            var usableWidth = options.TotalWidth - options.Gap * (row.ColumnCount - 1);
            if (row.ColumnCount <= 0 || row.Height <= 0 ||
                (options.UseRandomWidths && usableWidth < row.ColumnCount * BackgroundWallOptions.MinimumPanelWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(options), "背景墙分排不能满足最小板宽。");
            }
        }
    }
}
