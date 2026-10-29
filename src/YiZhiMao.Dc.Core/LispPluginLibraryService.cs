namespace YiZhiMao.Dc.Core;

public sealed record LispPluginItem(
    string Name,
    string Type,
    string RelativePath,
    string FullPath,
    long SizeBytes)
{
    public string SizeText => SizeBytes < 1024
        ? $"{SizeBytes} B"
        : $"{SizeBytes / 1024d:0.#} KB";
}

public static class LispPluginLibraryService
{
    private static readonly string[] SupportedExtensions = [".lsp", ".fas", ".vlx"];

    public static string EnsureDefaultFolder(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        return EnsureFolder(Path.Combine(pluginDirectory, "Modules"));
    }

    public static string EnsureFolder(string modulesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);

        var directory = Path.Combine(Path.GetFullPath(modulesRoot), "LispPlugins");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static IReadOnlyList<LispPluginItem> Scan(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return [];
        }

        var root = Path.GetFullPath(directory);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.CurrentCultureIgnoreCase)
            .Select(path => new LispPluginItem(
                Path.GetFileNameWithoutExtension(path),
                Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
                Path.GetRelativePath(root, path),
                path,
                new FileInfo(path).Length))
            .ToList();
    }

    public static string BuildLoadExpression(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只支持加载 LSP、FAS 或 VLX 文件。");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("LISP 插件文件不存在。", fullPath);
        }

        var lispPath = fullPath.Replace('\\', '/').Replace("\"", "\\\"");
        return $"(load \"{lispPath}\") ";
    }
}
