namespace YiZhiMao.Dc.Core;

public sealed record DoorPanelDivision(IReadOnlyList<double> PanelWidths, double ActualGap);

public static class DoorPanelDivisionCalculator
{
    private const double Tolerance = 0.000001;

    public static DoorPanelDivision Calculate(
        double totalWidth,
        int doorCount,
        double targetGap,
        DoorPanelCalculationMode calculationMode)
    {
        ValidateInputs(totalWidth, doorCount, targetGap, calculationMode);

        if (doorCount == 1)
        {
            return new DoorPanelDivision([totalWidth], 0);
        }

        var roundedGap = Math.Round(targetGap, MidpointRounding.AwayFromZero);
        var configuredGap = calculationMode is DoorPanelCalculationMode.DoorRoundedGapDistributed or
            DoorPanelCalculationMode.GapFixedDoorDistributed
            ? targetGap
            : roundedGap;
        var availableWidth = totalWidth - configuredGap * (doorCount - 1);
        if (availableWidth <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(targetGap), "门缝总宽度不能大于门板总宽度。");
        }

        if (calculationMode == DoorPanelCalculationMode.GapFixedDoorDistributed)
        {
            return new DoorPanelDivision(
                Enumerable.Repeat(availableWidth / doorCount, doorCount).ToArray(),
                targetGap);
        }

        if (calculationMode == DoorPanelCalculationMode.DoorRoundedGapDistributed)
        {
            var roundedDoor = Math.Round(availableWidth / doorCount, MidpointRounding.AwayFromZero);
            if (roundedDoor <= Tolerance || roundedDoor * doorCount >= totalWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(targetGap), "门板取整后没有可用的门缝宽度。");
            }

            var widths = Enumerable.Repeat(roundedDoor, doorCount).ToArray();
            return new DoorPanelDivision(widths, (totalWidth - widths.Sum()) / (doorCount - 1));
        }

        if (calculationMode == DoorPanelCalculationMode.GapRoundedDoorDistributed)
        {
            return new DoorPanelDivision(
                Enumerable.Repeat(availableWidth / doorCount, doorCount).ToArray(),
                roundedGap);
        }

        var integerDoor = Math.Floor(availableWidth / doorCount);
        if (integerDoor <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(totalWidth), "门板取整后的宽度必须大于 0。");
        }

        var integerWidths = Enumerable.Repeat(integerDoor, doorCount).ToArray();
        integerWidths[integerWidths.Length - 1] += availableWidth - integerDoor * doorCount;
        return new DoorPanelDivision(integerWidths, roundedGap);
    }

    public static DoorPanelDivision CalculateWithGapCompensation(
        double totalWidth,
        int doorCount,
        double targetGap,
        DoorPanelCalculationMode calculationMode)
    {
        ValidateInputs(totalWidth, doorCount, targetGap, calculationMode);
        if (doorCount == 1)
        {
            return new DoorPanelDivision([totalWidth], 0);
        }

        var configuredGap = calculationMode is DoorPanelCalculationMode.GapRoundedDoorDistributed or
            DoorPanelCalculationMode.DoorAndGapRoundedRemainderRight
            ? Math.Round(targetGap, MidpointRounding.AwayFromZero)
            : targetGap;
        var actualGap = configuredGap + (doorCount - 1d) / (doorCount + 1d);
        var availableWidth = totalWidth - actualGap * (doorCount - 1);
        if (availableWidth <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(targetGap), "补偿后的门缝总宽度不能大于门板总宽度。");
        }

        if (calculationMode is DoorPanelCalculationMode.GapFixedDoorDistributed or
            DoorPanelCalculationMode.GapRoundedDoorDistributed)
        {
            return new DoorPanelDivision(
                Enumerable.Repeat(availableWidth / doorCount, doorCount).ToArray(),
                actualGap);
        }

        var integerDoor = calculationMode == DoorPanelCalculationMode.DoorRoundedGapDistributed
            ? Math.Round(availableWidth / doorCount, MidpointRounding.AwayFromZero)
            : Math.Floor(availableWidth / doorCount);
        var widths = Enumerable.Repeat(integerDoor, doorCount).ToArray();
        widths[widths.Length - 1] += availableWidth - integerDoor * doorCount;
        if (widths.Any(width => width <= Tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(totalWidth), "门板取整后的宽度必须大于 0。");
        }

        return new DoorPanelDivision(widths, actualGap);
    }

    private static void ValidateInputs(
        double totalWidth,
        int doorCount,
        double targetGap,
        DoorPanelCalculationMode calculationMode)
    {
        if (!Compatibility.IsFinite(totalWidth) || totalWidth <= Tolerance ||
            doorCount <= 0 || !Compatibility.IsFinite(targetGap) || targetGap < 0 ||
            !Enum.IsDefined(typeof(DoorPanelCalculationMode), calculationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(totalWidth), "门板分格参数无效。");
        }
    }
}
