namespace YiZhiMao.Dc.Core;

public sealed class ShortcutConfiguration
{
    public const int CurrentSchemaVersion = 10;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<ShortcutItem> ShortcutItems { get; set; } = [];
}
