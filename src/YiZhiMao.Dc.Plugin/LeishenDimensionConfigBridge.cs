using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class LeishenDimensionConfigBridge
{
    public static bool IsAvailable => FindCoreAssembly() is not null;

    public static bool TryApply(SmartAnnotationOptions options, out string message)
    {
        try
        {
            var assembly = FindCoreAssembly();
            if (assembly is null)
            {
                message = "未检测到已加载的雷神工具箱，无法执行既有智能标注命令。";
                return false;
            }

            var contentsDirectory = Path.GetDirectoryName(assembly.Location);
            var configPath = contentsDirectory is null
                ? null
                : Path.Combine(contentsDirectory, "Modules", "DimensionConfig.json");
            if (configPath is null || !File.Exists(configPath))
            {
                message = "未找到雷神标注参数文件 DimensionConfig.json。";
                return false;
            }

            var root = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject();
            if (root is null)
            {
                message = "雷神标注参数文件格式无效。";
                return false;
            }

            // CADDC has already synchronized the template into the current drawing.
            // Keep the external command on the current style so it cannot repeat the
            // legacy cross-database synchronization that raised eInvalidInput.
            root["UseTemplateStyle"] = false;
            root["CurrentTemplateName"] = options.TemplateName;
            root["OffsetInner"] = options.InnerOffset;
            root["OffsetOuter"] = options.OuterOffset;
            root["IsFullGraphicMode"] = options.FullGraphicMode;
            root["IsFilterThickness"] = options.FilterByThickness;
            root["FilterThicknessValues"] = options.FilterThickness.ToString("0.###", CultureInfo.InvariantCulture);
            root["IsFilterMinDim"] = options.FilterMinimumDimension;
            root["FilterMinDimValue"] = options.MinimumDimension;
            root["IsIncludeGenericGraphics"] = options.MergeOverlappingGeometry;
            root["IsDeleteByThickness"] = options.DeleteByThickness;
            root["DeleteThicknessValue"] = options.DeleteThickness.ToString("0.###", CultureInfo.InvariantCulture);
            root["FilterCircle"] = options.FilterPoints;
            root["FilterLine"] = options.FilterLines;
            root["FilterDashedLine"] = options.FilterDashedLines;
            root["FilterPolyline"] = options.FilterPolylines;
            root["FilterArc"] = options.FilterArcs;
            root["FilterText"] = options.FilterText;
            root["FilterBlock"] = options.FilterBlocks;
            root["FilterHatch"] = options.FilterHatches;

            var tempPath = $"{configPath}.{System.Diagnostics.Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(tempPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                Compatibility.MoveFileReplacingExisting(tempPath, configPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            message = "智能标注参数已同步。";
            return true;
        }
        catch (System.Exception exception)
        {
            message = exception.Message;
            return false;
        }
    }

    private static System.Reflection.Assembly? FindCoreAssembly() =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, "LS.Core", StringComparison.OrdinalIgnoreCase));
}
