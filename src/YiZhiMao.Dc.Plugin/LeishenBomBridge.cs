using System.Globalization;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class LeishenBomBridge
{
    public const string PartsCommand = "LS_CMD_BOM_STAT";
    public const string DoorCommand = "LS_CMD_BOM_DOORSTAT";
    public const string FrameDoorCommand = "LS_CMD_BOM_FRAMEDOORSTAT";
    public const string WholeHouseCommand = "LS_CMD_BOM_ONECLICK";
    public const string BoardNestingCommand = "LS_CMD_BOM_NESTING";
    public const string VisualCopyCommand = "LS_CMD_BOM_VISUALCOPY";
    public const string CabinetBomCommand = "LS_CMD_BOM_EXTRACTCABINET";

    private const string AssemblyName = "LS.Module.Bom";

    public static bool TryQueueCommand(Document document, string command, out string error)
    {
        try
        {
            var assembly = GetBomAssembly();
            ApplySettings(assembly, ToolboxSettingsRuntime.Settings);
            document.SendStringToExecute(command + " ", true, false, true);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.GetBaseException().Message;
            return false;
        }
    }

    private static Assembly GetBomAssembly()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));
        if (loaded is not null)
        {
            return loaded;
        }

        foreach (var path in GetCandidatePaths())
        {
            if (File.Exists(path))
            {
                return Assembly.LoadFrom(path);
            }
        }

        throw new FileNotFoundException("未找到已安装的拆单统计模块 LS.Module.Bom.dll。");
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var coreAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, "LS.Core", StringComparison.OrdinalIgnoreCase));
        var coreFolder = coreAssembly is null
            ? null
            : Path.GetDirectoryName(coreAssembly.Location);
        if (!string.IsNullOrWhiteSpace(coreFolder))
        {
            yield return Path.Combine(coreFolder, "Modules", AssemblyName + ".dll");
        }

        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(
            applicationData,
            "Autodesk",
            "ApplicationPlugins",
            "LeishenToolbox.bundle",
            "Contents",
            "Modules",
            AssemblyName + ".dll");
    }

    private static void ApplySettings(Assembly assembly, ToolboxSettings settings)
    {
        var wholeHouse = settings.WholeHouseStatistics;
        SetProperties(assembly, "LS.Module.Bom.Models.BomParams", new Dictionary<string, object>
        {
            ["StatCabinet"] = wholeHouse.IncludeCabinetBoards,
            ["StatNormalDoor"] = wholeHouse.IncludeStandardDoors,
            ["StatFrameDoor"] = wholeHouse.IncludeFrameDoors,
            ["StatBoardNesting"] = wholeHouse.IncludeBoardProperties,
            ["StatClothesRail"] = wholeHouse.IncludeHangingRods,
            ["StatLightStrip"] = wholeHouse.IncludeLightStrips,
            ["StatHandle"] = wholeHouse.IncludeHandles,
            ["StatConcealedHandle"] = wholeHouse.IncludeConcealedHandles,
            ["StatHinge"] = wholeHouse.IncludeHinges,
            ["StatSlideRail"] = wholeHouse.IncludeSlides,
            ["TableScale"] = wholeHouse.TableScaleFactor,
            ["MaxRows"] = wholeHouse.MaxRowsPerPage
        });

        var door = settings.DoorStatistics;
        SetProperties(assembly, "LS.Module.Bom.Models.DoorStatCmdsParams", new Dictionary<string, object>
        {
            ["TableScale"] = door.TableScaleFactor,
            ["Thickness"] = door.Thickness,
            ["MaxRows"] = door.MaxRowsPerPage
        });

        var frame = settings.FrameDoorStatistics;
        SetProperties(assembly, "LS.Module.Bom.Models.FrameDoorParams", new Dictionary<string, object>
        {
            ["C"] = frame.CabinetThickness,
            ["D"] = frame.BoardCoreCount,
            ["N"] = frame.GlassCoreCount,
            ["F"] = frame.CoreThickness,
            ["E"] = frame.SideFrameWidth,
            ["E1"] = frame.TopFrameWidth,
            ["E2"] = frame.MiddleFrameWidth,
            ["E3"] = frame.BottomFrameWidth,
            ["G"] = frame.RightFrameTenon,
            ["M"] = frame.MiddleFrameTenon,
            ["I_TopTenon"] = frame.CoreTopTenon,
            ["J_BottomTenon"] = frame.CoreBottomTenon,
            ["K_LeftTenon"] = frame.CoreLeftTenon,
            ["L_RightTenon"] = frame.CoreRightTenon,
            ["IsMiddleCentered"] = frame.CenterMiddleFrame,
            ["P"] = frame.LowerFramePosition,
            ["TableScale"] = frame.TableScaleFactor,
            ["MaxRows"] = frame.MaxRowsPerPage
        });

        var cabinet = settings.CabinetBom;
        SetProperties(assembly, "LS.Module.Bom.Models.CabinetBomParams", new Dictionary<string, object>
        {
            ["StatBasicPlates"] = cabinet.IncludeBasicBoards,
            ["StatBackPlate"] = cabinet.IncludeBackboards,
            ["StatCabinetSummary"] = cabinet.IncludeCabinetSummary,
            ["TableScale"] = cabinet.TableScaleFactor,
            ["MaxRows"] = cabinet.MaxRowsPerPage
        });

        var nesting = settings.PanelNesting;
        SetProperties(assembly, "LS.Module.Bom.Models.PanelNestingParams", new Dictionary<string, object>
        {
            ["IsWoodGrain"] = nesting.UseWoodGrain,
            ["IsSolidColor"] = nesting.UseSolidColor,
            ["Is48Panel"] = nesting.Use48Sheet,
            ["Is49Panel"] = nesting.Use49Sheet,
            ["TargetCabinet"] = nesting.IncludeCabinetBoards,
            ["TargetFlatDoor"] = nesting.IncludeFlatDoors,
            ["TableScale"] = nesting.TableScaleFactor,
            ["MaxRows"] = nesting.MaxRowsPerPage
        });
    }

    private static void SetProperties(
        Assembly assembly,
        string typeName,
        IReadOnlyDictionary<string, object> values)
    {
        var type = assembly.GetType(typeName, throwOnError: true)!;
        var instanceProperty = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static) ??
            throw new MissingMemberException(typeName, "Instance");
        var instance = instanceProperty.GetValue(null) ??
            throw new InvalidOperationException($"{typeName}.Instance 返回空值。");
        foreach (var pair in values)
        {
            var property = type.GetProperty(pair.Key, BindingFlags.Public | BindingFlags.Instance) ??
                throw new MissingMemberException(typeName, pair.Key);
            if (!property.CanWrite)
            {
                throw new InvalidOperationException($"{typeName}.{pair.Key} 不可写。");
            }

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            var converted = targetType.IsInstanceOfType(pair.Value)
                ? pair.Value
                : Convert.ChangeType(pair.Value, targetType, CultureInfo.InvariantCulture);
            property.SetValue(instance, converted);
        }
    }
}
