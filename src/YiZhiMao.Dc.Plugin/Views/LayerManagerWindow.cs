using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Autodesk.AutoCAD.DatabaseServices;
using TextBox = System.Windows.Controls.TextBox;

namespace YiZhiMao.Dc.Plugin.Views;

internal sealed class LayerManagerWindow : Window
{
    private readonly Database database;
    private readonly ObservableCollection<LayerRow> rows = [];
    private readonly ICollectionView view;
    private readonly TextBox searchBox = new() { Height = 28, Margin = new Thickness(0, 0, 8, 0) };

    public LayerManagerWindow(Database database)
    {
        this.database = database;
        Title = "一只猫dc 图层管理器";
        Width = 520;
        Height = 650;
        MinWidth = 430;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/YiZhiMao.Dc.Plugin;component/Themes/ToolboxTheme.xaml", UriKind.RelativeOrAbsolute)
        });
        Background = (Brush)FindResource("ThemePageBrush");
        FontFamily = new FontFamily("Microsoft YaHei UI");
        searchBox.Style = (Style)FindResource("DcTextBoxStyle");
        searchBox.Height = 28;

        LoadRows();
        view = CollectionViewSource.GetDefaultView(rows);
        searchBox.TextChanged += (_, _) => view.Refresh();
        view.Filter = item => item is LayerRow row &&
            (string.IsNullOrWhiteSpace(searchBox.Text) || Compatibility.Contains(row.Name, searchBox.Text, StringComparison.OrdinalIgnoreCase));

        var grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = new Grid();
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.Children.Add(searchBox);
        var restore = new Button
        {
            Content = "恢复全部",
            Height = 28,
            Padding = new Thickness(12, 2, 12, 2),
            Style = (Style)FindResource("DcButtonStyle")
        };
        restore.Click += (_, _) => { foreach (var row in rows) { row.Hidden = false; row.Isolate = false; } };
        Grid.SetColumn(restore, 1);
        toolbar.Children.Add(restore);
        grid.Children.Add(toolbar);

        var dataGrid = new DataGrid
        {
            ItemsSource = view,
            Style = (Style)FindResource("DcDataGridStyle"),
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0, 8, 0, 8)
        };
        dataGrid.Columns.Add(new DataGridTextColumn { Header = "图层名称", Binding = new Binding(nameof(LayerRow.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120, IsReadOnly = true });
        dataGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "隐藏", Binding = new Binding(nameof(LayerRow.Hidden)), Width = 66, MinWidth = 66 });
        dataGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "选择只显示", Binding = new Binding(nameof(LayerRow.Isolate)), Width = 92, MinWidth = 92 });
        Grid.SetRow(dataGrid, 1);
        grid.Children.Add(dataGrid);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button
        {
            Content = "取消",
            Width = 84,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true,
            Style = (Style)FindResource("DcButtonStyle")
        };
        var apply = new Button
        {
            Content = "应用",
            Width = 84,
            Height = 30,
            IsDefault = true,
            Style = (Style)FindResource("DcPrimaryButtonStyle")
        };
        apply.Click += (_, _) => ApplyAndClose();
        actions.Children.Add(cancel);
        actions.Children.Add(apply);
        Grid.SetRow(actions, 2);
        grid.Children.Add(actions);
        Content = grid;
    }

    private void LoadRows()
    {
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        foreach (ObjectId id in layers)
        {
            var layer = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForRead);
            rows.Add(new LayerRow(layer.Name, layer.IsOff));
        }
    }

    private void ApplyAndClose()
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var isolated = new HashSet<string>(
            rows.Where(row => row.Isolate).Select(row => row.Name),
            StringComparer.OrdinalIgnoreCase);
        if (layers.Has("0"))
        {
            database.Clayer = layers["0"];
        }

        foreach (var row in rows)
        {
            if (!layers.Has(row.Name))
            {
                continue;
            }

            var layer = (LayerTableRecord)transaction.GetObject(layers[row.Name], OpenMode.ForWrite);
            layer.IsOff = isolated.Count > 0 ? !isolated.Contains(row.Name) : row.Hidden;
        }

        transaction.Commit();
        DialogResult = true;
    }

    private sealed class LayerRow : INotifyPropertyChanged
    {
        private bool hidden;
        private bool isolate;

        public LayerRow(string name, bool hidden)
        {
            Name = name;
            this.hidden = hidden;
        }

        public string Name { get; }

        public bool Hidden
        {
            get => hidden;
            set { hidden = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hidden))); }
        }

        public bool Isolate
        {
            get => isolate;
            set { isolate = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Isolate))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
