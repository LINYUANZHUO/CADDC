using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Shortcuts;

internal static class ShortcutCatalog
{
    public static IReadOnlyList<ShortcutItem> CreateDefaults()
    {
        return typeof(PluginApplication).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Select(method => new
            {
                Metadata = method.GetCustomAttribute<ShortcutCommandAttribute>(),
                Command = method.GetCustomAttributes<CommandMethodAttribute>().FirstOrDefault()
            })
            .Where(item => item.Metadata is not null && item.Command is not null)
            .Select(item =>
            {
                var defaultAlias = ShortcutValidator.NormalizeAlias(item.Metadata!.DefaultAlias);
                return new ShortcutItem
                {
                    Category = item.Metadata.Category,
                    Description = item.Metadata.Description,
                    CoreCommand = item.Command!.GlobalName.ToUpperInvariant(),
                    DefaultAlias = defaultAlias,
                    CustomAlias = defaultAlias
                };
            })
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Description, StringComparer.Ordinal)
            .ToList();
    }
}
