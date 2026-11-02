namespace YiZhiMao.Dc.Core;

public enum BoardDivisionMode
{
    SmartMaximumClearSpan,
    CompartmentCount
}

public sealed record BoardDivisionOptions(
    BoardDivisionMode Mode,
    double Thickness,
    bool LeaveRemainderAtEnd,
    double MaximumClearSpan = 430,
    int CompartmentCount = 3,
    double RoundingIncrement = 1);

public sealed record BoardDivisionLayout(
    int CompartmentCount,
    IReadOnlyList<double> ClearSpans,
    IReadOnlyList<double> DividerOffsets);

public sealed record BoardDivisionPlacement(
    BoardOffsetSide RemainderSide,
    bool IsVertical,
    BoardDivisionLayout Layout,
    IReadOnlyList<double> DividerCoordinates);

public sealed record DoorPartitionGuideLayout(
    int DoorCount,
    IReadOnlyList<double> BoundaryOffsets);

public static class BoardDivisionCalculator
{
    private const double Tolerance = 0.000001;

    public static DoorPartitionGuideLayout CalculateDoorGuideLayout(
        double span,
        bool smartMode,
        double maximumDoorWidth,
        int defaultDoorCount,
        int countAdjustment = 0)
    {
        if (!Compatibility.IsFinite(span) || span <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "分区范围必须大于 0。");
        }

        if (!Compatibility.IsFinite(maximumDoorWidth) || maximumDoorWidth <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDoorWidth), "最大门扇宽度必须大于 0。");
        }

        if (defaultDoorCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDoorCount), "默认门扇数量至少为 1。");
        }

        var baseCount = smartMode
            ? Math.Max(1, (int)Math.Ceiling(span / maximumDoorWidth - Tolerance))
            : defaultDoorCount;
        var doorCount = Math.Max(1, baseCount + countAdjustment);
        var offsets = new double[doorCount + 1];
        for (var index = 0; index <= doorCount; index++)
        {
            offsets[index] = span * index / doorCount;
        }

        return new DoorPartitionGuideLayout(doorCount, offsets);
    }

    public static BoardDivisionLayout CalculateLayout(double span, BoardDivisionOptions options)
    {
        ValidateCommon(span, options);
        if (options.Mode == BoardDivisionMode.CompartmentCount)
        {
            if (options.CompartmentCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.CompartmentCount),
                    "按格数分时格数至少为 2。");
            }

            return CalculateByCount(span, options, options.CompartmentCount);
        }

        if (!Compatibility.IsFinite(options.MaximumClearSpan) || options.MaximumClearSpan <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.MaximumClearSpan),
                "智能分格的最大净空必须大于 0。");
        }

        var compartmentCount = Math.Max(
            1,
            (int)Math.Ceiling(
                (span + options.Thickness) /
                (options.MaximumClearSpan + options.Thickness) -
                Tolerance));
        while (true)
        {
            var layout = CalculateByCount(span, options, compartmentCount);
            if (layout.ClearSpans.Max() <= options.MaximumClearSpan + Tolerance)
            {
                return layout;
            }

            compartmentCount++;
        }
    }

    public static BoardDivisionPlacement CalculatePlacement(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        BoardDivisionOptions options)
    {
        if (!Compatibility.IsFinite(bounds.Left) || !Compatibility.IsFinite(bounds.Right) ||
            !Compatibility.IsFinite(bounds.Bottom) || !Compatibility.IsFinite(bounds.Top) ||
            bounds.Right - bounds.Left <= Tolerance || bounds.Top - bounds.Bottom <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(bounds), "内空边界必须形成有效矩形。");
        }

        if (!Compatibility.IsFinite(cursor.X) || !Compatibility.IsFinite(cursor.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "鼠标位置必须是有效坐标。");
        }

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var deltaX = cursor.X - centerX;
        var deltaY = cursor.Y - centerY;
        if (Math.Abs(deltaX) <= Tolerance && Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "请把鼠标移到空间中心的上、下、左或右侧。");
        }

        var isVertical = Math.Abs(deltaX) >= Math.Abs(deltaY);
        var remainderSide = isVertical
            ? deltaX >= 0 ? BoardOffsetSide.Right : BoardOffsetSide.Left
            : deltaY >= 0 ? BoardOffsetSide.Top : BoardOffsetSide.Bottom;
        var span = isVertical ? bounds.Right - bounds.Left : bounds.Top - bounds.Bottom;
        var layout = CalculateLayout(span, options);
        var coordinates = layout.DividerOffsets.Select(offset =>
        {
            if (isVertical)
            {
                return remainderSide == BoardOffsetSide.Right
                    ? bounds.Left + offset
                    : bounds.Right - offset;
            }

            return remainderSide == BoardOffsetSide.Top
                ? bounds.Bottom + offset
                : bounds.Top - offset;
        }).ToArray();

        return new BoardDivisionPlacement(remainderSide, isVertical, layout, coordinates);
    }

    private static BoardDivisionLayout CalculateByCount(
        double span,
        BoardDivisionOptions options,
        int compartmentCount)
    {
        var clearSpan = span - (compartmentCount - 1d) * options.Thickness;
        if (!Compatibility.IsFinite(clearSpan) || clearSpan <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Thickness),
                "板厚总和必须小于均分范围。");
        }

        double[] clearSpans;
        if (!options.LeaveRemainderAtEnd || compartmentCount == 1)
        {
            var clearCompartment = clearSpan / compartmentCount;
            clearSpans = Enumerable.Repeat(clearCompartment, compartmentCount).ToArray();
        }
        else
        {
            var exact = clearSpan / compartmentCount;
            var regular = Math.Floor((exact + Tolerance) / options.RoundingIncrement) *
                          options.RoundingIncrement;
            if (regular <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options.RoundingIncrement),
                    "当前取整精度会产生零净空，请减少格数或关闭余量留最后。");
            }

            clearSpans = Enumerable.Repeat(regular, compartmentCount).ToArray();
            clearSpans[clearSpans.Length - 1] = clearSpan - regular * (compartmentCount - 1);
        }

        var dividerOffsets = new double[Math.Max(0, compartmentCount - 1)];
        var cursor = 0d;
        for (var index = 0; index < dividerOffsets.Length; index++)
        {
            cursor += clearSpans[index];
            dividerOffsets[index] = cursor + options.Thickness / 2;
            cursor += options.Thickness;
        }

        return new BoardDivisionLayout(compartmentCount, clearSpans, dividerOffsets);
    }

    private static void ValidateCommon(double span, BoardDivisionOptions options)
    {
        if (!Compatibility.IsFinite(span) || span <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "均分范围必须大于 0。");
        }

        if (!Compatibility.IsFinite(options.Thickness) || options.Thickness <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(options.Thickness), "均分板厚必须大于 0。");
        }

        if (!Compatibility.IsFinite(options.RoundingIncrement) || options.RoundingIncrement <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(options.RoundingIncrement), "余量取整精度必须大于 0。");
        }
    }
}
