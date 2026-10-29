using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin.Views;

internal sealed class RadialMenuWindow : Window
{
    private readonly RadialMenuOptions options;

    public RadialMenuWindow(RadialMenuOptions options)
    {
        this.options = options;
        var diameter = options.Radius * 2 + 4;
        Width = diameter;
        Height = diameter;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        Opacity = options.Opacity;

        var control = new RadialMenuControl(options);
        control.CloseRequested += Close;
        control.CommandSelected += ExecuteCommand;
        Content = control;
        PreviewKeyDown += OnPreviewKeyDown;
        MouseRightButtonUp += (_, _) => Close();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var mainWindow = AcadApplication.MainWindow;
        if (mainWindow is not null)
        {
            new WindowInteropHelper(this).Owner = mainWindow.Handle;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var cursorInDip = transform.Transform(new Point(cursor.X, cursor.Y));
        Left = cursorInDip.X - Width / 2;
        Top = cursorInDip.Y - Height / 2;
        Activate();
        Focus();
    }

    private void ExecuteCommand(RadialMenuItem item)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            Close();
            return;
        }

        if (options.AutoCloseAfterCommand)
        {
            Close();
        }

        document.SendStringToExecute(item.CommandAlias.Trim() + " ", true, false, true);
        if (!options.AutoCloseAfterCommand && AcadApplication.MainWindow is { } mainWindow)
        {
            SetForegroundWindow(mainWindow.Handle);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

internal static class RadialMenuWindowManager
{
    private static RadialMenuWindow? window;

    public static void Show(RadialMenuOptions options)
    {
        Dispose();
        window = new RadialMenuWindow(options);
        window.Closed += (_, _) => window = null;
        window.Show();
    }

    public static void Dispose()
    {
        window?.Close();
        window = null;
    }
}
