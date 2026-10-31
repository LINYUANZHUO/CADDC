using System.Security.Cryptography;

namespace YiZhiMao.Dc.Core;

public enum ShxFontSyncStatus
{
    Copied,
    Existing,
    Missing,
    Conflict,
    Failed
}

public sealed record ShxFontSyncItem(
    string FileName,
    ShxFontSyncStatus Status,
    string? Error = null);

public sealed class ShxFontSyncReport(IReadOnlyList<ShxFontSyncItem> items)
{
    public IReadOnlyList<ShxFontSyncItem> Items { get; } = items;

    public int CopiedCount => Count(ShxFontSyncStatus.Copied);

    public int ExistingCount => Count(ShxFontSyncStatus.Existing);

    public int MissingCount => Count(ShxFontSyncStatus.Missing);

    public int ConflictCount => Count(ShxFontSyncStatus.Conflict);

    public int FailedCount => Count(ShxFontSyncStatus.Failed);

    private int Count(ShxFontSyncStatus status) => Items.Count(item => item.Status == status);
}

public static class ShxFontSyncService
{
    public static ShxFontSyncReport Sync(
        string libraryDirectory,
        string drawingDirectory,
        IEnumerable<string> referencedFontFiles)
    {
        if (string.IsNullOrWhiteSpace(libraryDirectory))
        {
            throw new ArgumentException("字体库目录不能为空。", nameof(libraryDirectory));
        }

        if (string.IsNullOrWhiteSpace(drawingDirectory))
        {
            throw new ArgumentException("图纸目录不能为空。", nameof(drawingDirectory));
        }

        if (referencedFontFiles is null)
        {
            throw new ArgumentNullException(nameof(referencedFontFiles));
        }

        var libraryPath = Path.GetFullPath(libraryDirectory);
        var targetPath = Path.GetFullPath(drawingDirectory);
        Directory.CreateDirectory(targetPath);

        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(libraryPath))
        {
            foreach (var sourcePath in Directory.EnumerateFiles(libraryPath, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(sourcePath);
                if (string.Equals(Path.GetExtension(fileName), ".shx", StringComparison.OrdinalIgnoreCase) &&
                    !sources.ContainsKey(fileName))
                {
                    sources.Add(fileName, sourcePath);
                }
            }
        }

        var fileNames = referencedFontFiles
            .Select(NormalizeShxFileName)
            .Where(fileName => fileName is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase);

        var results = new List<ShxFontSyncItem>();
        foreach (var fileName in fileNames)
        {
            if (!sources.TryGetValue(fileName, out var sourcePath))
            {
                results.Add(new ShxFontSyncItem(fileName, ShxFontSyncStatus.Missing));
                continue;
            }

            var destinationPath = Path.Combine(targetPath, fileName);
            try
            {
                if (File.Exists(destinationPath))
                {
                    results.Add(new ShxFontSyncItem(
                        fileName,
                        FilesHaveSameContent(sourcePath, destinationPath)
                            ? ShxFontSyncStatus.Existing
                            : ShxFontSyncStatus.Conflict));
                    continue;
                }

                try
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                    results.Add(new ShxFontSyncItem(fileName, ShxFontSyncStatus.Copied));
                }
                catch (IOException) when (File.Exists(destinationPath))
                {
                    results.Add(new ShxFontSyncItem(
                        fileName,
                        FilesHaveSameContent(sourcePath, destinationPath)
                            ? ShxFontSyncStatus.Existing
                            : ShxFontSyncStatus.Conflict));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                results.Add(new ShxFontSyncItem(fileName, ShxFontSyncStatus.Failed, exception.Message));
            }
        }

        return new ShxFontSyncReport(results.AsReadOnly());
    }

    private static string? NormalizeShxFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var segments = value!.Trim().Replace('\\', '/').Split('/');
        if (segments.Any(segment => segment == ".."))
        {
            return null;
        }

        var fileName = segments.LastOrDefault();
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetExtension(fileName), ".shx", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fileName;
    }

    private static bool FilesHaveSameContent(string firstPath, string secondPath)
    {
        var firstInfo = new FileInfo(firstPath);
        var secondInfo = new FileInfo(secondPath);
        if (firstInfo.Length != secondInfo.Length)
        {
            return false;
        }

        using var algorithm = SHA256.Create();
        using var first = File.OpenRead(firstPath);
        var firstHash = algorithm.ComputeHash(first);
        using var second = File.OpenRead(secondPath);
        var secondHash = algorithm.ComputeHash(second);
        return firstHash.SequenceEqual(secondHash);
    }
}
