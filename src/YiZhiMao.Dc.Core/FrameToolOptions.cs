using System.Text.Json;

namespace YiZhiMao.Dc.Core;

public enum FrameAlignmentMode
{
    Left,
    Bottom
}

public enum DetailBoundaryShape
{
    Circle,
    RoundedRectangle
}

public sealed record FrameTemplateItem(string Name, string DwgPath, bool IsExternal = false);

public sealed class FrameToolOptions
{
    public string SelectedTemplatePath { get; set; } = string.Empty;

    public double FrameMargin { get; set; } = 50;

    public bool GeneratePageStatistics { get; set; }

    public FrameAlignmentMode AlignmentMode { get; set; } = FrameAlignmentMode.Left;

    public double HorizontalGap { get; set; } = 100;

    public double VerticalGap { get; set; } = 100;

    public DetailBoundaryShape DetailShape { get; set; } = DetailBoundaryShape.Circle;

    public double DetailScale { get; set; } = 3;

    public double DetailCornerRadius { get; set; } = 10;

    public double ClipScale { get; set; } = 1;

    public double ClipCornerRadius { get; set; } = 10;

    public FrameToolOptions Clone() => (FrameToolOptions)MemberwiseClone();

    public void Validate()
    {
        if (!double.IsFinite(FrameMargin) || FrameMargin < 0)
        {
            throw new InvalidOperationException("图形到图框的距离不能小于 0。");
        }

        if (!double.IsFinite(HorizontalGap) || HorizontalGap < 0 ||
            !double.IsFinite(VerticalGap) || VerticalGap < 0)
        {
            throw new InvalidOperationException("图框排列间距不能小于 0。");
        }

        if (!double.IsFinite(DetailScale) || DetailScale <= 0 ||
            !double.IsFinite(ClipScale) || ClipScale <= 0)
        {
            throw new InvalidOperationException("局部放大和截图倍数必须大于 0。");
        }

        if (!double.IsFinite(DetailCornerRadius) || DetailCornerRadius < 0 ||
            !double.IsFinite(ClipCornerRadius) || ClipCornerRadius < 0)
        {
            throw new InvalidOperationException("矩形圆角不能小于 0。");
        }
    }
}

public sealed class FrameToolOptionsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FrameToolOptions Load()
    {
        if (!File.Exists(path))
        {
            return new FrameToolOptions();
        }

        try
        {
            var options = JsonSerializer.Deserialize<FrameToolOptions>(File.ReadAllText(path), JsonOptions)
                ?? new FrameToolOptions();
            options.Validate();
            return options;
        }
        catch (JsonException)
        {
            return new FrameToolOptions();
        }
        catch (InvalidOperationException)
        {
            return new FrameToolOptions();
        }
    }

    public void Save(FrameToolOptions options)
    {
        options.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(options, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

public readonly record struct FramePlacement(double Scale, double InsertionX, double InsertionY);

public static class FramePlacementCalculator
{
    public static FramePlacement Calculate(PrintFrame templateFrame, PrintFrame contentFrame, double margin)
    {
        if (!templateFrame.IsFinite || templateFrame.Width <= 0 || templateFrame.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(templateFrame), "模板图框必须具有有效宽高。");
        }

        if (!contentFrame.IsFinite || contentFrame.Width <= 0 || contentFrame.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentFrame), "内容范围必须具有有效宽高。");
        }

        if (!double.IsFinite(margin) || margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin));
        }

        var scale = Math.Max(
            (contentFrame.Width + margin * 2) / templateFrame.Width,
            (contentFrame.Height + margin * 2) / templateFrame.Height);
        var templateCenterX = (templateFrame.MinX + templateFrame.MaxX) / 2;
        var templateCenterY = (templateFrame.MinY + templateFrame.MaxY) / 2;
        var contentCenterX = (contentFrame.MinX + contentFrame.MaxX) / 2;
        var contentCenterY = (contentFrame.MinY + contentFrame.MaxY) / 2;
        return new FramePlacement(
            scale,
            contentCenterX - templateCenterX * scale,
            contentCenterY - templateCenterY * scale);
    }
}

public static class FrameTemplateLibraryService
{
    public static string EnsureDefaultFolder(string pluginDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);
        return EnsureFolder(Path.Combine(pluginDirectory, "Modules"));
    }

    public static string EnsureFolder(string modulesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modulesRoot);
        var directory = Path.Combine(Path.GetFullPath(modulesRoot), "FrameTemplates");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static IReadOnlyList<FrameTemplateItem> Scan(string directory, string? externalPath = null)
    {
        var items = new List<FrameTemplateItem>();
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            items.AddRange(Directory.EnumerateFiles(directory, "*.dwg", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath)
                .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase)
                .Select(path => new FrameTemplateItem(Path.GetFileNameWithoutExtension(path), path)));
        }

        if (!string.IsNullOrWhiteSpace(externalPath) && File.Exists(externalPath) &&
            string.Equals(Path.GetExtension(externalPath), ".dwg", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(externalPath);
            if (!items.Any(item => string.Equals(item.DwgPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(new FrameTemplateItem(Path.GetFileNameWithoutExtension(fullPath), fullPath, true));
            }
        }

        return items;
    }
}
