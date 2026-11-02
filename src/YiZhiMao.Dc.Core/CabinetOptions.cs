namespace YiZhiMao.Dc.Core;

public sealed record CabinetOptions
{
    public bool GenerateElevation { get; init; } = true;
    public bool GeneratePlan { get; init; } = true;
    public bool GenerateSide { get; init; } = true;
    public double PlanViewSpacing { get; init; } = 400;
    public double SideViewSpacing { get; init; } = 400;
    public double Width { get; init; } = 900;
    public double Height { get; init; } = 2100;
    public double Depth { get; init; } = 580;
    public double Thickness { get; init; } = 18;

    public double LeftSideTopExtension { get; init; }
    public double LeftSideBottomExtension { get; init; }
    public double LeftSideFrontExtension { get; init; }
    public double LeftSideRearExtension { get; init; }
    public double RightSideTopExtension { get; init; }
    public double RightSideBottomExtension { get; init; }
    public double RightSideFrontExtension { get; init; }
    public double RightSideRearExtension { get; init; }

    public double TopFrontInset { get; init; }
    public double TopRearInset { get; init; }
    public double BottomFrontInset { get; init; }
    public double BottomRearInset { get; init; }

    public bool BackboardRearCoverMode { get; init; }
    public double BackboardThickness { get; init; } = 18;
    public double BackboardOverallInset { get; init; }
    public double BackboardTopExtension { get; init; }
    public double BackboardBottomExtension { get; init; }
    public double BackboardLeftExtension { get; init; }
    public double BackboardRightExtension { get; init; }

    public double TopRailHeight { get; init; } = 50;
    public double TopRailAuxiliaryDepth { get; init; } = 70;
    public double TopRailLeftExtension { get; init; }
    public double TopRailRightExtension { get; init; }
    public double TopRailRearInset { get; init; }

    public double FrontToeKickHeight { get; init; } = 80;
    public double FrontToeKickFrontInset { get; init; }
    public double FrontToeKickLeftExtension { get; init; }
    public double FrontToeKickRightExtension { get; init; }
    public double RearToeKickHeight { get; init; } = 80;
    public double RearToeKickRearInset { get; init; } = 20;
    public double RearToeKickLeftExtension { get; init; }
    public double RearToeKickRightExtension { get; init; }

    public static CabinetOptions Default { get; } = new();

    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (!GenerateElevation && !GeneratePlan && !GenerateSide)
        {
            error = "正视图、俯视图和侧视图至少生成一种。";
            return false;
        }

        if (!Positive(Width, Height, Depth, Thickness, BackboardThickness, TopRailHeight,
                TopRailAuxiliaryDepth,
                FrontToeKickHeight, RearToeKickHeight) ||
            !NonNegative(
                PlanViewSpacing, SideViewSpacing,
                LeftSideTopExtension, LeftSideBottomExtension, LeftSideFrontExtension, LeftSideRearExtension,
                RightSideTopExtension, RightSideBottomExtension, RightSideFrontExtension, RightSideRearExtension,
                TopFrontInset, TopRearInset, BottomFrontInset, BottomRearInset,
                BackboardOverallInset, BackboardTopExtension, BackboardBottomExtension,
                BackboardLeftExtension, BackboardRightExtension,
                TopRailLeftExtension, TopRailRightExtension, TopRailRearInset,
                FrontToeKickFrontInset, FrontToeKickLeftExtension, FrontToeKickRightExtension,
                RearToeKickRearInset, RearToeKickLeftExtension, RearToeKickRightExtension))
        {
            error = "柜体基础尺寸必须大于 0，延伸、缩进和视图间距不能小于 0。";
            return false;
        }

        if (Width <= Thickness * 2)
        {
            error = "柜宽必须大于两块侧板厚度。";
            return false;
        }

        var bottomHeight = Math.Max(FrontToeKickHeight, RearToeKickHeight);
        if (Height <= bottomHeight + Thickness * 2 + TopRailHeight)
        {
            error = "柜高不足以容纳踢脚线、底板和顶线。";
            return false;
        }

        if (TopFrontInset + TopRearInset >= Depth ||
            BottomFrontInset + BottomRearInset >= Depth ||
            BackboardOverallInset >= Depth ||
            TopRailRearInset + Thickness + TopRailAuxiliaryDepth > Depth ||
            FrontToeKickFrontInset + Thickness > Depth ||
            RearToeKickRearInset + Thickness > Depth)
        {
            error = "顶底板、背板、顶线或踢脚线的前后缩进已超出柜深。";
            return false;
        }

        return true;
    }

    private static bool Positive(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value > 0);

    private static bool NonNegative(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value >= 0);
}
