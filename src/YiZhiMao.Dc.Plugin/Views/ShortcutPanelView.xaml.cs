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
        LoadBoardModule();
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

    private void OnDrawingLayerModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.DrawingLayers);
    }

    private void OnBoardModuleClick(object sender, RoutedEventArgs e)
    {
        SelectModule(ToolboxModule.Boards);
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
        DoorPanelModuleView.Visibility = module == ToolboxModule.DoorPanels ? Visibility.Visible : Visibility.Collapsed;
        HardwareModuleView.Visibility = module == ToolboxModule.Hardware ? Visibility.Visible : Visibility.Collapsed;
        WallPanelModuleView.Visibility = module == ToolboxModule.WallPanels ? Visibility.Visible : Visibility.Collapsed;
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
        DoorPanelModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.DoorPanels ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        HardwareModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.Hardware ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
        WallPanelModuleButton.Style = (Style)FindResource(
            module == ToolboxModule.WallPanels ? "SelectedModuleButtonStyle" : "ModuleButtonStyle");
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
        BoardModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Boards));
        DoorPanelModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.DoorPanels));
        HardwareModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Hardware));
        WallPanelModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.WallPanels));
        ImageModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.Images));
        OneClickFrameModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.OneClickFrame));
        BatchPrintModuleButton.Visibility = ToVisibility(preferences.IsEnabled(ToolboxFeatureModule.BatchPrint));
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
            case ToolboxModule.Boards: featureModule = ToolboxFeatureModule.Boards; return true;
            case ToolboxModule.DoorPanels: featureModule = ToolboxFeatureModule.DoorPanels; return true;
            case ToolboxModule.Hardware: featureModule = ToolboxFeatureModule.Hardware; return true;
            case ToolboxModule.WallPanels: featureModule = ToolboxFeatureModule.WallPanels; return true;
            case ToolboxModule.Images: featureModule = ToolboxFeatureModule.Images; return true;
            case ToolboxModule.OneClickFrame: featureModule = ToolboxFeatureModule.OneClickFrame; return true;
            case ToolboxModule.BatchPrint: featureModule = ToolboxFeatureModule.BatchPrint; return true;
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
        DrawerFaceVerticalRadio.IsChecked = drawerOptions.Orientation == DrawerFaceOrientation.Vertical;
        DrawerFaceHorizontalRadio.IsChecked = drawerOptions.Orientation == DrawerFaceOrientation.Horizontal;
        DrawerFaceCountBox.Text = drawerOptions.Count.ToString(CultureInfo.CurrentCulture);
        DrawerFaceGapBox.Text = drawerOptions.Gap.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceTopExtensionBox.Text = drawerOptions.TopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceBottomExtensionBox.Text = drawerOptions.BottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceLeftExtensionBox.Text = drawerOptions.LeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceRightExtensionBox.Text = drawerOptions.RightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        DrawerFaceGenerateMaskCheck.IsChecked = drawerOptions.GenerateMask;
        DrawerFaceCalculationModeCombo.SelectedIndex = drawerOptions.CalculationMode switch
        {
            DoorPanelCalculationMode.GapRoundedDoorDistributed => 1,
            DoorPanelCalculationMode.DoorAndGapRoundedRemainderRight => 2,
            _ => 0
        };
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
        DrawerFaceActualSizeText.Text = layout is null
            ? "实际单块尺寸/缝隙：—（选择柜体内空后计算）"
            : $"实际单块尺寸：{layout.FaceWidth:0.###} × {layout.FaceHeight:0.###} mm；实际缝隙：{layout.ActualGap:0.###} mm";
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
        SetDrawerFaceStatus("依次选择柜体内空两个对角点；左右键或 +/- 调整数量，竖向也可用上下键。", Brushes.DimGray);
        document.SendStringToExecute(
            "DC_CMD_DRAWER_FACE ",
            activate: true,
            wrapUpInactiveDoc: false,
            echoCommand: false);
    }

    private bool TryReadDrawerFaceOptions(out DrawerFaceOptions options, out string error)
    {
        options = DrawerFaceOptions.Default;
        error = string.Empty;
        var orientation = DrawerFaceHorizontalRadio.IsChecked == true
            ? DrawerFaceOrientation.Horizontal
            : DrawerFaceOrientation.Vertical;
        var modeName = (DrawerFaceCalculationModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!Enum.TryParse(modeName, out DoorPanelCalculationMode calculationMode))
        {
            error = "请选择有效的抽面尺寸计算模式。";
            return false;
        }

        if (!TryReadColorIndex(DrawerFaceColorBox.Text, out var lineColor, out error) ||
            !DrawerFaceOptions.TryCreate(
            orientation,
            DrawerFaceCountBox.Text,
            DrawerFaceGapBox.Text,
            DrawerFaceTopExtensionBox.Text,
            DrawerFaceBottomExtensionBox.Text,
            DrawerFaceLeftExtensionBox.Text,
            DrawerFaceRightExtensionBox.Text,
            DrawerFaceGenerateMaskCheck.IsChecked == true,
            false,
            out options,
            out error))
        {
            return false;
        }

        options = options with { CalculationMode = calculationMode, LineColorIndex = lineColor };
        return true;
    }

    private bool TrySaveDoorAndHingeOptions(out string error)
    {
        if (!TryReadDoorPanelOptions(out var doorOptions, out error) ||
            !TryReadHardwareOptions(out var hardwareOptions, out error))
        {
            return false;
        }

        ToolboxSettingsRuntime.Update(settings =>
        {
            settings.DoorPanel = doorOptions;
            settings.Hardware = hardwareOptions;
        });
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

    private void OnPickHandleClick(object sender, RoutedEventArgs e)
    {
        RunDrawingCommand("DC_CMD_HANDLE_PICK", "请拾取一个现有块作为当前拉手。", SetDrawerFaceStatus);
        PickedHandleText.Text = "当前拉手：拾取完成后由命令行确认";
    }

    private void OnApplyHandleClick(object sender, RoutedEventArgs e)
    {
        RunDrawingCommand("DC_CMD_HANDLE_APPLY", "请选择闭合门板/抽面，或需要原位替换的拉手块。", SetDrawerFaceStatus);
    }

    private void LoadBoardModule()
    {
        BoardThicknessBox.Text = BoardRuntimeSettings.Thickness.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDepthBox.Text = BoardRuntimeSettings.Depth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardCenterDepthBox.Text = BoardRuntimeSettings.CenterDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTagScaleBox.Text = BoardRuntimeSettings.TagScale.ToString("0.###", CultureInfo.CurrentCulture);
        BoardTagTypeCombo.SelectedItem = BoardTagTypeCombo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Content?.ToString(), BoardRuntimeSettings.TagType, StringComparison.Ordinal));
        BoardLineThicknessBox.Text = BoardRuntimeSettings.LineThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDoubleModeCombo.SelectedIndex = BoardRuntimeSettings.DoubleAsRectangle ? 1 : 0;
        BoardTripleModeCombo.SelectedIndex = BoardRuntimeSettings.TripleAsRectangle ? 1 : 0;
        BoardExtendStartBox.Text = BoardRuntimeSettings.ExtendStart.ToString("0.###", CultureInfo.CurrentCulture);
        BoardExtendEndBox.Text = BoardRuntimeSettings.ExtendEnd.ToString("0.###", CultureInfo.CurrentCulture);
        WineRackDepthBox.Text = BoardRuntimeSettings.WineRackDepth.ToString("0.###", CultureInfo.CurrentCulture);
        WineCrossThicknessBox.Text = BoardRuntimeSettings.CrossThickness.ToString("0.###", CultureInfo.CurrentCulture);
        WineDiagonalThicknessBox.Text = BoardRuntimeSettings.DiagonalThickness.ToString("0.###", CultureInfo.CurrentCulture);
        WineCellWidthBox.Text = BoardRuntimeSettings.WineCellWidth.ToString("0.###", CultureInfo.CurrentCulture);
        WineRowsBox.Text = BoardRuntimeSettings.WineRows.ToString(CultureInfo.CurrentCulture);
        WineColumnsBox.Text = BoardRuntimeSettings.WineColumns.ToString(CultureInfo.CurrentCulture);
        AbaOuterThicknessBox.Text = AdvancedBoardRuntimeSettings.AbaOuterThickness.ToString("0.###", CultureInfo.CurrentCulture);
        AbaCoreThicknessBox.Text = AdvancedBoardRuntimeSettings.AbaCoreThickness.ToString("0.###", CultureInfo.CurrentCulture);
        AbaDepthBox.Text = AdvancedBoardRuntimeSettings.AbaDepth.ToString("0.###", CultureInfo.CurrentCulture);
        BoardDivideCountBox.Text = AdvancedBoardRuntimeSettings.DivideCount.ToString(CultureInfo.CurrentCulture);
        BoardDivideThicknessBox.Text = AdvancedBoardRuntimeSettings.DivideThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardThicknessBox.Text = AdvancedBoardRuntimeSettings.BackboardThickness.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardInsetBox.Text = AdvancedBoardRuntimeSettings.BackboardInset.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardLeftBox.Text = AdvancedBoardRuntimeSettings.BackboardLeftExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardRightBox.Text = AdvancedBoardRuntimeSettings.BackboardRightExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardTopBox.Text = AdvancedBoardRuntimeSettings.BackboardTopExtension.ToString("0.###", CultureInfo.CurrentCulture);
        BackboardBottomBox.Text = AdvancedBoardRuntimeSettings.BackboardBottomExtension.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private void OnDrawBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DRAW", "请选择板件起点和终点。", ReadBoardSettings);

    private void OnDrawCenterBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_CENTER", "请选择柜体内空两个对角点。", ReadBoardSettings);

    private void OnBoardSingleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_SINGLE", "请选择基准直线和偏移侧。", ReadLineSettings);

    private void OnBoardDoubleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_DOUBLE", "请选择基准直线和偏移侧。", ReadLineSettings);

    private void OnBoardTripleLineClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TRIPLE", "请选择基准直线和偏移侧。", ReadLineSettings);

    private void OnBoardLineToDoubleClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TO_DOUBLE", "请选择要转换的单线。", ReadLineSettings);

    private void OnBoardLineToTripleClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_LINE_TO_TRIPLE", "请选择要转换的单线。", ReadLineSettings);

    private void OnCrossWineRackClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_WINE_RACK_CROSS", "请选择十字酒架范围。", ReadWineRackSettings);

    private void OnDiagonalWineRackClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_WINE_RACK_DIAGONAL", "请选择斜酒架左下起点。", ReadWineRackSettings);

    private void OnDisplaceBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DISPLACE", "请选择板件并指定基点和目标点。", () => null);

    private void OnAbaBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_ABA", "请选择ABA夹层的起点和终点。", ReadAbaSettings);

    private void OnDivideBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_DIVIDE", "请选择均分范围和方向。", ReadDivideSettings);

    private void OnBackboardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_BACKBOARD", "请选择背板内空两个对角点。", ReadBackboardSettings);

    private void OnSplitBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_SPLIT", "请选择矩形板件和拆分位置。", () => null);

    private void OnMergeBoardsClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_MERGE", "请选择两块边界对齐的相邻矩形板件。", () => null);

    private void OnExtendBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_EXTEND", "请选择需要延伸的线/矩形边和边界线。", () => null);

    private void OnTrimBoardClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_TRIM", "请选择需要修剪的线/矩形边和边界线。", () => null);

    private void OnShelfProjectClick(object sender, RoutedEventArgs e) => RunBoardCommand("DC_CMD_BOARD_SHELF_PROJECT", "请选择层板对象并指定投影位置。", () => null);

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
            TagScale = tagScale,
            TagType = (BoardTagTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "活动层板带文字"
        });
        return null;
    }

    private string? ReadLineSettings()
    {
        if (!TryReadPositive(BoardLineThicknessBox, "偏移板厚", out var thickness) ||
            !TryReadNonNegative(BoardExtendStartBox, "上延伸", out var extendStart) ||
            !TryReadNonNegative(BoardExtendEndBox, "下延伸", out var extendEnd))
        {
            return "偏移板厚必须大于 0，延伸值不能小于 0。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            LineThickness = thickness,
            ExtendStart = extendStart,
            ExtendEnd = extendEnd,
            DoubleAsRectangle = BoardDoubleModeCombo.SelectedIndex == 1,
            TripleAsRectangle = BoardTripleModeCombo.SelectedIndex == 1
        });
        return null;
    }

    private string? ReadWineRackSettings()
    {
        if (!TryReadPositive(WineRackDepthBox, "酒架深度", out var depth) ||
            !TryReadPositive(WineCrossThicknessBox, "十字酒架板厚", out var crossThickness) ||
            !TryReadPositive(WineDiagonalThicknessBox, "斜酒架板厚", out var diagonalThickness) ||
            !TryReadPositive(WineCellWidthBox, "内格净宽", out var cellWidth) ||
            !int.TryParse(WineRowsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var rows) || rows < 1 ||
            !int.TryParse(WineColumnsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var columns) || columns < 1)
        {
            return "酒架尺寸必须大于 0，横板数和竖板数至少为 1。";
        }

        BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
        {
            WineRackDepth = depth,
            CrossThickness = crossThickness,
            DiagonalThickness = diagonalThickness,
            WineCellWidth = cellWidth,
            WineRows = rows,
            WineColumns = columns
        });
        return null;
    }

    private string? ReadAbaSettings()
    {
        if (!TryReadPositive(AbaOuterThicknessBox, "A层厚度", out var outerThickness) ||
            !TryReadPositive(AbaCoreThicknessBox, "B层厚度", out var coreThickness) ||
            !TryReadPositive(AbaDepthBox, "夹层深度", out var depth))
        {
            return "ABA夹层厚度和深度必须大于 0。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            AbaOuterThickness = outerThickness,
            AbaCoreThickness = coreThickness,
            AbaDepth = depth
        });
        return null;
    }

    private string? ReadDivideSettings()
    {
        if (!int.TryParse(BoardDivideCountBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var count) || count < 2 ||
            !TryReadPositive(BoardDivideThicknessBox, "均分板厚", out var thickness))
        {
            return "均分格数至少为 2，板厚必须大于 0。";
        }

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            DivideCount = count,
            DivideThickness = thickness
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

        AdvancedBoardRuntimeSettings.Save(AdvancedBoardRuntimeSettings.Current with
        {
            BackboardThickness = thickness,
            BackboardInset = inset,
            BackboardLeftExtension = left,
            BackboardRightExtension = right,
            BackboardTopExtension = top,
            BackboardBottomExtension = bottom
        });
        return null;
    }

    private void OnPartsStatisticsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_PARTS_STATISTICS", "请框选需要统计的部件。", SetStatisticsStatus);

    private void OnWholeHouseStatisticsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_WHOLE_HOUSE_BOM", "请选择放置CAD表格或导出Excel总表。", SetStatisticsStatus);

    private void OnDoorStatisticsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_DOOR_STATISTICS", "请框选普通门板或抽面。", SetStatisticsStatus);

    private void OnFrameDoorStatisticsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_FRAME_DOOR_STATISTICS", "请框选边框/玻璃门板。", SetStatisticsStatus);

    private void OnCabinetBomClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_CABINET_BOM", "请框选柜体板件。", SetStatisticsStatus);

    private void OnBoardStatisticsClick(object sender, RoutedEventArgs e) => RunDrawingCommand("DC_CMD_BOARD_STATISTICS", "请框选需要统计的板件。", SetStatisticsStatus);

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

        DrawingLayerRuntimeSettings.Save(new DrawingLayerToolOptions(lineScale, hatchScale));
        RunDrawingCommand("DC_CMD_DRAWING_DISPLAY_REPAIR", "正在修复虚线和墙体填充显示。", SetDrawingLayerStatus);
    }

    private void LoadDrawingLayerModule()
    {
        var options = DrawingLayerRuntimeSettings.Current;
        DrawingLinetypeScaleBox.Text = options.LinetypeScale.ToString("0.###", CultureInfo.CurrentCulture);
        WallHatchScaleBox.Text = options.WallHatchScale.ToString("0.###", CultureInfo.CurrentCulture);
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

    private static bool Contains(string value, string keyword)
    {
        return Compatibility.Contains(value, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
