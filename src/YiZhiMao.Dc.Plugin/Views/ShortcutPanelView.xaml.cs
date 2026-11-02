using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Printing;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using ComboBox = System.Windows.Controls.ComboBox;
using Image = System.Windows.Controls.Image;
using LayerTable = Autodesk.AutoCAD.DatabaseServices.LayerTable;
using LayerTableRecord = Autodesk.AutoCAD.DatabaseServices.LayerTableRecord;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using TextBox = System.Windows.Controls.TextBox;

namespace YiZhiMao.Dc.Plugin.Views;

public partial class ShortcutPanelView : UserControl
{
    private const int DefaultPointStyleMode = 35;
    private static readonly PointStyleOption[] PointStyleOptions =
    [
        new(0, "点"),
        new(1, "不显示"),
        new(2, "十字"),
        new(3, "叉"),
        new(4, "竖线"),
        new(32, "圆＋点"),
        new(33, "圆"),
        new(34, "圆＋十字"),
        new(35, "圆＋叉（默认）"),
        new(36, "圆＋竖线"),
        new(64, "方框＋点"),
        new(65, "方框"),
        new(66, "方框＋十字"),
        new(67, "方框＋叉"),
        new(68, "方框＋竖线"),
        new(96, "圆框＋方框＋点"),
        new(97, "圆框＋方框"),
        new(98, "圆框＋方框＋十字"),
        new(99, "圆框＋方框＋叉"),
        new(100, "圆框＋方框＋竖线")
    ];
    private static readonly SaveFileFormatOption[] SaveFileFormatOptions =
    [
        new(DrawingSaveFileFormat.AutoCad2000, "AutoCAD 2000 / LT2000 图形"),
        new(DrawingSaveFileFormat.AutoCad2004, "AutoCAD 2004 / LT2004 图形"),
        new(DrawingSaveFileFormat.AutoCad2007, "AutoCAD 2007 / LT2007 图形"),
        new(DrawingSaveFileFormat.AutoCad2010, "AutoCAD 2010 / LT2010 图形"),
        new(DrawingSaveFileFormat.AutoCad2013, "AutoCAD 2013 / LT2013 图形"),
        new(DrawingSaveFileFormat.AutoCad2018, "AutoCAD 2018 图形")
    ];

    private readonly DispatcherTimer statusClearTimer = new()
    {
        Interval = TimeSpan.FromSeconds(4)
    };
    private ObservableCollection<ShortcutItem> items = [];
    private ICollectionView? cadCommandView;
    private HardwareLibraryFolders? hardwareFolders;
    private string? frameTemplateDirectory;
    private string? lispPluginDirectory;
    private ObservableCollection<RadialMenuItem> radialMenuItems = [];
    private RadialMenuItem? draggedRadialMenuItem;
    private List<ModulePreferenceRow> modulePreferenceRows = [];
    private readonly List<DrawingLayerMappingRow> drawingLayerMappingRows = [];
    private IReadOnlyList<string> drawingLayerNames = [];
    private ToolboxModule selectedModule = ToolboxModule.Preferences;
    private bool isLoadingPreferences;
    private bool isLoadingRadialMenu;

    public ShortcutPanelView()
    {
        InitializeComponent();
        InitializePointStyleOptions();
        InitializeSaveFileFormatOptions();

        statusClearTimer.Tick += OnStatusClearTimerTick;

        LoadPreferencesModule();
        LoadItems();
        LoadCadCommands();
        LoadImageBatchOptions();
        LoadPrintSettings();
        LoadDoorPanelModule();
        Loaded += OnShortcutPanelViewLoaded;
        Unloaded += OnShortcutPanelViewUnloaded;
        LoadHardwareModule();
        LoadWallPanelModule();
        LoadSmartAnnotationModule();
        LoadCabinetModule();
        LoadBoardModule();
        LoadStatisticsModule();
        LoadDrawingLayerModule();
        LoadFrameModule();
        LoadRadialMenuModule();
        LoadLispMarketModule();
        ApplyModuleVisibility();
    }

    private void LoadImageBatchOptions()
    {
        var options = ImageBatchRuntimeSettings.Current;
        ImageGapBox.Text = options.Gap.ToString("0.0###", CultureInfo.CurrentCulture);
        UseUnifiedHeightCheckBox.IsChecked = options.UseUnifiedHeight;
        ImagePixelHeightBox.Text = options.PixelHeight.ToString(CultureInfo.CurrentCulture);
    }

    private void OnShortcutModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Shortcuts);
    }

    private void OnPreferencesModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Preferences);
    }

    private void OnModuleNavigationSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (double.IsNaN(e.HorizontalChange) || double.IsInfinity(e.HorizontalChange))
        {
            return;
        }

        var maximumWidth = ActualWidth - ModuleNavigationSplitter.ActualWidth - ModuleContentColumn.MinWidth;
        if (maximumWidth < ModuleNavigationColumn.MinWidth)
        {
            return;
        }

        var requestedWidth = ModuleNavigationColumn.ActualWidth + e.HorizontalChange;
        var constrainedWidth = Math.Max(
            ModuleNavigationColumn.MinWidth,
            Math.Min(maximumWidth, requestedWidth));
        ModuleNavigationColumn.Width = new GridLength(constrainedWidth, GridUnitType.Pixel);
    }

    private void OnImageModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Images);
    }

    private void OnBatchPrintModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.BatchPrint);
    }

    private void OnSuperLibraryModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.SuperLibrary);
    }

    private void OnOpenSuperLibraryClick(object sender, RoutedEventArgs e)
    {
        AcadApplication.DocumentManager.MdiActiveDocument?.SendStringToExecute(
            "DC_CMD_SUPER_LIBRARY ",
            true,
            false,
            true);
    }

    private void OnOpenSuperLibraryFolderClick(object sender, RoutedEventArgs e)
    {
        var root = SuperLibraryRuntime.RootDirectory;
        Directory.CreateDirectory(root);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{root}\"")
        {
            UseShellExecute = true
        });
    }

    private void OnDoorPanelModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.DoorPanels);
    }

    private void OnHardwareModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Hardware);
        RefreshHardwareLibraries();
    }

    private void OnWallPanelModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.WallPanels);
    }

    private void OnSmartAnnotationModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.SmartAnnotation);
    }

    private void OnDrawingLayerModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.DrawingLayers);
    }

    private void OnBoardModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Boards);
    }

    private void OnCabinetModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Cabinet);
    }

    private void OnStatisticsModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Statistics);
    }

    private void OnOneClickFrameModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.OneClickFrame);
    }

    private void OnRadialMenuModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.RadialMenu);
    }

    private void OnLispMarketModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.LispMarket);
        RefreshLispPlugins();
    }

    internal void SelectModule(ToolboxModule module)
    {
        if (TryMapFeatureModule(module, out var featureModule) &&
            !ModulePreferencesRuntime.IsEnabled(featureModule))
        {
            module = ToolboxModule.Preferences;
        }

        selectedModule = module;
        PreferencesModuleView.Visibility = module == ToolboxModule.Preferences ? Visibility.Visible : Visibility.Collapsed;
        ShortcutModuleView.Visibility = module == ToolboxModule.Shortcuts ? Visibility.Visible : Visibility.Collapsed;
        ImageModuleView.Visibility = module == ToolboxModule.Images ? Visibility.Visible : Visibility.Collapsed;
        BatchPrintModuleView.Visibility = module == ToolboxModule.BatchPrint ? Visibility.Visible : Visibility.Collapsed;
        SuperLibraryModuleView.Visibility = module == ToolboxModule.SuperLibrary ? Visibility.Visible : Visibility.Collapsed;
        DoorPanelModuleView.Visibility = module == ToolboxModule.DoorPanels ? Visibility.Visible : Visibility.Collapsed;
        HardwareModuleView.Visibility = module == ToolboxModule.Hardware ? Visibility.Visible : Visibility.Collapsed;
        WallPanelModuleView.Visibility = module == ToolboxModule.WallPanels ? Visibility.Visible : Visibility.Collapsed;
        SmartAnnotationModuleView.Visibility = module == ToolboxModule.SmartAnnotation ? Visibility.Visible : Visibility.Collapsed;
        CabinetModuleView.Visibility = module == ToolboxModule.Cabinet ? Visibility.Visible : Visibility.Collapsed;
        BoardModuleView.Visibility = module == ToolboxModule.Boards ? Visibility.Visible : Visibility.Collapsed;
        StatisticsModuleView.Visibility = module == ToolboxModule.Statistics ? Visibility.Visible : Visibility.Collapsed;
        DrawingLayerModuleView.Visibility = module == ToolboxModule.DrawingLayers ? Visibility.Visible : Visibility.Collapsed;
        OneClickFrameModuleView.Visibility = module == ToolboxModule.OneClickFrame ? Visibility.Visible : Visibility.Collapsed;
        RadialMenuModuleView.Visibility = module == ToolboxModule.RadialMenu ? Visibility.Visible : Visibility.Collapsed;
        LispMarketModuleView.Visibility = module == ToolboxModule.LispMarket ? Visibility.Visible : Visibility.Collapsed;
        HardwareStatusBar.Visibility = module == ToolboxModule.Hardware ? Visibility.Visible : Visibility.Collapsed;
        PreferencesModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Preferences ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        ShortcutModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Shortcuts ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        ImageModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Images ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        BatchPrintModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.BatchPrint ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        SuperLibraryModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.SuperLibrary ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        DoorPanelModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.DoorPanels ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        HardwareModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Hardware ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        WallPanelModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.WallPanels ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        SmartAnnotationModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.SmartAnnotation ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        CabinetModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Cabinet ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        BoardModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Boards ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        StatisticsModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Statistics ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        DrawingLayerModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.DrawingLayers ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        OneClickFrameModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.OneClickFrame ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        RadialMenuModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.RadialMenu ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        LispMarketModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.LispMarket ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
    }

    private void LoadPreferencesModule()
    {
        isLoadingPreferences = true;
        var preferences = ModulePreferencesRuntime.Current;
        var smartInputMethod = ToolboxSettingsRuntime.Settings.SmartInputMethod;
        AutoExpandSidebarCheckBox.IsChecked = preferences.AutoExpandSidebar;
        SmartInputMethodEnabledCheckBox.IsChecked = smartInputMethod.Enabled;
        KeepCadCommandsEnglishCheckBox.IsChecked = smartInputMethod.KeepCadCommandsEnglish;
        AutoChineseForTextCheckBox.IsChecked = smartInputMethod.AutoChineseForText;
        RestoreEnglishAfterTextCheckBox.IsChecked = smartInputMethod.RestoreEnglishAfterText;
        AutoReplaceMissingShxCheckBox.IsChecked = ToolboxSettingsRuntime.Settings.AutoReplaceMissingShx;
        SyncReferencedShxOnSaveCheckBox.IsChecked = ToolboxSettingsRuntime.Settings.SyncReferencedShxOnSave;
        UpdateSmartInputMethodControlState();
        LoadPointerInputState();
        LoadPointStyleState();
        LoadSaveFileFormatState();
        modulePreferenceRows = ToolboxFeatureModuleCatalog.All
            .Select(info => new ModulePreferenceRow(
                info.Module,
                info.Name,
                info.Description,
                preferences.IsEnabled(info.Module)))
            .ToList();
        ModulePreferenceItemsControl.ItemsSource = modulePreferenceRows;
        AllModulesVisibleCheckBox.IsChecked = preferences.EnabledModuleCount == ToolboxFeatureModuleCatalog.All.Count;
        isLoadingPreferences = false;
    }

    private void OnAutoExpandSidebarChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        ToolboxSettingsRuntime.Update(settings =>
            settings.Preferences = settings.Preferences with
            {
                AutoExpandSidebar = AutoExpandSidebarCheckBox.IsChecked == true
            });
        ShowPreferenceStatus("常规设置已保存，下次启动 AutoCAD 时生效。", Brushes.SeaGreen);
    }

    private void OnSmartInputMethodChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        ToolboxSettingsRuntime.Update(settings =>
            settings.SmartInputMethod = new SmartInputMethodOptions(
                SmartInputMethodEnabledCheckBox.IsChecked == true,
                KeepCadCommandsEnglishCheckBox.IsChecked == true,
                AutoChineseForTextCheckBox.IsChecked == true,
                RestoreEnglishAfterTextCheckBox.IsChecked == true));
        UpdateSmartInputMethodControlState();
        SmartInputMethodRuntime.RefreshSettings();
        ShowPreferenceStatus("智能输入法设置已保存，将在下一次 CAD 输入状态变化时生效。", Brushes.SeaGreen);
    }

    private void OnPointerInputChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        var enabled = PointerInputCheckBox.IsChecked == true;
        if (CadQuickToggleRuntime.TrySetPointerInputEnabled(enabled, out var error))
        {
            ShowPreferenceStatus(enabled ? "指针输入已开启。" : "指针输入已关闭。", Brushes.SeaGreen);
            return;
        }

        isLoadingPreferences = true;
        LoadPointerInputState();
        isLoadingPreferences = false;
        ShowPreferenceStatus($"指针输入切换失败：{error}", Brushes.Firebrick);
    }

    private void OnCadQuickToggleExpanded(object sender, RoutedEventArgs e)
    {
        var wasLoading = isLoadingPreferences;
        isLoadingPreferences = true;
        LoadPointerInputState();
        LoadPointStyleState();
        LoadSaveFileFormatState();
        isLoadingPreferences = wasLoading;
    }

    private void InitializePointStyleOptions()
    {
        var wasLoading = isLoadingPreferences;
        isLoadingPreferences = true;
        PointStyleListBox.ItemsSource = PointStyleOptions;
        PointStyleListBox.SelectedValue = DefaultPointStyleMode;
        isLoadingPreferences = wasLoading;
    }

    private void OnPointStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingPreferences || PointStyleListBox.SelectedValue is not int mode)
        {
            return;
        }

        if (CadQuickToggleRuntime.TrySetPointStyle(mode, out var error))
        {
            var name = PointStyleOptions.First(option => option.Mode == mode).Name;
            ShowPreferenceStatus($"点样式已切换为“{name}”。", Brushes.SeaGreen);
            return;
        }

        isLoadingPreferences = true;
        LoadPointStyleState();
        isLoadingPreferences = false;
        ShowPreferenceStatus($"点样式切换失败：{error}", Brushes.Firebrick);
    }

    private void LoadPointStyleState()
    {
        if (CadQuickToggleRuntime.TryGetPointStyle(out var mode, out var error))
        {
            PointStyleListBox.SelectedValue = mode;
            PointStyleListBox.IsEnabled = true;
            PointStyleListBox.ToolTip = "点样式快捷设置";
            return;
        }

        PointStyleListBox.SelectedValue = DefaultPointStyleMode;
        PointStyleListBox.IsEnabled = false;
        PointStyleListBox.ToolTip = $"无法读取 PDMODE：{error}";
    }

    private void LoadPointerInputState()
    {
        if (CadQuickToggleRuntime.TryGetPointerInputEnabled(out var enabled, out var error))
        {
            PointerInputCheckBox.IsChecked = enabled;
            PointerInputCheckBox.IsEnabled = true;
            PointerInputCheckBox.ToolTip = null;
            return;
        }

        PointerInputCheckBox.IsChecked = false;
        PointerInputCheckBox.IsEnabled = false;
        PointerInputCheckBox.ToolTip = $"无法读取 DYNMODE：{error}";
    }

    private void InitializeSaveFileFormatOptions()
    {
        var wasLoading = isLoadingPreferences;
        isLoadingPreferences = true;
        SaveFileFormatComboBox.ItemsSource = SaveFileFormatOptions;
        LoadSaveFileFormatState();
        isLoadingPreferences = wasLoading;
    }

    private void OnSaveFileFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingPreferences || SaveFileFormatComboBox.SelectedValue is not DrawingSaveFileFormat format)
        {
            return;
        }

        if (CadQuickToggleRuntime.TrySetSaveFileFormat(format, out var error))
        {
            ToolboxSettingsRuntime.Update(settings => settings.DefaultSaveFileFormat = format);
            var name = SaveFileFormatOptions.First(option => option.Format == format).Name;
            ShowPreferenceStatus($"默认保存版本已切换为“{name}”。", Brushes.SeaGreen);
            return;
        }

        isLoadingPreferences = true;
        LoadSaveFileFormatState();
        isLoadingPreferences = false;
        ShowPreferenceStatus($"默认保存版本切换失败：{error}", Brushes.Firebrick);
    }

    private void LoadSaveFileFormatState()
    {
        SaveFileFormatComboBox.SelectedValue = ToolboxSettingsRuntime.Settings.DefaultSaveFileFormat;
        SaveFileFormatComboBox.IsEnabled = true;
    }

    private void OnSyncReferencedShxOnSaveChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        var enabled = SyncReferencedShxOnSaveCheckBox.IsChecked == true;
        ToolboxSettingsRuntime.Update(settings => settings.SyncReferencedShxOnSave = enabled);
        ShowPreferenceStatus(
            enabled ? "保存后同步本图 SHX 已开启。" : "保存后同步本图 SHX 已关闭。",
            Brushes.SeaGreen);
    }

    private void OnAutoReplaceMissingShxChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        var enabled = AutoReplaceMissingShxCheckBox.IsChecked == true;
        ToolboxSettingsRuntime.Update(settings => settings.AutoReplaceMissingShx = enabled);
        if (enabled)
        {
            MissingShxReplacementRuntime.QueueActiveDocument();
        }

        ShowPreferenceStatus(
            enabled ? "缺失文字 SHX 自动替换已开启。" : "缺失文字 SHX 自动替换已关闭。",
            Brushes.SeaGreen);
    }

    private async void OnExportCurrentShxClick(object sender, RoutedEventArgs e)
    {
        ExportCurrentShxButton.IsEnabled = false;
        ShowPreferenceStatus("正在导出本图引用的 SHX…", Brushes.DimGray);
        try
        {
            var report = await ShxFontSyncRuntime.ExportActiveDocumentAsync();
            var foreground = report.FailedCount > 0
                ? Brushes.Firebrick
                : report.MissingCount > 0 || report.ConflictCount > 0
                    ? Brushes.DarkOrange
                    : Brushes.SeaGreen;
            ShowPreferenceStatus(ShxFontSyncRuntime.FormatReport(report), foreground);
        }
        catch (System.Exception exception)
        {
            ShowPreferenceStatus($"SHX 导出失败：{exception.Message}", Brushes.Firebrick);
        }
        finally
        {
            ExportCurrentShxButton.IsEnabled = true;
        }
    }

    private sealed record PointStyleOption(int Mode, string Name);

    private sealed record SaveFileFormatOption(DrawingSaveFileFormat Format, string Name);

    private void UpdateSmartInputMethodControlState()
    {
        var enabled = SmartInputMethodEnabledCheckBox.IsChecked == true;
        KeepCadCommandsEnglishCheckBox.IsEnabled = enabled;
        AutoChineseForTextCheckBox.IsEnabled = enabled;
        RestoreEnglishAfterTextCheckBox.IsEnabled = enabled;
    }

    private void OnModulePreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences || sender is not CheckBox { DataContext: ModulePreferenceRow row } checkBox)
        {
            return;
        }

        var enabled = checkBox.IsChecked == true;
        var next = ModulePreferencesRuntime.Current.WithModuleEnabled(row.Module, enabled);
        if (next.EnabledModuleCount == 0)
        {
            isLoadingPreferences = true;
            row.IsEnabled = true;
            checkBox.IsChecked = true;
            isLoadingPreferences = false;
            ShowPreferenceStatus("至少要保留一个功能模块。", Brushes.Firebrick);
            return;
        }

        row.IsEnabled = enabled;
        ToolboxSettingsRuntime.Update(settings => settings.Preferences = next);
        AllModulesVisibleCheckBox.IsChecked = next.EnabledModuleCount == ToolboxFeatureModuleCatalog.All.Count;
        ApplyModuleVisibility();
        ShowPreferenceStatus(
            enabled ? $"已启用“{row.Name}”模块。" : $"已禁用“{row.Name}”模块。",
            Brushes.SeaGreen);
    }

    private void OnShowAllModulesClick(object sender, RoutedEventArgs e)
    {
        if (isLoadingPreferences)
        {
            return;
        }

        if (AllModulesVisibleCheckBox.IsChecked != true)
        {
            isLoadingPreferences = true;
            AllModulesVisibleCheckBox.IsChecked =
                ModulePreferencesRuntime.Current.EnabledModuleCount == ToolboxFeatureModuleCatalog.All.Count;
            isLoadingPreferences = false;
            ShowPreferenceStatus("如需隐藏模块，请逐项取消勾选。", Brushes.DarkOrange);
            return;
        }

        var next = ModulePreferencesRuntime.Current;
        foreach (var info in ToolboxFeatureModuleCatalog.All)
        {
            next = next.WithModuleEnabled(info.Module, true);
        }

        ToolboxSettingsRuntime.Update(settings => settings.Preferences = next);
        LoadPreferencesModule();
        ApplyModuleVisibility();
        ShowPreferenceStatus("全部功能模块已显示。", Brushes.SeaGreen);
    }

    private void ApplyModuleVisibility()
    {
        var preferences = ModulePreferencesRuntime.Current;
        CabinetModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Cabinet));
        BoardModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Boards));
        DoorPanelModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.DoorPanels));
        HardwareModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Hardware));
        WallPanelModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.WallPanels));
        SmartAnnotationModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.SmartAnnotation));
        ImageModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Images));
        OneClickFrameModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.OneClickFrame));
        BatchPrintModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.BatchPrint));
        SuperLibraryModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.SuperLibrary));
        StatisticsModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Statistics));
        DrawingLayerModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.DrawingLayers));
        RadialMenuModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.RadialMenu));
        LispMarketModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.LispMarket));
        ShortcutModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Shortcuts));

        if (TryMapFeatureModule(selectedModule, out var selectedFeature) &&
            !preferences.IsEnabled(selectedFeature))
        {
            SelectModule(ToolboxModule.Preferences);
        }
    }

    private static Visibility ToVisibility(bool visible) =>
        visible ? Visibility.Visible : Visibility.Collapsed;

    private static bool TryMapFeatureModule(ToolboxModule module, out ToolboxFeatureModule featureModule)
    {
        switch (module)
        {
            case ToolboxModule.Cabinet: featureModule = ToolboxFeatureModule.Cabinet; return true;
            case ToolboxModule.Boards: featureModule = ToolboxFeatureModule.Boards; return true;
            case ToolboxModule.DoorPanels: featureModule = ToolboxFeatureModule.DoorPanels; return true;
            case ToolboxModule.Hardware: featureModule = ToolboxFeatureModule.Hardware; return true;
            case ToolboxModule.WallPanels: featureModule = ToolboxFeatureModule.WallPanels; return true;
            case ToolboxModule.SmartAnnotation: featureModule = ToolboxFeatureModule.SmartAnnotation; return true;
            case ToolboxModule.Images: featureModule = ToolboxFeatureModule.Images; return true;
            case ToolboxModule.OneClickFrame: featureModule = ToolboxFeatureModule.OneClickFrame; return true;
            case ToolboxModule.BatchPrint: featureModule = ToolboxFeatureModule.BatchPrint; return true;
            case ToolboxModule.SuperLibrary: featureModule = ToolboxFeatureModule.SuperLibrary; return true;
            case ToolboxModule.Statistics: featureModule = ToolboxFeatureModule.Statistics; return true;
            case ToolboxModule.DrawingLayers: featureModule = ToolboxFeatureModule.DrawingLayers; return true;
            case ToolboxModule.RadialMenu: featureModule = ToolboxFeatureModule.RadialMenu; return true;
            case ToolboxModule.LispMarket: featureModule = ToolboxFeatureModule.LispMarket; return true;
            case ToolboxModule.Shortcuts: featureModule = ToolboxFeatureModule.Shortcuts; return true;
            default:
                featureModule = default;
                return false;
        }
    }

    private void ShowPreferenceStatus(string message, Brush foreground)
    {
        PreferenceStatusText.Foreground = foreground;
        PreferenceStatusText.Text = message;
    }

    private sealed class ModulePreferenceRow(
        ToolboxFeatureModule module,
        string name,
        string description,
        bool isEnabled)
    {
        public ToolboxFeatureModule Module { get; } = module;
        public string Name { get; } = name;
        public string Description { get; } = description;
        public bool IsEnabled { get; set; } = isEnabled;
    }

    private void LoadRadialMenuModule()
    {
        var options = RadialMenuRuntime.Options;
        RadialMenuEnabledCheck.IsChecked = options.Enabled;
        RadialMenuAutoCloseCheck.IsChecked = options.AutoCloseAfterCommand;
        RadialMenuThirdRingCheck.IsChecked = options.ShowThirdRing;
        RadialMenuSizeSlider.Value = options.Radius;
        RadialMenuOpacitySlider.Value = options.Opacity;
        isLoadingRadialMenu = true;
        try
        {
            RadialMenuSkinCombo.SelectedIndex = options.Skin switch
            {
                RadialMenuOptions.CadDarkSkin => 1,
                RadialMenuOptions.PurpleGraySkin => 2,
                _ => 0
            };
        }
        finally
        {
            isLoadingRadialMenu = false;
        }
        radialMenuItems = new ObservableCollection<RadialMenuItem>(
            options.Items.OrderBy(item => item.Ring).ThenBy(item => item.Position).Select(item => item.Clone()));
        RadialMenuGrid.ItemsSource = radialMenuItems;
    }

    private void OnRadialMenuSkinSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingRadialMenu ||
            (RadialMenuSkinCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() is not { } selectedSkin ||
            !RadialMenuOptions.IsSupportedSkin(selectedSkin))
        {
            return;
        }

        try
        {
            var options = RadialMenuRuntime.Options;
            options.Skin = selectedSkin;
            RadialMenuRuntime.Save(options);
            RadialMenuWindowManager.ApplySkin(selectedSkin);
            RadialMenuStatusText.Foreground = Brushes.SeaGreen;
            RadialMenuStatusText.Text = $"圆盘外观已切换为“{selectedSkin}”。";
        }
        catch (Exception exception)
        {
            RadialMenuStatusText.Foreground = Brushes.Firebrick;
            RadialMenuStatusText.Text = exception.Message;
        }
    }

    private void OnSaveRadialMenuClick(object sender, RoutedEventArgs e)
    {
        try
        {
            RadialMenuGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            RadialMenuGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var options = new RadialMenuOptions
            {
                Enabled = RadialMenuEnabledCheck.IsChecked == true,
                AutoCloseAfterCommand = RadialMenuAutoCloseCheck.IsChecked == true,
                ShowThirdRing = RadialMenuThirdRingCheck.IsChecked == true,
                Radius = RadialMenuSizeSlider.Value,
                Opacity = RadialMenuOpacitySlider.Value,
                Skin = (RadialMenuSkinCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
                    ?? RadialMenuOptions.DefaultSkin,
                Items = radialMenuItems.OrderBy(item => item.Ring).ThenBy(item => item.Position)
                    .Select(item => item.Clone()).ToList()
            };
            RadialMenuRuntime.Save(options);
            RadialMenuStatusText.Foreground = Brushes.SeaGreen;
            RadialMenuStatusText.Text = "圆盘配置已保存并立即生效。双击鼠标中键或输入 DCYP 打开圆盘。";
        }
        catch (Exception exception)
        {
            RadialMenuStatusText.Foreground = Brushes.Firebrick;
            RadialMenuStatusText.Text = exception.Message;
        }
    }

    private void OnOpenRadialMenuClick(object sender, RoutedEventArgs e) =>
        RunDrawingCommand("DC_CMD_RADIAL_MENU", "正在打开圆盘工具。", (message, brush) =>
        {
            RadialMenuStatusText.Text = message;
            RadialMenuStatusText.Foreground = brush;
        });

    private void LoadLispMarketModule()
    {
        try
        {
            lispPluginDirectory = LispPluginLibraryService.EnsureDefaultFolder(PluginResourceLocator.GetPluginRoot());
            RefreshLispPlugins();
        }
        catch (Exception exception)
        {
            SetLispMarketStatus($"初始化本地 LISP 库失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void RefreshLispPlugins()
    {
        if (string.IsNullOrWhiteSpace(lispPluginDirectory))
        {
            LispPluginGrid.ItemsSource = null;
            SetLispMarketStatus("本地 LISP 库尚未初始化。", Brushes.Firebrick);
            return;
        }

        try
        {
            var plugins = LispPluginLibraryService.Scan(lispPluginDirectory!);
            LispPluginGrid.ItemsSource = plugins;
            SetLispMarketStatus(
                plugins.Count == 0
                    ? "本地库暂无插件，请将 LSP、FAS 或 VLX 文件放入目录后刷新。"
                    : $"已读取 {plugins.Count} 个本地 LISP 插件。",
                plugins.Count == 0 ? Brushes.DimGray : Brushes.SeaGreen);
        }
        catch (Exception exception)
        {
            LispPluginGrid.ItemsSource = null;
            SetLispMarketStatus($"读取本地 LISP 库失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void OnRefreshLispLibraryClick(object sender, RoutedEventArgs e) => RefreshLispPlugins();

    private void OnOpenLispLibraryClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(lispPluginDirectory))
        {
            SetLispMarketStatus("本地 LISP 库尚未初始化。", Brushes.Firebrick);
            return;
        }

        try
        {
            Directory.CreateDirectory(lispPluginDirectory);
            Process.Start(new ProcessStartInfo { FileName = lispPluginDirectory, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SetLispMarketStatus($"打开本地库失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void OnLoadSelectedLispClick(object sender, RoutedEventArgs e)
    {
        if (LispPluginGrid.SelectedItem is not LispPluginItem plugin)
        {
            SetLispMarketStatus("请先在列表中选择一个 LISP 插件。", Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetLispMarketStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        try
        {
            var expression = LispPluginLibraryService.BuildLoadExpression(plugin.FullPath);
            document.SendStringToExecute(expression, activate: true, wrapUpInactiveDoc: false, echoCommand: false);
            SetLispMarketStatus($"已发送加载请求：{plugin.Name}，请查看 CAD 命令行结果。", Brushes.SeaGreen);
        }
        catch (Exception exception)
        {
            RefreshLispPlugins();
            SetLispMarketStatus($"加载失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void SetLispMarketStatus(string message, Brush brush)
    {
        LispMarketStatusText.Text = message;
        LispMarketStatusText.Foreground = brush;
    }

    private void OnRadialMenuGridMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is not null)
        {
            draggedRadialMenuItem = null;
            return;
        }

        draggedRadialMenuItem = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item as RadialMenuItem;
    }

    private void OnRadialMenuGridMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && draggedRadialMenuItem is not null)
        {
            DragDrop.DoDragDrop(RadialMenuGrid, draggedRadialMenuItem, DragDropEffects.Move);
        }
    }

    private void OnRadialMenuGridDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(RadialMenuItem)) ||
            e.Data.GetData(typeof(RadialMenuItem)) is not RadialMenuItem source ||
            FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject)?.Item is not RadialMenuItem target ||
            source.Ring != target.Ring || source == target)
        {
            return;
        }

        radialMenuItems.Move(radialMenuItems.IndexOf(source), radialMenuItems.IndexOf(target));
        var position = 1;
        foreach (var item in radialMenuItems.Where(item => item.Ring == source.Ring))
        {
            item.Position = position++;
        }
        RadialMenuGrid.Items.Refresh();
        draggedRadialMenuItem = null;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T value)
            {
                return value;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void LoadFrameModule()
    {
        var options = FrameToolRuntime.Options;
        frameTemplateDirectory = FrameTemplateLibraryService.EnsureDefaultFolder(PluginResourceLocator.GetPluginRoot());
        RefreshFrameTemplates(options.SelectedTemplatePath);
        FrameMarginBox.Text = options.FrameMargin.ToString("0.###", CultureInfo.CurrentCulture);
        FramePageStatisticsCheck.IsChecked = options.GeneratePageStatistics;
        FrameAlignLeftRadio.IsChecked = options.AlignmentMode == FrameAlignmentMode.Left;
        FrameAlignBottomRadio.IsChecked = options.AlignmentMode == FrameAlignmentMode.Bottom;
        FrameHorizontalGapBox.Text = options.HorizontalGap.ToString("0.###", CultureInfo.CurrentCulture);
        FrameVerticalGapBox.Text = options.VerticalGap.ToString("0.###", CultureInfo.CurrentCulture);
        DetailCircleRadio.IsChecked = options.DetailShape == DetailBoundaryShape.Circle;
        DetailRectangleRadio.IsChecked = options.DetailShape == DetailBoundaryShape.RoundedRectangle;
        DetailScaleBox.Text = options.DetailScale.ToString("0.###", CultureInfo.CurrentCulture);
        DetailCornerRadiusBox.Text = options.DetailCornerRadius.ToString("0.###", CultureInfo.CurrentCulture);
        ClipScaleBox.Text = options.ClipScale.ToString("0.###", CultureInfo.CurrentCulture);
        ClipCornerRadiusBox.Text = options.ClipCornerRadius.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private void RefreshFrameTemplates(string? selectedPath)
    {
        if (string.IsNullOrWhiteSpace(frameTemplateDirectory))
        {
            return;
        }

        var templates = FrameTemplateLibraryService.Scan(frameTemplateDirectory!, selectedPath);
        FrameTemplateCombo.ItemsSource = templates;
        FrameTemplateCombo.SelectedItem = templates.FirstOrDefault(item =>
            string.Equals(item.DwgPath, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? templates.FirstOrDefault();
        FrameStatusText.Text = templates.Count == 0
            ? "请将 DWG 图框放入 Modules\\FrameTemplates，或点击右侧按钮选择外部模板。"
            : $"已读取 {templates.Count} 个图框模板。";
    }

    private void OnBrowseFrameTemplateClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            DefaultExt = ".dwg",
            Filter = "AutoCAD 图形 (*.dwg)|*.dwg",
            Title = "选择图框 DWG 模板"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        RefreshFrameTemplates(dialog.FileName);
        SetFrameStatus($"已选择外部模板：{Path.GetFileNameWithoutExtension(dialog.FileName)}", Brushes.SeaGreen);
    }

    private bool TrySaveFrameOptions()
    {
        if (FrameTemplateCombo.SelectedItem is not FrameTemplateItem template)
        {
            SetFrameStatus("请先选择有效的 DWG 图框模板。", Brushes.Firebrick);
            return false;
        }

        if (!TryReadNonNegative(FrameMarginBox, "图形到图框距离", out var margin) ||
            !TryReadNonNegative(FrameHorizontalGapBox, "左右排列间距", out var horizontalGap) ||
            !TryReadNonNegative(FrameVerticalGapBox, "上下排列间距", out var verticalGap) ||
            !TryReadPositive(DetailScaleBox, "局部放大倍数", out var detailScale) ||
            !TryReadNonNegative(DetailCornerRadiusBox, "大样矩形圆角", out var detailRadius) ||
            !TryReadPositive(ClipScaleBox, "局部截图倍数", out var clipScale) ||
            !TryReadNonNegative(ClipCornerRadiusBox, "截图矩形圆角", out var clipRadius))
        {
            SetFrameStatus("距离和圆角不能小于 0，放大与截图倍数必须大于 0。", Brushes.Firebrick);
            return false;
        }

        var options = new FrameToolOptions
        {
            SelectedTemplatePath = template.DwgPath,
            FrameMargin = margin,
            GeneratePageStatistics = FramePageStatisticsCheck.IsChecked == true,
            AlignmentMode = FrameAlignBottomRadio.IsChecked == true ? FrameAlignmentMode.Bottom : FrameAlignmentMode.Left,
            HorizontalGap = horizontalGap,
            VerticalGap = verticalGap,
            DetailShape = DetailRectangleRadio.IsChecked == true
                ? DetailBoundaryShape.RoundedRectangle
                : DetailBoundaryShape.Circle,
            DetailScale = detailScale,
            DetailCornerRadius = detailRadius,
            ClipScale = clipScale,
            ClipCornerRadius = clipRadius
        };

        try
        {
            FrameToolRuntime.Save(options);
            return true;
        }
        catch (InvalidOperationException exception)
        {
            SetFrameStatus(exception.Message, Brushes.Firebrick);
            return false;
        }
    }

    private void OnAutoFrameClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveFrameOptions())
        {
            RunDrawingCommand("DC_CMD_FRAME_AUTO", "请选择需要套图框的图形或块参照。", SetFrameStatus);
        }
    }

    private void OnAlignFrameClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveFrameOptions())
        {
            RunDrawingCommand("DC_CMD_FRAME_ALIGN", "请选择一键加框生成的图框，再选择对齐基准线。", SetFrameStatus);
        }
    }

    private void OnDetailDrawClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveFrameOptions())
        {
            RunDrawingCommand("DC_CMD_DETAIL_DRAW", "请框选需要生成大样的区域。", SetFrameStatus);
        }
    }

    private void OnDetailClipClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveFrameOptions())
        {
            RunDrawingCommand("DC_CMD_DETAIL_CLIP", "请框选需要生成局部截图的区域。", SetFrameStatus);
        }
    }

    private void SetFrameStatus(string message, Brush brush)
    {
        FrameStatusText.Text = message;
        FrameStatusText.Foreground = brush;
    }

    private void LoadWallPanelModule()
    {
        var options = WainscotRuntimeSettings.Grille;
        GrilleVerticalRadio.IsChecked = options.IsVertical;
        GrilleHorizontalRadio.IsChecked = !options.IsVertical;
        GrillePlanViewCheck.IsChecked = options.NeedPlanView;
        GrilleSideViewCheck.IsChecked = options.NeedSideView;
        GrilleWidthBox.Text = options.Width.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleHeightBox.Text = options.Height.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleBackThicknessBox.Text = options.BackThickness.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleStripThicknessBox.Text = options.StripThickness.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleStripWidthBox.Text = options.StripWidth.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleStripGapBox.Text = options.StripGap.ToString("0.###", CultureInfo.CurrentCulture);
        GrilleHatchCombo.SelectedItem = GrilleHatchCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag?.ToString(),
                options.HatchPattern,
                StringComparison.OrdinalIgnoreCase));

        var perforated = WainscotRuntimeSettings.PerforatedPanel;
        PerforatedPlanViewCheck.IsChecked = perforated.NeedPlanView;
        PerforatedSideViewCheck.IsChecked = perforated.NeedSideView;
        PerforatedWidthBox.Text = perforated.Width.ToString("0.###", CultureInfo.CurrentCulture);
        PerforatedHeightBox.Text = perforated.Height.ToString("0.###", CultureInfo.CurrentCulture);
        PerforatedDepthBox.Text = perforated.Depth.ToString("0.###", CultureInfo.CurrentCulture);
        PerforatedInnerOffsetBox.Text = perforated.InnerOffset.ToString("0.###", CultureInfo.CurrentCulture);
        PerforatedHoleDiameterBox.Text = perforated.HoleDiameter.ToString("0.###", CultureInfo.CurrentCulture);
        PerforatedHorizontalCountBox.Text = perforated.HorizontalCount.ToString(CultureInfo.CurrentCulture);
        PerforatedVerticalCountBox.Text = perforated.VerticalCount.ToString(CultureInfo.CurrentCulture);

        var backgroundWall = WainscotRuntimeSettings.BackgroundWall;
        BackgroundWallPlanViewCheck.IsChecked = backgroundWall.NeedPlanView;
        BackgroundWallSideViewCheck.IsChecked = backgroundWall.NeedSideView;
        BackgroundWallVerticalGapCheck.IsChecked = backgroundWall.LeaveVerticalGap;
        BackgroundWallAllPlanRowsCheck.IsChecked = backgroundWall.GenerateAllPlanRows;
        BackgroundWallEqualRadio.IsChecked = !backgroundWall.UseRandomWidths;
        BackgroundWallRandomRadio.IsChecked = backgroundWall.UseRandomWidths;
        BackgroundWallWidthBox.Text = backgroundWall.TotalWidth.ToString("0.###", CultureInfo.CurrentCulture);
        BackgroundWallDepthBox.Text = backgroundWall.Depth.ToString("0.###", CultureInfo.CurrentCulture);
        BackgroundWallGapBox.Text = backgroundWall.Gap.ToString("0.###", CultureInfo.CurrentCulture);
        BackgroundWallSlotCheck.IsChecked = backgroundWall.CreateSlots;
        BackgroundWallRowCountCombo.SelectedIndex = backgroundWall.Rows.Count - 1;
        var rowBoxes = GetBackgroundWallRowBoxes();
        for (var index = 0; index < backgroundWall.Rows.Count; index++)
        {
            rowBoxes[index].ColumnCount.Text = backgroundWall.Rows[index].ColumnCount.ToString(CultureInfo.CurrentCulture);
            rowBoxes[index].Height.Text = backgroundWall.Rows[index].Height.ToString("0.###", CultureInfo.CurrentCulture);
        }

        UpdateBackgroundWallRowVisibility();
    }

    private void OnDrawGrilleClick(object sender, RoutedEventArgs e)
    {
        RunGrilleCommand(
            "DC_CMD_WAINSCOT_GRILLE_DRAW",
            "请在图纸中指定格栅正视图左下角。");
    }

    private void OnFillGrilleClick(object sender, RoutedEventArgs e)
    {
        RunGrilleCommand(
            "DC_CMD_WAINSCOT_GRILLE_FILL",
            "请点击需要填充格栅的闭合内空。");
    }

    private void RunGrilleCommand(string command, string statusMessage)
    {
        var hatchPattern = (GrilleHatchCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!GrilleOptions.TryCreate(
                GrilleVerticalRadio.IsChecked == true,
                GrillePlanViewCheck.IsChecked == true,
                GrilleSideViewCheck.IsChecked == true,
                GrilleWidthBox.Text,
                GrilleHeightBox.Text,
                GrilleBackThicknessBox.Text,
                GrilleStripThicknessBox.Text,
                GrilleStripWidthBox.Text,
                GrilleStripGapBox.Text,
                hatchPattern,
                out var options,
                out var error))
        {
            WallPanelStatusText.Foreground = Brushes.Firebrick;
            WallPanelStatusText.Text = error;
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            WallPanelStatusText.Foreground = Brushes.Firebrick;
            WallPanelStatusText.Text = "当前没有可用图纸。";
            return;
        }

        WainscotRuntimeSettings.SaveGrille(options);
        WallPanelStatusText.Foreground = Brushes.DimGray;
        WallPanelStatusText.Text = statusMessage;
        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private void OnDrawPerforatedPanelClick(object sender, RoutedEventArgs e)
    {
        RunPerforatedPanelCommand(
            "DC_CMD_WAINSCOT_PERFORATED_DRAW",
            "请在图纸中指定洞洞板正视图左下角。");
    }

    private void OnFillPerforatedPanelClick(object sender, RoutedEventArgs e)
    {
        RunPerforatedPanelCommand(
            "DC_CMD_WAINSCOT_PERFORATED_FILL",
            "请点击需要填充洞洞板的闭合内空。");
    }

    private void RunPerforatedPanelCommand(string command, string statusMessage)
    {
        if (!PerforatedPanelOptions.TryCreate(
                PerforatedPlanViewCheck.IsChecked == true,
                PerforatedSideViewCheck.IsChecked == true,
                PerforatedWidthBox.Text,
                PerforatedHeightBox.Text,
                PerforatedDepthBox.Text,
                PerforatedInnerOffsetBox.Text,
                PerforatedHoleDiameterBox.Text,
                PerforatedHorizontalCountBox.Text,
                PerforatedVerticalCountBox.Text,
                out var options,
                out var error))
        {
            PerforatedStatusText.Foreground = Brushes.Firebrick;
            PerforatedStatusText.Text = error;
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            PerforatedStatusText.Foreground = Brushes.Firebrick;
            PerforatedStatusText.Text = "当前没有可用图纸。";
            return;
        }

        WainscotRuntimeSettings.SavePerforatedPanel(options);
        PerforatedStatusText.Foreground = Brushes.DimGray;
        PerforatedStatusText.Text = statusMessage;
        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private void OnDrawBackgroundWallClick(object sender, RoutedEventArgs e)
    {
        var rows = GetBackgroundWallRowBoxes()
            .Select(boxes => new BackgroundWallRowInput(boxes.ColumnCount.Text, boxes.Height.Text))
            .ToArray();
        if (!BackgroundWallOptions.TryCreate(
                BackgroundWallPlanViewCheck.IsChecked == true,
                BackgroundWallSideViewCheck.IsChecked == true,
                BackgroundWallRandomRadio.IsChecked == true,
                BackgroundWallVerticalGapCheck.IsChecked == true,
                BackgroundWallAllPlanRowsCheck.IsChecked == true,
                BackgroundWallWidthBox.Text,
                BackgroundWallDepthBox.Text,
                BackgroundWallGapBox.Text,
                BackgroundWallSlotCheck.IsChecked == true,
                (BackgroundWallRowCountCombo.SelectedIndex + 1).ToString(CultureInfo.InvariantCulture),
                rows,
                out var options,
                out var error))
        {
            BackgroundWallStatusText.Foreground = Brushes.Firebrick;
            BackgroundWallStatusText.Text = error;
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            BackgroundWallStatusText.Foreground = Brushes.Firebrick;
            BackgroundWallStatusText.Text = "当前没有可用图纸。";
            return;
        }

        WainscotRuntimeSettings.SaveBackgroundWall(options);
        BackgroundWallStatusText.Foreground = Brushes.DimGray;
        BackgroundWallStatusText.Text = "请在图纸中指定背景墙正视图左下角。";
        document.SendStringToExecute(
            "DC_CMD_WAINSCOT_BACKGROUND_DRAW ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private void OnBackgroundWallRowCountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateBackgroundWallRowVisibility();
        }
    }

    private void UpdateBackgroundWallRowVisibility()
    {
        var rowCount = BackgroundWallRowCountCombo.SelectedIndex + 1;
        var rows = new[]
        {
            BackgroundWallRow1,
            BackgroundWallRow2,
            BackgroundWallRow3,
            BackgroundWallRow4,
            BackgroundWallRow5
        };
        var labels = new[]
        {
            BackgroundWallRow1Label,
            BackgroundWallRow2Label,
            BackgroundWallRow3Label,
            BackgroundWallRow4Label,
            BackgroundWallRow5Label
        };
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index].Visibility = index < rowCount ? Visibility.Visible : Visibility.Collapsed;
            labels[index].Text = GetBackgroundWallRowLabel(index, rowCount);
        }
    }

    private static string GetBackgroundWallRowLabel(int index, int rowCount)
    {
        var names = new[] { "第一", "第二", "第三", "第四", "第五" };
        var position = index == 0 ? "底" : index == rowCount - 1 ? "顶" : "中";
        return $"{names[index]}排({position})";
    }

    private (TextBox ColumnCount, TextBox Height)[] GetBackgroundWallRowBoxes()
    {
        return
        [
            (BackgroundWallRow1ColumnCountBox, BackgroundWallRow1HeightBox),
            (BackgroundWallRow2ColumnCountBox, BackgroundWallRow2HeightBox),
            (BackgroundWallRow3ColumnCountBox, BackgroundWallRow3HeightBox),
            (BackgroundWallRow4ColumnCountBox, BackgroundWallRow4HeightBox),
            (BackgroundWallRow5ColumnCountBox, BackgroundWallRow5HeightBox)
        ];
    }

    private void LoadSmartAnnotationModule()
    {
        var options = SmartAnnotationRuntimeSettings.Current;
        SmartAnnotationUseTemplateCheck.IsChecked = options.UseTemplateStyle;
        SmartAnnotationInnerOffsetBox.Text = options.InnerOffset.ToString("0.###", CultureInfo.CurrentCulture);
        SmartAnnotationOuterOffsetBox.Text = options.OuterOffset.ToString("0.###", CultureInfo.CurrentCulture);
        SmartAnnotationFullGraphicCheck.IsChecked = options.FullGraphicMode;
        SmartAnnotationFilterThicknessCheck.IsChecked = options.FilterByThickness;
        SmartAnnotationFilterThicknessBox.Text = options.FilterThickness.ToString("0.###", CultureInfo.CurrentCulture);
        SmartAnnotationFilterMinimumCheck.IsChecked = options.FilterMinimumDimension;
        SmartAnnotationMinimumBox.Text = options.MinimumDimension.ToString("0.###", CultureInfo.CurrentCulture);
        SmartAnnotationMergeOverlapCheck.IsChecked = options.MergeOverlappingGeometry;
        SmartAnnotationFilterPointCheck.IsChecked = options.FilterPoints;
        SmartAnnotationFilterLineCheck.IsChecked = options.FilterLines;
        SmartAnnotationFilterDashedCheck.IsChecked = options.FilterDashedLines;
        SmartAnnotationFilterPolylineCheck.IsChecked = options.FilterPolylines;
        SmartAnnotationFilterArcCheck.IsChecked = options.FilterArcs;
        SmartAnnotationFilterTextCheck.IsChecked = options.FilterText;
        SmartAnnotationFilterBlockCheck.IsChecked = options.FilterBlocks;
        SmartAnnotationFilterHatchCheck.IsChecked = options.FilterHatches;
        SmartAnnotationDeleteThicknessCheck.IsChecked = options.DeleteByThickness;
        SmartAnnotationDeleteThicknessBox.Text = options.DeleteThickness.ToString("0.###", CultureInfo.CurrentCulture);

        try
        {
            var templates = DimensionTemplateLibraryService.Scan();
            SmartAnnotationTemplateCombo.ItemsSource = templates;
            SmartAnnotationTemplateCombo.SelectedItem = templates.FirstOrDefault(item =>
                string.Equals(item.Name, options.TemplateName, StringComparison.OrdinalIgnoreCase)) ?? templates.FirstOrDefault();
            SetSmartAnnotationStatus(
                templates.Count == 0 ? "标注模板目录为空。" : $"已读取 {templates.Count} 个标注模板。",
                templates.Count == 0 ? Brushes.Firebrick : Brushes.DimGray);
        }
        catch (Exception exception)
        {
            SmartAnnotationTemplateCombo.ItemsSource = null;
            SetSmartAnnotationStatus($"读取标注模板失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void OnOpenDimTemplateFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var directory = DimensionTemplateLibraryService.GetDirectory();
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SetSmartAnnotationStatus($"打开模板目录失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private bool TrySaveSmartAnnotationOptions()
    {
        if (SmartAnnotationTemplateCombo.SelectedItem is not DimensionTemplateItem template)
        {
            SetSmartAnnotationStatus("请先选择有效的 DWG 标注模板。", Brushes.Firebrick);
            return false;
        }

        if (!TryReadPositive(SmartAnnotationInnerOffsetBox, "内层偏移", out var innerOffset) ||
            !TryReadPositive(SmartAnnotationOuterOffsetBox, "外层总尺寸偏移", out var outerOffset) ||
            !TryReadPositive(SmartAnnotationFilterThicknessBox, "板厚过滤值", out var filterThickness) ||
            !TryReadPositive(SmartAnnotationMinimumBox, "最小标注值", out var minimumDimension) ||
            !TryReadPositive(SmartAnnotationDeleteThicknessBox, "清除板厚值", out var deleteThickness))
        {
            SetSmartAnnotationStatus("偏移、过滤和清理尺寸必须是大于 0 的数字。", Brushes.Firebrick);
            return false;
        }

        var options = new SmartAnnotationOptions(
            SmartAnnotationUseTemplateCheck.IsChecked == true,
            template.Name,
            innerOffset,
            outerOffset,
            SmartAnnotationFullGraphicCheck.IsChecked == true,
            SmartAnnotationFilterThicknessCheck.IsChecked == true,
            filterThickness,
            SmartAnnotationFilterMinimumCheck.IsChecked == true,
            minimumDimension,
            SmartAnnotationMergeOverlapCheck.IsChecked == true,
            SmartAnnotationFilterPointCheck.IsChecked == true,
            SmartAnnotationFilterLineCheck.IsChecked == true,
            SmartAnnotationFilterDashedCheck.IsChecked == true,
            SmartAnnotationFilterPolylineCheck.IsChecked == true,
            SmartAnnotationFilterArcCheck.IsChecked == true,
            SmartAnnotationFilterTextCheck.IsChecked == true,
            SmartAnnotationFilterBlockCheck.IsChecked == true,
            SmartAnnotationFilterHatchCheck.IsChecked == true,
            SmartAnnotationDeleteThicknessCheck.IsChecked == true,
            deleteThickness);
        SmartAnnotationRuntimeSettings.Save(options);
        return true;
    }

    private void RunSmartAnnotationCommand(string command, string prompt)
    {
        if (!TrySaveSmartAnnotationOptions())
        {
            return;
        }

        RunDrawingCommand(command, prompt, SetSmartAnnotationStatus);
    }

    private void SetSmartAnnotationStatus(string message, Brush brush)
    {
        SmartAnnotationStatusText.Text = message;
        SmartAnnotationStatusText.Foreground = brush;
    }

    private void OnDimensionCabinetClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_CABINET", "请选择柜体图形进行标注。");
    private void OnDimensionPanelClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_PANEL", "请选择门板图形进行标注。");
    private void OnDimensionLeftBottomClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_LEFT_BOTTOM", "请选择需要左下标注的图形。");
    private void OnDimensionRightTopClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_RIGHT_TOP", "请选择需要右上标注的图形。");
    private void OnDimensionInnerClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_INNER", "请选择需要内空标注的图形。");
    private void OnDimensionSingleClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_SINGLE", "请选择单边标注的起终点。");
    private void OnDimensionContinueClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_CONTINUE", "请选择连续标注对象。");
    private void OnDimensionSmartObjectClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_SMART_OBJECT", "请选择智能标注对象。");
    private void OnDimensionBatchDynamicClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_BATCH_DYNAMIC", "请选择批量动态标注对象。");
    private void OnDimensionSelectDynamicClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_SELECT_DYNAMIC", "请选择动态标注对象。");
    private void OnDimensionAlignBatchClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_ALIGN_BATCH", "请选择需要批量对齐的标注。");
    private void OnDimensionAlignContinueClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_ALIGN_CONTINUE", "请选择需要连续对齐的标注。");
    private void OnDimensionOuterClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_OUTER", "请选择外围线条标注对象。");
    private void OnDimensionScaleClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_SCALE", "请选择需要缩放的标注。");
    private void OnDimensionSplitClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_SPLIT", "请选择需要分割的标注。");
    private void OnDimensionAlignClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_ALIGN", "请选择需要对齐的标注。");
    private void OnDimensionJoinClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_JOIN", "请选择需要合并的标注。");
    private void OnDimensionDeleteAllClick(object sender, RoutedEventArgs e) => RunSmartAnnotationCommand("DC_CMD_DIM_DELETE_ALL", "请选择需要清除标注的范围。");

    private void LoadDoorPanelModule()
    {
        var options = DoorPanelRuntimeSettings.Current;
        DoorPanelCalculationModeCombo.SelectedIndex = options.CalculationMode switch
        {
            DoorPanelCalculationMode.GapRoundedDoorDistributed => 1,
            DoorPanelCalculationMode.DoorAndGapRoundedRemainderRight => 2,
            _ => 0
        };
        DoorPanelTopExtensionBox.Text = options.TopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelBottomExtensionBox.Text = options.BottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelLeftExtensionBox.Text = options.LeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelRightExtensionBox.Text = options.RightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelGenerateOpeningLinesCheck.IsChecked = options.GenerateOpeningLines;
        DoorPanelGenerateHingesCheck.IsChecked = options.GenerateHinges;
        DoorPanelAvoidShelvesCheck.IsChecked = options.AvoidShelves;
        DoorPanelGenerateMaskCheck.IsChecked = options.GenerateMask;
        DoorPanelColorBox.Text = options.LineColorIndex.ToString(CultureInfo.CurrentCulture);

        var planOptions = PlanDoorPanelRuntimeSettings.Current;
        PlanDoorPanelCalculationModeCombo.SelectedIndex = planOptions.CalculationMode switch
        {
            DoorPanelCalculationMode.DoorRoundedGapDistributed => 1,
            DoorPanelCalculationMode.GapRoundedDoorDistributed => 2,
            DoorPanelCalculationMode.DoorAndGapRoundedRemainderRight => 3,
            _ => 0
        };
        PlanDoorPanelThicknessBox.Text = planOptions.Thickness.ToString("0.###", CultureInfo.CurrentCulture);
        PlanDoorPanelFrontClearanceBox.Text = planOptions.FrontClearance.ToString("0.###", CultureInfo.CurrentCulture);
        PlanDoorPanelGapBox.Text = planOptions.Gap.ToString("0.###", CultureInfo.CurrentCulture);
        PlanDoorPanelLeftExtensionBox.Text = planOptions.LeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        PlanDoorPanelRightExtensionBox.Text = planOptions.RightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        PlanDoorPanelColorBox.Text = planOptions.LineColorIndex.ToString(CultureInfo.CurrentCulture);
        UpdatePlanDoorPanelActualGap(PlanDoorPanelRuntimeSettings.LastActualGap);

        var drawerOptions = DrawerFaceRuntimeSettings.Current;
        DrawerFaceRowCountBox.Text = drawerOptions.RowCount.ToString(CultureInfo.CurrentCulture);
        DrawerFaceColumnCountBox.Text = drawerOptions.ColumnCount.ToString(CultureInfo.CurrentCulture);
        DrawerFaceGapBox.Text = drawerOptions.Gap.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceHandlelessCheck.IsChecked = drawerOptions.Handleless;
        ApplyHandlelessProfileOptions(drawerOptions);
        DrawerFaceTopExtensionBox.Text = drawerOptions.TopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceBottomExtensionBox.Text = drawerOptions.BottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceLeftExtensionBox.Text = drawerOptions.LeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceRightExtensionBox.Text = drawerOptions.RightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceGenerateOpeningLinesCheck.IsChecked = drawerOptions.GenerateOpeningLines;
        DrawerFaceGenerateMaskCheck.IsChecked = drawerOptions.GenerateMask;
        DrawerFaceColorBox.Text = drawerOptions.LineColorIndex.ToString(CultureInfo.CurrentCulture);
        UpdateDrawerFaceActualSize(DrawerFaceRuntimeSettings.LastLayout);
    }

    private void OnShortcutPanelViewUnloaded(object sender, RoutedEventArgs e)
    {
        statusClearTimer.Stop();
        PlanDoorPanelRuntimeSettings.ActualGapChanged -= OnPlanDoorPanelActualGapChanged;
        DrawerFaceRuntimeSettings.LayoutChanged -= OnDrawerFaceLayoutChanged;
    }

    private void OnShortcutPanelViewLoaded(object sender, RoutedEventArgs e)
    {
        PlanDoorPanelRuntimeSettings.ActualGapChanged -= OnPlanDoorPanelActualGapChanged;
        PlanDoorPanelRuntimeSettings.ActualGapChanged += OnPlanDoorPanelActualGapChanged;
        UpdatePlanDoorPanelActualGap(PlanDoorPanelRuntimeSettings.LastActualGap);
        DrawerFaceRuntimeSettings.LayoutChanged -= OnDrawerFaceLayoutChanged;
        DrawerFaceRuntimeSettings.LayoutChanged += OnDrawerFaceLayoutChanged;
        UpdateDrawerFaceActualSize(DrawerFaceRuntimeSettings.LastLayout);
    }

    private void OnPlanDoorPanelActualGapChanged(double actualGap)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => UpdatePlanDoorPanelActualGap(actualGap));
            return;
        }

        UpdatePlanDoorPanelActualGap(actualGap);
    }

    private void UpdatePlanDoorPanelActualGap(double? actualGap)
    {
        PlanDoorPanelActualGapText.Text = actualGap.HasValue
            ? $"实际门缝：{actualGap.Value:0.###} mm"
            : "实际门缝：—（选择柜体前沿后实时计算）";
    }

    private void OnDrawerFaceLayoutChanged(DrawerFaceLayout layout)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(() => UpdateDrawerFaceActualSize(layout));
            return;
        }

        UpdateDrawerFaceActualSize(layout);
    }

    private void UpdateDrawerFaceActualSize(DrawerFaceLayout? layout)
    {
        if (layout is not null)
        {
            DrawerFaceRowCountBox.Text = layout.RowCount.ToString(CultureInfo.CurrentCulture);
            DrawerFaceColumnCountBox.Text = layout.ColumnCount.ToString(CultureInfo.CurrentCulture);
        }

        DrawerFaceActualSizeText.Text = layout is null
            ? "实际单块尺寸/缝隙：—（选择柜体内空后计算）"
            : $"{layout.RowCount} 行 × {layout.ColumnCount} 列；单块：{layout.FaceWidth:0.###} × {layout.FaceHeight:0.###} mm；" +
              $"左右缝：{layout.ActualHorizontalGap:0.###} mm；上下缝：{layout.ActualVerticalGap:0.###} mm";
    }

    private void OnSaveDoorPanelOptionsClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveDoorAndHingeOptions(out var error))
        {
            SetDoorPanelStatus("门板与铰链参数已保存。", Brushes.SeaGreen);
        }
        else
        {
            SetDoorPanelStatus(error, Brushes.Firebrick);
        }
    }

    private void OnGenerateDoorPanelClick(object sender, RoutedEventArgs e)
    {
        if (!TrySaveDoorAndHingeOptions(out var error))
        {
            SetDoorPanelStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetDoorPanelStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        SetDoorPanelStatus("点击柜体内空自动识别；W/+、S/- 调整门数，左键生成，Esc 退出。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_ELEVATION_DOOR ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private void OnGenerateDoorHandlelessProfileClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadDoorHandlelessProfileOptions(out var options, out var error))
        {
            SetDoorPanelStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetDoorPanelStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        DrawerFaceRuntimeSettings.Save(options);
        ApplyHandlelessProfileOptions(options);
        SetDoorPanelStatus($"在{options.HandlelessGap:0.###}mm免拉手缝、门板边线或端角附近单击；随后将鼠标移向柜前方向确认。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_DRAWER_HANDLELESS_PROFILE ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private void OnSavePlanDoorPanelOptionsClick(object sender, RoutedEventArgs e)
    {
        if (TryReadPlanDoorPanelOptions(out var options, out var error))
        {
            PlanDoorPanelRuntimeSettings.Save(options);
            SetPlanDoorPanelStatus("俯视门板参数已独立保存。", Brushes.SeaGreen);
            return;
        }

        SetPlanDoorPanelStatus(error, Brushes.Firebrick);
    }

    private void OnGeneratePlanDoorPanelClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPlanDoorPanelOptions(out var options, out var error))
        {
            SetPlanDoorPanelStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetPlanDoorPanelStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        PlanDoorPanelRuntimeSettings.Save(options);
        SetPlanDoorPanelStatus("指定柜体前沿起终点；鼠标选侧，R翻转，W/+、S/-调整门数。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_PLAN_DOOR ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private bool TryReadPlanDoorPanelOptions(out PlanDoorPanelOptions options, out string error)
    {
        options = PlanDoorPanelOptions.Default;
        error = string.Empty;
        var current = PlanDoorPanelRuntimeSettings.Current;
        var modeName = (PlanDoorPanelCalculationModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(modeName, out DoorPanelCalculationMode calculationMode))
        {
            options = PlanDoorPanelOptions.Default;
            error = "请选择有效的俯视门板尺寸计算模式。";
            return false;
        }

        if (!TryReadColorIndex(PlanDoorPanelColorBox.Text, out var lineColor, out error) ||
            !PlanDoorPanelOptions.TryCreate(
            calculationMode,
            current.DoorCount.ToString(CultureInfo.InvariantCulture),
            PlanDoorPanelThicknessBox.Text,
            PlanDoorPanelFrontClearanceBox.Text,
            PlanDoorPanelGapBox.Text,
            PlanDoorPanelLeftExtensionBox.Text,
            PlanDoorPanelRightExtensionBox.Text,
            out options,
            out error))
        {
            return false;
        }

        options = options with { LineColorIndex = lineColor };
        return true;
    }

    private void OnSaveDrawerFaceOptionsClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadDrawerFaceOptions(out var options, out var error))
        {
            SetDrawerFaceStatus(error, Brushes.Firebrick);
            return;
        }

        DrawerFaceRuntimeSettings.Save(options);
        ApplyHandlelessProfileOptions(options);
        SetDrawerFaceStatus("抽面参数已独立保存。", Brushes.SeaGreen);
    }

    private void OnGenerateDrawerFacesClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadDrawerFaceOptions(out var options, out var error))
        {
            SetDrawerFaceStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetDrawerFaceStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        DrawerFaceRuntimeSettings.Save(options);
        ApplyHandlelessProfileOptions(options);
        SetDrawerFaceStatus("点击柜体内空自动识别；W/S、上/下或 +/- 调整行数，A/D 或左/右调整列数。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_DRAWER_FACE ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private void OnGenerateHandlelessProfileClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadDrawerFaceOptions(out var options, out var error))
        {
            SetDrawerFaceStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetDrawerFaceStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        DrawerFaceRuntimeSettings.Save(options);
        ApplyHandlelessProfileOptions(options);
        SetDrawerFaceStatus($"在{options.HandlelessGap:0.###}mm免拉手缝、门板边线或端角附近单击；随后将鼠标移向柜前方向确认。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_DRAWER_HANDLELESS_PROFILE ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private bool TryReadDrawerFaceOptions(out DrawerFaceOptions options, out string error)
    {
        options = DrawerFaceOptions.Default;
        error = string.Empty;
        var profileModeName = (DrawerFaceHandlelessProfileModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(profileModeName, out HandlelessProfileMode profileMode))
        {
            error = "请选择有效的免拉手侧视图识别模式。";
            return false;
        }

        if (!TryReadPositive(DrawerFaceHandlelessProfileSizeBox, "挡板尺寸", out var profileSize))
        {
            error = "免拉手挡板尺寸必须大于 0。";
            return false;
        }

        if (!TryReadColorIndex(DrawerFaceColorBox.Text, out var lineColor, out error) ||
            !DrawerFaceOptions.TryCreateGrid(
            DrawerFaceRowCountBox.Text,
            DrawerFaceColumnCountBox.Text,
            DrawerFaceGapBox.Text,
            DrawerFaceHandlelessGapBox.Text,
            DrawerFaceTopExtensionBox.Text,
            DrawerFaceBottomExtensionBox.Text,
            DrawerFaceLeftExtensionBox.Text,
            DrawerFaceRightExtensionBox.Text,
            DrawerFaceHandlelessCheck.IsChecked == true,
            DrawerFaceGenerateOpeningLinesCheck.IsChecked == true,
            DrawerFaceGenerateMaskCheck.IsChecked == true,
            out options,
            out error))
        {
            return false;
        }

        if (profileSize <= options.HandlelessGap)
        {
            error = "免拉手挡板尺寸必须大于免拉手上下缝。";
            return false;
        }

        options = options with
        {
            LineColorIndex = lineColor,
            HandlelessProfileMode = profileMode,
            HandlelessProfileSize = profileSize
        };
        return true;
    }

    private bool TryReadDoorHandlelessProfileOptions(out DrawerFaceOptions options, out string error)
    {
        options = DrawerFaceRuntimeSettings.Current;
        error = string.Empty;
        var profileModeName = (DoorPanelHandlelessProfileModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(profileModeName, out HandlelessProfileMode profileMode))
        {
            error = "请选择有效的免拉手侧视图识别模式。";
            return false;
        }

        if (!TryReadPositive(DoorPanelHandlelessGapBox, "免拉手缝", out var handlelessGap))
        {
            error = "免拉手缝必须大于 0。";
            return false;
        }

        if (!TryReadPositive(DoorPanelHandlelessProfileSizeBox, "挡板尺寸", out var profileSize))
        {
            error = "免拉手挡板尺寸必须大于 0。";
            return false;
        }

        if (profileSize <= handlelessGap)
        {
            error = "免拉手挡板尺寸必须大于免拉手缝。";
            return false;
        }

        options = options with
        {
            HandlelessGap = handlelessGap,
            HandlelessProfileMode = profileMode,
            HandlelessProfileSize = profileSize
        };
        return true;
    }

    private void ApplyHandlelessProfileOptions(DrawerFaceOptions options)
    {
        DrawerFaceHandlelessGapBox.Text = options.HandlelessGap.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceHandlelessProfileSizeBox.Text = options.HandlelessProfileSize.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelHandlelessGapBox.Text = options.HandlelessGap.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPanelHandlelessProfileSizeBox.Text = options.HandlelessProfileSize.ToString("0.###", CultureInfo.CurrentCulture);

        foreach (var comboBox in new[] { DrawerFaceHandlelessProfileModeCombo, DoorPanelHandlelessProfileModeCombo })
        {
            comboBox.SelectedItem = comboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(
                    item.Tag?.ToString(),
                    options.HandlelessProfileMode.ToString(),
                    StringComparison.Ordinal));
        }
    }

    private bool TrySaveDoorAndHingeOptions(out string error)
    {
        if (!TryReadDoorPanelOptions(out var doorOptions, out error) ||
            !TryReadHardwareOptions(out var hardwareOptions, out error) ||
            !TryReadDoorHandlelessProfileOptions(out var handlelessOptions, out error))
        {
            return false;
        }

        ToolboxSettingsRuntime.Update(settings =>
        {
            settings.DoorPanel = doorOptions;
            settings.Hardware = hardwareOptions;
            settings.DrawerFace = handlelessOptions;
        });
        ApplyHandlelessProfileOptions(handlelessOptions);
        return true;
    }

    private bool TryReadDoorPanelOptions(out DoorPanelOptions options, out string error)
    {
        var defaults = DoorPanelOptions.Default;
        options = defaults;
        var modeName = (DoorPanelCalculationModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(modeName, out DoorPanelCalculationMode calculationMode))
        {
            error = "请选择有效的门板尺寸计算模式。";
            return false;
        }

        if (!TryReadColorIndex(DoorPanelColorBox.Text, out var lineColor, out error) ||
            !DoorPanelOptions.TryCreate(
                calculationMode,
                defaults.DoorCount.ToString(CultureInfo.InvariantCulture),
                defaults.Gap.ToString("0.###", CultureInfo.InvariantCulture),
                DoorPanelTopExtensionBox.Text,
                DoorPanelBottomExtensionBox.Text,
                DoorPanelLeftExtensionBox.Text,
                DoorPanelRightExtensionBox.Text,
                DoorPanelGenerateOpeningLinesCheck.IsChecked == true,
                DoorPanelGenerateHingesCheck.IsChecked == true,
                DoorPanelAvoidShelvesCheck.IsChecked == true,
                out options,
                out error))
        {
            return false;
        }

        options = options with
        {
            LineColorIndex = lineColor,
            GenerateMask = DoorPanelGenerateMaskCheck.IsChecked == true
        };
        return true;
    }

    private void OnDoorPanelColorClick(object sender, RoutedEventArgs e) => PickColor(DoorPanelColorBox);

    private void OnPlanDoorPanelColorClick(object sender, RoutedEventArgs e) => PickColor(PlanDoorPanelColorBox);

    private void OnDrawerFaceColorClick(object sender, RoutedEventArgs e) => PickColor(DrawerFaceColorBox);

    private static void PickColor(ComboBox target)
    {
        var initial = TryReadColorIndex(target.Text, out var index, out _) ? index : (short)7;
        var dialog = new Autodesk.AutoCAD.Windows.ColorDialog();
        dialog.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
            Autodesk.AutoCAD.Colors.ColorMethod.ByAci,
            initial);
        var result = dialog.ShowDialog();
        short selectedIndex = dialog.Color.ColorIndex;
        if (string.Equals(result.ToString(), "OK", StringComparison.OrdinalIgnoreCase) &&
            selectedIndex is >= 1 and <= 255)
        {
            target.Text = selectedIndex.ToString(CultureInfo.CurrentCulture);
        }
    }

    private void LoadCabinetModule()
    {
        ApplyCabinetOptions(CabinetRuntimeSettings.Current);
        ApplyPlanCabinetOptions(PlanCabinetRuntimeSettings.Current);
    }

    private void ApplyCabinetOptions(CabinetOptions options)
    {
        CabinetGenerateElevationCheck.IsChecked = options.GenerateElevation;
        CabinetGeneratePlanCheck.IsChecked = options.GeneratePlan;
        CabinetGenerateSideCheck.IsChecked = options.GenerateSide;
        SetNumber(CabinetPlanViewSpacingBox, options.PlanViewSpacing);
        SetNumber(CabinetSideViewSpacingBox, options.SideViewSpacing);
        SetNumber(CabinetWidthBox, options.Width);
        SetNumber(CabinetHeightBox, options.Height);
        SetNumber(CabinetDepthBox, options.Depth);
        SetNumber(CabinetThicknessBox, options.Thickness);
        SetNumber(CabinetLeftSideTopExtensionBox, options.LeftSideTopExtension);
        SetNumber(CabinetLeftSideBottomExtensionBox, options.LeftSideBottomExtension);
        SetNumber(CabinetLeftSideFrontExtensionBox, options.LeftSideFrontExtension);
        SetNumber(CabinetLeftSideRearExtensionBox, options.LeftSideRearExtension);
        SetNumber(CabinetRightSideTopExtensionBox, options.RightSideTopExtension);
        SetNumber(CabinetRightSideBottomExtensionBox, options.RightSideBottomExtension);
        SetNumber(CabinetRightSideFrontExtensionBox, options.RightSideFrontExtension);
        SetNumber(CabinetRightSideRearExtensionBox, options.RightSideRearExtension);
        SetNumber(CabinetTopFrontInsetBox, options.TopFrontInset);
        SetNumber(CabinetTopRearInsetBox, options.TopRearInset);
        SetNumber(CabinetBottomFrontInsetBox, options.BottomFrontInset);
        SetNumber(CabinetBottomRearInsetBox, options.BottomRearInset);
        CabinetBackboardRearCoverCheck.IsChecked = options.BackboardRearCoverMode;
        SetNumber(CabinetBackboardThicknessBox, options.BackboardThickness);
        SetNumber(CabinetBackboardOverallInsetBox, options.BackboardOverallInset);
        SetNumber(CabinetBackboardTopExtensionBox, options.BackboardTopExtension);
        SetNumber(CabinetBackboardBottomExtensionBox, options.BackboardBottomExtension);
        SetNumber(CabinetBackboardLeftExtensionBox, options.BackboardLeftExtension);
        SetNumber(CabinetBackboardRightExtensionBox, options.BackboardRightExtension);
        SetNumber(CabinetTopRailHeightBox, options.TopRailHeight);
        SetNumber(CabinetTopRailAuxiliaryDepthBox, options.TopRailAuxiliaryDepth);
        SetNumber(CabinetTopRailLeftExtensionBox, options.TopRailLeftExtension);
        SetNumber(CabinetTopRailRightExtensionBox, options.TopRailRightExtension);
        SetNumber(CabinetTopRailRearInsetBox, options.TopRailRearInset);
        SetNumber(CabinetFrontToeKickHeightBox, options.FrontToeKickHeight);
        SetNumber(CabinetFrontToeKickFrontInsetBox, options.FrontToeKickFrontInset);
        SetNumber(CabinetFrontToeKickLeftExtensionBox, options.FrontToeKickLeftExtension);
        SetNumber(CabinetFrontToeKickRightExtensionBox, options.FrontToeKickRightExtension);
        SetNumber(CabinetRearToeKickHeightBox, options.RearToeKickHeight);
        SetNumber(CabinetRearToeKickRearInsetBox, options.RearToeKickRearInset);
        SetNumber(CabinetRearToeKickLeftExtensionBox, options.RearToeKickLeftExtension);
        SetNumber(CabinetRearToeKickRightExtensionBox, options.RearToeKickRightExtension);
    }

    private static void SetNumber(TextBox textBox, double value) =>
        textBox.Text = value.ToString("0.###", CultureInfo.CurrentCulture);

    private void ApplyPlanCabinetOptions(PlanCabinetOptions options)
    {
        SetNumber(PlanCabinetLengthBox, options.Length);
        SetNumber(PlanCabinetDepthBox, options.Depth);
        SetNumber(PlanCabinetThicknessBox, options.Thickness);
        PlanCabinetGenerateDoorsCheck.IsChecked = options.GenerateDoors;
        SetNumber(PlanCabinetDoorThicknessBox, options.DoorThickness);
        SetNumber(PlanCabinetDoorGapBox, options.DoorGap);
        SetNumber(PlanCabinetDoorLeftExtensionBox, options.DoorLeftExtension);
        SetNumber(PlanCabinetDoorRightExtensionBox, options.DoorRightExtension);
        SetNumber(PlanCabinetDoorFrontClearanceBox, options.DoorFrontClearance);
        PlanCabinetDoorModeCombo.SelectedItem = PlanCabinetDoorModeCombo.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag?.ToString(),
                options.DoorCalculationMode.ToString(),
                StringComparison.Ordinal));
        PlanCabinetGenerateMiddlePanelsCheck.IsChecked = options.GenerateMiddlePanels;
        PlanCabinetMiddleSameDepthCheck.IsChecked = options.MiddlePanelSameDepthAsSide;
        PlanCabinetMiddleModeCombo.SelectedItem = PlanCabinetMiddleModeCombo.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Tag?.ToString(),
                options.MiddlePanelMode.ToString(),
                StringComparison.Ordinal));
        SetNumber(PlanCabinetTopFrontInsetBox, options.TopFrontInset);
        SetNumber(PlanCabinetTopRearInsetBox, options.TopRearInset);
        SetNumber(PlanCabinetLeftFrontExtensionBox, options.LeftSideFrontExtension);
        SetNumber(PlanCabinetLeftRearExtensionBox, options.LeftSideRearExtension);
        SetNumber(PlanCabinetRightFrontExtensionBox, options.RightSideFrontExtension);
        SetNumber(PlanCabinetRightRearExtensionBox, options.RightSideRearExtension);
        PlanCabinetBackboardRearCoverCheck.IsChecked = options.BackboardRearCover;
        SetNumber(PlanCabinetBackboardThicknessBox, options.BackboardThickness);
        SetNumber(PlanCabinetBackboardInsetBox, options.BackboardInset);
        SetNumber(PlanCabinetBackboardLeftGrooveBox, options.BackboardLeftGroove);
        SetNumber(PlanCabinetBackboardRightGrooveBox, options.BackboardRightGroove);
    }

    private void OnGenerateStandardCabinetClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadCabinetOptions(out var options, out var error))
        {
            SetCabinetStatus(error, Brushes.Firebrick);
            return;
        }

        CabinetRuntimeSettings.Save(options);
        RunDrawingCommand(
            "DC_CMD_CABINET_STANDARD",
            "移动鼠标放置正视图左下基点；俯视图在下方，侧视图在右侧。",
            SetCabinetStatus);
    }

    private void OnGenerateUniversalCabinetClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadCabinetOptions(out var options, out var error))
        {
            SetCabinetStatus(error, Brushes.Firebrick);
            return;
        }

        CabinetRuntimeSettings.Save(options);
        RunDrawingCommand(
            "DC_CMD_CABINET_UNIVERSAL",
            "点取正视图两个对角点确定任意宽高，其余结构使用当前柜体参数。",
            SetCabinetStatus);
    }

    private void OnRefreshCabinetsClick(object sender, RoutedEventArgs e) =>
        RunDrawingCommand(
            "DC_CMD_CABINET_REFRESH",
            "正在按每个柜体保存的参数重建当前空间全部插件柜体。",
            SetCabinetStatus);

    private void OnResetCabinetDefaultsClick(object sender, RoutedEventArgs e)
    {
        CabinetRuntimeSettings.Save(CabinetOptions.Default);
        ApplyCabinetOptions(CabinetOptions.Default);
        SetCabinetStatus("标准单元柜参数已恢复默认值，图纸中已有柜体未修改。", Brushes.SeaGreen);
    }

    private void OnGeneratePlanCabinetClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPlanCabinetOptions(out var options, out var error))
        {
            SetPlanCabinetStatus(error, Brushes.Firebrick);
            return;
        }

        PlanCabinetRuntimeSettings.Save(options);
        RunDrawingCommand(
            "DC_CMD_PLAN_CABINET_GENERATE",
            "移动鼠标放置俯视图；W/+、S/-调整门数，前方默认在图形下侧。",
            SetPlanCabinetStatus);
    }

    private void OnReplacePlanCabinetDoorsClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadPlanCabinetOptions(out var options, out var error))
        {
            SetPlanCabinetStatus(error, Brushes.Firebrick);
            return;
        }

        PlanCabinetRuntimeSettings.Save(options);
        RunDrawingCommand(
            "DC_CMD_PLAN_CABINET_REPLACE_DOORS",
            "选择由 DCFST 生成的俯视图任一矩形；只替换门板，不修改柜体结构。",
            SetPlanCabinetStatus);
    }

    private void OnResetPlanCabinetDefaultsClick(object sender, RoutedEventArgs e)
    {
        PlanCabinetRuntimeSettings.Save(PlanCabinetOptions.Default);
        ApplyPlanCabinetOptions(PlanCabinetOptions.Default);
        SetPlanCabinetStatus("柜体俯视图参数已恢复默认值。", Brushes.SeaGreen);
    }

    private bool TryReadCabinetOptions(out CabinetOptions options, out string error)
    {
        options = CabinetOptions.Default;
        error = string.Empty;
        if (!TryReadNonNegative(CabinetPlanViewSpacingBox, "俯视图间距", out var planSpacing) ||
            !TryReadNonNegative(CabinetSideViewSpacingBox, "侧视图间距", out var sideSpacing) ||
            !TryReadPositive(CabinetWidthBox, "柜宽", out var width) ||
            !TryReadPositive(CabinetHeightBox, "柜高", out var height) ||
            !TryReadPositive(CabinetDepthBox, "柜深", out var depth) ||
            !TryReadPositive(CabinetThicknessBox, "板厚", out var thickness) ||
            !TryReadNonNegative(CabinetLeftSideTopExtensionBox, "左侧板上延伸", out var leftTop) ||
            !TryReadNonNegative(CabinetLeftSideBottomExtensionBox, "左侧板下延伸", out var leftBottom) ||
            !TryReadNonNegative(CabinetLeftSideFrontExtensionBox, "左侧板前延伸", out var leftFront) ||
            !TryReadNonNegative(CabinetLeftSideRearExtensionBox, "左侧板后延伸", out var leftRear) ||
            !TryReadNonNegative(CabinetRightSideTopExtensionBox, "右侧板上延伸", out var rightTop) ||
            !TryReadNonNegative(CabinetRightSideBottomExtensionBox, "右侧板下延伸", out var rightBottom) ||
            !TryReadNonNegative(CabinetRightSideFrontExtensionBox, "右侧板前延伸", out var rightFront) ||
            !TryReadNonNegative(CabinetRightSideRearExtensionBox, "右侧板后延伸", out var rightRear) ||
            !TryReadNonNegative(CabinetTopFrontInsetBox, "顶板前缩", out var topFront) ||
            !TryReadNonNegative(CabinetTopRearInsetBox, "顶板后缩", out var topRear) ||
            !TryReadNonNegative(CabinetBottomFrontInsetBox, "底板前缩", out var bottomFront) ||
            !TryReadNonNegative(CabinetBottomRearInsetBox, "底板后缩", out var bottomRear) ||
            !TryReadPositive(CabinetBackboardThicknessBox, "背板厚度", out var backThickness) ||
            !TryReadNonNegative(CabinetBackboardOverallInsetBox, "背板整体后缩", out var backInset) ||
            !TryReadNonNegative(CabinetBackboardTopExtensionBox, "背板上延伸", out var backTop) ||
            !TryReadNonNegative(CabinetBackboardBottomExtensionBox, "背板下延伸", out var backBottom) ||
            !TryReadNonNegative(CabinetBackboardLeftExtensionBox, "背板左延伸", out var backLeft) ||
            !TryReadNonNegative(CabinetBackboardRightExtensionBox, "背板右延伸", out var backRight) ||
            !TryReadPositive(CabinetTopRailHeightBox, "顶线高度", out var topRailHeight) ||
            !TryReadPositive(CabinetTopRailAuxiliaryDepthBox, "顶线辅助板深度", out var topRailAuxiliaryDepth) ||
            !TryReadNonNegative(CabinetTopRailLeftExtensionBox, "顶线左延伸", out var topRailLeft) ||
            !TryReadNonNegative(CabinetTopRailRightExtensionBox, "顶线右延伸", out var topRailRight) ||
            !TryReadNonNegative(CabinetTopRailRearInsetBox, "顶线后缩", out var topRailRear) ||
            !TryReadPositive(CabinetFrontToeKickHeightBox, "前踢脚线高度", out var frontKickHeight) ||
            !TryReadNonNegative(CabinetFrontToeKickFrontInsetBox, "前踢脚线前缩", out var frontKickInset) ||
            !TryReadNonNegative(CabinetFrontToeKickLeftExtensionBox, "前踢脚线左延伸", out var frontKickLeft) ||
            !TryReadNonNegative(CabinetFrontToeKickRightExtensionBox, "前踢脚线右延伸", out var frontKickRight) ||
            !TryReadPositive(CabinetRearToeKickHeightBox, "后踢脚线高度", out var rearKickHeight) ||
            !TryReadNonNegative(CabinetRearToeKickRearInsetBox, "后踢脚线后缩", out var rearKickInset) ||
            !TryReadNonNegative(CabinetRearToeKickLeftExtensionBox, "后踢脚线左延伸", out var rearKickLeft) ||
            !TryReadNonNegative(CabinetRearToeKickRightExtensionBox, "后踢脚线右延伸", out var rearKickRight))
        {
            error = "柜体宽、高、深、板厚和各板件尺寸必须大于 0；延伸、缩进与视图间距不能小于 0。";
            return false;
        }

        options = CabinetRuntimeSettings.Current with
        {
            GenerateElevation = CabinetGenerateElevationCheck.IsChecked == true,
            GeneratePlan = CabinetGeneratePlanCheck.IsChecked == true,
            GenerateSide = CabinetGenerateSideCheck.IsChecked == true,
            PlanViewSpacing = planSpacing,
            SideViewSpacing = sideSpacing,
            Width = width,
            Height = height,
            Depth = depth,
            Thickness = thickness,
            LeftSideTopExtension = leftTop,
            LeftSideBottomExtension = leftBottom,
            LeftSideFrontExtension = leftFront,
            LeftSideRearExtension = leftRear,
            RightSideTopExtension = rightTop,
            RightSideBottomExtension = rightBottom,
            RightSideFrontExtension = rightFront,
            RightSideRearExtension = rightRear,
            TopFrontInset = topFront,
            TopRearInset = topRear,
            BottomFrontInset = bottomFront,
            BottomRearInset = bottomRear,
            BackboardRearCoverMode = CabinetBackboardRearCoverCheck.IsChecked == true,
            BackboardThickness = backThickness,
            BackboardOverallInset = backInset,
            BackboardTopExtension = backTop,
            BackboardBottomExtension = backBottom,
            BackboardLeftExtension = backLeft,
            BackboardRightExtension = backRight,
            TopRailHeight = topRailHeight,
            TopRailAuxiliaryDepth = topRailAuxiliaryDepth,
            TopRailLeftExtension = topRailLeft,
            TopRailRightExtension = topRailRight,
            TopRailRearInset = topRailRear,
            FrontToeKickHeight = frontKickHeight,
            FrontToeKickFrontInset = frontKickInset,
            FrontToeKickLeftExtension = frontKickLeft,
            FrontToeKickRightExtension = frontKickRight,
            RearToeKickHeight = rearKickHeight,
            RearToeKickRearInset = rearKickInset,
            RearToeKickLeftExtension = rearKickLeft,
            RearToeKickRightExtension = rearKickRight
        };
        return options.TryValidate(out error);
    }

    private bool TryReadPlanCabinetOptions(out PlanCabinetOptions options, out string error)
    {
        options = PlanCabinetOptions.Default;
        var doorModeName = (PlanCabinetDoorModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var middleModeName = (PlanCabinetMiddleModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(doorModeName, out DoorPanelCalculationMode doorMode) ||
            !Enum.TryParse(middleModeName, out PlanCabinetMiddlePanelMode middleMode))
        {
            error = "请选择有效的门板缝隙模式和中竖板生成模式。";
            return false;
        }

        if (!TryReadPositive(PlanCabinetLengthBox, "柜长", out var length) ||
            !TryReadPositive(PlanCabinetDepthBox, "柜深", out var depth) ||
            !TryReadPositive(PlanCabinetThicknessBox, "板厚", out var thickness) ||
            !TryReadPositive(PlanCabinetDoorThicknessBox, "门板厚度", out var doorThickness) ||
            !TryReadNonNegative(PlanCabinetDoorGapBox, "门缝", out var doorGap) ||
            !TryReadNonNegative(PlanCabinetDoorLeftExtensionBox, "门板左延伸", out var doorLeft) ||
            !TryReadNonNegative(PlanCabinetDoorRightExtensionBox, "门板右延伸", out var doorRight) ||
            !TryReadNonNegative(PlanCabinetDoorFrontClearanceBox, "门板距柜体间距", out var frontClearance) ||
            !TryReadNonNegative(PlanCabinetTopFrontInsetBox, "顶板前缩", out var topFront) ||
            !TryReadNonNegative(PlanCabinetTopRearInsetBox, "顶板后缩", out var topRear) ||
            !TryReadNonNegative(PlanCabinetLeftFrontExtensionBox, "左侧板前延伸", out var leftFront) ||
            !TryReadNonNegative(PlanCabinetLeftRearExtensionBox, "左侧板后延伸", out var leftRear) ||
            !TryReadNonNegative(PlanCabinetRightFrontExtensionBox, "右侧板前延伸", out var rightFront) ||
            !TryReadNonNegative(PlanCabinetRightRearExtensionBox, "右侧板后延伸", out var rightRear) ||
            !TryReadPositive(PlanCabinetBackboardThicknessBox, "背板厚度", out var backThickness) ||
            !TryReadNonNegative(PlanCabinetBackboardInsetBox, "背板后缩", out var backInset) ||
            !TryReadNonNegative(PlanCabinetBackboardLeftGrooveBox, "背板左端插槽", out var backLeft) ||
            !TryReadNonNegative(PlanCabinetBackboardRightGrooveBox, "背板右端插槽", out var backRight))
        {
            error = "柜长、柜深、板厚、门板和背板厚度必须大于 0；门缝、延伸、缩进和插槽不能小于 0。";
            return false;
        }

        options = PlanCabinetRuntimeSettings.Current with
        {
            Length = length,
            Depth = depth,
            Thickness = thickness,
            GenerateDoors = PlanCabinetGenerateDoorsCheck.IsChecked == true,
            DoorThickness = doorThickness,
            DoorGap = doorGap,
            DoorLeftExtension = doorLeft,
            DoorRightExtension = doorRight,
            DoorCalculationMode = doorMode,
            DoorFrontClearance = frontClearance,
            GenerateMiddlePanels = PlanCabinetGenerateMiddlePanelsCheck.IsChecked == true,
            MiddlePanelSameDepthAsSide = PlanCabinetMiddleSameDepthCheck.IsChecked == true,
            MiddlePanelMode = middleMode,
            TopFrontInset = topFront,
            TopRearInset = topRear,
            LeftSideFrontExtension = leftFront,
            LeftSideRearExtension = leftRear,
            RightSideFrontExtension = rightFront,
            RightSideRearExtension = rightRear,
            BackboardRearCover = PlanCabinetBackboardRearCoverCheck.IsChecked == true,
            BackboardThickness = backThickness,
            BackboardInset = backInset,
            BackboardLeftGroove = backLeft,
            BackboardRightGroove = backRight
        };
        return options.TryValidate(out error);
    }

    private void LoadBoardModule()
    {
        BoardThicknessBox.Text = BoardRuntimeSettings.Thickness.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDepthBox.Text = BoardRuntimeSettings.Depth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTrimFaceWidthBox.Text = BoardRuntimeSettings.TrimFaceWidth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTrimAuxiliaryDepthBox.Text = BoardRuntimeSettings.TrimAuxiliaryDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardSideTrimSectionTypeCombo.SelectedItem = BoardSideTrimSectionTypeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), BoardRuntimeSettings.Current.SideTrimSectionType, StringComparison.Ordinal));
        BoardTopTrimSectionTypeCombo.SelectedItem = BoardTopTrimSectionTypeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), BoardRuntimeSettings.Current.TopTrimSectionType, StringComparison.Ordinal));
        BoardCenterDepthBox.Text = BoardRuntimeSettings.CenterDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTagScaleBox.Text = BoardRuntimeSettings.TagScale.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTagTypeCombo.SelectedItem = BoardTagTypeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), BoardRuntimeSettings.TagType, StringComparison.Ordinal));
        BoardPropertyNormalRadio.IsChecked = BoardRuntimeSettings.PropertyType == "普通板件";
        BoardPropertySideRadio.IsChecked = BoardRuntimeSettings.PropertyType == "侧收口";
        BoardPropertyTopRadio.IsChecked = BoardRuntimeSettings.PropertyType == "顶收口";
        BoardPropertyVisibleRadio.IsChecked = BoardRuntimeSettings.PropertyType == "见光板";
        BoardLineThicknessBox.Text = BoardRuntimeSettings.LineThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDoubleModeCombo.SelectedIndex = BoardRuntimeSettings.DoubleAsRectangle ? 1 : 0;
        BoardTripleModeCombo.SelectedIndex = BoardRuntimeSettings.TripleAsRectangle ? 1 : 0;
        BoardConvertLineThicknessBox.Text = BoardRuntimeSettings.ConvertLineThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BoardConvertDoubleModeCombo.SelectedIndex = BoardRuntimeSettings.ConvertDoubleAsRectangle ? 1 : 0;
        BoardConvertTripleModeCombo.SelectedIndex = BoardRuntimeSettings.ConvertTripleAsRectangle ? 1 : 0;
        BoardExtendStartBox.Text = BoardRuntimeSettings.ExtendStart.ToString("0.###", CultureInfo.CurrentCulture);
        BoardExtendEndBox.Text = BoardRuntimeSettings.ExtendEnd.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPartitionTopExtensionBox.Text = BoardRuntimeSettings.DoorPartitionTopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPartitionBottomExtensionBox.Text = BoardRuntimeSettings.DoorPartitionBottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPartitionMaximumWidthBox.Text = BoardRuntimeSettings.DoorPartitionMaximumWidth.ToString("0.###", CultureInfo.CurrentCulture);
        DoorPartitionDefaultCountBox.Text = BoardRuntimeSettings.DoorPartitionDefaultCount.ToString(CultureInfo.CurrentCulture);
        DoorPartitionSmartRadio.IsChecked = BoardRuntimeSettings.DoorPartitionSmartMode;
        DoorPartitionCountRadio.IsChecked = !BoardRuntimeSettings.DoorPartitionSmartMode;
        ShoeShelfDepthBox.Text = BoardRuntimeSettings.ShoeShelfDepth.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfSmartAngleRadio.IsChecked = BoardRuntimeSettings.ShoeShelfSmartAngle;
        ShoeShelfManualAngleRadio.IsChecked = !BoardRuntimeSettings.ShoeShelfSmartAngle;
        ShoeShelfAngleBox.Text = BoardRuntimeSettings.ShoeShelfAngleDegrees.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfTargetSpacingBox.Text = BoardRuntimeSettings.ShoeShelfTargetSpacing.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfTopReserveBox.Text = BoardRuntimeSettings.ShoeShelfTopReserve.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfBottomReserveBox.Text = BoardRuntimeSettings.ShoeShelfBottomReserve.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfFrontClearanceBox.Text = BoardRuntimeSettings.ShoeShelfFrontClearance.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfRearClearanceBox.Text = BoardRuntimeSettings.ShoeShelfRearClearance.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfStopperHeightBox.Text = BoardRuntimeSettings.ShoeShelfStopperHeight.ToString("0.###", CultureInfo.CurrentCulture);
        ShoeShelfGenerateStopperCheck.IsChecked = BoardRuntimeSettings.ShoeShelfGenerateStopper;
        BoardDisplaceDistanceBox.Text = AdvancedBoardRuntimeSettings.DisplaceDistance.ToString("0.###", CultureInfo.CurrentCulture);
        AbaFirstThicknessBox.Text = AdvancedBoardRuntimeSettings.AbaFirstThickness.ToString("0.###", CultureInfo.CurrentCulture);
        AbaSecondThicknessBox.Text = AdvancedBoardRuntimeSettings.AbaSecondThickness.ToString("0.###", CultureInfo.CurrentCulture);
        AbaThirdThicknessBox.Text = AdvancedBoardRuntimeSettings.AbaThirdThickness.ToString("0.###", CultureInfo.CurrentCulture);
        AbaFirstDepthBox.Text = AdvancedBoardRuntimeSettings.AbaFirstDepth.ToString("0.###", CultureInfo.CurrentCulture);
        AbaSecondDepthBox.Text = AdvancedBoardRuntimeSettings.AbaSecondDepth.ToString("0.###", CultureInfo.CurrentCulture);
        AbaThirdDepthBox.Text = AdvancedBoardRuntimeSettings.AbaThirdDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDivideDepthBox.Text = AdvancedBoardRuntimeSettings.DivideDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDivideMaximumClearBox.Text = AdvancedBoardRuntimeSettings.DivideMaximumClearSpan.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDivideSmartRadio.IsChecked = AdvancedBoardRuntimeSettings.DivideSmartMode;
        BoardDivideCountRadio.IsChecked = !AdvancedBoardRuntimeSettings.DivideSmartMode;
        BoardDivideRemainderCheck.IsChecked = AdvancedBoardRuntimeSettings.DivideRemainderAtEnd;
        BackboardThicknessBox.Text = AdvancedBoardRuntimeSettings.BackboardThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardInsetBox.Text = AdvancedBoardRuntimeSettings.BackboardInset.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardLeftBox.Text = AdvancedBoardRuntimeSettings.BackboardLeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardRightBox.Text = AdvancedBoardRuntimeSettings.BackboardRightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardTopBox.Text = AdvancedBoardRuntimeSettings.BackboardTopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardBottomBox.Text = AdvancedBoardRuntimeSettings.BackboardBottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardGeneratePlanRadio.IsChecked = AdvancedBoardRuntimeSettings.BackboardGeneratePlan;
        BackboardGenerateElevationRadio.IsChecked = AdvancedBoardRuntimeSettings.BackboardGenerateElevation;
        BackboardHideElevationCheck.IsChecked = AdvancedBoardRuntimeSettings.BackboardHideElevationLayer;
    }

    private void OnDrawBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DRAW", "请选择板件起点和终点。", ReadBoardSettings);

    private void OnDrawCenterBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_CENTER", "请在柜体内空中单击，识别失败时可手动框选。", ReadBoardSettings);

    private void OnBoardSingleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_SINGLE", "请在矩形内空中点击，输入偏移距离后移动鼠标预览并确认。", ReadOffsetLineSettings);

    private void OnBoardDoubleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_DOUBLE", "请在矩形内空中点击，输入偏移距离后移动鼠标预览并确认。", ReadOffsetLineSettings);

    private void OnBoardTripleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TRIPLE", "请在矩形内空中点击，输入偏移距离后移动鼠标预览并确认。", ReadOffsetLineSettings);

    private void OnBoardLineToDoubleClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TO_DOUBLE", "请选择要转换的单线。", ReadConvertLineSettings);

    private void OnBoardLineToTripleClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TO_TRIPLE", "请选择要转换的单线。", ReadConvertLineSettings);

    private void OnDoorPartitionClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_DOOR_PARTITION_GUIDE", "请在横向基准线上点取起点，再移动鼠标预览分区。", ReadDoorPartitionSettings);

    private void OnShoeShelfClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_SHOE_SHELVES", "请在鞋柜侧视内空中单击，再移动鼠标选择柜前方向。", ReadShoeShelfSettings);

    private void OnDisplaceBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DISPLACE", "请在柜体内空中单击，输入内退距离后移动鼠标选择方向。", ReadDisplaceSettings);

    private void OnAbaBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_ABA", "请在柜体内空中单击，输入空间内退后移动鼠标选择方向。", ReadAbaSettings);

    private void OnDivideBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DIVIDE", "请在柜体内空中单击，按格数模式输入格数后移动鼠标选择方向和余量侧。", ReadDivideSettings);

    private void OnBackboardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_BACKBOARD", "请在柜体内空中单击，再移动鼠标指定后沿方向。", ReadBackboardSettings);

    private void OnSplitBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_SPLIT", "选择矩形后点击两个分割位置；横板按X、竖板按Y生成三个独立矩形，同位置则生成两个。", () => null);

    private void OnMergeBoardsClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_MERGE", "请选择两块边界对齐的相邻矩形板件。", () => null);

    private void OnExtendBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_EXTEND", "请点击板件靠近需要延伸的一端；只延伸长度，厚度保持不变。", () => null);

    private void OnTrimBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_TRIM", "直接点击需要删除的直线段或矩形端部，自动修剪到点击端内侧最近交线。", () => null);

    private void OnShelfProjectClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_SHELF_PROJECT", "请选择正视图层板或立板，生成对应的层板侧视或立板俯视辅助图。", () => null);

    private void RunBoardCommand(string command, string status, Func<string?> readSettings)
    {
        var error = readSettings();
        if (error is not null)
        {
            SetBoardStatus(error, Brushes.Firebrick);
            return;
        }

        RunDrawingCommand(command, status, SetBoardStatus);
    }

    private string? ReadBoardSettings()
    {
        if (!TryReadPositive(BoardThicknessBox, "全局板厚", out var thickness) ||
            !TryReadPositive(BoardDepthBox, "板件深度", out var depth) ||
            !TryReadPositive(BoardTrimFaceWidthBox, "收口正面宽", out var trimFaceWidth) ||
            !TryReadPositive(BoardTrimAuxiliaryDepthBox, "辅助板深度", out var trimAuxiliaryDepth) ||
            !TryReadPositive(BoardCenterDepthBox, "中分板深度", out var centerDepth) ||
            !double.TryParse(BoardTagScaleBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var tagScale) ||
            tagScale < 0.1 || tagScale > 10)
        {
            return "板厚和深度必须大于 0，标识比例需为 0.1~10。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            Thickness = thickness,
            Depth = depth,
            CenterDepth = centerDepth,
            TrimFaceWidth = trimFaceWidth,
            TrimAuxiliaryDepth = trimAuxiliaryDepth,
            SideTrimSectionType = (BoardSideTrimSectionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "L",
            TopTrimSectionType = (BoardTopTrimSectionTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "L",
            TagScale = tagScale,
            TagType = (BoardTagTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "活动层板带文字",
            PropertyType = BoardPropertySideRadio.IsChecked == true ? "侧收口" :
                BoardPropertyTopRadio.IsChecked == true ? "顶收口" :
                BoardPropertyVisibleRadio.IsChecked == true ? "见光板" : "普通板件"
        });
        return null;
    }

    private string? ReadOffsetLineSettings()
    {
        if (!TryReadPositive(BoardLineThicknessBox, "偏移板厚", out var thickness))
        {
            return "偏移板厚必须大于 0。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            LineThickness = thickness,
            DoubleAsRectangle = BoardDoubleModeCombo.SelectedIndex == 1,
            TripleAsRectangle = BoardTripleModeCombo.SelectedIndex == 1
        });
        return null;
    }

    private string? ReadConvertLineSettings()
    {
        if (!TryReadPositive(BoardConvertLineThicknessBox, "板件厚度", out var thickness) ||
            !TryReadNonNegative(BoardExtendStartBox, "上/左延伸", out var extendStart) ||
            !TryReadNonNegative(BoardExtendEndBox, "下/右延伸", out var extendEnd))
        {
            return "板件厚度必须大于 0，延伸值不能小于 0。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            ConvertLineThickness = thickness,
            ExtendStart = extendStart,
            ExtendEnd = extendEnd,
            ConvertDoubleAsRectangle = BoardConvertDoubleModeCombo.SelectedIndex == 1,
            ConvertTripleAsRectangle = BoardConvertTripleModeCombo.SelectedIndex == 1
        });
        return null;
    }

    private string? ReadDoorPartitionSettings()
    {
        if (!TryReadNonNegative(DoorPartitionTopExtensionBox, "上延伸高度", out var topExtension) ||
            !TryReadNonNegative(DoorPartitionBottomExtensionBox, "下延伸高度", out var bottomExtension) ||
            topExtension + bottomExtension <= 0 ||
            !TryReadPositive(DoorPartitionMaximumWidthBox, "最大门扇宽度", out var maximumWidth) ||
            !int.TryParse(DoorPartitionDefaultCountBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var defaultCount) ||
            defaultCount < 1)
        {
            return "上下延伸不能同时为 0，最大门扇宽度必须大于 0，默认数量至少为 1。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            DoorPartitionTopExtension = topExtension,
            DoorPartitionBottomExtension = bottomExtension,
            DoorPartitionMaximumWidth = maximumWidth,
            DoorPartitionDefaultCount = defaultCount,
            DoorPartitionSmartMode = DoorPartitionSmartRadio.IsChecked == true
        });
        return null;
    }

    private string? ReadShoeShelfSettings()
    {
        if (!TryReadPositive(ShoeShelfDepthBox, "层板实际深度", out var depth) ||
            !TryReadPositive(ShoeShelfAngleBox, "层板角度", out var angle) || angle >= 89 ||
            !TryReadPositive(ShoeShelfTargetSpacingBox, "目标层距", out var targetSpacing) ||
            !TryReadNonNegative(ShoeShelfTopReserveBox, "顶部预留", out var topReserve) ||
            !TryReadNonNegative(ShoeShelfBottomReserveBox, "底部预留", out var bottomReserve) ||
            !TryReadNonNegative(ShoeShelfFrontClearanceBox, "前预留", out var frontClearance) ||
            !TryReadNonNegative(ShoeShelfRearClearanceBox, "后预留", out var rearClearance) ||
            !TryReadPositive(ShoeShelfStopperHeightBox, "挡条高出层板", out var stopperHeight))
        {
            return "层板深度、目标层距和挡条高度必须大于 0，角度须大于 0 且小于 89°，预留不能为负数。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            ShoeShelfDepth = depth,
            ShoeShelfSmartAngle = ShoeShelfSmartAngleRadio.IsChecked == true,
            ShoeShelfAngleDegrees = angle,
            ShoeShelfTargetSpacing = targetSpacing,
            ShoeShelfTopReserve = topReserve,
            ShoeShelfBottomReserve = bottomReserve,
            ShoeShelfFrontClearance = frontClearance,
            ShoeShelfRearClearance = rearClearance,
            ShoeShelfStopperHeight = stopperHeight,
            ShoeShelfGenerateStopper = ShoeShelfGenerateStopperCheck.IsChecked == true
        });
        return null;
    }

    private string? ReadDisplaceSettings()
    {
        if (!TryReadPositive(BoardDisplaceDistanceBox, "内退距离", out var distance))
        {
            return "内退距离必须大于 0。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            DisplaceDistance = distance
        });
        return null;
    }

    private string? ReadAbaSettings()
    {
        if (!TryReadPositive(AbaFirstThicknessBox, "第一层厚度", out var firstThickness) ||
            !TryReadPositive(AbaSecondThicknessBox, "第二层厚度", out var secondThickness) ||
            !TryReadPositive(AbaThirdThicknessBox, "第三层厚度", out var thirdThickness) ||
            !TryReadPositive(AbaFirstDepthBox, "第一层深度", out var firstDepth) ||
            !TryReadPositive(AbaSecondDepthBox, "第二层深度", out var secondDepth) ||
            !TryReadPositive(AbaThirdDepthBox, "第三层深度", out var thirdDepth))
        {
            return "ABA夹心层板的三层厚度和深度必须大于 0。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            AbaOuterThickness = firstThickness,
            AbaCoreThickness = secondThickness,
            AbaDepth = Math.Max(firstDepth, Math.Max(secondDepth, thirdDepth)),
            AbaFirstThickness = firstThickness,
            AbaSecondThickness = secondThickness,
            AbaThirdThickness = thirdThickness,
            AbaFirstDepth = firstDepth,
            AbaSecondDepth = secondDepth,
            AbaThirdDepth = thirdDepth
        });
        return null;
    }

    private string? ReadDivideSettings()
    {
        if (!TryReadPositive(BoardDivideDepthBox, "均分板深", out var depth) ||
            !TryReadPositive(BoardDivideMaximumClearBox, "最大净空", out var maximumClearSpan))
        {
            return "均分板深和最大净空必须大于 0。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            DivideDepth = depth,
            DivideMaximumClearSpan = maximumClearSpan,
            DivideSmartMode = BoardDivideSmartRadio.IsChecked == true,
            DivideRemainderAtEnd = BoardDivideRemainderCheck.IsChecked == true
        });
        return null;
    }

    private string? ReadBackboardSettings()
    {
        if (!TryReadPositive(BackboardThicknessBox, "背板厚度", out var thickness) ||
            !TryReadNonNegative(BackboardInsetBox, "缩进距离", out var inset) ||
            !TryReadNonNegative(BackboardLeftBox, "左延伸", out var left) ||
            !TryReadNonNegative(BackboardRightBox, "右延伸", out var right) ||
            !TryReadNonNegative(BackboardTopBox, "上延伸", out var top) ||
            !TryReadNonNegative(BackboardBottomBox, "下延伸", out var bottom))
        {
            return "背板厚度必须大于 0，缩进与延伸不能小于 0。";
        }

        var generatePlan = BackboardGeneratePlanRadio.IsChecked == true;
        var generateElevation = BackboardGenerateElevationRadio.IsChecked == true;
        if (!generatePlan && !generateElevation)
        {
            return "俯视图背板和正视图背板至少选择一种。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            BackboardThickness = thickness,
            BackboardInset = inset,
            BackboardLeftExtension = left,
            BackboardRightExtension = right,
            BackboardTopExtension = top,
            BackboardBottomExtension = bottom,
            BackboardGeneratePlan = generatePlan,
            BackboardGenerateElevation = generateElevation,
            BackboardHideElevationLayer = BackboardHideElevationCheck.IsChecked == true
        });
        return null;
    }

    private void OnPartsStatisticsClick(object sender, RoutedEventArgs e) =>
        RunDrawingCommand("DC_CMD_PARTS_STATISTICS", "请框选需要统计的部件。", SetStatisticsStatus);

    private void LoadStatisticsModule()
    {
        var settings = ToolboxSettingsRuntime.Settings;
        var wholeHouse = settings.WholeHouseStatistics;
        WholeHouseCabinetBoardsCheck.IsChecked = wholeHouse.IncludeCabinetBoards;
        WholeHouseStandardDoorsCheck.IsChecked = wholeHouse.IncludeStandardDoors;
        WholeHouseFrameDoorsCheck.IsChecked = wholeHouse.IncludeFrameDoors;
        WholeHouseBoardPropertiesCheck.IsChecked = wholeHouse.IncludeBoardProperties;
        WholeHouseHangingRodsCheck.IsChecked = wholeHouse.IncludeHangingRods;
        WholeHouseLightStripsCheck.IsChecked = wholeHouse.IncludeLightStrips;
        WholeHouseHandlesCheck.IsChecked = wholeHouse.IncludeHandles;
        WholeHouseConcealedHandlesCheck.IsChecked = wholeHouse.IncludeConcealedHandles;
        WholeHouseHingesCheck.IsChecked = wholeHouse.IncludeHinges;
        WholeHouseSlidesCheck.IsChecked = wholeHouse.IncludeSlides;
        WholeHouseTableScaleBox.Text = ToStatisticsText(wholeHouse.TableScaleFactor);
        WholeHouseMaxRowsBox.Text = wholeHouse.MaxRowsPerPage.ToString(CultureInfo.CurrentCulture);

        var door = settings.DoorStatistics;
        DoorStatisticsTableScaleBox.Text = ToStatisticsText(door.TableScaleFactor);
        DoorStatisticsThicknessBox.Text = ToStatisticsText(door.Thickness);
        DoorStatisticsMaxRowsBox.Text = door.MaxRowsPerPage.ToString(CultureInfo.CurrentCulture);

        var frame = settings.FrameDoorStatistics;
        FrameDoorCabinetThicknessBox.Text = ToStatisticsText(frame.CabinetThickness);
        FrameDoorBoardCoreCountBox.Text = frame.BoardCoreCount.ToString(CultureInfo.CurrentCulture);
        FrameDoorGlassCoreCountBox.Text = frame.GlassCoreCount.ToString(CultureInfo.CurrentCulture);
        FrameDoorCoreThicknessBox.Text = ToStatisticsText(frame.CoreThickness);
        FrameDoorSideFrameWidthBox.Text = ToStatisticsText(frame.SideFrameWidth);
        FrameDoorTopFrameWidthBox.Text = ToStatisticsText(frame.TopFrameWidth);
        FrameDoorMiddleFrameWidthBox.Text = ToStatisticsText(frame.MiddleFrameWidth);
        FrameDoorBottomFrameWidthBox.Text = ToStatisticsText(frame.BottomFrameWidth);
        FrameDoorRightFrameTenonBox.Text = ToStatisticsText(frame.RightFrameTenon);
        FrameDoorMiddleFrameTenonBox.Text = ToStatisticsText(frame.MiddleFrameTenon);
        FrameDoorCoreTopTenonBox.Text = ToStatisticsText(frame.CoreTopTenon);
        FrameDoorCoreBottomTenonBox.Text = ToStatisticsText(frame.CoreBottomTenon);
        FrameDoorCoreLeftTenonBox.Text = ToStatisticsText(frame.CoreLeftTenon);
        FrameDoorCoreRightTenonBox.Text = ToStatisticsText(frame.CoreRightTenon);
        FrameDoorCenterMiddleFrameCheck.IsChecked = frame.CenterMiddleFrame;
        FrameDoorLowerFramePositionBox.Text = ToStatisticsText(frame.LowerFramePosition);
        FrameDoorTableScaleBox.Text = ToStatisticsText(frame.TableScaleFactor);
        FrameDoorMaxRowsBox.Text = frame.MaxRowsPerPage.ToString(CultureInfo.CurrentCulture);

        var cabinet = settings.CabinetBom;
        CabinetBomBasicBoardsCheck.IsChecked = cabinet.IncludeBasicBoards;
        CabinetBomBackboardsCheck.IsChecked = cabinet.IncludeBackboards;
        CabinetBomSummaryCheck.IsChecked = cabinet.IncludeCabinetSummary;
        CabinetBomTableScaleBox.Text = ToStatisticsText(cabinet.TableScaleFactor);
        CabinetBomMaxRowsBox.Text = cabinet.MaxRowsPerPage.ToString(CultureInfo.CurrentCulture);

        var nesting = settings.PanelNesting;
        PanelNestingWoodGrainRadio.IsChecked = nesting.UseWoodGrain;
        PanelNestingSolidColorRadio.IsChecked = nesting.UseSolidColor;
        PanelNesting48SheetRadio.IsChecked = nesting.Use48Sheet;
        PanelNesting49SheetRadio.IsChecked = nesting.Use49Sheet;
        PanelNestingCabinetCheck.IsChecked = nesting.IncludeCabinetBoards;
        PanelNestingFlatDoorCheck.IsChecked = nesting.IncludeFlatDoors;
        PanelNestingTableScaleBox.Text = ToStatisticsText(nesting.TableScaleFactor);
        PanelNestingMaxRowsBox.Text = nesting.MaxRowsPerPage.ToString(CultureInfo.CurrentCulture);
    }

    private void OnWholeHouseStatisticsClick(object sender, RoutedEventArgs e)
    {
        if (!TrySaveWholeHouseStatisticsSettings(out var error))
        {
            SetStatisticsStatus(error, Brushes.Firebrick);
            return;
        }

        RunDrawingCommand("DC_CMD_WHOLE_HOUSE_BOM", "请框选参与一键全屋拆单的对象。", SetStatisticsStatus);
    }

    private bool TrySaveWholeHouseStatisticsSettings(out string error)
    {
        var options = new WholeHouseStatisticsOptions(
            WholeHouseCabinetBoardsCheck.IsChecked == true,
            WholeHouseStandardDoorsCheck.IsChecked == true,
            WholeHouseFrameDoorsCheck.IsChecked == true,
            WholeHouseBoardPropertiesCheck.IsChecked == true,
            WholeHouseHangingRodsCheck.IsChecked == true,
            WholeHouseLightStripsCheck.IsChecked == true,
            WholeHouseHandlesCheck.IsChecked == true,
            WholeHouseConcealedHandlesCheck.IsChecked == true,
            WholeHouseHingesCheck.IsChecked == true,
            WholeHouseSlidesCheck.IsChecked == true,
            double.TryParse(WholeHouseTableScaleBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var scale)
                ? scale
                : 0,
            int.TryParse(WholeHouseMaxRowsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var maxRows)
                ? maxRows
                : 0);

        if (!options.HasAnyModule)
        {
            error = "请至少勾选一个全屋拆单项目。";
            return false;
        }

        if (options.TableScaleFactor is < 1 or > 100 ||
            double.IsNaN(options.TableScaleFactor) ||
            double.IsInfinity(options.TableScaleFactor))
        {
            error = "表格缩放倍数需为 1~100。";
            return false;
        }

        if (options.MaxRowsPerPage is < 1 or > 500)
        {
            error = "每页最大行数需为 1~500。";
            return false;
        }

        ToolboxSettingsRuntime.Update(settings => settings.WholeHouseStatistics = options);
        error = string.Empty;
        return true;
    }

    private void OnDoorStatisticsClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveDoorStatisticsSettings(out var error))
        {
            RunDrawingCommand("DC_CMD_DOOR_STATISTICS", "请框选普通门板或抽面。", SetStatisticsStatus);
        }
        else
        {
            SetStatisticsStatus(error, Brushes.Firebrick);
        }
    }

    private void OnFrameDoorStatisticsClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveFrameDoorStatisticsSettings(out var error))
        {
            RunDrawingCommand("DC_CMD_FRAME_DOOR_STATISTICS", "请框选边框/玻璃门板。", SetStatisticsStatus);
        }
        else
        {
            SetStatisticsStatus(error, Brushes.Firebrick);
        }
    }

    private void OnCabinetBomClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveCabinetBomSettings(out var error))
        {
            RunDrawingCommand("DC_CMD_CABINET_BOM", "请框选柜体板件。", SetStatisticsStatus);
        }
        else
        {
            SetStatisticsStatus(error, Brushes.Firebrick);
        }
    }

    private void OnBoardStatisticsClick(object sender, RoutedEventArgs e)
    {
        if (TrySavePanelNestingSettings(out var error))
        {
            RunDrawingCommand("DC_CMD_BOARD_STATISTICS", "请框选需要统计排版的板件。", SetStatisticsStatus);
        }
        else
        {
            SetStatisticsStatus(error, Brushes.Firebrick);
        }
    }

    private void OnBomVisualCopyClick(object sender, RoutedEventArgs e) =>
        RunDrawingCommand("DC_CMD_BOM_VISUAL_COPY", "请按命令提示复制外观图。", SetStatisticsStatus);

    private bool TrySaveDoorStatisticsSettings(out string error)
    {
        if (!TryReadTableOptions(
                DoorStatisticsTableScaleBox,
                DoorStatisticsMaxRowsBox,
                out var scale,
                out var maxRows,
                out error) ||
            !TryReadPositive(DoorStatisticsThicknessBox, string.Empty, out var thickness))
        {
            error = string.IsNullOrEmpty(error) ? "柜门默认厚度必须大于 0。" : error;
            return false;
        }

        ToolboxSettingsRuntime.Update(settings => settings.DoorStatistics = new DoorStatisticsOptions(
            scale,
            thickness,
            maxRows));
        error = string.Empty;
        return true;
    }

    private bool TrySaveFrameDoorStatisticsSettings(out string error)
    {
        if (!TryReadNonNegativeInteger(FrameDoorBoardCoreCountBox, out var boardCoreCount) ||
            !TryReadNonNegativeInteger(FrameDoorGlassCoreCountBox, out var glassCoreCount) ||
            boardCoreCount + glassCoreCount == 0)
        {
            error = "板芯和玻璃芯数量必须为非负整数，且合计至少为 1。";
            return false;
        }

        var boxes = new[]
        {
            FrameDoorCabinetThicknessBox,
            FrameDoorCoreThicknessBox,
            FrameDoorSideFrameWidthBox,
            FrameDoorTopFrameWidthBox,
            FrameDoorMiddleFrameWidthBox,
            FrameDoorBottomFrameWidthBox,
            FrameDoorRightFrameTenonBox,
            FrameDoorMiddleFrameTenonBox,
            FrameDoorCoreTopTenonBox,
            FrameDoorCoreBottomTenonBox,
            FrameDoorCoreLeftTenonBox,
            FrameDoorCoreRightTenonBox,
            FrameDoorLowerFramePositionBox
        };
        var values = new double[boxes.Length];
        for (var index = 0; index < boxes.Length; index++)
        {
            if (!TryReadPositive(boxes[index], string.Empty, out values[index]))
            {
                error = "边框门板尺寸和进榫参数必须大于 0。";
                return false;
            }
        }

        if (!TryReadTableOptions(
                FrameDoorTableScaleBox,
                FrameDoorMaxRowsBox,
                out var scale,
                out var maxRows,
                out error))
        {
            return false;
        }

        ToolboxSettingsRuntime.Update(settings => settings.FrameDoorStatistics = new FrameDoorStatisticsOptions
        {
            CabinetThickness = values[0],
            BoardCoreCount = boardCoreCount,
            GlassCoreCount = glassCoreCount,
            CoreThickness = values[1],
            SideFrameWidth = values[2],
            TopFrameWidth = values[3],
            MiddleFrameWidth = values[4],
            BottomFrameWidth = values[5],
            RightFrameTenon = values[6],
            MiddleFrameTenon = values[7],
            CoreTopTenon = values[8],
            CoreBottomTenon = values[9],
            CoreLeftTenon = values[10],
            CoreRightTenon = values[11],
            CenterMiddleFrame = FrameDoorCenterMiddleFrameCheck.IsChecked == true,
            LowerFramePosition = values[12],
            TableScaleFactor = scale,
            MaxRowsPerPage = maxRows
        });
        error = string.Empty;
        return true;
    }

    private bool TrySaveCabinetBomSettings(out string error)
    {
        var options = new CabinetBomOptions(
            CabinetBomBasicBoardsCheck.IsChecked == true,
            CabinetBomBackboardsCheck.IsChecked == true,
            CabinetBomSummaryCheck.IsChecked == true,
            0,
            0);
        if (!options.HasAnyModule)
        {
            error = "请至少勾选一个柜体拆单项目。";
            return false;
        }

        if (!TryReadTableOptions(
                CabinetBomTableScaleBox,
                CabinetBomMaxRowsBox,
                out var scale,
                out var maxRows,
                out error))
        {
            return false;
        }

        ToolboxSettingsRuntime.Update(settings => settings.CabinetBom = options with
        {
            TableScaleFactor = scale,
            MaxRowsPerPage = maxRows
        });
        error = string.Empty;
        return true;
    }

    private bool TrySavePanelNestingSettings(out string error)
    {
        var useWoodGrain = PanelNestingWoodGrainRadio.IsChecked == true;
        var useSolidColor = PanelNestingSolidColorRadio.IsChecked == true;
        var use48Sheet = PanelNesting48SheetRadio.IsChecked == true;
        var use49Sheet = PanelNesting49SheetRadio.IsChecked == true;
        var includeCabinet = PanelNestingCabinetCheck.IsChecked == true;
        var includeFlatDoor = PanelNestingFlatDoorCheck.IsChecked == true;
        if (useWoodGrain == useSolidColor || use48Sheet == use49Sheet)
        {
            error = "板材材质和开料板规格必须分别选择一项。";
            return false;
        }

        if (!includeCabinet && !includeFlatDoor)
        {
            error = "请至少选择一种板件统计对象。";
            return false;
        }

        if (!TryReadTableOptions(
                PanelNestingTableScaleBox,
                PanelNestingMaxRowsBox,
                out var scale,
                out var maxRows,
                out error))
        {
            return false;
        }

        ToolboxSettingsRuntime.Update(settings => settings.PanelNesting = new PanelNestingOptions(
            useWoodGrain,
            useSolidColor,
            use48Sheet,
            use49Sheet,
            includeCabinet,
            includeFlatDoor,
            scale,
            maxRows));
        error = string.Empty;
        return true;
    }

    private static bool TryReadTableOptions(
        TextBox scaleBox,
        TextBox maxRowsBox,
        out double scale,
        out int maxRows,
        out string error)
    {
        if (!double.TryParse(scaleBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out scale) ||
            double.IsNaN(scale) ||
            double.IsInfinity(scale) ||
            scale is < 1 or > 100)
        {
            maxRows = 0;
            error = "表格缩放倍数需为 1~100。";
            return false;
        }

        if (!int.TryParse(maxRowsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out maxRows) ||
            maxRows is < 1 or > 500)
        {
            error = "每页最大行数需为 1~500。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryReadNonNegativeInteger(TextBox textBox, out int value) =>
        int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) && value >= 0;

    private static string ToStatisticsText(double value) =>
        value.ToString("0.###", CultureInfo.CurrentCulture);

    private void SetStatisticsStatus(string message, Brush brush)
    {
        StatisticsStatusText.Text = message;
        StatisticsStatusText.Foreground = brush;
    }

    private static bool TryReadPositive(TextBox textBox, string _, out double value) =>
        double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value > 0;

    private static bool TryReadNonNegative(TextBox textBox, string _, out double value) =>
        double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value >= 0;

    private void OnLayerManagerClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_MANAGER", "打开快速图层管理器。", SetDrawingLayerStatus);
    private void OnCreateStandardLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_STANDARDIZE", "正在补齐标准图层。", SetDrawingLayerStatus);
    private void OnNormalizeLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_NORMALIZE", "仅归类标注、文字、填充和已知插件图层。", SetDrawingLayerStatus);
    private void OnPurgeDrawingClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_DRAWING_PURGE", "请在确认后执行深度清理。", SetDrawingLayerStatus);
    private void OnHideHardwareLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_HIDE_HARDWARE", "已请求隐藏五金图层。", SetDrawingLayerStatus);
    private void OnHideDoorLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_HIDE_DOOR", "已请求隐藏门板图层。", SetDrawingLayerStatus);
    private void OnHideCabinetLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_HIDE_CABINET", "已请求隐藏柜体图层。", SetDrawingLayerStatus);
    private void OnHideAnnotationLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_HIDE_ANNOTATION", "已请求隐藏标注图层。", SetDrawingLayerStatus);
    private void OnShowAllLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_SHOW_ALL", "已请求恢复全部图层。", SetDrawingLayerStatus);
    private void OnMakeLayerCurrentClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_LAYER_MAKE_CURRENT", "请选择一个图形，将其图层置为当前。", SetDrawingLayerStatus);
    private void OnHideEntitiesClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_HIDE", "请选择需要隐藏的图形。", SetDrawingLayerStatus);
    private void OnUnhideLastEntitiesClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_UNHIDE_LAST", "正在恢复最近隐藏的图形。", SetDrawingLayerStatus);
    private void OnShowAllEntitiesClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_SHOW_ALL", "正在显示全部隐藏图形。", SetDrawingLayerStatus);
    private void OnLockEntityLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_LOCK_LAYERS", "请选择需要锁定的图形。", SetDrawingLayerStatus);
    private void OnUnlockEntityLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_UNLOCK_LAYERS", "请选择需要解锁的图形。", SetDrawingLayerStatus);
    private void OnUnlockAllLayersClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_ENTITY_UNLOCK_ALL_LAYERS", "正在解锁全部图层。", SetDrawingLayerStatus);

    private void OnSelectDoorLayerClick(object sender, RoutedEventArgs e) => RunConfiguredSelectionCommand("DC_CMD_SELECT_DOOR_LAYER", "请选择门板对象。" );
    private void OnSelectCabinetLayerClick(object sender, RoutedEventArgs e) => RunConfiguredSelectionCommand("DC_CMD_SELECT_CABINET_LAYER", "请选择柜体对象。" );
    private void OnSelectCustomLayerOneClick(object sender, RoutedEventArgs e) => RunConfiguredSelectionCommand("DC_CMD_SELECT_CUSTOM_LAYER_ONE", "请选择自定义一对象。" );
    private void OnSelectCustomLayerTwoClick(object sender, RoutedEventArgs e) => RunConfiguredSelectionCommand("DC_CMD_SELECT_CUSTOM_LAYER_TWO", "请选择自定义二对象。" );
    private void OnSelectAllAnnotationsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_SELECT_ALL_ANNOTATIONS", "正在选择当前空间的全部标注。", SetDrawingLayerStatus);
    private void OnSelectSimilarObjectsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_SELECT_SIMILAR_IN_RANGE", "先框选匹配范围，再选择参照对象。", SetDrawingLayerStatus);

    private void OnLoadDrawingLayerNamesClick(object sender, RoutedEventArgs e)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetDrawingLayerStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        drawingLayerNames = layers
            .Cast<Autodesk.AutoCAD.DatabaseServices.ObjectId>()
            .Select(id => ((LayerTableRecord)transaction.GetObject(id, OpenMode.ForRead)).Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        RefreshDrawingLayerComboSources();
        SetDrawingLayerStatus($"已读取当前图纸的 {drawingLayerNames.Count} 个图层名称。", Brushes.SeaGreen);
    }

    private void OnAddDrawingLayerMappingClick(object sender, RoutedEventArgs e)
    {
        var category = DrawingLayerNewCategoryBox.Text.Trim();
        var layerName = DrawingLayerNewLayerBox.Text.Trim();
        if (category.Length == 0 || layerName.Length == 0)
        {
            SetDrawingLayerStatus("新增分类和目标图层都不能为空。", Brushes.Firebrick);
            return;
        }

        if (drawingLayerMappingRows.Any(row => string.Equals(row.Category, category, StringComparison.Ordinal)))
        {
            SetDrawingLayerStatus($"分类“{category}”已经存在。", Brushes.Firebrick);
            return;
        }

        AddDrawingLayerMappingRow(category, layerName);
        DrawingLayerNewCategoryBox.Clear();
        DrawingLayerNewLayerBox.Text = string.Empty;
        SetDrawingLayerStatus("已添加分类，请点击“保存配置”持久化。", Brushes.DimGray);
    }

    private void OnRestoreDrawingLayerConfigurationClick(object sender, RoutedEventArgs e)
    {
        var defaults = DrawingLayerToolOptions.Default;
        BuildDrawingLayerMappingRows(DrawingLayerCategories.CreateDefaultMappings());
        DrawingLayerDoorSelectionBox.Text = defaults.DoorSelectionLayer;
        DrawingLayerCabinetSelectionBox.Text = defaults.CabinetSelectionLayer;
        DrawingLayerCustomSelectionOneBox.Text = defaults.CustomSelectionLayerOne;
        DrawingLayerCustomSelectionTwoBox.Text = defaults.CustomSelectionLayerTwo;
        SaveDrawingLayerConfiguration("图层配置已恢复默认并保存。" );
    }

    private void OnSaveDrawingLayerConfigurationClick(object sender, RoutedEventArgs e) =>
        SaveDrawingLayerConfiguration("图层配置已保存。" );

    private void RunConfiguredSelectionCommand(string command, string status)
    {
        if (!TrySaveDrawingLayerConfiguration(out _))
        {
            return;
        }

        RunDrawingCommand(command, status, SetDrawingLayerStatus);
    }

    private void SaveDrawingLayerConfiguration(string successMessage)
    {
        if (!TrySaveDrawingLayerConfiguration(out _))
        {
            return;
        }

        SetDrawingLayerStatus(successMessage, Brushes.SeaGreen);
    }

    private bool TrySaveDrawingLayerConfiguration(out DrawingLayerToolOptions options)
    {
        if (!ReadDrawingLayerConfiguration(out options, out var error))
        {
            SetDrawingLayerStatus(error, Brushes.Firebrick);
            return false;
        }

        DrawingLayerRuntimeSettings.Save(options);
        return true;
    }

    private bool ReadDrawingLayerConfiguration(out DrawingLayerToolOptions options, out string error)
    {
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in drawingLayerMappingRows)
        {
            var layerName = row.LayerBox.Text.Trim();
            if (row.Category.Length == 0 || layerName.Length == 0)
            {
                options = DrawingLayerRuntimeSettings.Current;
                error = "分类名称和目标图层都不能为空；不需要的行请点击红色 × 删除。";
                return false;
            }

            if (mappings.ContainsKey(row.Category))
            {
                options = DrawingLayerRuntimeSettings.Current;
                error = $"分类“{row.Category}”重复。";
                return false;
            }

            mappings.Add(row.Category, layerName);
        }

        var doorLayer = DrawingLayerDoorSelectionBox.Text.Trim();
        var cabinetLayer = DrawingLayerCabinetSelectionBox.Text.Trim();
        var customOne = DrawingLayerCustomSelectionOneBox.Text.Trim();
        var customTwo = DrawingLayerCustomSelectionTwoBox.Text.Trim();
        if (mappings.Count == 0 || doorLayer.Length == 0 || cabinetLayer.Length == 0 ||
            customOne.Length == 0 || customTwo.Length == 0)
        {
            options = DrawingLayerRuntimeSettings.Current;
            error = "至少保留一个分类映射，四个智能选择图层都不能为空。";
            return false;
        }

        options = DrawingLayerRuntimeSettings.Current with
        {
            CategoryLayers = mappings,
            DoorSelectionLayer = doorLayer,
            CabinetSelectionLayer = cabinetLayer,
            CustomSelectionLayerOne = customOne,
            CustomSelectionLayerTwo = customTwo
        };
        error = string.Empty;
        return true;
    }

    private void BuildDrawingLayerMappingRows(IReadOnlyDictionary<string, string> mappings)
    {
        DrawingLayerMappingRowsPanel.Children.Clear();
        drawingLayerMappingRows.Clear();
        foreach (var pair in mappings)
        {
            AddDrawingLayerMappingRow(pair.Key, pair.Value);
        }
    }

    private void AddDrawingLayerMappingRow(string category, string layerName)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });

        var label = new TextBlock { Text = category, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(label);

        var combo = new ComboBox
        {
            IsEditable = true,
            Text = layerName,
            Style = (Style)FindResource("DoorPanelCompactComboStyle")
        };
        Grid.SetColumn(combo, 1);
        grid.Children.Add(combo);

        var remove = new Button
        {
            Content = "×",
            Foreground = Brushes.Red,
            Style = (Style)FindResource("DoorPanelCompactActionStyle")
        };
        Grid.SetColumn(remove, 3);
        grid.Children.Add(remove);

        var row = new DrawingLayerMappingRow(category, combo, grid);
        remove.Click += (_, _) =>
        {
            DrawingLayerMappingRowsPanel.Children.Remove(row.Container);
            drawingLayerMappingRows.Remove(row);
        };
        drawingLayerMappingRows.Add(row);
        DrawingLayerMappingRowsPanel.Children.Add(grid);
        RefreshDrawingLayerComboSource(combo);
    }

    private void RefreshDrawingLayerComboSources()
    {
        foreach (var row in drawingLayerMappingRows)
        {
            RefreshDrawingLayerComboSource(row.LayerBox);
        }

        foreach (var combo in new[]
                 {
                     DrawingLayerNewLayerBox, DrawingLayerDoorSelectionBox, DrawingLayerCabinetSelectionBox,
                     DrawingLayerCustomSelectionOneBox, DrawingLayerCustomSelectionTwoBox
                 })
        {
            RefreshDrawingLayerComboSource(combo);
        }
    }

    private void RefreshDrawingLayerComboSource(ComboBox combo)
    {
        var text = combo.Text;
        combo.ItemsSource = drawingLayerNames;
        combo.Text = text;
    }

    private void OnRepairDrawingDisplayClick(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(DrawingLinetypeScaleBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var lineScale) ||
            lineScale < 1 || lineScale > 500 ||
            !double.TryParse(WallHatchScaleBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var hatchScale) ||
            hatchScale < 0.5)
        {
            SetDrawingLayerStatus("虚线比例需为 1~500，墙体填充比例不能小于 0.5。", Brushes.Firebrick);
            return;
        }

        DrawingLayerRuntimeSettings.Save(DrawingLayerRuntimeSettings.Current with
        {
            LinetypeScale = lineScale,
            WallHatchScale = hatchScale,
            CategoryLayers = new Dictionary<string, string>(
                DrawingLayerRuntimeSettings.Current.CategoryLayers,
                StringComparer.Ordinal)
        });
        RunDrawingCommand("DC_CMD_DRAWING_DISPLAY_REPAIR", "正在修复虚线和墙体填充显示。", SetDrawingLayerStatus);
    }

    private void LoadDrawingLayerModule()
    {
        var options = DrawingLayerRuntimeSettings.Current;
        DrawingLinetypeScaleBox.Text = options.LinetypeScale.ToString("0.###", CultureInfo.CurrentCulture);
        WallHatchScaleBox.Text = options.WallHatchScale.ToString("0.###", CultureInfo.CurrentCulture);
        BuildDrawingLayerMappingRows(options.CategoryLayers);
        DrawingLayerDoorSelectionBox.Text = options.DoorSelectionLayer;
        DrawingLayerCabinetSelectionBox.Text = options.CabinetSelectionLayer;
        DrawingLayerCustomSelectionOneBox.Text = options.CustomSelectionLayerOne;
        DrawingLayerCustomSelectionTwoBox.Text = options.CustomSelectionLayerTwo;
    }

    private static void RunDrawingCommand(string command, string status, Action<string, Brush> setStatus)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            setStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        setStatus(status, Brushes.DimGray);
        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private static bool TryReadColorIndex(string? text, out short colorIndex, out string error)
    {
        var value = text?.Split(new[] { '-' }, 2)[0].Trim();
        if (!short.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out colorIndex) ||
            colorIndex < 1 || colorIndex > 255)
        {
            error = "颜色索引必须是 1 到 255 的整数。";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void LoadHardwareModule()
    {
        var options = HardwareRuntimeSettings.Current;
        HangingRodTopOffsetBox.Text = options.HangingRodTopOffset.ToString("0.###", CultureInfo.CurrentCulture);
        LightStripTopOffsetBox.Text = options.LightStripTopOffset.ToString("0.###", CultureInfo.CurrentCulture);
        LightStripSideOpeningBox.Text = options.LightStripSideOpening.ToString("0.###", CultureInfo.CurrentCulture);
        HingeTopOffsetBox.Text = options.HingeTopOffset.ToString("0.###", CultureInfo.CurrentCulture);
        HingeBottomOffsetBox.Text = options.HingeBottomOffset.ToString("0.###", CultureInfo.CurrentCulture);
        HingeCountBelow900Box.Text = options.HingeCountBelow900.ToString(CultureInfo.CurrentCulture);
        HingeCount900To1500Box.Text = options.HingeCount900To1500.ToString(CultureInfo.CurrentCulture);
        HingeCount1500To2000Box.Text = options.HingeCount1500To2000.ToString(CultureInfo.CurrentCulture);
        HingeCount2000To2500Box.Text = options.HingeCount2000To2500.ToString(CultureInfo.CurrentCulture);
        HingeCountAtLeast2500Box.Text = options.HingeCountAtLeast2500.ToString(CultureInfo.CurrentCulture);
        UseFixedHingeSpacingCheck.IsChecked = options.UseFixedHingeSpacing;
        FixedHingeSpacingBox.Text = options.FixedHingeSpacing.ToString("0.###", CultureInfo.CurrentCulture);
        GenerateHingeDimensionsCheck.IsChecked = options.GenerateHingeDimensions;
        HingeDimensionModeCombo.SelectedIndex = options.HingeDimensionMode == HingeDimensionMode.SplitFromEnds ? 1 : 0;
        HingeDimensionOffsetBox.Text = options.HingeDimensionOffset.ToString("0.###", CultureInfo.CurrentCulture);
        HingeDimensionLineSpacingBox.Text = options.HingeDimensionLineSpacing.ToString("0.###", CultureInfo.CurrentCulture);
        HingeDimensionTextHeightBox.Text = options.HingeDimensionTextHeight.ToString("0.###", CultureInfo.CurrentCulture);
        HingeTypeTextHeightBox.Text = options.HingeTypeTextHeight.ToString("0.###", CultureInfo.CurrentCulture);
        HingeTypeTextSpacingBox.Text = options.HingeTypeTextSpacing.ToString("0.###", CultureInfo.CurrentCulture);

        try
        {
            hardwareFolders = HardwareLibraryService.EnsureDefaultFolders(PluginResourceLocator.GetPluginRoot());
            RefreshHardwareLibraries();
        }
        catch (Exception exception)
        {
            SetHardwareStatus($"五金素材目录初始化失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void RefreshHardwareLibraries()
    {
        if (hardwareFolders is null)
        {
            return;
        }

        var selectedRodPath = (HangingRodList.SelectedItem as HardwareLibraryItem)?.DwgPath;
        var selectedLightPath = (LightStripList.SelectedItem as HardwareLibraryItem)?.DwgPath;
        var rods = HardwareLibraryService.Scan(hardwareFolders.HangingRodDirectory);
        var lights = HardwareLibraryService.Scan(hardwareFolders.LightStripDirectory);
        HangingRodList.ItemsSource = rods;
        LightStripList.ItemsSource = lights;
        HangingRodList.SelectedItem = rods.FirstOrDefault(item =>
            string.Equals(item.DwgPath, selectedRodPath, StringComparison.OrdinalIgnoreCase)) ?? rods.FirstOrDefault();
        LightStripList.SelectedItem = lights.FirstOrDefault(item =>
            string.Equals(item.DwgPath, selectedLightPath, StringComparison.OrdinalIgnoreCase)) ?? lights.FirstOrDefault();

        SetHardwareStatus(
            rods.Count == 0 && lights.Count == 0
                ? $"素材库为空，请把 DWG 和可选的同名预览图放入：{hardwareFolders.RootDirectory}"
                : $"已读取挂衣杆 {rods.Count} 项、灯带 {lights.Count} 项。",
            Brushes.DimGray);
    }

    private void OnHardwareSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender == HangingRodList)
        {
            var item = HangingRodList.SelectedItem as HardwareLibraryItem;
            HardwareRuntimeSettings.SelectedHangingRod = item;
            SetPreview(HangingRodPreviewImage, HangingRodPreviewHint, item);
        }
        else if (sender == LightStripList)
        {
            var item = LightStripList.SelectedItem as HardwareLibraryItem;
            HardwareRuntimeSettings.SelectedLightStrip = item;
            SetPreview(LightStripPreviewImage, LightStripPreviewHint, item);
        }
    }

    private static void SetPreview(Image image, TextBlock hint, HardwareLibraryItem? item)
    {
        image.Source = null;
        hint.Text = item is null ? "未选择素材" : "未提供同名预览图";
        hint.Visibility = Visibility.Visible;
        if (item?.PreviewPath is null || !File.Exists(item.PreviewPath))
        {
            return;
        }

        try
        {
            using var stream = new FileStream(item.PreviewPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
            hint.Visibility = Visibility.Collapsed;
        }
        catch
        {
            hint.Text = "预览图读取失败";
        }
    }

    private void OnOpenHangingRodDirectoryClick(object sender, RoutedEventArgs e)
    {
        OpenHardwareDirectory(hardwareFolders?.HangingRodDirectory);
    }

    private void OnOpenLightStripDirectoryClick(object sender, RoutedEventArgs e)
    {
        OpenHardwareDirectory(hardwareFolders?.LightStripDirectory);
    }

    private void OpenHardwareDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            SetHardwareStatus("五金素材目录尚未初始化。", Brushes.Firebrick);
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SetHardwareStatus($"打开目录失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void OnInsertHangingRodClick(object sender, RoutedEventArgs e)
    {
        RunHardwareCommand(
            HangingRodList.SelectedItem as HardwareLibraryItem,
            "DC_CMD_HANGING_ROD",
            "挂衣杆",
            "请选择柜内左到右的水平基准线；程序会自动识别最近的上方覆盖线。");
    }

    private void OnInsertLightStripClick(object sender, RoutedEventArgs e)
    {
        RunHardwareCommand(
            LightStripList.SelectedItem as HardwareLibraryItem,
            "DC_CMD_LIGHT_STRIP",
            "灯带",
            "请在图纸中指定灯带插入点。");
    }

    private void RunHardwareCommand(
        HardwareLibraryItem? item,
        string command,
        string displayName,
        string statusMessage)
    {
        if (item is null)
        {
            SetHardwareStatus($"请先向素材目录加入并选择{displayName} DWG。", Brushes.Firebrick);
            return;
        }

        if (!TrySaveHardwareOptions(out var error))
        {
            SetHardwareStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            SetHardwareStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        SetHardwareStatus(statusMessage, Brushes.DimGray);
        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private void OnInsertHingesClick(object sender, RoutedEventArgs e)
    {
        RunHingeCommand("DC_CMD_HINGE_INSERT", "先选择门板上边线，再选择下边线，最后点击左侧或右侧确定铰链侧。");
    }

    private void OnLabelHingesClick(object sender, RoutedEventArgs e)
    {
        RunHingeCommand("DC_CMD_HINGE_LABEL", "左键框选或点选多个铰链圆，右键或空格结束后在鼠标下方选择铰链类型。");
    }

    private void OnSaveHardwareHingeOptionsClick(object sender, RoutedEventArgs e)
    {
        if (TrySaveHardwareOptions(out var error))
        {
            SetHardwareStatus("铰链参数已保存，并与门板抽面设置同步。", Brushes.SeaGreen);
        }
        else
        {
            SetHardwareStatus(error, Brushes.Firebrick);
        }
    }

    private void OnHardwareInsertHingesClick(object sender, RoutedEventArgs e)
    {
        RunHingeCommand(
            "DC_CMD_HINGE_INSERT",
            "先选择门板上边线，再选择下边线，最后点击左侧或右侧确定铰链侧。",
            SetHardwareStatus);
    }

    private void OnHardwareLabelHingesClick(object sender, RoutedEventArgs e)
    {
        RunHingeCommand(
            "DC_CMD_HINGE_LABEL",
            "左键框选或点选多个铰链圆，右键或空格结束后在鼠标下方选择铰链类型。",
            SetHardwareStatus);
    }

    private void RunHingeCommand(string command, string statusMessage)
    {
        RunHingeCommand(command, statusMessage, SetDoorPanelStatus);
    }

    private void RunHingeCommand(string command, string statusMessage, Action<string, Brush> setStatus)
    {
        if (!TrySaveHardwareOptions(out var error))
        {
            setStatus(error, Brushes.Firebrick);
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            setStatus("当前没有可用图纸。", Brushes.Firebrick);
            return;
        }

        setStatus(statusMessage, Brushes.DimGray);
        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private bool TrySaveHardwareOptions(out string error)
    {
        if (!TryReadHardwareOptions(out var options, out error))
        {
            return false;
        }

        HardwareRuntimeSettings.Save(options);
        return true;
    }

    private bool TryReadHardwareOptions(out HardwareOptions options, out string error)
    {
        options = HardwareOptions.Default;
        var modeName = (HingeDimensionModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(modeName, out HingeDimensionMode dimensionMode))
        {
            error = "请选择有效的铰链标注模式。";
            return false;
        }

        if (!HardwareOptions.TryCreate(
                HangingRodTopOffsetBox.Text,
                LightStripTopOffsetBox.Text,
                LightStripSideOpeningBox.Text,
                HingeTopOffsetBox.Text,
                HingeBottomOffsetBox.Text,
                HingeCountBelow900Box.Text,
                HingeCount900To1500Box.Text,
                HingeCount1500To2000Box.Text,
                HingeCount2000To2500Box.Text,
                HingeCountAtLeast2500Box.Text,
                UseFixedHingeSpacingCheck.IsChecked == true,
                FixedHingeSpacingBox.Text,
                GenerateHingeDimensionsCheck.IsChecked == true,
                dimensionMode,
                HingeDimensionOffsetBox.Text,
                HingeDimensionLineSpacingBox.Text,
                HingeDimensionTextHeightBox.Text,
                HingeTypeTextHeightBox.Text,
                HingeTypeTextSpacingBox.Text,
                out options,
                out error))
        {
            return false;
        }

        return true;
    }

    private void SetDoorPanelStatus(string message, Brush foreground)
    {
        DoorPanelStatusText.Foreground = foreground;
        DoorPanelStatusText.Text = message;
    }

    private void SetDrawingLayerStatus(string message, Brush foreground)
    {
        DrawingLayerStatusText.Foreground = foreground;
        DrawingLayerStatusText.Text = message;
    }

    private void SetBoardStatus(string message, Brush foreground)
    {
        BoardStatusText.Foreground = foreground;
        BoardStatusText.Text = message;
    }

    private void SetCabinetStatus(string message, Brush foreground)
    {
        CabinetStatusText.Foreground = foreground;
        CabinetStatusText.Text = message;
    }

    private void SetPlanCabinetStatus(string message, Brush foreground)
    {
        PlanCabinetStatusText.Foreground = foreground;
        PlanCabinetStatusText.Text = message;
    }

    private void SetPlanDoorPanelStatus(string message, Brush foreground)
    {
        PlanDoorPanelStatusText.Foreground = foreground;
        PlanDoorPanelStatusText.Text = message;
    }

    private void SetDrawerFaceStatus(string message, Brush foreground)
    {
        DrawerFaceStatusText.Foreground = foreground;
        DrawerFaceStatusText.Text = message;
    }

    private void SetHardwareStatus(string message, Brush foreground)
    {
        HardwareStatusText.Foreground = foreground;
        HardwareStatusText.Text = message;
    }

    private void LoadPrintSettings()
    {
        var settings = PrintRuntime.Settings.Clone();
        PlotTransparencyCheck.IsChecked = settings.PlotTransparency;
        PlotLineweightsCheck.IsChecked = settings.PlotLineweights;
        OpenOutputFolderCheck.IsChecked = settings.OpenOutputFolder;
        OpenMergedPdfCheck.IsChecked = settings.OpenMergedPdf;
        SelectPdfOutputMode(settings.PdfOutputMode);
        ImageDpiSlider.Value = settings.ImageDpi;
        PrintImageDpiBox.Text = settings.ImageDpi.ToString(CultureInfo.CurrentCulture);
        DetectPolylineCheck.IsChecked = settings.DetectClosedPolylines;
        DetectLooseLinesCheck.IsChecked = settings.DetectLooseLines;
        DetectBlocksCheck.IsChecked = settings.DetectBlocks;
        IgnoreEmptyFramesCheck.IsChecked = settings.IgnoreEmptyFrames;
    }

    private void OnSavePrintSettingsClick(object sender, RoutedEventArgs e)
    {
        if (TrySavePrintSettings(out var error))
        {
            PrintSettingsStatusText.Foreground = Brushes.SeaGreen;
            PrintSettingsStatusText.Text = "批量打印设置已保存。";
        }
        else
        {
            PrintSettingsStatusText.Foreground = Brushes.Firebrick;
            PrintSettingsStatusText.Text = error;
        }
    }

    private void OnRunPrintCommandClick(object sender, RoutedEventArgs e)
    {
        if (!TrySavePrintSettings(out var error))
        {
            PrintSettingsStatusText.Foreground = Brushes.Firebrick;
            PrintSettingsStatusText.Text = error;
            return;
        }

        if (sender is not Button { Tag: string command })
        {
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            PrintSettingsStatusText.Text = "当前没有可用图纸。";
            return;
        }

        document.SendStringToExecute(command + " ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private bool TrySavePrintSettings(out string error)
    {
        error = string.Empty;
        if (!int.TryParse(PrintImageDpiBox.Text, out var imageDpi))
        {
            error = "着色/光栅分辨率必须填写整数。";
            return false;
        }

        var modeTag = (PdfOutputModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(modeTag, out PdfOutputMode outputMode))
        {
            outputMode = PdfOutputMode.MergedOnly;
        }

        var settings = PrintRuntime.Settings.Clone();
        settings.PlotTransparency = PlotTransparencyCheck.IsChecked == true;
        settings.PlotLineweights = PlotLineweightsCheck.IsChecked == true;
        settings.OpenOutputFolder = OpenOutputFolderCheck.IsChecked == true;
        settings.OpenMergedPdf = OpenMergedPdfCheck.IsChecked == true;
        settings.PdfOutputMode = outputMode;
        settings.ImageDpi = imageDpi;
        settings.DetectClosedPolylines = DetectPolylineCheck.IsChecked == true;
        settings.DetectLooseLines = DetectLooseLinesCheck.IsChecked == true;
        settings.DetectBlocks = DetectBlocksCheck.IsChecked == true;
        settings.IgnoreEmptyFrames = IgnoreEmptyFramesCheck.IsChecked == true;

        try
        {
            PrintRuntime.Save(settings);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void SelectPdfOutputMode(PdfOutputMode mode)
    {
        PdfOutputModeCombo.SelectedItem = PdfOutputModeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal));
    }

    private void OnDpiSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var text = Math.Round(e.NewValue).ToString(CultureInfo.CurrentCulture);
        if (sender == ImageDpiSlider && PrintImageDpiBox is not null)
        {
            PrintImageDpiBox.Text = text;
        }
    }

    private void OnInsertImagesClick(object sender, RoutedEventArgs e)
    {
        if (!ImageBatchOptions.TryCreate(
                ImageGapBox.Text,
                UseUnifiedHeightCheckBox.IsChecked == true,
                ImagePixelHeightBox.Text,
                out var options,
                out var error))
        {
            ImageStatusText.Foreground = Brushes.Firebrick;
            ImageStatusText.Text = error;
            return;
        }

        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            ImageStatusText.Foreground = Brushes.Firebrick;
            ImageStatusText.Text = "当前没有可用图纸。";
            return;
        }

        ImageBatchRuntimeSettings.Save(options);
        ImageStatusText.Foreground = Brushes.DimGray;
        ImageStatusText.Text = "请在图片选择窗口中选择一张或多张图片。";
        document.SendStringToExecute("DC_CMD_IMAGE_BATCH ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
    }

    private void LoadItems()
    {
        try
        {
            items = new ObservableCollection<ShortcutItem>(ShortcutManager.Load().ShortcutItems);
            ShortcutGrid.ItemsSource = items;
            UpdateShortcutColumnMinimumWidths();
            SetStatus(string.Empty, Brushes.DimGray);
        }
        catch (Exception exception)
        {
            SetStatus(exception.Message, Brushes.Firebrick);
        }
    }

    private void LoadCadCommands()
    {
        cadCommandView = CollectionViewSource.GetDefaultView(CadCommandCatalog.GetAll());
        cadCommandView.GroupDescriptions.Clear();
        cadCommandView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CadCommandItem.Category)));
        CadCommandGrid.ItemsSource = cadCommandView;
        UpdateCadCommandColumnMinimumWidths();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchHint is not null)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (ShortcutGrid?.ItemsSource is null)
        {
            return;
        }

        var keyword = SearchBox.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(ShortcutGrid.ItemsSource);
        view.Filter = value => value is not ShortcutItem item ||
                               keyword.Length == 0 ||
                               Contains(item.Description, keyword) ||
                               Contains(item.DefaultAlias, keyword) ||
                               Contains(item.CustomAlias, keyword) ||
                               Contains(item.CoreCommand, keyword);
    }

    private void OnCadSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (CadSearchHint is not null)
        {
            CadSearchHint.Visibility = string.IsNullOrEmpty(CadSearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        if (cadCommandView is null)
        {
            return;
        }

        var keyword = CadSearchBox.Text;
        cadCommandView.Filter = value => value is not CadCommandItem item || CadCommandCatalog.Matches(item, keyword);
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CommitShortcutEdits();

        try
        {
            var result = ShortcutManager.SaveAndApply(items);
            SetStatus(
                result.Message,
                result.Success ? Brushes.SeaGreen : Brushes.Firebrick,
                autoClear: result.Success);

            if (result.Success)
            {
                items = new ObservableCollection<ShortcutItem>(result.Configuration.ShortcutItems);
                ShortcutGrid.ItemsSource = items;
                UpdateShortcutColumnMinimumWidths();
                OnSearchTextChanged(SearchBox, new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None));
            }
        }
        catch (Exception exception)
        {
            SetStatus($"保存失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void OnAddMappingClick(object sender, RoutedEventArgs e)
    {
        CommitShortcutEdits();
        SearchBox.Text = string.Empty;
        var mapping = ShortcutItem.CreateNewMapping();
        items.Add(mapping);
        UpdateShortcutColumnMinimumWidths();
        SetStatus("已添加新映射；可修改功能说明、默认和自定义快捷键，底层命令保持只读。", Brushes.DimGray);

        Dispatcher.BeginInvoke(() =>
        {
            ShortcutGrid.SelectedItem = mapping;
            ShortcutGrid.ScrollIntoView(mapping);
            ShortcutGrid.CurrentCell = new DataGridCellInfo(mapping, ShortcutGrid.Columns[0]);
            ShortcutGrid.Focus();
            ShortcutGrid.BeginEdit();
        }, DispatcherPriority.Background);
    }

    private void OnDeleteSelectedClick(object sender, RoutedEventArgs e)
    {
        CommitShortcutEdits();
        if (ShortcutGrid.SelectedItem is not ShortcutItem selectedItem)
        {
            SetStatus("请先选中需要删除的用户映射。", Brushes.Firebrick);
            return;
        }

        if (!selectedItem.IsUserDefined)
        {
            SetStatus("系统映射不能删除；可以修改功能说明、默认或自定义快捷键。", Brushes.Firebrick);
            return;
        }

        var result = MessageBox.Show(
            $"确定删除用户映射“{selectedItem.Description}”吗？",
            "一只猫dc",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        items.Remove(selectedItem);
        CollectionViewSource.GetDefaultView(ShortcutGrid.ItemsSource).Refresh();
        UpdateShortcutColumnMinimumWidths();
        SetStatus("用户映射已从列表移除；点击“保存并应用快捷键”后生效。", Brushes.DimGray);
    }

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        CommitShortcutEdits();
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".csv",
            FileName = $"一只猫dc_快捷键_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV 文件 (*.csv)|*.csv",
            OverwritePrompt = true,
            Title = "导出快捷键"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ShortcutCsvExporter.Export(dialog.FileName, items);
            SetStatus($"已导出到：{dialog.FileName}", Brushes.SeaGreen, autoClear: true);
        }
        catch (Exception exception)
        {
            SetStatus($"导出失败：{exception.Message}", Brushes.Firebrick);
        }
    }

    private void CommitShortcutEdits()
    {
        ShortcutGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        ShortcutGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
    }

    private void OnShortcutCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateShortcutColumnMinimumWidths, DispatcherPriority.Background);
    }

    private void SetStatus(string message, Brush foreground, bool autoClear = false)
    {
        statusClearTimer.Stop();
        StatusText.Foreground = foreground;
        StatusText.Text = message;

        if (autoClear)
        {
            statusClearTimer.Start();
        }
    }

    private void OnStatusClearTimerTick(object? sender, EventArgs e)
    {
        statusClearTimer.Stop();
        StatusText.Text = string.Empty;
    }

    private void UpdateShortcutColumnMinimumWidths()
    {
        SetColumnMinimumWidth(ShortcutGrid.Columns[0], items.Select(item => item.Description));
        SetColumnMinimumWidth(ShortcutGrid.Columns[1], items.Select(item => item.DefaultAlias));
        SetColumnMinimumWidth(ShortcutGrid.Columns[2], items.Select(item => item.CustomAlias), FontWeights.SemiBold);
        SetColumnMinimumWidth(ShortcutGrid.Columns[3], []);
        SetColumnMinimumWidth(ShortcutGrid.Columns[4], items.Select(item => item.CoreCommand));
    }

    private void UpdateCadCommandColumnMinimumWidths()
    {
        var commands = CadCommandCatalog.GetAll();
        SetColumnMinimumWidth(CadCommandGrid.Columns[0], commands.Select(item => item.Shortcut));
        SetColumnMinimumWidth(CadCommandGrid.Columns[1], commands.Select(item => item.FullCommand));
        SetColumnMinimumWidth(CadCommandGrid.Columns[2], commands.Select(item => item.Description));
    }

    private void SetColumnMinimumWidth(
        DataGridColumn column,
        IEnumerable<string> values,
        FontWeight? fontWeight = null)
    {
        var texts = values.Concat(new[] { column.Header?.ToString() ?? string.Empty });
        var minimumWidth = texts.Max(value => MeasureTextWidth(value, fontWeight ?? FontWeights.Normal)) + 28;
        column.MinWidth = Math.Ceiling(minimumWidth);
    }

    private double MeasureTextWidth(string value, FontWeight fontWeight)
    {
        var formattedText = new FormattedText(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal),
            FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return formattedText.WidthIncludingTrailingWhitespace;
    }

    private sealed record DrawingLayerMappingRow(string Category, ComboBox LayerBox, Grid Container);

    private static bool Contains(string value, string keyword)
    {
        return Compatibility.Contains(value, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
