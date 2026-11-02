using System.Text;
using System.Text.Json;

namespace YiZhiMao.Dc.Core;

public sealed class ShortcutConfigurationStore(string configurationPath)
{
    private static readonly Dictionary<string, HashSet<string>> LegacyDefaultAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DC_CMD_STATUS"] = new(StringComparer.OrdinalIgnoreCase) { "JC" },
            ["DC_CMD_IMAGE_BATCH"] = new(StringComparer.OrdinalIgnoreCase) { "OLE" },
            ["DC_CMD_BATCH_PRINT"] = new(StringComparer.OrdinalIgnoreCase) { "DY" },
            ["DC_CMD_PRINT_PAPER"] = new(StringComparer.OrdinalIgnoreCase) { "D1" },
            ["DC_CMD_PRINT_PDF"] = new(StringComparer.OrdinalIgnoreCase) { "D2" },
            ["DC_CMD_PRINT_LAYOUT_PAPER"] = new(StringComparer.OrdinalIgnoreCase) { "D3" },
            ["DC_CMD_PRINT_LAYOUT_PDF"] = new(StringComparer.OrdinalIgnoreCase) { "D4" },
            ["DC_CMD_LAYER_MANAGER"] = new(StringComparer.OrdinalIgnoreCase) { "TC" },
            ["DC_CMD_LAYER_STANDARDIZE"] = new(StringComparer.OrdinalIgnoreCase) { "TCJZ" },
            ["DC_CMD_LAYER_NORMALIZE"] = new(StringComparer.OrdinalIgnoreCase) { "TCGC" },
            ["DC_CMD_DRAWING_PURGE"] = new(StringComparer.OrdinalIgnoreCase) { "PU" },
            ["DC_CMD_LAYER_HIDE_HARDWARE"] = new(StringComparer.OrdinalIgnoreCase) { "TCWJ" },
            ["DC_CMD_LAYER_HIDE_DOOR"] = new(StringComparer.OrdinalIgnoreCase) { "TCMB" },
            ["DC_CMD_LAYER_HIDE_CABINET"] = new(StringComparer.OrdinalIgnoreCase) { "TCHG" },
            ["DC_CMD_LAYER_HIDE_ANNOTATION"] = new(StringComparer.OrdinalIgnoreCase) { "TCBZ" },
            ["DC_CMD_LAYER_SHOW_ALL"] = new(StringComparer.OrdinalIgnoreCase) { "TCXS" },
            ["DC_CMD_DRAWING_DISPLAY_REPAIR"] = new(StringComparer.OrdinalIgnoreCase) { "TXF" },
            ["DC_CMD_LAYER_MAKE_CURRENT"] = new(StringComparer.OrdinalIgnoreCase) { "T1" },
            ["DC_CMD_ENTITY_HIDE"] = new(StringComparer.OrdinalIgnoreCase) { "H1" },
            ["DC_CMD_ENTITY_UNHIDE_LAST"] = new(StringComparer.OrdinalIgnoreCase) { "H2" },
            ["DC_CMD_ENTITY_SHOW_ALL"] = new(StringComparer.OrdinalIgnoreCase) { "H3" },
            ["DC_CMD_ENTITY_LOCK_LAYERS"] = new(StringComparer.OrdinalIgnoreCase) { "S1" },
            ["DC_CMD_ENTITY_UNLOCK_LAYERS"] = new(StringComparer.OrdinalIgnoreCase) { "S2" },
            ["DC_CMD_ENTITY_UNLOCK_ALL_LAYERS"] = new(StringComparer.OrdinalIgnoreCase) { "S3" },
            ["DC_CMD_SELECT_DOOR_LAYER"] = new(StringComparer.OrdinalIgnoreCase) { "X1" },
            ["DC_CMD_SELECT_CABINET_LAYER"] = new(StringComparer.OrdinalIgnoreCase) { "X2" },
            ["DC_CMD_SELECT_CUSTOM_LAYER_ONE"] = new(StringComparer.OrdinalIgnoreCase) { "X3" },
            ["DC_CMD_SELECT_CUSTOM_LAYER_TWO"] = new(StringComparer.OrdinalIgnoreCase) { "X4" },
            ["DC_CMD_SELECT_ALL_ANNOTATIONS"] = new(StringComparer.OrdinalIgnoreCase) { "X5" },
            ["DC_CMD_SELECT_SIMILAR_IN_RANGE"] = new(StringComparer.OrdinalIgnoreCase) { "XZ" },
            ["DC_CMD_LIGHT_STRIP"] = new(StringComparer.OrdinalIgnoreCase) { "DD" },
            ["DC_CMD_HINGE_INSERT"] = new(StringComparer.OrdinalIgnoreCase) { "JL" },
            ["DC_CMD_HANGING_ROD"] = new(StringComparer.OrdinalIgnoreCase) { "YG" },
            ["DC_CMD_HINGE_LABEL"] = new(StringComparer.OrdinalIgnoreCase) { "HJL" },
            ["DC_CMD_WAINSCOT_GRILLE_DRAW"] = new(StringComparer.OrdinalIgnoreCase) { "GS" },
            ["DC_CMD_WAINSCOT_GRILLE_FILL"] = new(StringComparer.OrdinalIgnoreCase) { "GST" },
            ["DC_CMD_WAINSCOT_PERFORATED_DRAW"] = new(StringComparer.OrdinalIgnoreCase) { "DB" },
            ["DC_CMD_WAINSCOT_PERFORATED_FILL"] = new(StringComparer.OrdinalIgnoreCase) { "DBT" },
            ["DC_CMD_WAINSCOT_BACKGROUND_DRAW"] = new(StringComparer.OrdinalIgnoreCase) { "BJQ" },
            ["DC_CMD_LINE_TO_DOUBLE"] = new(StringComparer.OrdinalIgnoreCase) { "DCZ2" },
            ["DC_CMD_LINE_TO_TRIPLE"] = new(StringComparer.OrdinalIgnoreCase) { "DCZ3" },
            ["DC_CMD_CABINET_STANDARD"] = new(StringComparer.OrdinalIgnoreCase) { "DG" },
            ["DC_CMD_CABINET_REFRESH"] = new(StringComparer.OrdinalIgnoreCase) { "XX" },
            ["DC_CMD_CABINET_UNIVERSAL"] = new(StringComparer.OrdinalIgnoreCase) { "WNG" }
        };

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

            var savedDefaultAlias = ShortcutValidator.NormalizeAlias(savedItem.DefaultAlias);
            var savedCustomAlias = ShortcutValidator.NormalizeAlias(savedItem.CustomAlias);
            var savedDefaultIsLegacy =
                savedConfiguration!.SchemaVersion < ShortcutConfiguration.CurrentSchemaVersion &&
                IsLegacyDefaultAlias(item.CoreCommand, savedDefaultAlias);

            if (savedConfiguration.SchemaVersion >= ShortcutConfiguration.CurrentSchemaVersion ||
                (savedDefaultAlias.Length > 0 && !savedDefaultIsLegacy))
            {
                item.Description = savedItem.Description;
                item.DefaultAlias = savedDefaultAlias;
            }

            var unchangedLegacyAlias =
                savedConfiguration.SchemaVersion < ShortcutConfiguration.CurrentSchemaVersion &&
                savedDefaultIsLegacy &&
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

        var tempPath = $"{ConfigurationPath}.{System.Diagnostics.Process.GetCurrentProcess().Id}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                tempPath,
                JsonSerializer.Serialize(normalized, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Compatibility.MoveFileReplacingExisting(tempPath, ConfigurationPath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool IsLegacyDefaultAlias(string coreCommand, string alias) =>
        alias.Length > 0 &&
        LegacyDefaultAliases.TryGetValue(coreCommand, out var aliases) &&
        aliases.Contains(alias);

    private static bool Equivalent(ShortcutConfiguration saved, ShortcutConfiguration merged)
    {
        var savedItems = saved.ShortcutItems ?? [];
        if (saved.SchemaVersion != ShortcutConfiguration.CurrentSchemaVersion ||
            savedItems.Count != merged.ShortcutItems.Count)
        {
            return false;
        }

        return savedItems.Zip(merged.ShortcutItems, (first, second) => (First: first, Second: second)).All(pair =>
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
