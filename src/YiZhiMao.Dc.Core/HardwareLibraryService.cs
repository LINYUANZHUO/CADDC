namespace YiZhiMao.Dc.Core;

public sealed record HardwareLibraryItem(string Name, string DwgPath, string? PreviewPath);

public sealed record HardwareLibraryFolders(
    string RootDirectory,
    string HangingRodDirectory,
    string LightStripDirectory,
    string HandleDirectory,
    string InstructionsPath);

public static class HardwareLibraryService
{
    private static readonly string[] PreviewExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public static HardwareLibraryFolders EnsureDefaultFolders(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        var root = Path.Combine(pluginDirectory, "Modules");
        return EnsureFolders(root);
    }

    public static HardwareLibraryFolders EnsureFolders(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        var root = Path.GetFullPath(rootDirectory);
        var hangingRodDirectory = Path.Combine(root, "HangingRodBlocks");
        var lightStripDirectory = Path.Combine(root, "LightStripBlocks");
        var handleDirectory = Path.Combine(root, "HandleBlocks");
        var instructionsPath = Path.Combine(root, "素材放置说明.txt");

        Directory.CreateDirectory(hangingRodDirectory);
        Directory.CreateDirectory(lightStripDirectory);
        Directory.CreateDirectory(handleDirectory);
        if (!File.Exists(instructionsPath))
        {
            File.WriteAllText(
                instructionsPath,
                "一只猫dc 五金素材库\r\n\r\n" +
                "1. 挂衣杆 DWG 放入 HangingRodBlocks。\r\n" +
                "2. 灯带 DWG 放入 LightStripBlocks。\r\n" +
                "3. 可放置与 DWG 同名的 PNG、JPG、JPEG 或 BMP 作为预览图。\r\n" +
                "4. 抽面拉手 DWG 放入 HandleBlocks。\r\n" +
                "5. 插件只读取这些文件，不需要重新编译。\r\n");
        }

        return new HardwareLibraryFolders(
            root,
            hangingRodDirectory,
            lightStripDirectory,
            handleDirectory,
            instructionsPath);
    }

    public static IReadOnlyList<HardwareLibraryItem> Scan(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var previews = Directory.EnumerateFiles(directory)
            .Where(path => PreviewExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .GroupBy(path => Path.GetFileNameWithoutExtension(path)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(directory, "*.dwg", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFullPath)
            .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
            .Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                previews.TryGetValue(name, out var previewPath);
                return new HardwareLibraryItem(name, path, previewPath);
            })
            .ToList();
    }
}
