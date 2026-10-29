using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Views;

public partial class PdfToolWindow : Window
{
    private readonly ObservableCollection<PdfFileItem> files = [];

    public PdfToolWindow()
    {
        InitializeComponent();
        PdfGrid.ItemsSource = files;
    }

    private void OnScanClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择包含 PDF 的文件夹" };
        if (Directory.Exists(SourceFolderBox.Text))
        {
            dialog.InitialDirectory = SourceFolderBox.Text;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SourceFolderBox.Text = dialog.FolderName;
        if (string.IsNullOrWhiteSpace(OutputFolderBox.Text))
        {
            OutputFolderBox.Text = dialog.FolderName;
        }

        try
        {
            files.Clear();
            foreach (var file in PdfToolService.Scan(dialog.FolderName))
            {
                files.Add(file);
            }

            PdfStatusText.Text = $"已扫描 {files.Count} 个 PDF。";
        }
        catch (Exception exception)
        {
            PdfStatusText.Text = $"扫描失败：{exception.Message}";
        }
    }

    private void OnChooseOutputClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择合并文件的保存目录" };
        if (Directory.Exists(OutputFolderBox.Text))
        {
            dialog.InitialDirectory = OutputFolderBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            OutputFolderBox.Text = dialog.FolderName;
        }
    }

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var file in files)
        {
            file.IsSelected = true;
        }

        PdfGrid.Items.Refresh();
    }

    private void OnInvertClick(object sender, RoutedEventArgs e)
    {
        foreach (var file in files)
        {
            file.IsSelected = !file.IsSelected;
        }

        PdfGrid.Items.Refresh();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        files.Clear();
        PdfStatusText.Text = "列表已清空。";
    }

    private void OnMergeClick(object sender, RoutedEventArgs e)
    {
        var selected = files.Where(file => file.IsSelected).Select(file => file.Path).ToList();
        if (selected.Count == 0)
        {
            PdfStatusText.Text = "请至少勾选一个 PDF。";
            return;
        }

        if (!Directory.Exists(OutputFolderBox.Text))
        {
            PdfStatusText.Text = "请选择有效的输出目录。";
            return;
        }

        var outputPath = Path.Combine(OutputFolderBox.Text, $"合并文件_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        try
        {
            var pages = PdfToolService.Merge(selected, outputPath);
            PdfStatusText.Text = $"合并完成：{pages} 页，已保存到 {outputPath}";
            try
            {
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }
            catch
            {
                PdfStatusText.Text += "；但系统未能自动打开该文件。";
            }
        }
        catch (Exception exception)
        {
            PdfStatusText.Text = $"合并失败：{exception.Message}";
        }
    }
}
