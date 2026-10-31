using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin.Printing;

internal sealed record PrintRunResult(int PrintedJobs, string? OutputDirectory, string? MergedPdfPath);

internal static class CadPlotService
{
    public static void EnsurePaperPrintingAvailable()
    {
        try
        {
            using var spooler = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Spooler");
            if (spooler?.GetValue("Start") is int startMode && startMode == 4)
            {
                throw new InvalidOperationException(
                    "Windows 打印后台处理程序已被禁用。请先启用并启动 Print Spooler 服务，再使用 DCD1 打印纸张。");
            }
        }
        catch (System.Security.SecurityException)
        {
            // If service configuration is unreadable, let AutoCAD enumerate its available devices.
        }
        catch (UnauthorizedAccessException)
        {
            // If service configuration is unreadable, let AutoCAD enumerate its available devices.
        }
    }

    public static IReadOnlyList<string> GetDevices()
    {
        return ToList(PlotSettingsValidator.Current.GetPlotDeviceList());
    }

    public static IReadOnlyList<string> GetPaperDevices()
    {
        var devices = new List<string>();
        foreach (var device in GetDevices().Where(device =>
                     !device.Equals("None", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var configuration = PlotConfigManager.SetCurrentConfig(device);
                if (configuration.PlotToFileCapability != PlotToFileCapability.MustPlotToFile)
                {
                    devices.Add(device);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Unavailable Windows printers are skipped; PDF preview remains available.
            }
        }

        return devices;
    }

    public static IReadOnlyList<string> GetPdfDevices()
    {
        var devices = new List<string>();
        foreach (var device in GetDevices().Where(device =>
                     !device.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                     device.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            try
            {
                using var configuration = PlotConfigManager.SetCurrentConfig(device);
                if (configuration.PlotToFileCapability == PlotToFileCapability.MustPlotToFile)
                {
                    devices.Add(device);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Unavailable PDF configurations are not valid output devices.
            }
        }

        return devices
            .OrderByDescending(device => device.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase))
            .ThenBy(device => device, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> GetPlotStyles(Database database)
    {
        var extension = database.PlotStyleMode ? ".ctb" : ".stb";
        return ToList(PlotSettingsValidator.Current.GetPlotStyleSheetList())
            .Where(value => Path.GetExtension(value).Equals(extension, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<string> GetMedia(Document document, string device)
    {
        if (string.IsNullOrWhiteSpace(device))
        {
            return [];
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var currentSpace = (BlockTableRecord)transaction.GetObject(
            document.Database.CurrentSpaceId,
            OpenMode.ForRead);
        var layout = (Layout)transaction.GetObject(currentSpace.LayoutId, OpenMode.ForRead);
        using var plotSettings = new PlotSettings(layout.ModelType);
        plotSettings.CopyFrom(layout);
        return ConfigureDeviceAndGetMedia(plotSettings, device);
    }

    public static PrintRunResult Run(
        Document document,
        IReadOnlyList<CadPrintJob> jobs,
        PrintSettings settings,
        bool toPdf,
        string? outputDirectory)
    {
        settings.Validate();
        var selectedJobs = jobs.Where(job => job.IsSelected && job.Copies > 0).ToList();
        if (selectedJobs.Count == 0)
        {
            throw new InvalidOperationException("没有可打印的任务。");
        }

        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
        {
            throw new InvalidOperationException("AutoCAD 正在执行其他打印任务，请稍后再试。");
        }

        var previousBackgroundPlot = AcadApplication.GetSystemVariable("BACKGROUNDPLOT");
        string? temporarySinglesDirectory = null;
        AcadApplication.SetSystemVariable("BACKGROUNDPLOT", 0);
        try
        {
            var individualPdfs = new List<string>();
            if (toPdf)
            {
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new InvalidOperationException("请选择 PDF 输出目录。");
                }

                Directory.CreateDirectory(outputDirectory);
            }

            var runTag = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}".Substring(0, 28);
            var singleOutputDirectory = outputDirectory;
            if (toPdf && settings.PdfOutputMode == PdfOutputMode.MergedOnly)
            {
                temporarySinglesDirectory = Path.Combine(outputDirectory!, $".YiZhiMaoDc-{runTag}");
                Directory.CreateDirectory(temporarySinglesDirectory);
                singleOutputDirectory = temporarySinglesDirectory;
            }

            var printed = 0;
            var plan = PrintCopyPlanner.Build(selectedJobs.Select(job => job.Copies).ToList(), settings.Collate);
            foreach (var step in plan)
            {
                var job = selectedJobs[step.JobPosition];
                var outputPath = toPdf
                    ? CreateOutputPath(singleOutputDirectory!, document.Name, job, step.CopyIndex, job.Copies, runTag)
                    : null;
                PlotOne(document, job, settings, outputPath);
                if (outputPath is not null)
                {
                    individualPdfs.Add(outputPath);
                }

                printed++;
            }

            string? mergedPath = null;
            if (toPdf && settings.PdfOutputMode is PdfOutputMode.MergedOnly or PdfOutputMode.SeparateAndMerged)
            {
                mergedPath = Path.Combine(outputDirectory!, $"{Path.GetFileNameWithoutExtension(document.Name)}_合并_{runTag}.pdf");
                PdfToolService.Merge(individualPdfs, mergedPath);
            }

            if (settings.OpenMergedPdf && mergedPath is not null)
            {
                TryOpen(mergedPath);
            }
            if (settings.OpenOutputFolder && outputDirectory is not null)
            {
                TryOpen(outputDirectory);
            }

            return new PrintRunResult(printed, outputDirectory, mergedPath);
        }
        finally
        {
            if (temporarySinglesDirectory is not null)
            {
                TryDeleteDirectory(temporarySinglesDirectory);
            }

            AcadApplication.SetSystemVariable("BACKGROUNDPLOT", previousBackgroundPlot);
        }
    }

    public static string CreatePreview(
        Document document,
        IReadOnlyList<CadPrintJob> jobs,
        PrintSettings settings)
    {
        if (jobs.Count == 0)
        {
            throw new InvalidOperationException("没有可预览的任务。");
        }

        var previewJobs = jobs.Select(job => new CadPrintJob
        {
            Index = job.Index,
            LayoutId = job.LayoutId,
            LayoutName = job.LayoutName,
            Frame = job.Frame,
            Copies = 1,
            Paper = job.Paper,
            IsSelected = true
        }).ToList();
        var previewSettings = settings.Clone();
        previewSettings.Printer = PrintOptionSelector.SelectRequired(
            ["DWG To PDF.pc3"],
            GetPdfDevices(),
            "PDF打印设备");
        previewSettings.PdfOutputMode = PdfOutputMode.MergedOnly;
        previewSettings.OpenMergedPdf = false;
        previewSettings.OpenOutputFolder = false;
        previewSettings.Collate = true;

        var previewRoot = Path.Combine(Path.GetTempPath(), "YiZhiMaoDc", "Preview");
        CleanupOldPreviewDirectories(previewRoot);
        var previewDirectory = Path.Combine(
            previewRoot,
            $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}");
        var result = Run(document, previewJobs, previewSettings, toPdf: true, previewDirectory);
        var previewPath = result.MergedPdfPath;
        if (previewPath is null || !File.Exists(previewPath))
        {
            throw new InvalidOperationException("预览PDF生成失败。");
        }

        if (!TryOpen(previewPath))
        {
            throw new InvalidOperationException($"预览PDF已生成，但无法自动打开：{previewPath}");
        }

        return previewPath;
    }

    public static void ZoomTo(Document document, CadPrintJob job)
    {
        if (job.Frame is not { } frame)
        {
            return;
        }

        var view = document.Editor.GetCurrentView();
        view.CenterPoint = new Point2d((frame.MinX + frame.MaxX) / 2, (frame.MinY + frame.MaxY) / 2);
        view.Width = frame.Width * 1.12;
        view.Height = frame.Height * 1.12;
        document.Editor.SetCurrentView(view);
    }

    public static void ZoomTo(Document document, IReadOnlyList<CadPrintJob> jobs)
    {
        var frames = jobs.Where(job => job.Frame is not null).Select(job => job.Frame!.Value).ToList();
        if (frames.Count == 0)
        {
            return;
        }

        var combined = new PrintFrame(
            frames.Min(frame => frame.MinX),
            frames.Min(frame => frame.MinY),
            frames.Max(frame => frame.MaxX),
            frames.Max(frame => frame.MaxY));
        ZoomTo(document, new CadPrintJob
        {
            Index = 0,
            LayoutId = jobs[0].LayoutId,
            LayoutName = jobs[0].LayoutName,
            Frame = combined
        });
    }

    private static void PlotOne(Document document, CadPrintJob job, PrintSettings settings, string? outputPath)
    {
        var currentStage = "读取布局";
        try
        {
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var layout = (Layout)transaction.GetObject(job.LayoutId, OpenMode.ForRead);
            using var plotSettings = new PlotSettings(layout.ModelType);
            plotSettings.CopyFrom(layout);

            currentStage = "配置打印设备和纸张";
            var device = GetDevices().FirstOrDefault(value =>
                value.Equals(settings.Printer, StringComparison.OrdinalIgnoreCase));
            if (device is null)
            {
                throw new InvalidOperationException($"找不到打印设备“{settings.Printer}”。");
            }

            var validator = PlotSettingsValidator.Current;
            var previousPaper = plotSettings.CanonicalMediaName;
            var media = ConfigureDeviceAndGetMedia(plotSettings, device);
            var paper = PrintOptionSelector.SelectRequired(
                [job.Paper, settings.Paper, previousPaper],
                media,
                "纸张");
            validator.SetPlotConfigurationName(plotSettings, device, paper);
            validator.RefreshLists(plotSettings);

            currentStage = "配置打印样式";
            if (!PrintOptionSelector.IsPlotStyleCompatible(settings.PlotStyle, document.Database.PlotStyleMode))
            {
                var expected = document.Database.PlotStyleMode ? "CTB" : "STB";
                throw new InvalidOperationException($"当前图纸只能使用 {expected} 打印样式，不能使用“{settings.PlotStyle}”。");
            }

            if (!string.IsNullOrWhiteSpace(settings.PlotStyle))
            {
                var plotStyle = GetPlotStyles(document.Database).FirstOrDefault(value =>
                    value.Equals(settings.PlotStyle, StringComparison.OrdinalIgnoreCase));
                if (plotStyle is null)
                {
                    throw new InvalidOperationException($"找不到打印样式“{settings.PlotStyle}”。");
                }

                validator.SetCurrentStyleSheet(plotSettings, plotStyle);
            }

            plotSettings.PlotPlotStyles = !string.IsNullOrWhiteSpace(settings.PlotStyle);
            plotSettings.PrintLineweights = settings.PlotLineweights;
            plotSettings.PlotTransparency = settings.PlotTransparency;
            plotSettings.ShadePlotResLevel = ShadePlotResLevel.Custom;
            plotSettings.ShadePlotCustomDpi = (short)Math.Min(settings.ImageDpi, short.MaxValue);

            if (job.Frame is { } frame)
            {
                frame = new PrintFrame(
                    frame.MinX - settings.Margin,
                    frame.MinY - settings.Margin,
                    frame.MaxX + settings.Margin,
                    frame.MaxY + settings.Margin,
                    frame.SourceKey);
                currentStage = "转换打印窗口范围";
                var plotWindow = ToDisplayCoordinates(document, frame);

                currentStage = "准备打印窗口类型";
                try
                {
                    validator.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Extents);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception)
                {
                    // Some layouts already reject Extents; the window area can still be assigned directly.
                }

                currentStage = "设置打印窗口范围";
                validator.SetPlotWindowArea(plotSettings, plotWindow);

                currentStage = "设置窗口打印类型";
                validator.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);

                currentStage = "设置适合图纸缩放";
                validator.SetUseStandardScale(plotSettings, true);
                validator.SetStdScaleType(plotSettings, StdScaleType.ScaleToFit);

                currentStage = "设置打印居中";
                validator.SetPlotCentered(plotSettings, true);

                currentStage = "设置打印方向";
                var paperSize = plotSettings.PlotPaperSize;
                var frameIsLandscape = frame.Width >= frame.Height;
                var paperIsLandscape = paperSize.X >= paperSize.Y;
                validator.SetPlotRotation(plotSettings, frameIsLandscape == paperIsLandscape
                    ? PlotRotation.Degrees000
                    : PlotRotation.Degrees090);
            }
            else
            {
                validator.SetPlotType(plotSettings, Autodesk.AutoCAD.DatabaseServices.PlotType.Layout);
            }

            currentStage = "校验打印参数";
            using var plotInfo = new PlotInfo
            {
                Layout = job.LayoutId,
                OverrideSettings = plotSettings
            };
            using var validatorInfo = new PlotInfoValidator
            {
                MediaMatchingPolicy = MatchingPolicy.MatchEnabled
            };
            validatorInfo.Validate(plotInfo);

            currentStage = "生成打印输出";
            using var progress = new PlotProgressDialog(false, 1, true);
            using var engine = PlotFactory.CreatePublishEngine();
            var progressStarted = false;
            var plotStarted = false;
            var documentStarted = false;
            var pageStarted = false;
            var graphicsStarted = false;
            try
            {
                progress.set_PlotMsgString(PlotMessageIndex.DialogTitle, "一只猫dc 批量打印");
                progress.set_PlotMsgString(PlotMessageIndex.CancelJobButtonMessage, "取消");
                progress.set_PlotMsgString(PlotMessageIndex.SheetSetProgressCaption, "打印进度");
                progress.set_PlotMsgString(PlotMessageIndex.SheetProgressCaption, job.Notes);
                progress.LowerPlotProgressRange = 0;
                progress.UpperPlotProgressRange = 100;
                progress.PlotProgressPos = 0;
                progress.OnBeginPlot();
                progressStarted = true;
                progress.IsVisible = true;

                engine.BeginPlot(progress, null);
                plotStarted = true;
                engine.BeginDocument(plotInfo, document.Name, null, 1, outputPath is not null, outputPath);
                documentStarted = true;
                using var pageInfo = new PlotPageInfo();
                engine.BeginPage(pageInfo, plotInfo, true, null);
                pageStarted = true;
                engine.BeginGenerateGraphics(null);
                graphicsStarted = true;
                engine.EndGenerateGraphics(null);
                graphicsStarted = false;
                engine.EndPage(null);
                pageStarted = false;
                engine.EndDocument(null);
                documentStarted = false;
                progress.PlotProgressPos = 100;
                engine.EndPlot(null);
                plotStarted = false;
                progress.OnEndPlot();
                progressStarted = false;
            }
            finally
            {
                if (graphicsStarted)
                {
                    TryCompletePlotStage(() => engine.EndGenerateGraphics(null));
                }
                if (pageStarted)
                {
                    TryCompletePlotStage(() => engine.EndPage(null));
                }
                if (documentStarted)
                {
                    TryCompletePlotStage(() => engine.EndDocument(null));
                }
                if (plotStarted)
                {
                    TryCompletePlotStage(() => engine.EndPlot(null));
                }
                if (progressStarted)
                {
                    TryCompletePlotStage(progress.OnEndPlot);
                }
            }
            transaction.Commit();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception exception)
        {
            throw new InvalidOperationException(
                $"打印任务 {job.Index}（{job.Notes}）失败；当前阶段：{currentStage}；AutoCAD：{exception.ErrorStatus}。",
                exception);
        }
    }

    private static Extents2d ToDisplayCoordinates(Document document, PrintFrame frame)
    {
        using var view = document.Editor.GetCurrentView();
        var dcsToWcs = Matrix3d.PlaneToWorld(view.ViewDirection);
        dcsToWcs = Matrix3d.Displacement(view.Target - Point3d.Origin) * dcsToWcs;
        dcsToWcs = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * dcsToWcs;
        var wcsToDcs = dcsToWcs.Inverse();
        var extents = new Extents3d(
            new Point3d(frame.MinX, frame.MinY, 0),
            new Point3d(frame.MaxX, frame.MaxY, 0));
        extents.TransformBy(wcsToDcs);

        var result = new Extents2d(
            extents.MinPoint.X,
            extents.MinPoint.Y,
            extents.MaxPoint.X,
            extents.MaxPoint.Y);
        if (!Compatibility.IsFinite(result.MinPoint.X) || !Compatibility.IsFinite(result.MinPoint.Y) ||
            !Compatibility.IsFinite(result.MaxPoint.X) || !Compatibility.IsFinite(result.MaxPoint.Y) ||
            result.MaxPoint.X <= result.MinPoint.X || result.MaxPoint.Y <= result.MinPoint.Y)
        {
            throw new InvalidOperationException("图框转换后的打印窗口范围无效。");
        }

        return result;
    }

    private static IReadOnlyList<string> ConfigureDeviceAndGetMedia(PlotSettings plotSettings, string device)
    {
        var validator = PlotSettingsValidator.Current;
        validator.SetPlotConfigurationName(plotSettings, device, null);
        validator.RefreshLists(plotSettings);
        return ToList(validator.GetCanonicalMediaNameList(plotSettings));
    }

    private static string CreateOutputPath(
        string directory,
        string drawingName,
        CadPrintJob job,
        int copyIndex,
        int copyCount,
        string runTag)
    {
        var copySuffix = copyCount > 1 ? $"_{copyIndex + 1}" : string.Empty;
        var fileName = $"{Path.GetFileNameWithoutExtension(drawingName)}_{job.Index:000}_{Sanitize(job.LayoutName)}{copySuffix}_{runTag}.pdf";
        return Path.Combine(directory, fileName);
    }

    private static void TryCompletePlotStage(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Preserve the original plot/cancellation exception while releasing native plot state.
        }
    }

    private static void CleanupOldPreviewDirectories(string previewRoot)
    {
        if (!Directory.Exists(previewRoot))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-7);
        foreach (var directory in Directory.EnumerateDirectories(previewRoot))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
                // An opened preview can remain locked; retry during a later preview.
            }
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // A PDF driver can briefly retain a file handle; a later preview cleanup can remove it.
        }
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }

    private static bool TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ToList(StringCollection values)
    {
        return values.Cast<string>().ToList();
    }
}
