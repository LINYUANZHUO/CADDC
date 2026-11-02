namespace YiZhiMao.Dc.Core;

public enum CabinetViewKind
{
    Elevation,
    Plan,
    Side
}

public enum CabinetPartKind
{
    LeftSide,
    RightSide,
    Top,
    Bottom,
    Backboard,
    TopRail,
    TopRailAuxiliary,
    FrontToeKick,
    RearToeKick
}

public readonly record struct CabinetBox(
    double MinX,
    double MaxX,
    double MinY,
    double MaxY,
    double MinZ,
    double MaxZ);

public readonly record struct CabinetRectangle(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;

    public CabinetRectangle Offset(double x, double y) =>
        new(Left + x, Bottom + y, Right + x, Top + y);
}

public sealed record CabinetPart(
    CabinetPartKind Kind,
    string Name,
    string Category,
    CabinetBox Box,
    double CutWidth,
    double CutHeight,
    double CutThickness);

public sealed record CabinetViewShape(
    CabinetViewKind View,
    CabinetPartKind Part,
    CabinetRectangle Bounds,
    bool Hidden);

public sealed record CabinetLayout(
    IReadOnlyList<CabinetPart> Parts,
    IReadOnlyList<CabinetViewShape> Shapes,
    CabinetViewKind PrimaryView,
    CabinetRectangle ElevationBounds,
    CabinetRectangle PlanBounds,
    CabinetRectangle SideBounds,
    CabinetRectangle LabelBounds);

public static class CabinetLayoutCalculator
{
    public static CabinetLayout Calculate(CabinetOptions options)
    {
        Compatibility.NotNull(options, nameof(options));
        if (!options.TryValidate(out var error))
        {
            throw new ArgumentOutOfRangeException(nameof(options), error);
        }

        var parts = CreateParts(options);
        var elevation = parts
            .Where(part => IsIncludedInView(part.Kind, CabinetViewKind.Elevation))
            .Select(part => Project(part, CabinetViewKind.Elevation))
            .ToArray();
        var rawPlan = parts
            .Where(part => IsIncludedInView(part.Kind, CabinetViewKind.Plan))
            .Select(part => Project(part, CabinetViewKind.Plan))
            .ToArray();
        var rawSide = parts
            .Where(part => IsIncludedInView(part.Kind, CabinetViewKind.Side))
            .Select(part => Project(part, CabinetViewKind.Side))
            .ToArray();
        var elevationBounds = GetBounds(elevation);
        var rawPlanBounds = GetBounds(rawPlan);
        var rawSideBounds = GetBounds(rawSide);

        var plan = options.GenerateElevation
            ? Offset(
                rawPlan,
                elevationBounds.Left - rawPlanBounds.Left,
                elevationBounds.Bottom - options.PlanViewSpacing - rawPlanBounds.Top)
            : Offset(rawPlan, -rawPlanBounds.Left, -rawPlanBounds.Bottom);
        var planBounds = GetBounds(plan);
        var side = options.GenerateElevation
            ? Offset(
                rawSide,
                elevationBounds.Right + options.SideViewSpacing - rawSideBounds.Left,
                elevationBounds.Bottom - rawSideBounds.Bottom)
            : options.GeneratePlan
                ? Offset(
                    rawSide,
                    planBounds.Right + options.SideViewSpacing - rawSideBounds.Left,
                    planBounds.Bottom - rawSideBounds.Bottom)
                : Offset(rawSide, -rawSideBounds.Left, -rawSideBounds.Bottom);
        var sideBounds = GetBounds(side);
        var primary = options.GenerateElevation
            ? CabinetViewKind.Elevation
            : options.GeneratePlan ? CabinetViewKind.Plan : CabinetViewKind.Side;

        var shapes = new List<CabinetViewShape>();
        if (options.GenerateElevation)
        {
            shapes.AddRange(elevation);
        }

        if (options.GeneratePlan)
        {
            shapes.AddRange(plan);
        }

        if (options.GenerateSide)
        {
            shapes.AddRange(side);
        }

        var labelBounds = primary switch
        {
            CabinetViewKind.Plan => planBounds,
            CabinetViewKind.Side => sideBounds,
            _ => elevationBounds
        };
        return new CabinetLayout(parts, shapes, primary, elevationBounds, planBounds, sideBounds, labelBounds);
    }

    private static IReadOnlyList<CabinetPart> CreateParts(CabinetOptions options)
    {
        var thickness = options.Thickness;
        var bottomY = Math.Max(options.FrontToeKickHeight, options.RearToeKickHeight);
        var bodyTopY = options.Height - options.TopRailHeight;
        var backMaxZ = options.Depth - options.BackboardOverallInset;
        var backMinZ = options.BackboardRearCoverMode
            ? backMaxZ
            : backMaxZ - options.BackboardThickness;
        if (options.BackboardRearCoverMode)
        {
            backMaxZ += options.BackboardThickness;
        }

        var parts = new List<CabinetPart>
        {
            CreatePlate(
                CabinetPartKind.LeftSide,
                "左侧板",
                new CabinetBox(
                    0, thickness,
                    -options.LeftSideBottomExtension, bodyTopY + options.LeftSideTopExtension,
                    -options.LeftSideFrontExtension, options.Depth + options.LeftSideRearExtension),
                bodyTopY + options.LeftSideTopExtension + options.LeftSideBottomExtension,
                options.Depth + options.LeftSideFrontExtension + options.LeftSideRearExtension,
                thickness),
            CreatePlate(
                CabinetPartKind.RightSide,
                "右侧板",
                new CabinetBox(
                    options.Width - thickness, options.Width,
                    -options.RightSideBottomExtension, bodyTopY + options.RightSideTopExtension,
                    -options.RightSideFrontExtension, options.Depth + options.RightSideRearExtension),
                bodyTopY + options.RightSideTopExtension + options.RightSideBottomExtension,
                options.Depth + options.RightSideFrontExtension + options.RightSideRearExtension,
                thickness),
            CreatePlate(
                CabinetPartKind.Top,
                "顶板",
                new CabinetBox(
                    thickness, options.Width - thickness,
                    bodyTopY - thickness, bodyTopY,
                    options.TopFrontInset, options.Depth - options.TopRearInset),
                options.Width - thickness * 2,
                options.Depth - options.TopFrontInset - options.TopRearInset,
                thickness),
            CreatePlate(
                CabinetPartKind.Bottom,
                "底板",
                new CabinetBox(
                    thickness, options.Width - thickness,
                    bottomY, bottomY + thickness,
                    options.BottomFrontInset, options.Depth - options.BottomRearInset),
                options.Width - thickness * 2,
                options.Depth - options.BottomFrontInset - options.BottomRearInset,
                thickness),
            new CabinetPart(
                CabinetPartKind.Backboard,
                "背板",
                "背板",
                new CabinetBox(
                    thickness - options.BackboardLeftExtension,
                    options.Width - thickness + options.BackboardRightExtension,
                    bottomY + thickness - options.BackboardBottomExtension,
                    bodyTopY - thickness + options.BackboardTopExtension,
                    backMinZ, backMaxZ),
                options.Width - thickness * 2 +
                    options.BackboardLeftExtension + options.BackboardRightExtension,
                bodyTopY - bottomY - thickness * 2 +
                    options.BackboardTopExtension + options.BackboardBottomExtension,
                options.BackboardThickness),
            CreatePlate(
                CabinetPartKind.TopRail,
                "顶线",
                new CabinetBox(
                    -options.TopRailLeftExtension,
                    options.Width + options.TopRailRightExtension,
                    bodyTopY,
                    options.Height,
                    options.TopRailRearInset, options.TopRailRearInset + thickness),
                options.Width + options.TopRailLeftExtension + options.TopRailRightExtension,
                options.TopRailHeight,
                thickness),
            CreatePlate(
                CabinetPartKind.TopRailAuxiliary,
                "顶线辅助板",
                new CabinetBox(
                    -options.TopRailLeftExtension,
                    options.Width + options.TopRailRightExtension,
                    bodyTopY, bodyTopY + thickness,
                    options.TopRailRearInset + thickness,
                    options.TopRailRearInset + thickness + options.TopRailAuxiliaryDepth),
                options.Width + options.TopRailLeftExtension + options.TopRailRightExtension,
                options.TopRailAuxiliaryDepth,
                thickness),
            CreatePlate(
                CabinetPartKind.FrontToeKick,
                "前踢脚线",
                new CabinetBox(
                    -options.FrontToeKickLeftExtension, options.Width + options.FrontToeKickRightExtension,
                    0, options.FrontToeKickHeight,
                    options.FrontToeKickFrontInset, options.FrontToeKickFrontInset + thickness),
                options.Width + options.FrontToeKickLeftExtension + options.FrontToeKickRightExtension,
                options.FrontToeKickHeight,
                thickness),
            CreatePlate(
                CabinetPartKind.RearToeKick,
                "后踢脚线",
                new CabinetBox(
                    -options.RearToeKickLeftExtension, options.Width + options.RearToeKickRightExtension,
                    0, options.RearToeKickHeight,
                    options.Depth - options.RearToeKickRearInset - thickness,
                    options.Depth - options.RearToeKickRearInset),
                options.Width + options.RearToeKickLeftExtension + options.RearToeKickRightExtension,
                options.RearToeKickHeight,
                thickness)
        };
        return parts;
    }

    private static CabinetPart CreatePlate(
        CabinetPartKind kind,
        string name,
        CabinetBox box,
        double cutWidth,
        double cutHeight,
        double cutThickness) =>
        new(kind, name, "普通板件", box, cutWidth, cutHeight, cutThickness);

    private static CabinetViewShape Project(CabinetPart part, CabinetViewKind view)
    {
        var bounds = view switch
        {
            CabinetViewKind.Elevation => new CabinetRectangle(
                part.Box.MinX, part.Box.MinY, part.Box.MaxX, part.Box.MaxY),
            CabinetViewKind.Plan => new CabinetRectangle(
                part.Box.MinX, part.Box.MinZ, part.Box.MaxX, part.Box.MaxZ),
            CabinetViewKind.Side => new CabinetRectangle(
                -part.Box.MaxZ, part.Box.MinY, -part.Box.MinZ, part.Box.MaxY),
            _ => throw new ArgumentOutOfRangeException(nameof(view))
        };

        var hidden = (view == CabinetViewKind.Plan && part.Kind == CabinetPartKind.Backboard) ||
                     (view == CabinetViewKind.Side &&
                      part.Kind is CabinetPartKind.Backboard or CabinetPartKind.RearToeKick);
        return new CabinetViewShape(view, part.Kind, bounds, hidden);
    }

    private static bool IsIncludedInView(CabinetPartKind part, CabinetViewKind view) => view switch
    {
        CabinetViewKind.Elevation => part is
            CabinetPartKind.LeftSide or
            CabinetPartKind.RightSide or
            CabinetPartKind.Top or
            CabinetPartKind.Bottom or
            CabinetPartKind.TopRail or
            CabinetPartKind.FrontToeKick,
        CabinetViewKind.Plan => part is
            CabinetPartKind.LeftSide or
            CabinetPartKind.RightSide or
            CabinetPartKind.Top or
            CabinetPartKind.Backboard,
        CabinetViewKind.Side => part != CabinetPartKind.RightSide,
        _ => false
    };

    private static CabinetViewShape[] Offset(
        IEnumerable<CabinetViewShape> shapes,
        double x,
        double y) =>
        shapes.Select(shape => shape with { Bounds = shape.Bounds.Offset(x, y) }).ToArray();

    private static CabinetRectangle GetBounds(IReadOnlyList<CabinetViewShape> shapes) =>
        new(
            shapes.Min(shape => shape.Bounds.Left),
            shapes.Min(shape => shape.Bounds.Bottom),
            shapes.Max(shape => shape.Bounds.Right),
            shapes.Max(shape => shape.Bounds.Top));
}
