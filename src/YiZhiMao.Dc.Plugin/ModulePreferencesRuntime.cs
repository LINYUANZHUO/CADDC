using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class ModulePreferencesRuntime
{
    public static ToolboxPreferenceOptions Current => ToolboxSettingsRuntime.Settings.Preferences;

    public static bool IsEnabled(ToolboxFeatureModule module) => Current.IsEnabled(module);

    public static bool EnsureEnabled(ToolboxFeatureModule module, Document? document = null)
    {
        if (IsEnabled(module))
        {
            return true;
        }

        var info = ToolboxFeatureModuleCatalog.Get(module);
        (document ?? AcadApplication.DocumentManager.MdiActiveDocument)?.Editor.WriteMessage(
            $"\n[一只猫dc] “{info.Name}”模块已禁用，请在“偏好设置”中重新启用。");
        return false;
    }
}

internal static class ModuleCommandGuard
{
    private static readonly IReadOnlyDictionary<string, ToolboxFeatureModule> CommandModules = BuildCommandModules();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        AcadApplication.DocumentManager.DocumentLockModeChanged += OnDocumentLockModeChanged;
        initialized = true;
    }

    public static void Dispose()
    {
        if (!initialized)
        {
            return;
        }

        AcadApplication.DocumentManager.DocumentLockModeChanged -= OnDocumentLockModeChanged;
        initialized = false;
    }

    public static bool TryGetModule(string? commandName, out ToolboxFeatureModule module) =>
        CommandModules.TryGetValue(NormalizeCommandName(commandName), out module);

    private static void OnDocumentLockModeChanged(object sender, DocumentLockModeChangedEventArgs e)
    {
        if (!TryGetModule(e.GlobalCommandName, out var module) || ModulePreferencesRuntime.IsEnabled(module))
        {
            return;
        }

        e.Veto();
        _ = ModulePreferencesRuntime.EnsureEnabled(module, e.Document);
    }

    private static IReadOnlyDictionary<string, ToolboxFeatureModule> BuildCommandModules()
    {
        var commands = new Dictionary<string, ToolboxFeatureModule>(StringComparer.OrdinalIgnoreCase);
        foreach (var method in typeof(PluginApplication).Assembly
                     .GetTypes()
                     .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
        {
            var shortcut = method.GetCustomAttribute<ShortcutCommandAttribute>();
            if (shortcut is null ||
                !ToolboxFeatureModuleCatalog.TryGetByCommandCategory(shortcut.Category, out var module))
            {
                continue;
            }

            foreach (var command in method.GetCustomAttributes<CommandMethodAttribute>())
            {
                commands[NormalizeCommandName(command.GlobalName)] = module;
            }
        }

        Add(commands, ToolboxFeatureModule.BatchPrint, "DC_CMD_DRAW_PRINT_FRAME", "DC_CMD_PDF_TOOL");
        Add(commands, ToolboxFeatureModule.DoorPanels, "DCMB", "DCMC", "DCMBF", "DCSDH", "DCLAS", "DCHJL");
        return commands;
    }

    private static void Add(
        IDictionary<string, ToolboxFeatureModule> commands,
        ToolboxFeatureModule module,
        params string[] commandNames)
    {
        foreach (var commandName in commandNames)
        {
            commands[NormalizeCommandName(commandName)] = module;
        }
    }

    private static string NormalizeCommandName(string? commandName) =>
        (commandName ?? string.Empty).Trim().TrimStart('.', '_').ToUpperInvariant();
}
