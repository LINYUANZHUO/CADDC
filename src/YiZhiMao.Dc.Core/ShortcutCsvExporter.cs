using System.Text;

namespace YiZhiMao.Dc.Core;

public static class ShortcutCsvExporter
{
    private static readonly string[] Headers = ["功能说明", "默认", "自定义", "禁用默认", "底层命令"];

    public static void Export(string path, IEnumerable<ShortcutItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(items);

        if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("导出文件必须使用 .csv 扩展名。", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("导出路径缺少父目录。");
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var writer = new StreamWriter(tempPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
            {
                writer.NewLine = "\r\n";
                WriteRow(writer, Headers);
                foreach (var item in items)
                {
                    WriteRow(writer,
                    [
                        item.Description,
                        item.DefaultAlias,
                        item.CustomAlias,
                        item.DisableDefault ? "是" : "否",
                        item.CoreCommand
                    ]);
                }
            }

            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void WriteRow(TextWriter writer, IEnumerable<string> values)
    {
        writer.WriteLine(string.Join(",", values.Select(EscapeField)));
    }

    private static string EscapeField(string? value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return text;
        }

        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
