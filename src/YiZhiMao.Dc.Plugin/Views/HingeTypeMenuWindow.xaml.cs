using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace YiZhiMao.Dc.Plugin.Views;

public partial class HingeTypeMenuWindow : Window
{
    private const double CursorOffsetX = 12;
    private const double CursorOffsetY = 18;

    public HingeTypeMenuWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    public string? SelectedHingeType { get; private set; }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        PositionBelowCursor();
        FullCoverButton.Focus();
    }

    private void OnTypeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hingeType })
        {
            SelectedHingeType = hingeType;
        }

        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        SelectedHingeType = e.Key switch
        {
            Key.D => "内嵌",
            Key.X => "半盖",
            Key.Z or Key.Enter => "全盖",
            _ => SelectedHingeType
        };

        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (SelectedHingeType is not null)
        {
            Close();
        }
    }

    private void PositionBelowCursor()
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var left = cursor.X / dpi.DpiScaleX + CursorOffsetX;
        var top = cursor.Y / dpi.DpiScaleY + CursorOffsetY;
        var maximumLeft = SystemParameters.VirtualScreenLeft +
                          SystemParameters.VirtualScreenWidth - ActualWidth;
        var maximumTop = SystemParameters.VirtualScreenTop +
                         SystemParameters.VirtualScreenHeight - ActualHeight;
        Left = Math.Clamp(left, SystemParameters.VirtualScreenLeft, maximumLeft);
        Top = Math.Clamp(top, SystemParameters.VirtualScreenTop, maximumTop);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
