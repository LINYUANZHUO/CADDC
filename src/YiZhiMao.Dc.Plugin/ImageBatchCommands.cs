using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Win32;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DrawingImage = System.Drawing.Image;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

internal static class ImageBatchRuntimeSettings
{
    public static ImageBatchOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.ImageBatch;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.ImageBatch = value);
    }

    public static void Save(ImageBatchOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.ImageBatch = value);
}

public static class ImageBatchCommands
{
    private const uint ClipboardEnhancedMetafile = 14;
    private const uint WindowMessageCommand = 0x0111;
    private const int DialogOk = 1;
    private const uint MaximumMetafileBytes = 15 * 1024 * 1024;

    [CommandMethod("DC_CMD_IMAGE_BATCH", CommandFlags.Modal)]
    [ShortcutCommand("插入图片", "批量插入图片", "OLE")]
    public static async void InsertImages()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var database = document.Database;
        var dialog = new OpenFileDialog
        {
            Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件 (*.*)|*.*",
            Multiselect = true,
            Title = "请选择要批量插入的图片"
        };
        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
        {
            editor.WriteMessage("\n[提示] 未选择任何图片，操作已取消。");
            return;
        }

        var pointResult = editor.GetPoint("\n请指定图片批量排布的起点: ");
        if (pointResult.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var options = ImageBatchRuntimeSettings.Current;
        var currentPoint = pointResult.Value;
        object? oldCommandEcho = null;
        object? oldOleFrame = null;
        object? oldOleQuality = null;
        LayerState? layerState = null;
        var inserted = 0;
        var failed = 0;

        try
        {
            TryGetSystemVariable("CMDECHO", out oldCommandEcho);
            TryGetSystemVariable("OLEFRAME", out oldOleFrame);
            TryGetSystemVariable("OLEQUALITY", out oldOleQuality);
            TrySetSystemVariable("CMDECHO", 0);
            TrySetSystemVariable("OLEFRAME", 1);
            TrySetSystemVariable("OLEQUALITY", 3);
            layerState = PrepareLayerZero(database, editor);

            editor.WriteMessage($"\n[提示] 开始批量导入 {dialog.FileNames.Length} 张图片...");
            for (var index = 0; index < dialog.FileNames.Length; index++)
            {
                var filePath = dialog.FileNames[index];
                editor.WriteMessage($"\n正在处理 ({index + 1}/{dialog.FileNames.Length}): {Path.GetFileName(filePath)}");

                try
                {
                    using var image = PrepareImage(filePath, options);
                    if (!TrySetClipboardEnhancedMetafile(image, out var clipboardError))
                    {
                        throw new InvalidOperationException(clipboardError);
                    }

                    var appendedId = ObjectId.Null;
                    ObjectEventHandler handler = (_, eventArgs) =>
                    {
                        if (eventArgs.DBObject is Ole2Frame)
                        {
                            appendedId = eventArgs.DBObject.ObjectId;
                        }
                    };

                    using var watcherCancellation = new CancellationTokenSource();
                    database.ObjectAppended += handler;
                    StartDialogWatcher(watcherCancellation.Token);
                    try
                    {
                        await editor.CommandAsync("._PASTECLIP", currentPoint);
                    }
                    finally
                    {
                        watcherCancellation.Cancel();
                        database.ObjectAppended -= handler;
                    }

                    if (appendedId.IsNull || appendedId.IsErased)
                    {
                        throw new InvalidOperationException("AutoCAD 未创建 OLE 图片对象。");
                    }

                    currentPoint = PositionFrame(document, appendedId, currentPoint, image.Width, options);
                    inserted++;
                }
                catch (Exception exception)
                {
                    failed++;
                    editor.WriteMessage($"\n[错误] 导入图片 {Path.GetFileName(filePath)} 失败: {exception.Message}");
                }
            }

            editor.WriteMessage($"\n[提示] 批量图片导入完成。成功 {inserted} 张，失败 {failed} 张。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 批量图片导入失败: {exception.Message}");
        }
        finally
        {
            RestoreLayer(database, layerState, editor);
            RestoreSystemVariable("OLEQUALITY", oldOleQuality);
            RestoreSystemVariable("OLEFRAME", oldOleFrame);
            RestoreSystemVariable("CMDECHO", oldCommandEcho);
        }
    }

    private static Bitmap PrepareImage(string filePath, ImageBatchOptions options)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var source = DrawingImage.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        ApplyExifOrientation(source);

        var targetSize = options.CalculateTargetSize(source.Width, source.Height);
        var output = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb);
        output.SetResolution(96, 96);

        using var graphics = Graphics.FromImage(output);
        graphics.Clear(Color.White);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, targetSize.Width, targetSize.Height),
            0,
            0,
            source.Width,
            source.Height,
            GraphicsUnit.Pixel);
        return output;
    }

    private static void ApplyExifOrientation(DrawingImage image)
    {
        const int orientationPropertyId = 0x0112;
        if (!image.PropertyIdList.Contains(orientationPropertyId))
        {
            return;
        }

        var orientation = image.GetPropertyItem(orientationPropertyId)?.Value?.FirstOrDefault() ?? 1;
        var rotation = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        image.RotateFlip(rotation);
    }

    private static Point3d PositionFrame(
        Document document,
        ObjectId frameId,
        Point3d currentPoint,
        int pixelWidth,
        ImageBatchOptions options)
    {
        using var documentLock = document.LockDocument();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var frame = transaction.GetObject(frameId, OpenMode.ForWrite) as Ole2Frame
                    ?? throw new InvalidOperationException("插入结果不是 OLE 图片对象。");

        var width = PixelWidthToDrawingUnits(pixelWidth);
        try
        {
            var extents = frame.GeometricExtents;
            var measuredWidth = extents.MaxPoint.X - extents.MinPoint.X;
            if (double.IsFinite(measuredWidth) && measuredWidth > 0.1)
            {
                width = measuredWidth;
            }

            var target = new Point3d(currentPoint.X, currentPoint.Y, extents.MinPoint.Z);
            frame.TransformBy(Matrix3d.Displacement(target - extents.MinPoint));
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            // PASTECLIP 已使用 currentPoint；几何范围暂不可用时只采用像素宽度计算下一位置。
        }

        transaction.Commit();
        return new Point3d(options.CalculateNextX(currentPoint.X, width), currentPoint.Y, currentPoint.Z);
    }

    private static double PixelWidthToDrawingUnits(int pixelWidth)
    {
        short insertionUnits = 4;
        try
        {
            insertionUnits = Convert.ToInt16(AcadApplication.GetSystemVariable("INSUNITS"));
        }
        catch
        {
            // 无法读取时按毫米计算。
        }

        var millimetersPerDrawingUnit = insertionUnits switch
        {
            1 => 25.4,
            2 => 304.8,
            4 => 1.0,
            5 => 10.0,
            6 => 1000.0,
            _ => 1.0
        };
        return pixelWidth / 96.0 * 25.4 / millimetersPerDrawingUnit;
    }

    private static LayerState? PrepareLayerZero(Database database, Editor editor)
    {
        try
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var table = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            var layerId = table["0"];
            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
            var state = new LayerState(database.Clayer, layerId, layer.IsLocked, layer.IsOff, layer.IsFrozen);
            layer.IsLocked = false;
            layer.IsOff = false;
            layer.IsFrozen = false;
            database.Clayer = layerId;
            transaction.Commit();
            return state;
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[提示] 准备 0 层失败，将使用当前图层: {exception.Message}");
            return null;
        }
    }

    private static void RestoreLayer(Database database, LayerState? state, Editor editor)
    {
        if (state is null)
        {
            return;
        }

        try
        {
            using var transaction = database.TransactionManager.StartTransaction();
            if (!state.OriginalLayerId.IsNull && !state.OriginalLayerId.IsErased)
            {
                database.Clayer = state.OriginalLayerId;
            }

            var layer = transaction.GetObject(state.LayerZeroId, OpenMode.ForWrite) as LayerTableRecord;
            if (layer is not null)
            {
                layer.IsLocked = state.IsLocked;
                layer.IsOff = state.IsOff;
                layer.IsFrozen = state.IsFrozen;
            }

            transaction.Commit();
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[提示] 恢复图层状态失败: {exception.Message}");
        }
    }

    private static bool TrySetClipboardEnhancedMetafile(DrawingImage image, out string error)
    {
        error = string.Empty;
        IntPtr originalHandle = IntPtr.Zero;
        IntPtr clipboardHandle = IntPtr.Zero;
        var clipboardOwnsHandle = false;

        try
        {
            using var referenceBitmap = new Bitmap(1, 1);
            referenceBitmap.SetResolution(96, 96);
            using var referenceGraphics = Graphics.FromImage(referenceBitmap);
            var deviceContext = referenceGraphics.GetHdc();
            Metafile metafile;
            try
            {
                metafile = new Metafile(
                    deviceContext,
                    new Rectangle(0, 0, image.Width, image.Height),
                    MetafileFrameUnit.Pixel,
                    EmfType.EmfPlusDual);
            }
            finally
            {
                referenceGraphics.ReleaseHdc(deviceContext);
            }

            using (metafile)
            {
                using (var graphics = Graphics.FromImage(metafile))
                {
                    graphics.PageUnit = GraphicsUnit.Pixel;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                }

                originalHandle = metafile.GetHenhmetafile();
                clipboardHandle = CopyEnhMetaFile(originalHandle, null);
            }

            if (clipboardHandle == IntPtr.Zero)
            {
                error = "创建增强型图元文件失败。";
                return false;
            }

            var metafileBytes = GetEnhMetaFileBits(clipboardHandle, 0, IntPtr.Zero);
            if (metafileBytes > MaximumMetafileBytes)
            {
                error = $"图片转换后体积为 {metafileBytes / 1024d / 1024d:F2} MB，超出 15 MB 限制。";
                return false;
            }

            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        if (EmptyClipboard() &&
                            SetClipboardData(ClipboardEnhancedMetafile, clipboardHandle) != IntPtr.Zero)
                        {
                            clipboardOwnsHandle = true;
                            return true;
                        }
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }

                Thread.Sleep(50);
            }

            error = "写入系统剪贴板失败。";
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (originalHandle != IntPtr.Zero)
            {
                DeleteEnhMetaFile(originalHandle);
            }

            if (!clipboardOwnsHandle && clipboardHandle != IntPtr.Zero)
            {
                DeleteEnhMetaFile(clipboardHandle);
            }
        }
    }

    private static void StartDialogWatcher(CancellationToken cancellationToken)
    {
        _ = Task.Run(() =>
        {
            var titles = new[] { "OLE 文字大小", "OLE Text Size" };
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var title in titles)
                {
                    var window = FindWindow(null, title);
                    if (window != IntPtr.Zero)
                    {
                        SendMessage(window, WindowMessageCommand, (IntPtr)DialogOk, IntPtr.Zero);
                    }
                }

                Thread.Sleep(50);
            }
        }, cancellationToken);
    }

    private static bool TryGetSystemVariable(string name, out object? value)
    {
        try
        {
            value = AcadApplication.GetSystemVariable(name);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static void TrySetSystemVariable(string name, object value)
    {
        try
        {
            AcadApplication.SetSystemVariable(name, value);
        }
        catch
        {
            // 不支持的系统变量不应阻断图片导入。
        }
    }

    private static void RestoreSystemVariable(string name, object? value)
    {
        if (value is not null)
        {
            TrySetSystemVariable(name, value);
        }
    }

    private sealed record LayerState(
        ObjectId OriginalLayerId,
        ObjectId LayerZeroId,
        bool IsLocked,
        bool IsOff,
        bool IsFrozen);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CopyEnhMetaFile(IntPtr source, string? fileName);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteEnhMetaFile(IntPtr metafile);

    [DllImport("gdi32.dll")]
    private static extern uint GetEnhMetaFileBits(IntPtr metafile, uint bufferSize, IntPtr buffer);
}
