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
        ImageBatch = ImageBatch
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
            !IsValid(ImageBatch))
        {
            throw new InvalidOperationException("工具箱参数文件包含无效数据。");
        }
    }

    private static bool IsValid(DoorPanelOptions? value) =>
        value is not null &&
        Enum.IsDefined(value.CalculationMode) &&
        value.DoorCount > 0 &&
        IsNonNegative(value.Gap, value.TopExtension, value.BottomExtension, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(PlanDoorPanelOptions? value) =>
        value is not null &&
        Enum.IsDefined(value.CalculationMode) &&
        value.DoorCount > 0 &&
        IsPositive(value.Thickness) &&
        IsNonNegative(value.FrontClearance, value.Gap, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(DrawerFaceOptions? value) =>
        value is not null &&
        Enum.IsDefined(value.Orientation) &&
        Enum.IsDefined(value.CalculationMode) &&
        value.Count > 0 &&
        IsNonNegative(value.Gap, value.TopExtension, value.BottomExtension, value.LeftExtension, value.RightExtension) &&
        IsColor(value.LineColorIndex);

    private static bool IsValid(HardwareOptions? value) =>
        value is not null &&
        Enum.IsDefined(value.HingeDimensionMode) &&
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
        double.IsFinite(value.LinetypeScale) &&
        value.WallHatchScale >= 0.5 &&
        double.IsFinite(value.WallHatchScale);

    private static bool IsValid(ImageBatchOptions? value) =>
        value is not null &&
        IsNonNegative(value.Gap) &&
        value.PixelHeight is >= 1 and <= ImageBatchOptions.MaximumPixelHeight;

    private static bool IsColor(short value) => value is >= 1 and <= 255;

    private static bool IsPositive(params double[] values) =>
        values.All(value => double.IsFinite(value) && value > 0);

    private static bool IsNonNegative(params double[] values) =>
        values.All(value => double.IsFinite(value) && value >= 0);
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
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
