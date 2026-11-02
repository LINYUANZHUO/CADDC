namespace YiZhiMao.Dc.Core;

public enum ShoeCabinetFrontSide
{
    Left,
    Right
}

public sealed record ShoeShelfLayoutOptions(
    double Thickness,
    double ShelfDepth,
    bool SmartAngle,
    double PreferredAngleDegrees,
    double TargetSpacing,
    double TopReserve,
    double BottomReserve,
    double FrontClearance,
    double RearClearance,
    double StopperHeight,
    bool GenerateStopper,
    int CountAdjustment);

public sealed record ShoeShelfPartLayout(
    BoardPlacementPoint Start,
    BoardPlacementPoint End,
    BoardPlacementPolygon Polygon);

public sealed record ShoeShelfLayerLayout(
    ShoeShelfPartLayout Shelf,
    ShoeShelfPartLayout? Stopper);

public sealed record ShoeShelfLayout(
    ShoeCabinetFrontSide FrontSide,
    double AngleDegrees,
    double LayerSpacing,
    IReadOnlyList<ShoeShelfLayerLayout> Layers);

public sealed record ShoeShelfPreviewResolution(
    ShoeShelfLayout Layout,
    ShoeShelfLayoutOptions EffectiveOptions,
    bool UsedSmartFallback);

public static class ShoeShelfLayoutCalculator
{
    private const double MaximumAngleDegrees = 89;
    private const double Tolerance = 0.000001;

    public static ShoeShelfLayout Calculate(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        ShoeShelfLayoutOptions options)
    {
        Validate(bounds, cursor, options);

        var frontSide = cursor.X < (bounds.Left + bounds.Right) / 2
            ? ShoeCabinetFrontSide.Left
            : ShoeCabinetFrontSide.Right;
        var availableWidth = bounds.Right - bounds.Left - options.FrontClearance - options.RearClearance;
        if (availableWidth <= 0)
        {
            throw new InvalidOperationException("前后预留已占满柜体内空。" );
        }

        var angle = ResolveAngle(options, availableWidth);
        var unit = CreateRightUnit(angle, options);
        var unitPoints = GetPoints(unit).ToArray();
        var unitMinX = unitPoints.Min(point => point.X);
        var unitMaxX = unitPoints.Max(point => point.X);
        var shiftX = bounds.Left + options.RearClearance - unitMinX;
        var shifted = Translate(unit, shiftX, 0);
        if (GetPoints(shifted).Max(point => point.X) > bounds.Right - options.FrontClearance + Tolerance)
        {
            throw new InvalidOperationException("当前角度和预留无法放入柜体深度。" );
        }

        var shiftedPoints = GetPoints(shifted).ToArray();
        var minY = shiftedPoints.Min(point => point.Y);
        var maxY = shiftedPoints.Max(point => point.Y);
        var minimumAnchor = bounds.Bottom + options.BottomReserve - minY;
        var maximumAnchor = bounds.Top - options.TopReserve - maxY;
        if (maximumAnchor < minimumAnchor - Tolerance)
        {
            throw new InvalidOperationException("上下预留后没有足够高度生成斜层板。" );
        }

        var anchorRange = Math.Max(0, maximumAnchor - minimumAnchor);
        var baseCount = Math.Max(1, (int)Math.Floor((anchorRange + Tolerance) / options.TargetSpacing) + 1);
        var count = Math.Max(1, baseCount + options.CountAdjustment);
        var spacing = count == 1 ? 0 : anchorRange / (count - 1);
        var layers = new List<ShoeShelfLayerLayout>(count);
        for (var index = 0; index < count; index++)
        {
            var anchor = count == 1
                ? (minimumAnchor + maximumAnchor) / 2
                : minimumAnchor + spacing * index;
            var layer = Translate(shifted, 0, anchor);
            layers.Add(frontSide == ShoeCabinetFrontSide.Right
                ? layer
                : Mirror(layer, bounds.Left + bounds.Right));
        }

        return new ShoeShelfLayout(frontSide, angle, spacing, layers);
    }

    public static ShoeShelfPreviewResolution ResolvePreview(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        ShoeShelfLayoutOptions options)
    {
        try
        {
            return new ShoeShelfPreviewResolution(
                Calculate(bounds, cursor, options),
                options,
                false);
        }
        catch (InvalidOperationException) when (!options.SmartAngle)
        {
            var smartOptions = options with { SmartAngle = true };
            return new ShoeShelfPreviewResolution(
                Calculate(bounds, cursor, smartOptions),
                smartOptions,
                true);
        }
    }

    private static double ResolveAngle(ShoeShelfLayoutOptions options, double availableWidth)
    {
        if (GetWidth(CreateRightUnit(options.PreferredAngleDegrees, options)) <= availableWidth + Tolerance)
        {
            return options.PreferredAngleDegrees;
        }

        if (!options.SmartAngle)
        {
            throw new InvalidOperationException("当前角度的斜层板无法放入柜体深度。" );
        }

        if (GetWidth(CreateRightUnit(MaximumAngleDegrees, options)) > availableWidth + Tolerance)
        {
            throw new InvalidOperationException("柜体深度不足以放入当前层板和前挡条。" );
        }

        var low = options.PreferredAngleDegrees;
        var high = MaximumAngleDegrees;
        for (var index = 0; index < 64; index++)
        {
            var middle = (low + high) / 2;
            if (GetWidth(CreateRightUnit(middle, options)) <= availableWidth)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return high;
    }

    private static ShoeShelfLayerLayout CreateRightUnit(double angleDegrees, ShoeShelfLayoutOptions options)
    {
        var angle = angleDegrees * Math.PI / 180;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var shelfStart = new BoardPlacementPoint(0, 0);
        var shelfEnd = new BoardPlacementPoint(
            options.ShelfDepth * cosine,
            -options.ShelfDepth * sine);
        var shelf = CreatePart(shelfStart, shelfEnd, options.Thickness);
        ShoeShelfPartLayout? stopper = null;
        if (options.GenerateStopper)
        {
            var shelfBottomFront = new BoardPlacementPoint(
                shelfEnd.X - sine * options.Thickness / 2,
                shelfEnd.Y - cosine * options.Thickness / 2);
            var stopperStart = new BoardPlacementPoint(
                shelfBottomFront.X + cosine * options.Thickness / 2,
                shelfBottomFront.Y - sine * options.Thickness / 2);
            var stopperLength = options.Thickness + options.StopperHeight;
            var stopperEnd = new BoardPlacementPoint(
                stopperStart.X + stopperLength * sine,
                stopperStart.Y + stopperLength * cosine);
            stopper = CreatePart(stopperStart, stopperEnd, options.Thickness);
        }

        return new ShoeShelfLayerLayout(shelf, stopper);
    }

    private static ShoeShelfPartLayout CreatePart(
        BoardPlacementPoint start,
        BoardPlacementPoint end,
        double thickness)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var offsetX = -dy / length * thickness / 2;
        var offsetY = dx / length * thickness / 2;
        var points = new[]
        {
            new BoardPlacementPoint(start.X + offsetX, start.Y + offsetY),
            new BoardPlacementPoint(end.X + offsetX, end.Y + offsetY),
            new BoardPlacementPoint(end.X - offsetX, end.Y - offsetY),
            new BoardPlacementPoint(start.X - offsetX, start.Y - offsetY)
        };
        return new ShoeShelfPartLayout(start, end, new BoardPlacementPolygon(points));
    }

    private static double GetWidth(ShoeShelfLayerLayout layer)
    {
        var points = GetPoints(layer).ToArray();
        return points.Max(point => point.X) - points.Min(point => point.X);
    }

    private static IEnumerable<BoardPlacementPoint> GetPoints(ShoeShelfLayerLayout layer) =>
        layer.Stopper is null
            ? layer.Shelf.Polygon.Points
            : layer.Shelf.Polygon.Points.Concat(layer.Stopper.Polygon.Points);

    private static ShoeShelfLayerLayout Translate(ShoeShelfLayerLayout layer, double x, double y) =>
        new(Translate(layer.Shelf, x, y), layer.Stopper is null ? null : Translate(layer.Stopper, x, y));

    private static ShoeShelfPartLayout Translate(ShoeShelfPartLayout part, double x, double y) =>
        new(
            new BoardPlacementPoint(part.Start.X + x, part.Start.Y + y),
            new BoardPlacementPoint(part.End.X + x, part.End.Y + y),
            new BoardPlacementPolygon(part.Polygon.Points
                .Select(point => new BoardPlacementPoint(point.X + x, point.Y + y))
                .ToArray()));

    private static ShoeShelfLayerLayout Mirror(ShoeShelfLayerLayout layer, double xSum) =>
        new(Mirror(layer.Shelf, xSum), layer.Stopper is null ? null : Mirror(layer.Stopper, xSum));

    private static ShoeShelfPartLayout Mirror(ShoeShelfPartLayout part, double xSum) =>
        new(
            new BoardPlacementPoint(xSum - part.Start.X, part.Start.Y),
            new BoardPlacementPoint(xSum - part.End.X, part.End.Y),
            new BoardPlacementPolygon(part.Polygon.Points
                .Select(point => new BoardPlacementPoint(xSum - point.X, point.Y))
                .ToArray()));

    private static void Validate(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        ShoeShelfLayoutOptions options)
    {
        if (!IsFinite(bounds.Left, bounds.Right, bounds.Bottom, bounds.Top, cursor.X, cursor.Y) ||
            bounds.Right <= bounds.Left || bounds.Top <= bounds.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds));
        }

        if (!IsFinite(
                options.Thickness,
                options.ShelfDepth,
                options.PreferredAngleDegrees,
                options.TargetSpacing,
                options.TopReserve,
                options.BottomReserve,
                options.FrontClearance,
                options.RearClearance,
                options.StopperHeight) ||
            options.Thickness <= 0 || options.ShelfDepth <= 0 ||
            options.PreferredAngleDegrees <= 0 || options.PreferredAngleDegrees >= MaximumAngleDegrees ||
            options.TargetSpacing <= 0 || options.StopperHeight <= 0 ||
            options.TopReserve < 0 || options.BottomReserve < 0 ||
            options.FrontClearance < 0 || options.RearClearance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private static bool IsFinite(params double[] values) =>
        values.All(value => !double.IsNaN(value) && !double.IsInfinity(value));
}
