using Autodesk.AutoCAD.DatabaseServices;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Printing;

internal sealed class CadPrintJob
{
    public required ObjectId LayoutId { get; init; }

    public required string LayoutName { get; init; }

    public PrintFrame? Frame { get; init; }

    public int Copies { get; set; } = 1;

    public string Paper { get; set; } = string.Empty;

    public bool IsSelected { get; set; } = true;

    public int Index { get; set; }

    public string DrawingSize => Frame is { } frame
        ? $"{frame.Width:0.##} × {frame.Height:0.##}"
        : "布局";

    public string Notes => Frame is null ? LayoutName : $"{LayoutName} / 图框";
}
