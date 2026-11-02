using System.IO;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class SmartAnnotationRuntimeSettings
{
    public static SmartAnnotationOptions Current => ToolboxSettingsRuntime.Settings.SmartAnnotation;

    public static void Save(SmartAnnotationOptions options) =>
        ToolboxSettingsRuntime.Update(settings => settings.SmartAnnotation = options);
}

internal sealed record DimensionTemplateItem(string Name, string FullPath);

internal static class DimensionTemplateLibraryService
{
    public static string GetDirectory() => Path.Combine(
        PluginResourceLocator.GetPluginRoot(),
        "Modules",
        "DimTemplate");

    public static IReadOnlyList<DimensionTemplateItem> Scan()
    {
        var directory = GetDirectory();
        Directory.CreateDirectory(directory);
        return Directory.EnumerateFiles(directory, "*.dwg", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path => new DimensionTemplateItem(Path.GetFileName(path), path))
            .ToArray();
    }

    public static string? Resolve(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) ||
            !string.Equals(Path.GetFileName(templateName), templateName, StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(templateName), ".dwg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Path.Combine(GetDirectory(), templateName);
        return File.Exists(path) ? path : null;
    }
}
