using System.Text.Json;

namespace YiZhiMao.Dc.Core;

public enum PdfOutputMode
{
    MergedOnly,
    SeparateOnly,
    SeparateAndMerged
}

public sealed class PrintSettings
{
    public PrintSettings Clone()
    {
        return (PrintSettings)MemberwiseClone();
    }

    public bool PlotTransparency { get; set; }

    public bool PlotLineweights { get; set; }

    public bool OpenOutputFolder { get; set; } = true;

    public bool OpenMergedPdf { get; set; } = true;

    public PdfOutputMode PdfOutputMode { get; set; } = PdfOutputMode.MergedOnly;

    public int VectorDpi { get; set; } = 1200;

    public int GradientDpi { get; set; } = 400;

    public int ImageDpi { get; set; } = 200;

    public bool DetectClosedPolylines { get; set; } = true;

    public bool DetectLooseLines { get; set; } = true;

    public bool DetectBlocks { get; set; } = true;

    public bool IgnoreEmptyFrames { get; set; } = true;

    public string Printer { get; set; } = "DWG To PDF.pc3";

    public string Paper { get; set; } = string.Empty;

    public string PlotStyle { get; set; } = "monochrome.ctb";

    public double Margin { get; set; }

    public bool Collate { get; set; } = true;

    public void Validate()
    {
        if (VectorDpi is < 72 or > 4800 || GradientDpi is < 72 or > 4800 || ImageDpi is < 72 or > 4800)
        {
            throw new InvalidOperationException("分辨率必须在 72 至 4800 DPI 之间。");
        }

        if (!double.IsFinite(Margin) || Margin < 0)
        {
            throw new InvalidOperationException("页边留白不能为负数。");
        }

        if (string.IsNullOrWhiteSpace(Printer))
        {
            throw new InvalidOperationException("请选择打印设备。");
        }
    }
}

public sealed class PrintSettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PrintSettings Load()
    {
        if (!File.Exists(path))
        {
            return new PrintSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<PrintSettings>(File.ReadAllText(path), JsonOptions) ?? new PrintSettings();
            settings.Validate();
            return settings;
        }
        catch (JsonException)
        {
            return new PrintSettings();
        }
        catch (InvalidOperationException)
        {
            return new PrintSettings();
        }
    }

    public void Save(PrintSettings settings)
    {
        settings.Validate();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
