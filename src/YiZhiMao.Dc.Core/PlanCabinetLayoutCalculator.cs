namespace YiZhiMao.Dc.Core;

public enum PlanCabinetPartKind
{
    Top,
    LeftSide,
    RightSide,
    MiddlePanel,
    Backboard,
    Door
}

public sealed record PlanCabinetShape(
    PlanCabinetPartKind Kind,
    int Index,
    CabinetRectangle Bounds,
    bool Hidden,
    string Name,
    string Category,
    double CutWidth,
    double CutHeight,
    double CutThickness);

public sealed record PlanCabinetLayout(
    IReadOnlyList<PlanCabinetShape> Shapes,
    double ActualDoorGap,
    CabinetRectangle Bounds);

public static class PlanCabinetLayoutCalculator
{
    public static PlanCabinetLayout Calculate(PlanCabinetOptions options)
    {
        Compatibility.NotNull(options, nameof(options));
        if (!options.TryValidate(out var error))
        {
            throw new ArgumentOutOfRangeException(nameof(options), error);
        }

        var shapes = new List<PlanCabinetShape>();
        AddStructure(options, shapes);

        var division = DoorPanelDivisionCalculator.CalculateWithExteriorGaps(
            options.Length + options.DoorLeftExtension + options.DoorRightExtension,
            options.DoorCount,
            options.DoorGap,
            options.DoorCalculationMode);
        if (options.GenerateDoors)
        {
            AddDoors(options, division, shapes);
        }

        if (options.GenerateMiddlePanels && options.DoorCount > 1)
        {
            AddMiddlePanels(options, division, shapes);
        }

        var left = shapes.Min(shape => shape.Bounds.Left);
        var bottom = shapes.Min(shape => shape.Bounds.Bottom);
        var right = shapes.Max(shape => shape.Bounds.Right);
        var top = shapes.Max(shape => shape.Bounds.Top);
        return new PlanCabinetLayout(
            shapes,
            division.ActualGap,
            new CabinetRectangle(left, bottom, right, top));
    }

    public static IReadOnlyList<PlanCabinetShape> CalculateDoors(PlanCabinetOptions options) =>
        Calculate(options).Shapes.Where(shape => shape.Kind == PlanCabinetPartKind.Door).ToArray();

    private static void AddStructure(PlanCabinetOptions options, ICollection<PlanCabinetShape> shapes)
    {
        var topBottom = options.TopFrontInset;
        var topTop = options.Depth - options.TopRearInset;
        shapes.Add(Create(
            PlanCabinetPartKind.Top,
            0,
            new CabinetRectangle(options.Thickness, topBottom, options.Length - options.Thickness, topTop),
            false,
            "顶板",
            "柜体板",
            options.Length - options.Thickness * 2,
            topTop - topBottom,
            options.Thickness));
        shapes.Add(Create(
            PlanCabinetPartKind.LeftSide,
            0,
            new CabinetRectangle(
                0,
                -options.LeftSideFrontExtension,
                options.Thickness,
                options.Depth + options.LeftSideRearExtension),
            false,
            "左侧板",
            "柜体板",
            options.Depth + options.LeftSideFrontExtension + options.LeftSideRearExtension,
            options.Thickness,
            options.Thickness));
        shapes.Add(Create(
            PlanCabinetPartKind.RightSide,
            0,
            new CabinetRectangle(
                options.Length - options.Thickness,
                -options.RightSideFrontExtension,
                options.Length,
                options.Depth + options.RightSideRearExtension),
            false,
            "右侧板",
            "柜体板",
            options.Depth + options.RightSideFrontExtension + options.RightSideRearExtension,
            options.Thickness,
            options.Thickness));

        var backboardBounds = options.BackboardRearCover
            ? new CabinetRectangle(
                0,
                options.Depth + options.BackboardInset,
                options.Length,
                options.Depth + options.BackboardInset + options.BackboardThickness)
            : new CabinetRectangle(
                options.Thickness - options.BackboardLeftGroove,
                options.Depth - options.BackboardInset - options.BackboardThickness,
                options.Length - options.Thickness + options.BackboardRightGroove,
                options.Depth - options.BackboardInset);
        shapes.Add(Create(
            PlanCabinetPartKind.Backboard,
            0,
            backboardBounds,
            true,
            "背板",
            "背板",
            backboardBounds.Width,
            backboardBounds.Height,
            options.BackboardThickness));
    }

    private static void AddDoors(
        PlanCabinetOptions options,
        DoorPanelDivision division,
        ICollection<PlanCabinetShape> shapes)
    {
        var left = -options.DoorLeftExtension + division.ActualGap;
        var outerRight = options.Length + options.DoorRightExtension - division.ActualGap;
        for (var index = 0; index < division.PanelWidths.Count; index++)
        {
            var right = index == division.PanelWidths.Count - 1
                ? outerRight
                : left + division.PanelWidths[index];
            shapes.Add(Create(
                PlanCabinetPartKind.Door,
                index,
                new CabinetRectangle(
                    left,
                    -options.DoorFrontClearance - options.DoorThickness,
                    right,
                    -options.DoorFrontClearance),
                false,
                $"门板{index + 1}",
                "门板",
                right - left,
                options.DoorThickness,
                options.DoorThickness));
            left = right + division.ActualGap;
        }
    }

    private static void AddMiddlePanels(
        PlanCabinetOptions options,
        DoorPanelDivision division,
        ICollection<PlanCabinetShape> shapes)
    {
        var bottom = options.MiddlePanelSameDepthAsSide ? 0 : options.TopFrontInset;
        var top = options.MiddlePanelSameDepthAsSide ? options.Depth : options.Depth - options.TopRearInset;
        var centers = options.MiddlePanelMode == PlanCabinetMiddlePanelMode.Distributed
            ? Enumerable.Range(1, options.DoorCount - 1)
                .Select(index => options.Thickness +
                    (options.Length - options.Thickness * 2) * index / options.DoorCount)
                .ToArray()
            : DoorGapCenters(options, division);

        for (var index = 0; index < centers.Length; index++)
        {
            shapes.Add(Create(
                PlanCabinetPartKind.MiddlePanel,
                index,
                new CabinetRectangle(
                    centers[index] - options.Thickness / 2,
                    bottom,
                    centers[index] + options.Thickness / 2,
                    top),
                false,
                $"中竖板{index + 1}",
                "柜体板",
                top - bottom,
                options.Thickness,
                options.Thickness));
        }
    }

    private static double[] DoorGapCenters(PlanCabinetOptions options, DoorPanelDivision division)
    {
        var centers = new double[options.DoorCount - 1];
        var position = -options.DoorLeftExtension + division.ActualGap;
        for (var index = 0; index < centers.Length; index++)
        {
            position += division.PanelWidths[index];
            centers[index] = position + division.ActualGap / 2;
            position += division.ActualGap;
        }

        return centers;
    }

    private static PlanCabinetShape Create(
        PlanCabinetPartKind kind,
        int index,
        CabinetRectangle bounds,
        bool hidden,
        string name,
        string category,
        double cutWidth,
        double cutHeight,
        double cutThickness) =>
        new(kind, index, bounds, hidden, name, category, cutWidth, cutHeight, cutThickness);
}
