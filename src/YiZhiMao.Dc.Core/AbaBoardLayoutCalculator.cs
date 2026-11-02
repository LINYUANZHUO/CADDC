namespace YiZhiMao.Dc.Core;

public sealed record AbaBoardLayerLayout(
    int LayerNumber,
    double Thickness,
    double Depth,
    double CenterOffset,
    double FrontOffset,
    double BackOffset);

public sealed record AbaBoardLayout(
    IReadOnlyList<AbaBoardLayerLayout> Layers,
    double TotalThickness,
    double MaximumDepth);

public sealed record AbaBoardSpacePlacement(
    BoardOffsetSide Side,
    BoardPlacementPoint Start,
    BoardPlacementPoint End);

public static class AbaBoardLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static AbaBoardLayout Calculate(
        double firstThickness,
        double firstDepth,
        double secondThickness,
        double secondDepth,
        double thirdThickness,
        double thirdDepth)
    {
        var thicknesses = new[] { firstThickness, secondThickness, thirdThickness };
        var depths = new[] { firstDepth, secondDepth, thirdDepth };
        if (thicknesses.Any(value => !Compatibility.IsFinite(value) || value <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(firstThickness), "三层板厚必须大于 0。");
        }

        if (depths.Any(value => !Compatibility.IsFinite(value) || value <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(firstDepth), "三层板深必须大于 0。");
        }

        var totalThickness = thicknesses.Sum();
        var maximumDepth = depths.Max();
        var backOffset = maximumDepth;
        var layerEdgeOffset = totalThickness / 2;
        var layers = new AbaBoardLayerLayout[3];
        for (var index = 0; index < layers.Length; index++)
        {
            var thickness = thicknesses[index];
            var depth = depths[index];
            var centerOffset = layerEdgeOffset - thickness / 2;
            var frontOffset = maximumDepth - depth;
            layers[index] = new AbaBoardLayerLayout(
                index + 1,
                thickness,
                depth,
                centerOffset,
                frontOffset,
                backOffset);
            layerEdgeOffset -= thickness;
        }

        return new AbaBoardLayout(layers, totalThickness, maximumDepth);
    }

    public static AbaBoardSpacePlacement CalculateSpacePlacement(
        BoardSpaceBounds bounds,
        BoardPlacementPoint cursor,
        double spaceInset,
        double totalThickness)
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

        if (!Compatibility.IsFinite(spaceInset) || spaceInset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(spaceInset), "空间内退不能小于 0。");
        }

        if (!Compatibility.IsFinite(totalThickness) || totalThickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalThickness), "夹心层板总厚必须大于 0。");
        }

        var centerX = (bounds.Left + bounds.Right) / 2;
        var centerY = (bounds.Bottom + bounds.Top) / 2;
        var deltaX = cursor.X - centerX;
        var deltaY = cursor.Y - centerY;
        if (Math.Abs(deltaX) <= Tolerance && Math.Abs(deltaY) <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(cursor), "请把鼠标移到空间中心的上、下、左或右侧。");
        }

        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
        {
            if (spaceInset + totalThickness > bounds.Right - bounds.Left + Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(spaceInset), "空间内退与夹心层板总厚之和不能大于内空宽度。");
            }

            if (deltaX < 0)
            {
                var center = bounds.Left + spaceInset + totalThickness / 2;
                return new AbaBoardSpacePlacement(
                    BoardOffsetSide.Left,
                    new BoardPlacementPoint(center, bounds.Bottom),
                    new BoardPlacementPoint(center, bounds.Top));
            }

            var rightCenter = bounds.Right - spaceInset - totalThickness / 2;
            return new AbaBoardSpacePlacement(
                BoardOffsetSide.Right,
                new BoardPlacementPoint(rightCenter, bounds.Top),
                new BoardPlacementPoint(rightCenter, bounds.Bottom));
        }

        if (spaceInset + totalThickness > bounds.Top - bounds.Bottom + Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(spaceInset), "空间内退与夹心层板总厚之和不能大于内空高度。");
        }

        if (deltaY < 0)
        {
            var center = bounds.Bottom + spaceInset + totalThickness / 2;
            return new AbaBoardSpacePlacement(
                BoardOffsetSide.Bottom,
                new BoardPlacementPoint(bounds.Right, center),
                new BoardPlacementPoint(bounds.Left, center));
        }

        var topCenter = bounds.Top - spaceInset - totalThickness / 2;
        return new AbaBoardSpacePlacement(
            BoardOffsetSide.Top,
            new BoardPlacementPoint(bounds.Left, topCenter),
            new BoardPlacementPoint(bounds.Right, topCenter));
    }
}
