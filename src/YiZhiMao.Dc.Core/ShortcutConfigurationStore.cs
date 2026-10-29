using System.Text;
using System.Text.Json;

namespace YiZhiMao.Dc.Core;

public sealed class ShortcutConfigurationStore(string configurationPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string ConfigurationPath { get; } = configurationPath;

    public ShortcutConfiguration LoadAndMerge(IEnumerable<ShortcutItem> defaults)
    {
        var defaultItems = defaults.Select(item => item.Clone()).ToList();
        ShortcutConfiguration? savedConfiguration = null;

        if (File.Exists(ConfigurationPath))
        {
            try
            {
                savedConfiguration = JsonSerializer.Deserialize<ShortcutConfiguration>(
                    File.ReadAllText(ConfigurationPath, Encoding.UTF8),
                    JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("快捷键配置文件格式错误，请在面板中修正或移走该文件后重试。", exception);
            }
        }

        var savedItems = (savedConfiguration?.ShortcutItems ?? [])
            .OfType<ShortcutItem>()
            .Where(item => !string.IsNullOrWhiteSpace(item.CoreCommand))
            .ToList();
        var savedBuiltInItems = savedItems
            .Where(item => !item.IsUserDefined)
            .GroupBy(item => item.CoreCommand, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in defaultItems)
        {
            if (!savedBuiltInItems.TryGetValue(item.CoreCommand, out var savedItem))
            {
                continue;
            }

            if (savedConfiguration!.SchemaVersion >= ShortcutConfiguration.CurrentSchemaVersion)
            {
                item.Description = savedItem.Description;
                item.DefaultAlias = ShortcutValidator.NormalizeAlias(savedItem.DefaultAlias);
            }

            var savedDefaultAlias = ShortcutValidator.NormalizeAlias(savedItem.DefaultAlias);
            var savedCustomAlias = ShortcutValidator.NormalizeAlias(savedItem.CustomAlias);
            var unchangedLegacyAlias =
                savedConfiguration.SchemaVersion < ShortcutConfiguration.CurrentSchemaVersion &&
                savedCustomAlias.Length > 0 &&
                string.Equals(savedCustomAlias, savedDefaultAlias, StringComparison.Ordinal) &&
                !string.Equals(savedDefaultAlias, item.DefaultAlias, StringComparison.Ordinal);
            if (unchangedLegacyAlias)
            {
                item.CustomAlias = item.DefaultAlias;
            }
            else if (savedCustomAlias.Length > 0 || savedConfiguration.SchemaVersion >= 3)
            {
                item.CustomAlias = savedCustomAlias;
            }

            item.DisableDefault = savedItem.DisableDefault;
        }

        foreach (var savedItem in savedItems.Where(item => item.IsUserDefined))
        {
            defaultItems.Add(savedItem.Clone());
        }

        var merged = new ShortcutConfiguration { ShortcutItems = defaultItems };
        if ((savedConfiguration is null || !Equivalent(savedConfiguration, merged)) &&
            ShortcutValidator.Validate(merged.ShortcutItems).Count == 0)
        {
            Save(merged);
        }

        return merged;
    }

    public void Save(ShortcutConfiguration configuration)
    {
        var directory = Path.GetDirectoryName(ConfigurationPath)
            ?? throw new InvalidOperationException("快捷键配置路径缺少父目录。");
        Directory.CreateDirectory(directory);

        var normalized = new ShortcutConfiguration
        {
            ShortcutItems = configuration.ShortcutItems.Select(item =>
            {
                var copy = item.Clone();
                copy.DefaultAlias = ShortcutValidator.NormalizeAlias(copy.DefaultAlias);
                copy.CustomAlias = ShortcutValidator.NormalizeAlias(copy.CustomAlias);
                return copy;
            }).ToList()
        };

        var errors = ShortcutValidator.Validate(normalized.ShortcutItems);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        var tempPath = $"{ConfigurationPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                tempPath,
                JsonSerializer.Serialize(normalized, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, ConfigurationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool Equivalent(ShortcutConfiguration saved, ShortcutConfiguration merged)
    {
        var savedItems = saved.ShortcutItems ?? [];
        if (saved.SchemaVersion != ShortcutConfiguration.CurrentSchemaVersion ||
            savedItems.Count != merged.ShortcutItems.Count)
        {
            return false;
        }

        return savedItems.Zip(merged.ShortcutItems).All(pair =>
            string.Equals(pair.First.Category, pair.Second.Category, StringComparison.Ordinal) &&
            string.Equals(pair.First.Description, pair.Second.Description, StringComparison.Ordinal) &&
            string.Equals(pair.First.CoreCommand, pair.Second.CoreCommand, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                ShortcutValidator.NormalizeAlias(pair.First.DefaultAlias),
                ShortcutValidator.NormalizeAlias(pair.Second.DefaultAlias),
                StringComparison.Ordinal) &&
            string.Equals(
                ShortcutValidator.NormalizeAlias(pair.First.CustomAlias),
                ShortcutValidator.NormalizeAlias(pair.Second.CustomAlias),
                StringComparison.Ordinal) &&
            pair.First.DisableDefault == pair.Second.DisableDefault &&
            pair.First.IsUserDefined == pair.Second.IsUserDefined);
    }
}
