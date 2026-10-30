namespace YiZhiMao.Dc.Core;

public static class BoardGeometryCalculator
{
    private const double DefaultTolerance = 0.000001;

    public static IReadOnlyList<double> GetParallelLineOffsets(int lineCount, double thickness)
    {
        if (lineCount is not (2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(lineCount), "替换线数量只能是 2 或 3。");
        }

        if (!double.IsFinite(thickness) || thickness <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "偏移板厚必须大于 0。");
        }

        var half = thickness / 2;
        return lineCount == 2 ? [-half, half] : [-half, 0, half];
    }

    public static IReadOnlyList<double> GetDividerCenters(
        double span,
        int compartmentCount,
        double thickness)
    {
        if (!double.IsFinite(span) || span <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "均分范围必须大于 0。");
        }

        if (compartmentCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(compartmentCount), "均分格数至少为 2。");
        }

        if (!double.IsFinite(thickness) || thickness <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "均分板厚必须大于 0。");
        }

        var clearSpan = span - (compartmentCount - 1d) * thickness;
        if (!double.IsFinite(clearSpan) || clearSpan <= DefaultTolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), "板厚总和必须小于均分范围。");
        }

        var clearCompartment = clearSpan / compartmentCount;
        var centers = new double[compartmentCount - 1];
        for (var index = 1; index < compartmentCount; index++)
        {
            centers[index - 1] = index * clearCompartment + (index - 0.5) * thickness;
        }

        return centers;
    }

    public static bool IsInteriorSplitCoordinate(
        double minimum,
        double maximum,
        double coordinate,
        double tolerance = DefaultTolerance)
    {
        return double.IsFinite(minimum) &&
               double.IsFinite(maximum) &&
               double.IsFinite(coordinate) &&
               double.IsFinite(tolerance) &&
               tolerance >= 0 &&
               maximum - minimum > tolerance * 2 &&
               coordinate > minimum + tolerance &&
               coordinate < maximum - tolerance;
    }

    public static bool CanEditLineToIntersection(
        double intersectionParameter,
        bool pickedNearStart,
        bool isExtend,
        double tolerance = DefaultTolerance)
    {
        if (!double.IsFinite(intersectionParameter) || !double.IsFinite(tolerance) || tolerance < 0)
        {
            return false;
        }

        if (!isExtend)
        {
            return intersectionParameter > tolerance && intersectionParameter < 1 - tolerance;
        }

        return pickedNearStart
            ? intersectionParameter < -tolerance
            : intersectionParameter > 1 + tolerance;
    }

    public static bool HaveCompatibleMergeProperties(
        string firstLayer,
        string secondLayer,
        double firstThickness,
        double secondThickness,
        double firstElevation,
        double secondElevation,
        double tolerance = DefaultTolerance)
    {
        return !string.IsNullOrWhiteSpace(firstLayer) &&
               string.Equals(firstLayer, secondLayer, StringComparison.OrdinalIgnoreCase) &&
               double.IsFinite(firstThickness) &&
               double.IsFinite(secondThickness) &&
               double.IsFinite(firstElevation) &&
               double.IsFinite(secondElevation) &&
               double.IsFinite(tolerance) &&
               tolerance >= 0 &&
               Math.Abs(firstThickness - secondThickness) <= tolerance &&
               Math.Abs(firstElevation - secondElevation) <= tolerance;
    }
}
