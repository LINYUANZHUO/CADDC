using Autodesk.AutoCAD.ApplicationServices;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class RadialMenuMouseActivation
{
    private const int WmMiddleButtonDoubleClick = 0x0209;
    private static bool initialized;
    private static bool pending;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        initialized = true;
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

        AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
        AcadApplication.Idle -= OnIdle;
        pending = false;
        initialized = false;
    }

    private static void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
    {
        if (eventArgs.Message.message != WmMiddleButtonDoubleClick ||
            !RadialMenuRuntime.Options.Enabled)
        {
            return;
        }

        eventArgs.Handled = true;
        if (pending)
        {
            return;
        }

        pending = true;
        AcadApplication.Idle += OnIdle;
    }

    private static void OnIdle(object? sender, EventArgs eventArgs)
    {
        AcadApplication.Idle -= OnIdle;
        pending = false;
        RadialMenuCommands.ShowRadialMenu();
    }
}
