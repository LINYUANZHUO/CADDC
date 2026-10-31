using System.IO;
using System.Windows;

namespace YiZhiMao.Dc.Plugin;

internal static class FolderDialogService
{
    public static bool TrySelectFolder(
        Window owner,
        string title,
        string? initialDirectory,
        out string folderName)
    {
#if NETFRAMEWORK
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = title,
            SelectedPath = Directory.Exists(initialDirectory) ? initialDirectory : string.Empty,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            folderName = dialog.SelectedPath ?? string.Empty;
            return true;
        }
#else
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title };
        if (Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(owner) == true)
        {
            folderName = dialog.FolderName;
            return true;
        }
#endif

        folderName = string.Empty;
        return false;
    }
}
