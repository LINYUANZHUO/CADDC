using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace YiZhiMao.Dc.Core;

public sealed record PdfFileItem(string Path, string FileName, int PageCount)
{
    public bool IsSelected { get; set; } = true;
}

public static class PdfToolService
{
    public static IReadOnlyList<PdfFileItem> Scan(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"PDF 来源目录不存在：{directory}");
        }

        return Directory.EnumerateFiles(directory, "*.pdf", SearchOption.TopDirectoryOnly)
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .Select(file => new PdfFileItem(file, Path.GetFileName(file), GetPageCount(file)))
            .ToList();
    }

    public static int GetPageCount(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    public static int Merge(IEnumerable<string> inputPaths, string outputPath)
    {
        var inputs = inputPaths.Select(Path.GetFullPath).ToList();
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一个 PDF 文件。");
        }

        var resolvedOutput = Path.GetFullPath(outputPath);
        if (inputs.Contains(resolvedOutput, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("输出文件不能同时作为合并来源。");
        }

        var outputDirectory = Path.GetDirectoryName(resolvedOutput);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using var output = new PdfDocument();
        foreach (var inputPath in inputs)
        {
            using var input = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
            for (var index = 0; index < input.PageCount; index++)
            {
                output.AddPage(input.Pages[index]);
            }
        }

        var pageCount = output.PageCount;
        output.Save(resolvedOutput);
        return pageCount;
    }
}
