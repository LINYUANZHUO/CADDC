using System.Text.Json;
using System.Text.Json.Serialization;

namespace YiZhiMao.Dc.Core;

public sealed record BoardToolOptions(
    double Thickness,
    double Depth,
    double CenterDepth,
    double TagScale,
    string TagType,
    double LineThickness,
    bool DoubleAsRectangle,
    bool TripleAsRectangle,
    double ExtendStart,
    double ExtendEnd,
    double WineRackDepth,
    double CrossThickness,
    double DiagonalThickness,
    double WineCellWidth,
    int WineRows,
    int WineColumns,
    double AbaOuterThickness,
    double AbaCoreThickness,
    double AbaDepth,
    int DivideCount,
    double DivideThickness,
    double BackboardThickness,
    double BackboardInset,
    double BackboardLeftExtension,
    double BackboardRightExtension,
    double BackboardTopExtension,
    double BackboardBottomExtension)
{
    public double? AbaFirstThickness { get; init; }

    public double? AbaSecondThickness { get; init; }

    public double? AbaThirdThickness { get; init; }

    public double? AbaFirstDepth { get; init; }

    public double? AbaSecondDepth { get; init; }

    public double? AbaThirdDepth { get; init; }

    public double AbaPlacementInset { get; init; }

    public double DivideDepth { get; init; } = 100;

    public double DivideMaximumClearSpan { get; init; } = 430;

    public bool DivideSmartMode { get; init; } = true;

    public bool DivideRemainderAtEnd { get; init; } = true;

    public bool BackboardGeneratePlan { get; init; } = true;

    public bool BackboardGenerateElevation { get; init; }

    public bool BackboardHideElevationLayer { get; init; }

    public double ConvertLineThickness { get; init; } = 18;

    public bool ConvertDoubleAsRectangle { get; init; }

    public bool ConvertTripleAsRectangle { get; init; }

    public double DoorPartitionTopExtension { get; init; } = 2700;

    public double DoorPartitionBottomExtension { get; init; } = 300;

    public double DoorPartitionMaximumWidth { get; init; } = 450;

    public int DoorPartitionDefaultCount { get; init; } = 8;

    public bool DoorPartitionSmartMode { get; init; } = true;

    public double ShoeShelfDepth { get; init; } = 260;

    public bool ShoeShelfSmartAngle { get; init; } = true;

    public double ShoeShelfAngleDegrees { get; init; } = 20;

    public double ShoeShelfTargetSpacing { get; init; } = 200;

    public double ShoeShelfTopReserve { get; init; }

    public double ShoeShelfBottomReserve { get; init; }

    public double ShoeShelfFrontClearance { get; init; } = 20;

    public double ShoeShelfRearClearance { get; init; }

    public double ShoeShelfStopperHeight { get; init; } = 20;

    public bool ShoeShelfGenerateStopper { get; init; } = true;

    [JsonIgnore]
    public double EffectiveAbaFirstThickness => AbaFirstThickness ?? AbaOuterThickness;

    [JsonIgnore]
    public double EffectiveAbaSecondThickness => AbaSecondThickness ?? AbaCoreThickness;

    [JsonIgnore]
    public double EffectiveAbaThirdThickness => AbaThirdThickness ?? AbaOuterThickness;

    [JsonIgnore]
    public double EffectiveAbaFirstDepth => AbaFirstDepth ?? AbaDepth;

    [JsonIgnore]
    public double EffectiveAbaSecondDepth => AbaSecondDepth ?? AbaDepth;

    [JsonIgnore]
    public double EffectiveAbaThirdDepth => AbaThirdDepth ?? AbaDepth;

    public string PropertyType { get; init; } = "普通板件";

    public double TrimFaceWidth { get; init; } = 50;

    public double TrimAuxiliaryDepth { get; init; } = 70;

    public double DisplaceDistance { get; init; } = 100;

    public string SideTrimSectionType { get; init; } = "L";

    public string TopTrimSectionType { get; init; } = "L";

    public string PlacementView { get; init; } = "立面";

    public static BoardToolOptions Default { get; } = new(
        18, 500, 500, 1, "活动层板带文字",
        18, false, false, 0, 0,
        500, 18, 12, 100, 3, 3,
        18, 9, 500, 3, 18,
        9, 12, 5, 5, 5, 5)
    {
        AbaFirstThickness = 18,
        AbaSecondThickness = 9,
        AbaThirdThickness = 18,
        AbaFirstDepth = 500,
        AbaSecondDepth = 500,
        AbaThirdDepth = 500,
        AbaPlacementInset = 0,
        DivideDepth = 100,
        DivideMaximumClearSpan = 430,
        DivideSmartMode = true,
        DivideRemainderAtEnd = true,
        BackboardGeneratePlan = true,
        BackboardGenerateElevation = false,
        BackboardHideElevationLayer = false,
        ConvertLineThickness = 18,
        ConvertDoubleAsRectangle = false,
        ConvertTripleAsRectangle = false,
        DoorPartitionTopExtension = 2700,
        DoorPartitionBottomExtension = 300,
        DoorPartitionMaximumWidth = 450,
        DoorPartitionDefaultCount = 8,
        DoorPartitionSmartMode = true,
        ShoeShelfDepth = 260,
        ShoeShelfSmartAngle = true,
        ShoeShelfAngleDegrees = 20,
        ShoeShelfTargetSpacing = 200,
        ShoeShelfTopReserve = 0,
        ShoeShelfBottomReserve = 0,
        ShoeShelfFrontClearance = 20,
        ShoeShelfRearClearance = 0,
        ShoeShelfStopperHeight = 20,
        ShoeShelfGenerateStopper = true
    };
}

public static class DrawingLayerCategories
{
    public const string ActivityShelf = "活动层板";
    public const string VerticalBoard = "立板";
    public const string SideTrim = "侧收口";
    public const string TopTrim = "顶收口";
    public const string VisiblePanel = "见光板";
    public const string CabinetStructure = "柜体结构";
    public const string DoorPanel = "门板图层";
    public const string CabinetAnnotation = "标注-柜体";
    public const string DoorAnnotation = "标注-门板";

    public static Dictionary<string, string> CreateDefaultMappings() => new(StringComparer.Ordinal)
    {
        [ActivityShelf] = "0-柜体结构",
        [VerticalBoard] = "0-柜体结构",
        [SideTrim] = "0-柜体结构",
        [TopTrim] = "0-柜体结构",
        [VisiblePanel] = "0-柜体虚线",
        [CabinetStructure] = "0-柜体结构",
        [DoorPanel] = "0-门板图层",
        [CabinetAnnotation] = "0-标注-柜体",
        [DoorAnnotation] = "0-标注-门板"
    };
}

public sealed record DrawingLayerToolOptions(double LinetypeScale, double WallHatchScale)
{
    public Dictionary<string, string> CategoryLayers { get; init; } = DrawingLayerCategories.CreateDefaultMappings();

    public string DoorSelectionLayer { get; init; } = "0-门板图层";

    public string CabinetSelectionLayer { get; init; } = "0-柜体结构";

    public string CustomSelectionLayerOne { get; init; } = "0-五金";

    public string CustomSelectionLayerTwo { get; init; } = "0-文字";

    public static DrawingLayerToolOptions CreateDefault() => new(40, 1);

    public static DrawingLayerToolOptions Default { get; } = CreateDefault();
}

public sealed record SmartAnnotationOptions(
    bool UseTemplateStyle,
    string TemplateName,
    double InnerOffset,
    double OuterOffset,
    bool FullGraphicMode,
    bool FilterByThickness,
    double FilterThickness,
    bool FilterMinimumDimension,
    double MinimumDimension,
    bool MergeOverlappingGeometry,
    bool FilterPoints,
    bool FilterLines,
    bool FilterDashedLines,
    bool FilterPolylines,
    bool FilterArcs,
    bool FilterText,
    bool FilterBlocks,
    bool FilterHatches,
    bool DeleteByThickness,
    double DeleteThickness)
{
    public const string DefaultTemplateName = "DimTemplate.dwg";

    public static SmartAnnotationOptions Default { get; } = new(
        true,
        DefaultTemplateName,
        160,
        240,
        false,
        true,
        18,
        true,
        1,
        false,
        false,
        false,
        false,
        false,
        false,
        true,
        false,
        false,
        false,
        50);
}

public sealed record WholeHouseStatisticsOptions(
    bool IncludeCabinetBoards,
    bool IncludeStandardDoors,
    bool IncludeFrameDoors,
    bool IncludeBoardProperties,
    bool IncludeHangingRods,
    bool IncludeLightStrips,
    bool IncludeHandles,
    bool IncludeConcealedHandles,
    bool IncludeHinges,
    bool IncludeSlides,
    double TableScaleFactor,
    int MaxRowsPerPage)
{
    public static WholeHouseStatisticsOptions Default { get; } = new(
        true, true, true, true, true,
        true, true, true, true, true,
        8, 30);

    public bool HasAnyModule =>
        IncludeCabinetBoards || IncludeStandardDoors || IncludeFrameDoors || IncludeBoardProperties ||
        IncludeHangingRods || IncludeLightStrips || IncludeHandles || IncludeConcealedHandles ||
        IncludeHinges || IncludeSlides;
}

public sealed record DoorStatisticsOptions(
    double TableScaleFactor,
    double Thickness,
    int MaxRowsPerPage)
{
    public static DoorStatisticsOptions Default { get; } = new(8, 18, 30);
}

public sealed record FrameDoorStatisticsOptions
{
    public double CabinetThickness { get; init; } = 18;
    public int BoardCoreCount { get; init; } = 2;
    public int GlassCoreCount { get; init; }
    public double CoreThickness { get; init; } = 12;
    public double SideFrameWidth { get; init; } = 60;
    public double TopFrameWidth { get; init; } = 60;
    public double MiddleFrameWidth { get; init; } = 60;
    public double BottomFrameWidth { get; init; } = 60;
    public double RightFrameTenon { get; init; } = 10;
    public double MiddleFrameTenon { get; init; } = 10;
    public double CoreTopTenon { get; init; } = 8;
    public double CoreBottomTenon { get; init; } = 8;
    public double CoreLeftTenon { get; init; } = 8;
    public double CoreRightTenon { get; init; } = 8;
    public bool CenterMiddleFrame { get; init; } = true;
    public double LowerFramePosition { get; init; } = 600;
    public double TableScaleFactor { get; init; } = 8;
    public int MaxRowsPerPage { get; init; } = 30;

    public static FrameDoorStatisticsOptions Default { get; } = new();
}

public sealed record CabinetBomOptions(
    bool IncludeBasicBoards,
    bool IncludeBackboards,
    bool IncludeCabinetSummary,
    double TableScaleFactor,
    int MaxRowsPerPage)
{
    public static CabinetBomOptions Default { get; } = new(true, true, true, 8, 30);

    public bool HasAnyModule => IncludeBasicBoards || IncludeBackboards || IncludeCabinetSummary;
}

public sealed record PanelNestingOptions(
    bool UseWoodGrain,
    bool UseSolidColor,
    bool Use48Sheet,
    bool Use49Sheet,
    bool IncludeCabinetBoards,
    bool IncludeFlatDoors,
    double TableScaleFactor,
    int MaxRowsPerPage)
{
    public static PanelNestingOptions Default { get; } = new(
        true, false, true, false, true, true, 8, 30);

    public bool HasAnyTarget => IncludeCabinetBoards || IncludeFlatDoors;
}

public enum ToolboxFeatureModule
{
    Cabinet,
    Boards,
    DoorPanels,
    Hardware,
    WallPanels,
    SmartAnnotation,
    Images,
    OneClickFrame,
    BatchPrint,
    SuperLibrary,
    Statistics,
    DrawingLayers,
    RadialMenu,
    LispMarket,
    Shortcuts
}

public sealed record ToolboxFeatureModuleInfo(
    ToolboxFeatureModule Module,
    string Name,
    string Description,
    string? CommandCategory);

public static class ToolboxFeatureModuleCatalog
{
    private static readonly IReadOnlyList<ToolboxFeatureModuleInfo> Entries = new[]
    {
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Cabinet, "柜体", "标准单元柜与万能柜体生成、刷新", "柜体"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Boards, "板件", "板件绘制、单双线与组件工具", "板件"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.DoorPanels, "门板抽面", "立面/俯视门板、抽面、拉手与铰链", "门板抽面"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Hardware, "五金件", "挂衣杆、灯带与五金设置", "五金件"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.WallPanels, "墙板", "格栅板、洞洞板与背景墙", "墙板"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.SmartAnnotation, "智能标注", "柜体、门板与对象智能标注和修改", "智能标注"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Images, "插入图片", "批量插入并排列图片", "插入图片"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.OneClickFrame, "一键图框", "图框套用、对齐、局部大样与截图", "一键图框"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.BatchPrint, "批量打印", "图框/布局批量打印与 PDF 工具", "批量打印"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.SuperLibrary, "超级图库", "分类浏览、预览、保存并插入 DWG 图库", "超级图库"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Statistics, "拆单统计", "部件、门板、柜体与板件统计", "拆单统计"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.DrawingLayers, "图层图纸", "图层管理、隔离与图纸显示修复", "图层图纸"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.RadialMenu, "圆盘", "鼠标中键圆盘与命令配置", "圆盘"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.LispMarket, "LISP市场", "本地 LISP 插件扫描与加载", null),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Shortcuts, "快捷键", "快捷键映射与 CAD 命令大全", null)
    };

    public static IReadOnlyList<ToolboxFeatureModuleInfo> All => Entries;

    public static ToolboxFeatureModuleInfo Get(ToolboxFeatureModule module) =>
        Entries.Single(entry => entry.Module == module);

    public static bool TryGetByCommandCategory(string? category, out ToolboxFeatureModule module)
    {
        var entry = Entries.FirstOrDefault(item =>
            item.CommandCategory is not null &&
            string.Equals(item.CommandCategory, category, StringComparison.Ordinal));
        if (entry is null)
        {
            module = default;
            return false;
        }

        module = entry.Module;
        return true;
    }
}

public sealed record ToolboxPreferenceOptions(
    bool AutoExpandSidebar,
    bool CabinetEnabled,
    bool BoardsEnabled,
    bool DoorPanelsEnabled,
    bool HardwareEnabled,
    bool WallPanelsEnabled,
    bool ImagesEnabled,
    bool OneClickFrameEnabled,
    bool BatchPrintEnabled,
    bool StatisticsEnabled,
    bool DrawingLayersEnabled,
    bool RadialMenuEnabled,
    bool LispMarketEnabled,
    bool ShortcutsEnabled)
{
    public bool SmartAnnotationEnabled { get; init; } = true;
    public bool SuperLibraryEnabled { get; init; } = true;

    public static ToolboxPreferenceOptions Default { get; } = new(
        true,
        true,
        true, true, true, true, true, true,
        true, true, true, true, true, true);

    public int EnabledModuleCount => ToolboxFeatureModuleCatalog.All.Count(entry => IsEnabled(entry.Module));

    public bool IsEnabled(ToolboxFeatureModule module) => module switch
    {
        ToolboxFeatureModule.Cabinet => CabinetEnabled,
        ToolboxFeatureModule.Boards => BoardsEnabled,
        ToolboxFeatureModule.DoorPanels => DoorPanelsEnabled,
        ToolboxFeatureModule.Hardware => HardwareEnabled,
        ToolboxFeatureModule.WallPanels => WallPanelsEnabled,
        ToolboxFeatureModule.SmartAnnotation => SmartAnnotationEnabled,
        ToolboxFeatureModule.Images => ImagesEnabled,
        ToolboxFeatureModule.OneClickFrame => OneClickFrameEnabled,
        ToolboxFeatureModule.BatchPrint => BatchPrintEnabled,
        ToolboxFeatureModule.SuperLibrary => SuperLibraryEnabled,
        ToolboxFeatureModule.Statistics => StatisticsEnabled,
        ToolboxFeatureModule.DrawingLayers => DrawingLayersEnabled,
        ToolboxFeatureModule.RadialMenu => RadialMenuEnabled,
        ToolboxFeatureModule.LispMarket => LispMarketEnabled,
        ToolboxFeatureModule.Shortcuts => ShortcutsEnabled,
        _ => false
    };

    public ToolboxPreferenceOptions WithModuleEnabled(ToolboxFeatureModule module, bool enabled) => module switch
    {
        ToolboxFeatureModule.Cabinet => this with { CabinetEnabled = enabled },
        ToolboxFeatureModule.Boards => this with { BoardsEnabled = enabled },
        ToolboxFeatureModule.DoorPanels => this with { DoorPanelsEnabled = enabled },
        ToolboxFeatureModule.Hardware => this with { HardwareEnabled = enabled },
        ToolboxFeatureModule.WallPanels => this with { WallPanelsEnabled = enabled },
        ToolboxFeatureModule.SmartAnnotation => this with { SmartAnnotationEnabled = enabled },
        ToolboxFeatureModule.Images => this with { ImagesEnabled = enabled },
        ToolboxFeatureModule.OneClickFrame => this with { OneClickFrameEnabled = enabled },
        ToolboxFeatureModule.BatchPrint => this with { BatchPrintEnabled = enabled },
        ToolboxFeatureModule.SuperLibrary => this with { SuperLibraryEnabled = enabled },
        ToolboxFeatureModule.Statistics => this with { StatisticsEnabled = enabled },
        ToolboxFeatureModule.DrawingLayers => this with { DrawingLayersEnabled = enabled },
        ToolboxFeatureModule.RadialMenu => this with { RadialMenuEnabled = enabled },
        ToolboxFeatureModule.LispMarket => this with { LispMarketEnabled = enabled },
        ToolboxFeatureModule.Shortcuts => this with { ShortcutsEnabled = enabled },
        _ => this
    };
}

public sealed record SmartInputMethodOptions(
    bool Enabled,
    bool KeepCadCommandsEnglish,
    bool AutoChineseForText,
    bool RestoreEnglishAfterText)
{
    public static SmartInputMethodOptions Default { get; } = new(
        true,
        true,
        true,
        true);
}

public enum DrawingSaveFileFormat
{
    AutoCad2000 = 12,
    AutoCad2004 = 24,
    AutoCad2007 = 36,
    AutoCad2010 = 48,
    AutoCad2013 = 60,
    AutoCad2018 = 64
}

public sealed class ToolboxSettings
{
    public int Version { get; set; } = 5;

    public DoorPanelOptions DoorPanel { get; set; } = DoorPanelOptions.Default;

    public PlanDoorPanelOptions PlanDoorPanel { get; set; } = PlanDoorPanelOptions.Default;

    public DrawerFaceOptions DrawerFace { get; set; } = DrawerFaceOptions.Default;

    public CabinetOptions Cabinet { get; set; } = CabinetOptions.Default;

    public PlanCabinetOptions PlanCabinet { get; set; } = PlanCabinetOptions.Default;

    public HardwareOptions Hardware { get; set; } = HardwareOptions.Default;

    public GrilleOptions Grille { get; set; } = GrilleOptions.Default;

    public PerforatedPanelOptions PerforatedPanel { get; set; } = PerforatedPanelOptions.Default;

    public BackgroundWallOptions BackgroundWall { get; set; } = BackgroundWallOptions.Default;

    public BoardToolOptions Board { get; set; } = BoardToolOptions.Default;

    public DrawingLayerToolOptions DrawingLayer { get; set; } = DrawingLayerToolOptions.CreateDefault();

    public SmartAnnotationOptions SmartAnnotation { get; set; } = SmartAnnotationOptions.Default;

    public WholeHouseStatisticsOptions WholeHouseStatistics { get; set; } = WholeHouseStatisticsOptions.Default;

    public DoorStatisticsOptions DoorStatistics { get; set; } = DoorStatisticsOptions.Default;

    public FrameDoorStatisticsOptions FrameDoorStatistics { get; set; } = FrameDoorStatisticsOptions.Default;

    public CabinetBomOptions CabinetBom { get; set; } = CabinetBomOptions.Default;

    public PanelNestingOptions PanelNesting { get; set; } = PanelNestingOptions.Default;

    public ImageBatchOptions ImageBatch { get; set; } = ImageBatchOptions.Default;

    public ToolboxPreferenceOptions Preferences { get; set; } = ToolboxPreferenceOptions.Default;

    public SmartInputMethodOptions SmartInputMethod { get; set; } = SmartInputMethodOptions.Default;

    public DrawingSaveFileFormat DefaultSaveFileFormat { get; set; } = DrawingSaveFileFormat.AutoCad2007;

    public bool SyncReferencedShxOnSave { get; set; }

    public bool AutoReplaceMissingShx { get; set; } = true;

    public Dictionary<string, string> MissingShxReplacementMappings { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public static ToolboxSettings CreateDefault() => new();

    public ToolboxSettings Clone() => new()
    {
        Version = Version,
        DoorPanel = DoorPanel,
        PlanDoorPanel = PlanDoorPanel,
        DrawerFace = DrawerFace,
        Cabinet = Cabinet,
        PlanCabinet = PlanCabinet,
        Hardware = Hardware,
        Grille = Grille,
        PerforatedPanel = PerforatedPanel,
        BackgroundWall = BackgroundWall with { Rows = BackgroundWall.Rows.ToArray() },
        Board = Board,
        DrawingLayer = DrawingLayer with
        {
            CategoryLayers = new Dictionary<string, string>(DrawingLayer.CategoryLayers, StringComparer.Ordinal)
        },
        SmartAnnotation = SmartAnnotation,
        WholeHouseStatistics = WholeHouseStatistics,
        DoorStatistics = DoorStatistics,
        FrameDoorStatistics = FrameDoorStatistics,
        CabinetBom = CabinetBom,
        PanelNesting = PanelNesting,
        ImageBatch = ImageBatch,
        Preferences = Preferences,
        SmartInputMethod = SmartInputMethod,
        DefaultSaveFileFormat = DefaultSaveFileFormat,
        SyncReferencedShxOnSave = SyncReferencedShxOnSave,
        AutoReplaceMissingShx = AutoReplaceMissingShx,
        MissingShxReplacementMappings = new Dictionary<string, string>(
            MissingShxReplacementMappings,
            StringComparer.OrdinalIgnoreCase)
    };

    public void Validate()
    {
        if (Version != 5 ||
            !IsValid(DoorPanel) ||
            !IsValid(PlanDoorPanel) ||
            !IsValid(DrawerFace) ||
            !IsValid(Cabinet) ||
            !IsValid(PlanCabinet) ||
            !IsValid(Hardware) ||
            !IsValid(Grille) ||
            !IsValid(PerforatedPanel) ||
            !IsValid(BackgroundWall) ||
            !IsValid(Board) ||
            !IsValid(DrawingLayer) ||
            !IsValid(SmartAnnotation) ||
            !IsValid(WholeHouseStatistics) ||
            !IsValid(DoorStatistics) ||
            !IsValid(FrameDoorStatistics) ||
            !IsValid(CabinetBom) ||
            !IsValid(PanelNesting) ||
            !IsValid(ImageBatch) ||
            !IsValid(Preferences) ||
            SmartInputMethod is null ||
            MissingShxReplacementMappings is null ||
            MissingShxReplacementMappings.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)) ||
            !Enum.IsDefined(typeof(DrawingSaveFileFormat), DefaultSaveFileFormat))
        {
            throw new InvalidOperationException("工具箱参数文件包含无效数据。");
        }
    }

    private static bool IsValid(DoorPanelOptions? value) =>
        value is not null &&
        Enum.IsDefined(typeof(DoorPanelCalculationMode), value.CalculationMode) &&
        value.DoorCount > 0 &&
        IsNonNegative(value.Gap, value.TopExtension, value.BottomExtension, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(PlanDoorPanelOptions? value) =>
        value is not null &&
        Enum.IsDefined(typeof(DoorPanelCalculationMode), value.CalculationMode) &&
        value.DoorCount > 0 &&
        IsPositive(value.Thickness) &&
        IsNonNegative(value.FrontClearance, value.Gap, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(DrawerFaceOptions? value) =>
        value is not null &&
        Enum.IsDefined(typeof(DrawerFaceOrientation), value.Orientation) &&
        Enum.IsDefined(typeof(DoorPanelCalculationMode), value.CalculationMode) &&
        Enum.IsDefined(typeof(HandlelessProfileMode), value.HandlelessProfileMode) &&
        value.Count > 0 &&
        value.RowCount > 0 &&
        value.ColumnCount > 0 &&
        IsPositive(value.HandlelessProfileSize) &&
        value.HandlelessProfileSize > value.HandlelessGap &&
        IsNonNegative(
            value.Gap,
            value.HandlelessGap,
            value.TopExtension,
            value.BottomExtension,
            value.LeftExtension,
            value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(CabinetOptions? value) =>
        value is not null && value.TryValidate(out _);

    private static bool IsValid(PlanCabinetOptions? value) =>
        value is not null && value.TryValidate(out _);

    private static bool IsValid(HardwareOptions? value) =>
        value is not null &&
        Enum.IsDefined(typeof(HingeDimensionMode), value.HingeDimensionMode) &&
        IsNonNegative(
            value.HangingRodTopOffset,
            value.LightStripTopOffset,
            value.LightStripSideOpening,
            value.HingeTopOffset,
            value.HingeBottomOffset,
            value.HingeDimensionOffset,
            value.HingeDimensionLineSpacing,
            value.HingeTypeTextSpacing) &&
        value.HingeCountBelow900 > 0 &&
        value.HingeCount900To1500 > 0 &&
        value.HingeCount1500To2000 > 0 &&
        value.HingeCount2000To2500 > 0 &&
        value.HingeCountAtLeast2500 > 0 &&
        IsPositive(
            value.FixedHingeSpacing,
            value.HingeDimensionTextHeight,
            value.HingeTypeTextHeight);

    private static bool IsValid(GrilleOptions? value) =>
        value is not null &&
        IsPositive(value.Width, value.Height, value.BackThickness, value.StripThickness, value.StripWidth) &&
        IsNonNegative(value.StripGap) &&
        value.StripWidth <= (value.IsVertical ? value.Width : value.Height) &&
        !string.IsNullOrWhiteSpace(value.HatchPattern);

    private static bool IsValid(PerforatedPanelOptions? value) =>
        value is not null &&
        IsPositive(value.Width, value.Height, value.Depth, value.HoleDiameter) &&
        IsNonNegative(value.InnerOffset) &&
        value.InnerOffset >= value.HoleDiameter / 2 &&
        value.HorizontalCount > 0 &&
        value.VerticalCount > 0 &&
        PerforatedPanelOptions.CanPlaceAxis(value.Width, value.InnerOffset, value.HoleDiameter, value.HorizontalCount) &&
        PerforatedPanelOptions.CanPlaceAxis(value.Height, value.InnerOffset, value.HoleDiameter, value.VerticalCount);

    private static bool IsValid(BackgroundWallOptions? value)
    {
        if (value is null ||
            !IsPositive(value.TotalWidth, value.Depth) ||
            !IsNonNegative(value.Gap) ||
            value.Rows is null ||
            value.Rows.Count is < 1 or > 5)
        {
            return false;
        }

        return value.Rows.All(row =>
            row.ColumnCount > 0 &&
            IsPositive(row.Height) &&
            (!value.UseRandomWidths ||
             value.TotalWidth - value.Gap * (row.ColumnCount - 1) >= row.ColumnCount * BackgroundWallOptions.MinimumPanelWidth));
    }

    private static bool IsValid(BoardToolOptions? value) =>
        value is not null &&
        IsPositive(
            value.Thickness,
            value.Depth,
            value.CenterDepth,
            value.DisplaceDistance,
            value.LineThickness,
            value.ConvertLineThickness,
            value.WineRackDepth,
            value.CrossThickness,
            value.DiagonalThickness,
            value.WineCellWidth,
            value.EffectiveAbaFirstThickness,
            value.EffectiveAbaSecondThickness,
            value.EffectiveAbaThirdThickness,
            value.EffectiveAbaFirstDepth,
            value.EffectiveAbaSecondDepth,
            value.EffectiveAbaThirdDepth,
            value.DivideDepth,
            value.DivideMaximumClearSpan,
            value.DivideThickness,
            value.DoorPartitionMaximumWidth,
            value.ShoeShelfDepth,
            value.ShoeShelfTargetSpacing,
            value.ShoeShelfStopperHeight,
            value.BackboardThickness) &&
        value.TagScale is >= 0.1 and <= 10 &&
        !string.IsNullOrWhiteSpace(value.TagType) &&
        value.PropertyType is "普通板件" or "侧收口" or "顶收口" or "见光板" &&
        IsPositive(value.TrimFaceWidth, value.TrimAuxiliaryDepth) &&
        value.SideTrimSectionType is "L" or "T" or "I" &&
        value.TopTrimSectionType is "L" or "T" or "I" &&
        (value.SideTrimSectionType != "T" || value.TrimFaceWidth >= value.Thickness) &&
        (value.TopTrimSectionType != "T" || value.TrimFaceWidth >= value.Thickness) &&
        value.PlacementView is "立面" or "平面" &&
        IsNonNegative(
            value.ExtendStart,
            value.ExtendEnd,
            value.AbaPlacementInset,
            value.DoorPartitionTopExtension,
            value.DoorPartitionBottomExtension,
            value.BackboardInset,
            value.BackboardLeftExtension,
            value.BackboardRightExtension,
            value.BackboardTopExtension,
            value.BackboardBottomExtension) &&
        value.WineRows > 0 &&
        value.WineColumns > 0 &&
        value.DivideCount >= 2 &&
        value.DoorPartitionTopExtension + value.DoorPartitionBottomExtension > 0 &&
        value.DoorPartitionDefaultCount >= 1 &&
        value.ShoeShelfAngleDegrees is > 0 and < 89 &&
        Compatibility.IsFinite(value.ShoeShelfAngleDegrees) &&
        IsNonNegative(
            value.ShoeShelfTopReserve,
            value.ShoeShelfBottomReserve,
            value.ShoeShelfFrontClearance,
            value.ShoeShelfRearClearance) &&
        value.BackboardGeneratePlan != value.BackboardGenerateElevation;

    private static bool IsValid(DrawingLayerToolOptions? value) =>
        value is not null &&
        value.LinetypeScale is >= 1 and <= 500 &&
        Compatibility.IsFinite(value.LinetypeScale) &&
        value.WallHatchScale >= 0.5 &&
        Compatibility.IsFinite(value.WallHatchScale) &&
        value.CategoryLayers is { Count: > 0 } &&
        value.CategoryLayers.All(pair =>
            !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value)) &&
        !string.IsNullOrWhiteSpace(value.DoorSelectionLayer) &&
        !string.IsNullOrWhiteSpace(value.CabinetSelectionLayer) &&
        !string.IsNullOrWhiteSpace(value.CustomSelectionLayerOne) &&
        !string.IsNullOrWhiteSpace(value.CustomSelectionLayerTwo);

    private static bool IsValid(SmartAnnotationOptions? value) =>
        value is not null &&
        !string.IsNullOrWhiteSpace(value.TemplateName) &&
        string.Equals(Path.GetExtension(value.TemplateName), ".dwg", StringComparison.OrdinalIgnoreCase) &&
        IsPositive(
            value.InnerOffset,
            value.OuterOffset,
            value.FilterThickness,
            value.MinimumDimension,
            value.DeleteThickness);

    private static bool IsValid(WholeHouseStatisticsOptions? value) =>
        value is not null &&
        value.HasAnyModule &&
        Compatibility.IsFinite(value.TableScaleFactor) &&
        value.TableScaleFactor is >= 1 and <= 100 &&
        value.MaxRowsPerPage is >= 1 and <= 500;

    private static bool IsValid(DoorStatisticsOptions? value) =>
        value is not null &&
        IsTableOptionsValid(value.TableScaleFactor, value.MaxRowsPerPage) &&
        IsPositive(value.Thickness);

    private static bool IsValid(FrameDoorStatisticsOptions? value) =>
        value is not null &&
        IsTableOptionsValid(value.TableScaleFactor, value.MaxRowsPerPage) &&
        value.BoardCoreCount >= 0 &&
        value.GlassCoreCount >= 0 &&
        value.BoardCoreCount + value.GlassCoreCount > 0 &&
        IsPositive(
            value.CabinetThickness,
            value.CoreThickness,
            value.SideFrameWidth,
            value.TopFrameWidth,
            value.MiddleFrameWidth,
            value.BottomFrameWidth,
            value.RightFrameTenon,
            value.MiddleFrameTenon,
            value.CoreTopTenon,
            value.CoreBottomTenon,
            value.CoreLeftTenon,
            value.CoreRightTenon,
            value.LowerFramePosition);

    private static bool IsValid(CabinetBomOptions? value) =>
        value is not null &&
        value.HasAnyModule &&
        IsTableOptionsValid(value.TableScaleFactor, value.MaxRowsPerPage);

    private static bool IsValid(PanelNestingOptions? value) =>
        value is not null &&
        value.UseWoodGrain != value.UseSolidColor &&
        value.Use48Sheet != value.Use49Sheet &&
        value.HasAnyTarget &&
        IsTableOptionsValid(value.TableScaleFactor, value.MaxRowsPerPage);

    private static bool IsTableOptionsValid(double tableScaleFactor, int maxRowsPerPage) =>
        Compatibility.IsFinite(tableScaleFactor) &&
        tableScaleFactor is >= 1 and <= 100 &&
        maxRowsPerPage is >= 1 and <= 500;

    private static bool IsValid(ImageBatchOptions? value) =>
        value is not null &&
        IsNonNegative(value.Gap) &&
        value.PixelHeight is >= 1 and <= ImageBatchOptions.MaximumPixelHeight;

    private static bool IsValid(ToolboxPreferenceOptions? value) =>
        value is not null && value.EnabledModuleCount > 0;

    private static bool IsColor(short value) => value is >= 1 and <= 255;

    private static bool IsPositive(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value > 0);

    private static bool IsNonNegative(params double[] values) =>
        values.All(value => Compatibility.IsFinite(value) && value >= 0);
}

public sealed class ToolboxSettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ToolboxSettings Load()
    {
        if (!File.Exists(path))
        {
            return ToolboxSettings.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<ToolboxSettings>(json, JsonOptions)
                ?? ToolboxSettings.CreateDefault();
            using (var document = JsonDocument.Parse(json))
            {
                if (settings.Version == 1)
                {
                    if (settings.Cabinet.Width == 2400 &&
                        settings.Cabinet.Height == 1000 &&
                        settings.Cabinet.Depth == 620)
                    {
                        settings.Cabinet = settings.Cabinet with
                        {
                            Width = CabinetOptions.Default.Width,
                            Height = CabinetOptions.Default.Height,
                            Depth = CabinetOptions.Default.Depth,
                            Thickness = CabinetOptions.Default.Thickness
                        };
                    }

                    settings.Version = 2;
                }

                if (settings.Version == 2)
                {
                    if (settings.Cabinet.Width == 900 &&
                        settings.Cabinet.Height == 2100 &&
                        settings.Cabinet.Depth == 600 &&
                        settings.Cabinet.Thickness == 18.5)
                    {
                        settings.Cabinet = settings.Cabinet with
                        {
                            Depth = CabinetOptions.Default.Depth,
                            Thickness = CabinetOptions.Default.Thickness
                        };
                    }

                    settings.Version = 3;
                }

                if (settings.Version == 3)
                {
                    if (settings.Cabinet.BackboardTopExtension == 18 &&
                        settings.Cabinet.BackboardBottomExtension == 18)
                    {
                        settings.Cabinet = settings.Cabinet with
                        {
                            BackboardTopExtension = CabinetOptions.Default.BackboardTopExtension,
                            BackboardBottomExtension = CabinetOptions.Default.BackboardBottomExtension
                        };
                    }

                    settings.Version = 4;
                }

                if (settings.Version == 4)
                {
                    settings.Version = 5;
                }

                if (document.RootElement.TryGetProperty(nameof(ToolboxSettings.Preferences), out var preferences) &&
                    !preferences.TryGetProperty(nameof(ToolboxPreferenceOptions.CabinetEnabled), out _))
                {
                    settings.Preferences = settings.Preferences with { CabinetEnabled = true };
                }

                if (document.RootElement.TryGetProperty(nameof(ToolboxSettings.DrawerFace), out var drawerFace) &&
                    !drawerFace.TryGetProperty(nameof(DrawerFaceOptions.RowCount), out _))
                {
                    var legacy = settings.DrawerFace;
                    settings.DrawerFace = legacy with
                    {
                        Count = legacy.Orientation == DrawerFaceOrientation.Vertical ? legacy.Count : 1,
                        RowCount = legacy.Orientation == DrawerFaceOrientation.Vertical ? legacy.Count : 1,
                        ColumnCount = legacy.Orientation == DrawerFaceOrientation.Horizontal ? legacy.Count : 1,
                        Gap = 2,
                        Handleless = true,
                        HandlelessGap = 26,
                        GenerateOpeningLines = true,
                        GenerateMask = true,
                        GenerateHandle = false,
                        CalculationMode = DoorPanelCalculationMode.GapFixedDoorDistributed
                    };
                }
            }

            if (settings.Board.BackboardGeneratePlan == settings.Board.BackboardGenerateElevation)
            {
                settings.Board = settings.Board with
                {
                    BackboardGeneratePlan = true,
                    BackboardGenerateElevation = false
                };
            }

            settings.Validate();
            return settings;
        }
        catch (JsonException)
        {
            return ToolboxSettings.CreateDefault();
        }
        catch (InvalidOperationException)
        {
            return ToolboxSettings.CreateDefault();
        }
    }

    public void Save(ToolboxSettings settings)
    {
        Compatibility.NotNull(settings, nameof(settings));
        settings.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        Compatibility.MoveFileReplacingExisting(temporaryPath, path);
    }
}
