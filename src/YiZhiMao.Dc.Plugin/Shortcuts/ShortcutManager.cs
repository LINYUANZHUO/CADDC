using System.Text;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin.Shortcuts;

internal static class ShortcutManager
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YiZhiMaoDc");

    private static readonly string ConfigurationPath = Path.Combine(DataDirectory, "ShortcutConfig.json");
    private static readonly string ScriptPath = Path.Combine(DataDirectory, "DC_CustomShortcuts.lsp");
    private static readonly ShortcutConfigurationStore Store = new(ConfigurationPath);

    public static ShortcutConfiguration Load()
    {
        return Store.LoadAndMerge(ShortcutCatalog.CreateDefaults());
    }

    public static SaveResult SaveAndApply(IEnumerable<ShortcutItem> items)
    {
        var configuration = new ShortcutConfiguration
        {
            ShortcutItems = items.Select(item => item.Clone()).ToList()
        };

        foreach (var item in configuration.ShortcutItems)
        {
            item.DefaultAlias = ShortcutValidator.NormalizeAlias(item.DefaultAlias);
            item.CustomAlias = ShortcutValidator.NormalizeAlias(item.CustomAlias);
        }

        var errors = ShortcutValidator.Validate(configuration.ShortcutItems);
        if (errors.Count > 0)
        {
            return new SaveResult(false, string.Join(Environment.NewLine, errors), configuration);
        }

        Store.Save(configuration);
        WriteScript(configuration);
        var applied = QueueLoad(AcadApplication.DocumentManager.MdiActiveDocument);
        var message = applied
            ? "快捷键已保存，正在当前图纸中应用。"
            : "快捷键已保存；打开图纸后将自动应用。";
        return new SaveResult(true, message, configuration);
    }

    public static void Initialize()
    {
        var configuration = Load();
        var errors = ShortcutValidator.Validate(configuration.ShortcutItems);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        WriteScript(configuration);
        QueueLoad(AcadApplication.DocumentManager.MdiActiveDocument);
    }

    public static void ApplyTo(Document document)
    {
        if (File.Exists(ScriptPath))
        {
            QueueLoad(document);
        }
    }

    public static string GetConfigurationPath()
    {
        return ConfigurationPath;
    }

    private static void WriteScript(ShortcutConfiguration configuration)
    {
        Directory.CreateDirectory(DataDirectory);
        var tempPath = $"{ScriptPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                tempPath,
                AutoLispShortcutGenerator.Generate(configuration),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, ScriptPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool QueueLoad(Document? document)
    {
        if (document is null || !File.Exists(ScriptPath))
        {
            return false;
        }

        var lispPath = ScriptPath.Replace('\\', '/').Replace("\"", "\\\"");
        document!.SendStringToExecute($"(load \"{lispPath}\") ", activate: true, wrapUpInactiveDoc: false, echoCommand: false);
        return true;
    }
}

internal sealed record SaveResult(bool Success, string Message, ShortcutConfiguration Configuration);
