namespace YiZhiMao.Dc.Core;

public readonly record struct HangingRodPlacement(double Left, double Right, double Y)
{
    public double Width => Right - Left;
}

public readonly record struct HorizontalLineSpan(double Left, double Right, double Y);

public readonly record struct VerticalLineSpan(double X, double Bottom, double Top);

public readonly record struct HingeDimensionSegment(double StartOffset, double EndOffset, int Level);

public readonly record struct HingeTypeLabelPlacement(double X, bool ExtendsRight);

public static class HardwareLayoutCalculator
{
    public static HorizontalLineSpan? CreateShelfSpanFromBounds(
        double left,
        double right,
        double bottom,
        double top)
    {
        if (!Compatibility.IsFinite(left) ||
            !Compatibility.IsFinite(right) ||
            !Compatibility.IsFinite(bottom) ||
            !Compatibility.IsFinite(top))
        {
            return null;
        }

        var width = right - left;
        var thickness = top - bottom;
        if (width <= 100 || thickness < 10 || thickness > 50)
        {
            return null;
        }

        return new HorizontalLineSpan(left, right, (bottom + top) / 2);
    }

    public static HorizontalLineSpan? FindNearestCoveringLineAbove(
        HorizontalLineSpan selected,
        IEnumerable<HorizontalLineSpan> candidates,
        double tolerance = 0.000001)
    {
        if (!Compatibility.IsFinite(selected.Left) ||
            !Compatibility.IsFinite(selected.Right) ||
            !Compatibility.IsFinite(selected.Y) ||
            selected.Right - selected.Left <= tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(selected), "所选横线长度必须大于 0。");
        }

        return candidates
            .Where(candidate =>
                Compatibility.IsFinite(candidate.Left) &&
                Compatibility.IsFinite(candidate.Right) &&
                Compatibility.IsFinite(candidate.Y) &&
                candidate.Right - candidate.Left > tolerance &&
                candidate.Y > selected.Y + tolerance &&
                candidate.Left <= selected.Left + tolerance &&
                candidate.Right >= selected.Right - tolerance)
            .OrderBy(candidate => candidate.Y)
            .Select(candidate => (HorizontalLineSpan?)candidate)
            .FirstOrDefault();
    }

    public static HorizontalLineSpan FindHorizontalBayAt(
        HorizontalLineSpan baseline,
        double pickedX,
        IEnumerable<VerticalLineSpan> verticalLines,
        double tolerance = 0.000001)
    {
        if (!Compatibility.IsFinite(baseline.Left) ||
            !Compatibility.IsFinite(baseline.Right) ||
            !Compatibility.IsFinite(baseline.Y) ||
            baseline.Right - baseline.Left <= tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(baseline), "所选横线长度必须大于 0。");
        }

        if (!Compatibility.IsFinite(pickedX) ||
            pickedX <= baseline.Left + tolerance ||
            pickedX >= baseline.Right - tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(pickedX), "点击位置必须位于所选横线内部。");
        }

        var boundaries = verticalLines
            .Where(line =>
                Compatibility.IsFinite(line.X) &&
                Compatibility.IsFinite(line.Bottom) &&
                Compatibility.IsFinite(line.Top) &&
                line.Top - line.Bottom > tolerance &&
                line.X >= baseline.Left - tolerance &&
                line.X <= baseline.Right + tolerance &&
                line.Bottom <= baseline.Y + tolerance &&
                line.Top >= baseline.Y - tolerance)
            .Select(line => line.X)
            .Concat(new[] { baseline.Left, baseline.Right })
            .ToArray();

        var left = boundaries.Where(value => value < pickedX - tolerance).Max();
        var right = boundaries.Where(value => value > pickedX + tolerance).Min();
        return new HorizontalLineSpan(left, right, baseline.Y);
    }

    public static HangingRodPlacement CalculateHangingRod(
        double left,
        double right,
        double top,
        double topOffset,
        double sideClearance)
    {
        if (!Compatibility.IsFinite(left) || !Compatibility.IsFinite(right) || right <= left)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "柜内净宽必须大于 0。");
        }

        if (!Compatibility.IsFinite(top) || !Compatibility.IsFinite(topOffset) || topOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topOffset), "挂衣杆离顶高度必须大于或等于 0。");
        }

        if (!Compatibility.IsFinite(sideClearance) || sideClearance < 0 || sideClearance * 2 >= right - left)
        {
            throw new ArgumentOutOfRangeException(nameof(sideClearance), "挂衣杆左右减尺超过柜内净宽。");
        }

        return new HangingRodPlacement(
            left + sideClearance,
            right - sideClearance,
            top - topOffset);
    }

    public static IReadOnlyList<double> CalculateHingeOffsets(
        double panelHeight,
        int hingeCount,
        double topOffset,
        double bottomOffset,
        bool useFixedSecondSpacing,
        double fixedSecondSpacing)
    {
        if (!Compatibility.IsFinite(panelHeight) || panelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(panelHeight), "门板高度必须大于 0。");
        }

        if (hingeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hingeCount), "铰链数量必须大于 0。");
        }

        if (!Compatibility.IsFinite(topOffset) || !Compatibility.IsFinite(bottomOffset) || topOffset < 0 || bottomOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topOffset), "铰链距门顶和距门底必须大于或等于 0。");
        }

        if (topOffset + bottomOffset >= panelHeight)
        {
            throw new ArgumentOutOfRangeException(nameof(panelHeight), "铰链距门顶与距门底之和必须小于门板高度。");
        }

        if (hingeCount == 1)
        {
            return [panelHeight / 2];
        }

        var lastOffset = panelHeight - bottomOffset;
        if (!useFixedSecondSpacing || hingeCount < 3)
        {
            return EvenlyDistribute(topOffset, lastOffset, hingeCount);
        }

        if (!Compatibility.IsFinite(fixedSecondSpacing) || fixedSecondSpacing <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedSecondSpacing), "第二铰链固定间距必须大于 0。");
        }

        var secondOffset = topOffset + fixedSecondSpacing;
        if (secondOffset >= lastOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedSecondSpacing), "第二铰链固定间距超过门板可用高度。");
        }

        var offsets = new List<double>(hingeCount) { topOffset, secondOffset };
        var remainingCount = hingeCount - 2;
        var interval = (lastOffset - secondOffset) / remainingCount;
        for (var index = 1; index <= remainingCount; index++)
        {
            offsets.Add(secondOffset + interval * index);
        }

        return offsets;
    }

    public static double CalculateHingeCenterX(
        double left,
        double right,
        bool isLeft,
        double edgeOffset,
        double symbolHalfWidth)
    {
        if (!Compatibility.IsFinite(left) || !Compatibility.IsFinite(right) || right <= left)
        {
            throw new ArgumentOutOfRangeException(nameof(right), "门板宽度必须大于 0。");
        }

        if (!Compatibility.IsFinite(edgeOffset) ||
            !Compatibility.IsFinite(symbolHalfWidth) ||
            edgeOffset < symbolHalfWidth ||
            edgeOffset + symbolHalfWidth > right - left)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeOffset), "铰链图形无法完整放入门板内部。");
        }

        return isLeft ? left + edgeOffset : right - edgeOffset;
    }

    public static IReadOnlyList<double> CalculateInnerOpeningHingeOffsets(
        DoorPanelLeaf panel,
        DoorPanelBounds innerBounds,
        HardwareOptions options)
    {
        Compatibility.NotNull(options, nameof(options));
        var topExtension = Math.Max(0, panel.Top - innerBounds.Top);
        var bottomExtension = Math.Max(0, innerBounds.Bottom - panel.Bottom);
        return CalculateHingeOffsets(
            panel.Height,
            options.GetHingeCount(innerBounds.Height),
            options.HingeTopOffset + topExtension,
            options.HingeBottomOffset + bottomExtension,
            options.UseFixedHingeSpacing,
            options.FixedHingeSpacing);
    }

    public static HingeTypeLabelPlacement CalculateHingeTypeLabelPlacement(
        double hingeCenterX,
        double hingeRadius,
        double doorLeft,
        double doorRight,
        double spacing)
    {
        if (!Compatibility.IsFinite(hingeCenterX) || !Compatibility.IsFinite(hingeRadius) ||
            !Compatibility.IsFinite(doorLeft) || !Compatibility.IsFinite(doorRight) ||
            !Compatibility.IsFinite(spacing) || hingeRadius <= 0 || spacing < 0 || doorRight <= doorLeft)
        {
            throw new ArgumentOutOfRangeException(nameof(doorRight), "门板范围、铰链半径或文字间距无效。");
        }

        var extendsRight = Math.Abs(hingeCenterX - doorLeft) <= Math.Abs(hingeCenterX - doorRight);
        return CalculateHingeTypeLabelPlacement(
            hingeCenterX,
            hingeRadius,
            extendsRight,
            spacing);
    }

    public static HingeTypeLabelPlacement CalculateHingeTypeLabelPlacement(
        double hingeCenterX,
        double hingeRadius,
        bool extendsRight,
        double spacing)
    {
        if (!Compatibility.IsFinite(hingeCenterX) || !Compatibility.IsFinite(hingeRadius) ||
            !Compatibility.IsFinite(spacing) || hingeRadius <= 0 || spacing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hingeRadius), "铰链半径或文字间距无效。");
        }

        var offset = hingeRadius + spacing;
        return new HingeTypeLabelPlacement(
            hingeCenterX + (extendsRight ? offset : -offset),
            extendsRight);
    }

    public static IReadOnlyList<double> MoveHingeOffsetsAwayFromShelves(
        DoorPanelLeaf panel,
        IReadOnlyList<double> offsets,
        IEnumerable<HorizontalLineSpan> shelves,
        double clearance,
        double minimumSpacing,
        double minimumEdgeOffset,
        double tolerance = 0.000001)
    {
        return MoveHingeOffsetsAwayFromShelves(
            panel,
            new DoorPanelBounds(panel.Left, panel.Right, panel.Bottom, panel.Top),
            offsets,
            shelves,
            clearance,
            minimumSpacing,
            minimumEdgeOffset,
            tolerance);
    }

    public static IReadOnlyList<double> MoveHingeOffsetsAwayFromShelves(
        DoorPanelLeaf panel,
        DoorPanelBounds shelfScope,
        IReadOnlyList<double> offsets,
        IEnumerable<HorizontalLineSpan> shelves,
        double clearance,
        double minimumSpacing,
        double minimumEdgeOffset,
        double tolerance = 0.000001)
    {
        var shelfOffsets = shelves
            .Where(shelf =>
                shelf.Right > shelfScope.Left + tolerance &&
                shelf.Left < shelfScope.Right - tolerance &&
                shelf.Y > shelfScope.Bottom + tolerance &&
                shelf.Y < shelfScope.Top - tolerance)
            .Select(shelf => panel.Top - shelf.Y)
            .ToArray();
        if (shelfOffsets.Length == 0)
        {
            return offsets;
        }

        var adjusted = new List<double>(offsets.Count);
        foreach (var requested in offsets)
        {
            var candidate = requested;
            for (var pass = 0; pass <= shelfOffsets.Length; pass++)
            {
                var conflict = shelfOffsets
                    .Where(shelf => Math.Abs(candidate - shelf) < clearance)
                    .OrderBy(shelf => Math.Abs(candidate - shelf))
                    .Select(shelf => (double?)shelf)
                    .FirstOrDefault();
                if (conflict is null)
                {
                    break;
                }

                var alternatives = new[] { conflict.Value - clearance, conflict.Value + clearance }
                    .Where(value =>
                        value >= minimumEdgeOffset &&
                        value <= panel.Height - minimumEdgeOffset &&
                        adjusted.All(existing => Math.Abs(existing - value) >= minimumSpacing))
                    .OrderBy(value => Math.Abs(value - requested))
                    .ToArray();
                if (alternatives.Length == 0)
                {
                    break;
                }

                candidate = alternatives[0];
            }

            adjusted.Add(candidate);
        }

        adjusted.Sort();
        return adjusted;
    }

    public static IReadOnlyList<HingeDimensionSegment> CalculateHingeDimensions(
        double panelHeight,
        IReadOnlyList<double> hingeOffsets,
        HingeDimensionMode mode)
    {
        if (!Compatibility.IsFinite(panelHeight) || panelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(panelHeight), "门板高度必须大于 0。");
        }

        if (hingeOffsets.Count == 0)
        {
            return [];
        }

        var offsets = hingeOffsets.OrderBy(offset => offset).ToArray();
        if (offsets.Any(offset => !Compatibility.IsFinite(offset) || offset <= 0 || offset >= panelHeight) ||
            offsets.Zip(offsets.Skip(1), (first, second) => second > first).Any(increasing => !increasing))
        {
            throw new ArgumentOutOfRangeException(nameof(hingeOffsets), "铰链位置必须位于门板内部且不能重复。");
        }

        if (mode == HingeDimensionMode.Continuous)
        {
            var boundaries = new[] { 0d }.Concat(offsets).Concat(new[] { panelHeight }).ToArray();
            return boundaries
                .Zip(boundaries.Skip(1), (start, end) => new HingeDimensionSegment(start, end, 0))
                .ToArray();
        }

        if (mode != HingeDimensionMode.SplitFromEnds)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "铰链标注模式无效。");
        }

        var dimensions = new List<HingeDimensionSegment>(offsets.Length);
        var topLevel = 0;
        foreach (var offset in offsets.Where(offset => offset <= panelHeight / 2))
        {
            dimensions.Add(new HingeDimensionSegment(0, offset, topLevel++));
        }

        var bottomLevel = 0;
        foreach (var offset in offsets.Where(offset => offset > panelHeight / 2).OrderByDescending(offset => offset))
        {
            dimensions.Add(new HingeDimensionSegment(panelHeight, offset, bottomLevel++));
        }

        return dimensions;
    }

    private static IReadOnlyList<double> EvenlyDistribute(double first, double last, int count)
    {
        var offsets = new List<double>(count);
        var interval = (last - first) / (count - 1);
        for (var index = 0; index < count; index++)
        {
            offsets.Add(first + interval * index);
        }

        return offsets;
    }
}
