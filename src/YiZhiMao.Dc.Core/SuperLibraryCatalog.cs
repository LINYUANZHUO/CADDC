namespace YiZhiMao.Dc.Core;

public sealed record SuperLibraryItem(
    string Name,
    string CategoryName,
    string DwgPath,
    string? PreviewPath);

public sealed record SuperLibraryCategory(
    string Name,
    string DirectoryPath,
    IReadOnlyList<SuperLibraryItem> Items);

public static class SuperLibraryCatalog
{
    public const string UnclassifiedCategoryName = "未分类";

    private static readonly string[] PreviewExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public static string EnsureRoot(string pluginRoot)
    {
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            throw new ArgumentException("插件根目录不能为空。", nameof(pluginRoot));
        }
        var root = Path.Combine(Path.GetFullPath(pluginRoot), "Modules", "SuperLibrary");
        Directory.CreateDirectory(root);
        return root;
    }

    public static IReadOnlyList<SuperLibraryCategory> Scan(string rootDirectory)
    {
        var root = ResolveRoot(rootDirectory, create: true);
        var categories = Directory.EnumerateDirectories(root)
            .Where(directory => (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
            .Select(directory => CreateCategorySnapshot(Path.GetFileName(directory), directory))
            .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var rootItems = ScanItems(root, UnclassifiedCategoryName);
        if (rootItems.Count > 0)
        {
            categories.Insert(0, new SuperLibraryCategory(UnclassifiedCategoryName, root, rootItems));
        }

        return categories;
    }

    public static string CreateCategory(string rootDirectory, string categoryName)
    {
        var root = ResolveRoot(rootDirectory, create: true);
        var category = ResolveCategoryDirectory(root, categoryName);
        if (Directory.Exists(category))
        {
            throw new InvalidOperationException($"分类“{categoryName.Trim()}”已经存在。");
        }

        Directory.CreateDirectory(category);
        return category;
    }

    public static string RenameCategory(string rootDirectory, string currentName, string newName)
    {
        var root = ResolveRoot(rootDirectory, create: false);
        var current = ResolveCategoryDirectory(root, currentName);
        var target = ResolveCategoryDirectory(root, newName);
        if (!Directory.Exists(current))
        {
            throw new InvalidOperationException($"分类“{currentName.Trim()}”不存在。");
        }

        if (Directory.Exists(target))
        {
            throw new InvalidOperationException($"分类“{newName.Trim()}”已经存在。");
        }

        Directory.Move(current, target);
        return target;
    }

    public static void DeleteCategory(string rootDirectory, string categoryName)
    {
        var root = ResolveRoot(rootDirectory, create: false);
        var category = ResolveCategoryDirectory(root, categoryName);
        if (!Directory.Exists(category))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(category).Any())
        {
            throw new InvalidOperationException("分类内仍有文件，请先删除图库项目后再删除分类。");
        }

        Directory.Delete(category, recursive: false);
    }

    public static int DeleteItems(string rootDirectory, IEnumerable<string> dwgPaths)
    {
        if (dwgPaths is null)
        {
            throw new ArgumentNullException(nameof(dwgPaths));
        }
        var root = ResolveRoot(rootDirectory, create: false);
        var paths = dwgPaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate => ResolveFileWithinRoot(root, candidate))
            .ToArray();
        if (paths.Any(path => !path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("超级图库只能删除 DWG 项目。");
        }

        var deleted = 0;
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            File.Delete(path);
            foreach (var extension in PreviewExtensions)
            {
                var preview = Path.ChangeExtension(path, extension);
                if (File.Exists(preview))
                {
                    File.Delete(preview);
                }
            }

            deleted++;
        }

        return deleted;
    }

    public static string ResolveSavePath(
        string rootDirectory,
        string categoryName,
        string itemName)
    {
        var root = ResolveRoot(rootDirectory, create: false);
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new InvalidOperationException("分类名称不能为空。");
        }

        var normalizedCategoryName = categoryName.Trim();
        var category = string.Equals(
            normalizedCategoryName,
            UnclassifiedCategoryName,
            StringComparison.OrdinalIgnoreCase)
            ? root
            : ResolveCategoryDirectory(root, normalizedCategoryName);
        if (!Directory.Exists(category))
        {
            throw new InvalidOperationException($"分类“{normalizedCategoryName}”不存在。");
        }

        var safeName = ValidateName(itemName, "图库名称");
        return ResolveFileWithinRoot(root, Path.Combine(category, safeName + ".dwg"));
    }

    private static SuperLibraryCategory CreateCategorySnapshot(string name, string directory)
    {
        return new SuperLibraryCategory(name, directory, ScanItems(directory, name));
    }

    private static IReadOnlyList<SuperLibraryItem> ScanItems(string directory, string categoryName)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .Select(path => new SuperLibraryItem(
                Path.GetFileNameWithoutExtension(path),
                categoryName,
                Path.GetFullPath(path),
                FindPreview(path)))
            .ToList();
    }

    private static string? FindPreview(string dwgPath)
    {
        foreach (var extension in PreviewExtensions)
        {
            var path = Path.ChangeExtension(dwgPath, extension);
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }

    private static string ResolveRoot(string rootDirectory, bool create)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("超级图库根目录不能为空。", nameof(rootDirectory));
        }
        var root = Path.GetFullPath(rootDirectory);
        if (create)
        {
            Directory.CreateDirectory(root);
        }
        else if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("超级图库目录不存在。");
        }

        return root;
    }

    private static string ResolveCategoryDirectory(string root, string categoryName)
    {
        var name = ValidateName(categoryName, "分类名称");
        if (string.Equals(name, UnclassifiedCategoryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("未分类是系统保留分类，不能新增、改名或删除。");
        }

        return ResolveFileWithinRoot(root, Path.Combine(root, name));
    }

    private static string ResolveFileWithinRoot(string root, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new ArgumentException("目标路径不能为空。", nameof(candidate));
        }
        var resolvedRoot = EnsureTrailingSeparator(Path.GetFullPath(root));
        var resolvedCandidate = Path.GetFullPath(candidate);
        if (!resolvedCandidate.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("目标路径超出超级图库根目录。");
        }

        EnsureNoReparsePoints(root, resolvedCandidate);

        return resolvedCandidate;
    }

    private static void EnsureNoReparsePoints(string root, string candidate)
    {
        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var directory = Directory.Exists(candidate)
            ? candidate
            : Path.GetDirectoryName(candidate);
        while (!string.IsNullOrWhiteSpace(directory) &&
               !string.Equals(directory, resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(directory) &&
                (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("超级图库不允许通过目录链接访问或修改外部文件。");
            }

            directory = Directory.GetParent(directory)?.FullName;
        }
    }

    private static string ValidateName(string value, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{displayName}不能为空。");
        }
        var name = value.Trim();
        if (name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException($"{displayName}包含无效字符。");
        }

        return name;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
