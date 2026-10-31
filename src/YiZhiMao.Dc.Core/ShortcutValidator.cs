using System.Text.RegularExpressions;

namespace YiZhiMao.Dc.Core;

public static class ShortcutValidator
{
    private static readonly Regex AliasRegex = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly Regex CommandRegex = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Validate(IEnumerable<ShortcutItem> items)
    {
        var errors = new List<string>();
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                errors.Add("功能说明不能为空。");
            }

            if (!CommandRegex.IsMatch(item.CoreCommand))
            {
                errors.Add($"底层命令 {item.CoreCommand} 无效，只能使用英文字母开头的字母、数字和下划线。");
            }

            if (item.IsUserDefined && GetEffectiveAliases(item).Count == 0)
            {
                errors.Add($"{item.Description} 至少需要一个可用快捷键。");
            }

            foreach (var alias in GetEffectiveAliases(item))
            {
                if (!AliasRegex.IsMatch(alias))
                {
                    errors.Add($"{item.Description} 的快捷键“{alias}”无效，只能使用英文字母开头的字母、数字和下划线。");
                    continue;
                }

                if (aliases.TryGetValue(alias, out var existingDescription))
                {
                    errors.Add($"快捷键“{alias}”重复：{existingDescription} 与 {item.Description}。");
                    continue;
                }

                aliases.Add(alias, item.Description);
            }
        }

        return errors;
    }

    public static IReadOnlyList<string> GetEffectiveAliases(ShortcutItem item)
    {
        var aliases = new List<string>(2);
        var defaultAlias = NormalizeAlias(item.DefaultAlias);
        var customAlias = NormalizeAlias(item.CustomAlias);

        if (!item.DisableDefault && defaultAlias.Length > 0)
        {
            aliases.Add(defaultAlias);
        }

        if (customAlias.Length > 0)
        {
            aliases.Add(customAlias);
        }

        return aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string NormalizeAlias(string? alias)
    {
        return alias?.Trim().ToUpperInvariant() ?? string.Empty;
    }

}
