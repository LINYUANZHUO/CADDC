using System.Globalization;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using AcadPreferencesApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class CadQuickToggleRuntime
{
    public static bool TryGetPointerInputEnabled(out bool enabled, out string error)
    {
        enabled = false;
        try
        {
            var dynMode = Convert.ToInt32(
                AcadApplication.GetSystemVariable("DYNMODE"),
                CultureInfo.InvariantCulture);
            enabled = dynMode > 0 && (dynMode & 1) != 0;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TrySetPointerInputEnabled(bool enabled, out string error)
    {
        try
        {
            var current = Convert.ToInt32(
                AcadApplication.GetSystemVariable("DYNMODE"),
                CultureInfo.InvariantCulture);
            var settings = Math.Abs(current);
            var next = enabled ? settings | 1 : settings & ~1;
            AcadApplication.SetSystemVariable("DYNMODE", next);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryGetPointStyle(out int mode, out string error)
    {
        mode = 35;
        try
        {
            var current = Convert.ToInt32(
                AcadApplication.GetSystemVariable("PDMODE"),
                CultureInfo.InvariantCulture);
            if (!IsSupportedPointStyle(current))
            {
                error = $"当前 PDMODE 值 {current} 不在标准点样式范围内。";
                return false;
            }

            mode = current;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TrySetPointStyle(int mode, out string error)
    {
        if (!IsSupportedPointStyle(mode))
        {
            error = $"不支持的点样式值：{mode}。";
            return false;
        }

        try
        {
            AcadApplication.SetSystemVariable("PDMODE", mode);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryGetSaveFileFormat(out DrawingSaveFileFormat format, out string error)
    {
        format = DrawingSaveFileFormat.AutoCad2007;
        try
        {
            dynamic preferences = AcadPreferencesApplication.Preferences;
            var current = Convert.ToInt32(
                preferences.OpenSave.SaveAsType,
                CultureInfo.InvariantCulture);
            if (!Enum.IsDefined(typeof(DrawingSaveFileFormat), current))
            {
                error = $"当前保存格式值 {current} 不在支持范围内。";
                return false;
            }

            format = (DrawingSaveFileFormat)current;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TrySetSaveFileFormat(DrawingSaveFileFormat format, out string error)
    {
        if (!Enum.IsDefined(typeof(DrawingSaveFileFormat), format))
        {
            error = $"不支持的 DWG 保存格式值：{(int)format}。";
            return false;
        }

        try
        {
            dynamic preferences = AcadPreferencesApplication.Preferences;
            preferences.OpenSave.SaveAsType = (int)format;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryApplyConfiguredSaveFileFormat(out string error) =>
        TrySetSaveFileFormat(ToolboxSettingsRuntime.Settings.DefaultSaveFileFormat, out error);

    private static bool IsSupportedPointStyle(int mode)
    {
        var marker = mode & 31;
        var frame = mode & 96;
        return mode == marker + frame && marker <= 4;
    }
}
