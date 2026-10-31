using System.Text.Json;

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
    public static BoardToolOptions Default { get; } = new(
        18, 500, 500, 5, "活动层板带文字",
        18, false, false, 0, 0,
        500, 18, 12, 100, 3, 3,
        18, 9, 500, 3, 18,
        8, 12, 5, 5, 5, 5);
}

public sealed record DrawingLayerToolOptions(double LinetypeScale, double WallHatchScale)
{
    public static DrawingLayerToolOptions Default { get; } = new(40, 1);
}

public enum ToolboxFeatureModule
{
    Boards,
    DoorPanels,
    Hardware,
    WallPanels,
    Images,
    OneClickFrame,
    BatchPrint,
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
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Boards, "板件", "板件绘制、单双线与组件工具", "板件"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.DoorPanels, "门板抽面", "立面/俯视门板、抽面、拉手与铰链", "门板抽面"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Hardware, "五金件", "挂衣杆、灯带与五金设置", "五金件"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.WallPanels, "墙板", "格栅板、洞洞板与背景墙", "墙板"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.Images, "插入图片", "批量插入并排列图片", "插入图片"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.OneClickFrame, "一键图框", "图框套用、对齐、局部大样与截图", "一键图框"),
        new ToolboxFeatureModuleInfo(ToolboxFeatureModule.BatchPrint, "批量打印", "图框/布局批量打印与 PDF 工具", "批量打印"),
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
    public static ToolboxPreferenceOptions Default { get; } = new(
        true,
        true, true, true, true, true, true,
        true, true, true, true, true, true);

    public int EnabledModuleCount => ToolboxFeatureModuleCatalog.All.Count(entry => IsEnabled(entry.Module));

    public bool IsEnabled(ToolboxFeatureModule module) => module switch
    {
        ToolboxFeatureModule.Boards => BoardsEnabled,
        ToolboxFeatureModule.DoorPanels => DoorPanelsEnabled,
        ToolboxFeatureModule.Hardware => HardwareEnabled,
        ToolboxFeatureModule.WallPanels => WallPanelsEnabled,
        ToolboxFeatureModule.Images => ImagesEnabled,
        ToolboxFeatureModule.OneClickFrame => OneClickFrameEnabled,
        ToolboxFeatureModule.BatchPrint => BatchPrintEnabled,
        ToolboxFeatureModule.Statistics => StatisticsEnabled,
        ToolboxFeatureModule.DrawingLayers => DrawingLayersEnabled,
        ToolboxFeatureModule.RadialMenu => RadialMenuEnabled,
        ToolboxFeatureModule.LispMarket => LispMarketEnabled,
        ToolboxFeatureModule.Shortcuts => ShortcutsEnabled,
        _ => false
    };

    public ToolboxPreferenceOptions WithModuleEnabled(ToolboxFeatureModule module, bool enabled) => module switch
    {
        ToolboxFeatureModule.Boards => this with { BoardsEnabled = enabled },
        ToolboxFeatureModule.DoorPanels => this with { DoorPanelsEnabled = enabled },
        ToolboxFeatureModule.Hardware => this with { HardwareEnabled = enabled },
        ToolboxFeatureModule.WallPanels => this with { WallPanelsEnabled = enabled },
        ToolboxFeatureModule.Images => this with { ImagesEnabled = enabled },
        ToolboxFeatureModule.OneClickFrame => this with { OneClickFrameEnabled = enabled },
        ToolboxFeatureModule.BatchPrint => this with { BatchPrintEnabled = enabled },
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
    public int Version { get; set; } = 1;

    public DoorPanelOptions DoorPanel { get; set; } = DoorPanelOptions.Default;

    public PlanDoorPanelOptions PlanDoorPanel { get; set; } = PlanDoorPanelOptions.Default;

    public DrawerFaceOptions DrawerFace { get; set; } = DrawerFaceOptions.Default;

    public HardwareOptions Hardware { get; set; } = HardwareOptions.Default;

    public GrilleOptions Grille { get; set; } = GrilleOptions.Default;

    public PerforatedPanelOptions PerforatedPanel { get; set; } = PerforatedPanelOptions.Default;

    public BackgroundWallOptions BackgroundWall { get; set; } = BackgroundWallOptions.Default;

    public BoardToolOptions Board { get; set; } = BoardToolOptions.Default;

    public DrawingLayerToolOptions DrawingLayer { get; set; } = DrawingLayerToolOptions.Default;

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
        Hardware = Hardware,
        Grille = Grille,
        PerforatedPanel = PerforatedPanel,
        BackgroundWall = BackgroundWall with { Rows = BackgroundWall.Rows.ToArray() },
        Board = Board,
        DrawingLayer = DrawingLayer,
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
        if (Version != 1 ||
            !IsValid(DoorPanel) ||
            !IsValid(PlanDoorPanel) ||
            !IsValid(DrawerFace) ||
            !IsValid(Hardware) ||
            !IsValid(Grille) ||
            !IsValid(PerforatedPanel) ||
            !IsValid(BackgroundWall) ||
            !IsValid(Board) ||
            !IsValid(DrawingLayer) ||
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
        value.Count > 0 &&
        IsNonNegative(value.Gap, value.TopExtension, value.BottomExtension, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

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
            value.LineThickness,
            value.WineRackDepth,
            value.CrossThickness,
            value.DiagonalThickness,
            value.WineCellWidth,
            value.AbaOuterThickness,
            value.AbaCoreThickness,
            value.AbaDepth,
            value.DivideThickness,
            value.BackboardThickness) &&
        value.TagScale is >= 0.1 and <= 10 &&
        !string.IsNullOrWhiteSpace(value.TagType) &&
        IsNonNegative(
            value.ExtendStart,
            value.ExtendEnd,
            value.BackboardInset,
            value.BackboardLeftExtension,
            value.BackboardRightExtension,
            value.BackboardTopExtension,
            value.BackboardBottomExtension) &&
        value.WineRows > 0 &&
        value.WineColumns > 0 &&
        value.DivideCount >= 2;

    private static bool IsValid(DrawingLayerToolOptions? value) =>
        value is not null &&
        value.LinetypeScale is >= 1 and <= 500 &&
        Compatibility.IsFinite(value.LinetypeScale) &&
        value.WallHatchScale >= 0.5 &&
        Compatibility.IsFinite(value.WallHatchScale);

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
            var settings = JsonSerializer.Deserialize<ToolboxSettings>(File.ReadAllText(path), JsonOptions)
                ?? ToolboxSettings.CreateDefault();
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
