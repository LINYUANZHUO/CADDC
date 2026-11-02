namespace YiZhiMao.Dc.Core;

public enum PlanCabinetMiddlePanelMode
{
    Distributed,
    AlignToDoorGaps
}

public sealed record PlanCabinetOptions
{
    public double Length { get; init; } = 1800;
    public double Depth { get; init; } = 550;
    public double Thickness { get; init; } = 18;

    public bool GenerateDoors { get; init; } = true;
    public double DoorThickness { get; init; } = 18;
    public double DoorGap { get; init; }
    public double DoorLeftExtension { get; init; }
    public double DoorRightExtension { get; init; }
    public DoorPanelCalculationMode DoorCalculationMode { get; init; } =
        DoorPanelCalculationMode.GapRoundedDoorDistributed;
    public double DoorFrontClearance { get; init; } = 2;
    public int DoorCount { get; init; } = 2;

    public bool GenerateMiddlePanels { get; init; } = true;
    public bool MiddlePanelSameDepthAsSide { get; init; }
    public PlanCabinetMiddlePanelMode MiddlePanelMode { get; init; } =
        PlanCabinetMiddlePanelMode.AlignToDoorGaps;

    public double TopFrontInset { get; init; }
    public double TopRearInset { get; init; }

    public double LeftSideFrontExtension { get; init; }
    public double LeftSideRearExtension { get; init; }
    public double RightSideFrontExtension { get; init; }
    public double RightSideRearExtension { get; init; }

    public bool BackboardRearCover { get; init; }
    public double BackboardThickness { get; init; } = 8;
    public double BackboardInset { get; init; } = 12;
    public double BackboardLeftGroove { get; init; } = 5;
    public double BackboardRightGroove { get; init; } = 5;

    public static PlanCabinetOptions Default { get; } = new();

    public bool TryValidate(out string error)
    {
        if (!Positive(Length, Depth, Thickness, DoorThickness, BackboardThickness) ||
            !NonNegative(
                DoorGap,
                DoorLeftExtension,
                DoorRightExtension,
                DoorFrontClearance,
                TopFrontInset,
                TopRearInset,
                LeftSideFrontExtension,
                LeftSideRearExtension,
                RightSideFrontExtension,
                RightSideRearExtension,
                BackboardInset,
                BackboardLeftGroove,
                BackboardRightGroove) ||
            DoorCount <= 0 ||
            !Enum.IsDefined(typeof(DoorPanelCalculationMode), DoorCalculationMode) ||
            !Enum.IsDefined(typeof(PlanCabinetMiddlePanelMode), MiddlePanelMode))
        {
            error = "柜体俯视图尺寸必须为有效数值，门数必须大于 0。";
            return false;
        }

        if (Length <= Thickness * 2)
        {
            error = "柜长必须大于左右侧板厚度之和。";
            return false;
        }

        if (TopFrontInset + TopRearInset >= Depth)
        {
            error = "顶板前后缩进之和必须小于柜深。";
            return false;
        }

        if (!BackboardRearCover && BackboardInset + BackboardThickness > Depth)
        {
            error = "槽装背板厚度与后缩之和不能超过柜深。";
            return false;
        }

        if (BackboardLeftGroove > Thickness || BackboardRightGroove > Thickness)
        {
            error = "背板左右插槽不能超过侧板厚度。";
            return false;
        }

        try
        {
            _ = DoorPanelDivisionCalculator.CalculateWithExteriorGaps(
                Length + DoorLeftExtension + DoorRightExtension,
                DoorCount,
                DoorGap,
                DoorCalculationMode);
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "当前门数、门缝或门板延伸无法生成有效门板。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool Positive(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value > 0);

    private static bool NonNegative(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value >= 0);
}
