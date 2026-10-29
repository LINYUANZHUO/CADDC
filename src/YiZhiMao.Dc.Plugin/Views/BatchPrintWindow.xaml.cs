using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Autodesk.AutoCAD.ApplicationServices;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Printing;

namespace YiZhiMao.Dc.Plugin.Views;

public partial class BatchPrintWindow : Window
{
    private readonly Document document;
    private readonly bool toPdf;
    private readonly ObservableCollection<CadPrintJob> jobs;

    internal BatchPrintWindow(Document document, IEnumerable<CadPrintJob> jobs, bool toPdf)
    {
        InitializeComponent();
        this.document = document;
        this.toPdf = toPdf;
        this.jobs = new ObservableCollection<CadPrintJob>(jobs);
        JobGrid.ItemsSource = this.jobs;

        var settings = PrintRuntime.Settings.Clone();
        var devices = toPdf ? CadPlotService.GetDevices() : CadPlotService.GetPaperDevices();
        PrinterCombo.ItemsSource = devices;
        var plotStyles = CadPlotService.GetPlotStyles(document.Database);
        PlotStyleCombo.ItemsSource = plotStyles;
        PlotStyleCombo.Text = plotStyles.FirstOrDefault(value =>
            value.Equals(settings.PlotStyle, StringComparison.OrdinalIgnoreCase)) ?? plotStyles.FirstOrDefault() ?? string.Empty;
        MarginBox.Text = settings.Margin.ToString("0.###", CultureInfo.CurrentCulture);
        CollateCheck.IsChecked = settings.Collate;
        StartButton.Content = toPdf ? "开始输出 PDF" : "开始打印";
        if (devices.Count > 0)
        {
            var preferredPrinter = PrintOptionSelector.SelectRequired(
                toPdf ? ["DWG To PDF.pc3", settings.Printer] : [settings.Printer, GetDefaultPrinter()],
                devices,
                "打印设备");
            PrinterCombo.SelectedItem = preferredPrinter;
            PrinterCombo.Text = preferredPrinter;
            RefreshMedia(settings.Paper);
        }
        else
        {
            StatusText.Text = "未检测到实体打印机；仍可使用预览，实际出纸前请启用 Print Spooler 并安装打印机。";
        }
    }

    private void OnPrinterChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshMedia(BulkPaperCombo.Text);
    }

    private void RefreshMedia(string preferred)
    {
        try
        {
            var media = CadPlotService.GetMedia(document, PrinterCombo.Text);
            var selected = PrintOptionSelector.SelectRequired(
                [preferred, BulkPaperCombo.Text],
                media,
                "纸张");
            BulkPaperCombo.ItemsSource = media;
            BulkPaperCombo.Text = selected;
            foreach (var job in jobs.Where(job => !media.Contains(job.Paper, StringComparer.OrdinalIgnoreCase)))
            {
                job.Paper = selected;
            }

            JobGrid.Items.Refresh();
        }
        catch (Exception exception)
        {
            StatusText.Text = $"读取纸张失败：{exception.Message}";
        }
    }

    private void OnApplyBulkClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(BulkCopiesBox.Text, out var copies) || copies < 1)
        {
            StatusText.Text = "批量份数必须是大于 0 的整数。";
            return;
        }

        foreach (var job in jobs.Where(job => job.IsSelected))
        {
            job.Copies = copies;
            job.Paper = BulkPaperCombo.Text.Trim();
        }

        JobGrid.Items.Refresh();
        StatusText.Text = "已将份数和纸张应用到勾选任务。";
    }

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        foreach (var job in jobs.Where(job => job.IsSelected).ToList())
        {
            jobs.Remove(job);
        }

        Reindex();
        StatusText.Text = "已移除勾选任务。";
    }

    private void OnDeleteRowClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CadPrintJob job)
        {
            jobs.Remove(job);
            Reindex();
        }
    }

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CadPrintJob job)
        {
            CreatePreview([job]);
        }
    }

    private void OnPreviewAllClick(object sender, RoutedEventArgs e)
    {
        var selectedJobs = jobs.Where(job => job.IsSelected).ToList();
        if (selectedJobs.Count == 0)
        {
            StatusText.Text = "没有勾选可预览的任务。";
            return;
        }

        CreatePreview(selectedJobs);
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(MarginBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var margin) || margin < 0)
        {
            StatusText.Text = "页边留白必须是非负数。";
            return;
        }

        string? outputDirectory = null;
        if (toPdf)
        {
            var folderDialog = new OpenFolderDialog { Title = "选择 PDF 输出目录" };
            if (folderDialog.ShowDialog(this) != true)
            {
                return;
            }

            outputDirectory = folderDialog.FolderName;
        }

        var settings = PrintRuntime.Settings.Clone();
        settings.Printer = PrinterCombo.Text.Trim();
        settings.Paper = BulkPaperCombo.Text.Trim();
        settings.PlotStyle = PlotStyleCombo.Text.Trim();
        settings.Margin = margin;
        settings.Collate = CollateCheck.IsChecked == true;

        try
        {
            if (!toPdf)
            {
                CadPlotService.EnsurePaperPrintingAvailable();
                var paperDevices = CadPlotService.GetPaperDevices();
                if (paperDevices.Count == 0)
                {
                    throw new InvalidOperationException(
                        "未检测到可用于纸张输出的实体打印机。请先在 Windows 安装打印机并启动 Print Spooler 服务；输出文件请使用 D2。");
                }
            }

            PrintRuntime.Save(settings);
            StartButton.IsEnabled = false;
            var result = CadPlotService.Run(document, jobs, settings, toPdf, outputDirectory);
            StatusText.Text = toPdf
                ? $"已完成 {result.PrintedJobs} 个输出任务。"
                : $"已完成 {result.PrintedJobs} 个打印任务。";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"执行失败：{exception.Message}";
        }
        finally
        {
            StartButton.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CreatePreview(IReadOnlyList<CadPrintJob> previewJobs)
    {
        if (!double.TryParse(MarginBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var margin) || margin < 0)
        {
            StatusText.Text = "页边留白必须是非负数。";
            return;
        }

        var settings = PrintRuntime.Settings.Clone();
        settings.Paper = BulkPaperCombo.Text.Trim();
        settings.PlotStyle = PlotStyleCombo.Text.Trim();
        settings.Margin = margin;

        try
        {
            var previewPath = CadPlotService.CreatePreview(document, previewJobs, settings);
            StatusText.Text = $"预览已生成并打开：{previewPath}";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"预览失败：{exception.Message}";
        }
    }

    private void Reindex()
    {
        for (var index = 0; index < jobs.Count; index++)
        {
            jobs[index].Index = index + 1;
        }

        JobGrid.Items.Refresh();
    }

    private static string GetDefaultPrinter()
    {
        try
        {
            return new PrinterSettings().PrinterName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
