using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Image = System.Windows.Controls.Image;

namespace YiZhiMao.Dc.Plugin.Views;

public partial class SuperLibraryWindow : Window
{
    private readonly ObservableCollection<SuperLibraryCategoryRow> categories = [];
    private readonly ObservableCollection<SuperLibraryItemRow> visibleItems = [];
    private IReadOnlyList<SuperLibraryCategory> catalog = [];

    public SuperLibraryWindow()
    {
        InitializeComponent();
        CategoryList.ItemsSource = categories;
        LibraryItemsControl.ItemsSource = visibleItems;
        Loaded += (_, _) => RefreshCatalog();
    }

    internal void RefreshCatalog()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RefreshCatalog);
            return;
        }

        var selectedCategory = (CategoryList.SelectedItem as SuperLibraryCategoryRow)?.Name ??
                               SuperLibraryRuntime.SelectedCategoryName;
        try
        {
            catalog = SuperLibraryCatalog.Scan(SuperLibraryRuntime.RootDirectory);
            categories.Clear();
            foreach (var category in catalog)
            {
                categories.Add(new SuperLibraryCategoryRow(category.Name, category.Items.Count));
            }

            CategoryList.SelectedItem = categories.FirstOrDefault(category =>
                string.Equals(category.Name, selectedCategory, StringComparison.OrdinalIgnoreCase)) ??
                categories.FirstOrDefault();
            if (CategoryList.SelectedItem is null)
            {
                ShowCategory(null);
            }

            SetStatus($"已读取 {categories.Count} 个分类、{catalog.Sum(category => category.Items.Count)} 个 DWG。", false);
        }
        catch (Exception exception)
        {
            SetStatus($"读取图库失败：{exception.Message}", true);
        }
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CategoryList.SelectedItem as SuperLibraryCategoryRow;
        if (selected is not null)
        {
            CategoryNameBox.Text = selected.Name == SuperLibraryCatalog.UnclassifiedCategoryName
                ? string.Empty
                : selected.Name;
            SuperLibraryRuntime.SelectCategory(selected.Name);
        }

        ShowCategory(selected?.Name);
    }

    private void ShowCategory(string? categoryName)
    {
        visibleItems.Clear();
        CurrentCategoryText.Text = categoryName ?? "暂无分类";
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            ItemCountText.Text = "";
            return;
        }

        var category = catalog
            .FirstOrDefault(item => string.Equals(item.Name, categoryName, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            ItemCountText.Text = "0 项";
            return;
        }

        foreach (var item in category.Items)
        {
            visibleItems.Add(new SuperLibraryItemRow(item, LoadPreview(item.PreviewPath), PreviewScaleSlider.Value));
        }

        ItemCountText.Text = $"{visibleItems.Count} 项";
    }

    private void OnLibraryItemClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (sender is not FrameworkElement { Tag: SuperLibraryItemRow row })
        {
            return;
        }

        SuperLibraryRuntime.SelectItem(row.Item);
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetStatus("当前没有可用的 CAD 图纸。", true);
            return;
        }

        document.SendStringToExecute("DC_CMD_SUPER_LIBRARY_INSERT ", true, false, true);
        SetStatus($"正在插入：{row.DisplayName}", false);
    }

    private void OnLibraryItemPreviewClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SuperLibraryItemRow row } || row.PreviewImage is null)
        {
            return;
        }

        e.Handled = true;
        var preview = new Window
        {
            Title = row.DisplayName,
            Width = 900,
            Height = 680,
            MinWidth = 480,
            MinHeight = 360,
            Owner = this,
            Background = new SolidColorBrush(Color.FromRgb(32, 37, 43)),
            Content = new Image
            {
                Source = row.PreviewImage,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(12)
            }
        };
        preview.Show();
    }

    private void OnItemCheckPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is CheckBox checkBox && checkBox.DataContext is SuperLibraryItemRow row)
        {
            row.IsSelected = !row.IsSelected;
        }
    }

    private void OnPreviewScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        foreach (var item in visibleItems)
        {
            item.SetCardWidth(e.NewValue);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshCatalog();

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        var root = SuperLibraryRuntime.RootDirectory;
        Directory.CreateDirectory(root);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"")
        {
            UseShellExecute = true
        });
    }

    private void OnSaveSelectionClick(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not SuperLibraryCategoryRow category)
        {
            SetStatus("请先选择保存分类。", true);
            return;
        }

        SuperLibraryRuntime.SelectCategory(category.Name);
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetStatus("当前没有可用的 CAD 图纸。", true);
            return;
        }

        document.SendStringToExecute("DC_CMD_SUPER_LIBRARY_SAVE ", true, false, true);
    }

    private void OnAddCategoryClick(object sender, RoutedEventArgs e)
    {
        RunCatalogChange(() => SuperLibraryCatalog.CreateCategory(
            SuperLibraryRuntime.RootDirectory,
            CategoryNameBox.Text));
    }

    private void OnRenameCategoryClick(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not SuperLibraryCategoryRow selected ||
            selected.Name == SuperLibraryCatalog.UnclassifiedCategoryName)
        {
            SetStatus("请选择可改名的文件夹分类。", true);
            return;
        }

        RunCatalogChange(() => SuperLibraryCatalog.RenameCategory(
            SuperLibraryRuntime.RootDirectory,
            selected.Name,
            CategoryNameBox.Text));
    }

    private void OnDeleteCategoryClick(object sender, RoutedEventArgs e)
    {
        if (CategoryList.SelectedItem is not SuperLibraryCategoryRow selected ||
            selected.Name == SuperLibraryCatalog.UnclassifiedCategoryName)
        {
            SetStatus("请选择可删除的空分类。", true);
            return;
        }

        if (MessageBox.Show(
                this,
                $"确定删除空分类“{selected.Name}”吗？",
                "删除分类",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        RunCatalogChange(() => SuperLibraryCatalog.DeleteCategory(
            SuperLibraryRuntime.RootDirectory,
            selected.Name));
    }

    private void OnBatchDeleteClick(object sender, RoutedEventArgs e)
    {
        var selected = visibleItems.Where(item => item.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            SetStatus("请先勾选需要删除的图库项目。", true);
            return;
        }

        if (MessageBox.Show(
                this,
                $"将永久删除 {selected.Length} 个 DWG 及其同名预览图，是否继续？",
                "批量删除图库项目",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        RunCatalogChange(() => SuperLibraryCatalog.DeleteItems(
            SuperLibraryRuntime.RootDirectory,
            selected.Select(item => item.Item.DwgPath)));
    }

    private void RunCatalogChange(Action change)
    {
        try
        {
            change();
            RefreshCatalog();
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private static ImageSource? LoadPreview(string? previewPath)
    {
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(previewPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 720;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(196, 59, 50))
            : new SolidColorBrush(Color.FromRgb(104, 112, 124));
        StatusText.Text = message;
    }

    private sealed record SuperLibraryCategoryRow(string Name, int ItemCount);

    private sealed class SuperLibraryItemRow : INotifyPropertyChanged
    {
        private bool isSelected;
        private double cardWidth;

        public SuperLibraryItemRow(SuperLibraryItem item, ImageSource? previewImage, double width)
        {
            Item = item;
            PreviewImage = previewImage;
            SetCardWidth(width);
        }

        public SuperLibraryItem Item { get; }

        public string DisplayName => Item.Name;

        public ImageSource? PreviewImage { get; }

        public double CardWidth
        {
            get => cardWidth;
            private set
            {
                cardWidth = value;
                OnPropertyChanged();
            }
        }

        public double CardHeight => CardWidth * 0.78;

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetCardWidth(double width)
        {
            CardWidth = width;
            OnPropertyChanged(nameof(CardHeight));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

internal static class SuperLibraryWindowManager
{
    private static SuperLibraryWindow? window;

    public static void Show()
    {
        if (window is null)
        {
            window = new SuperLibraryWindow();
            window.Closed += (_, _) => window = null;
            AcadApplication.ShowModelessWindow(window);
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.RefreshCatalog();
        window.Activate();
    }

    public static void Refresh()
    {
        window?.RefreshCatalog();
    }

    public static void Dispose()
    {
        window?.Close();
        window = null;
    }
}
