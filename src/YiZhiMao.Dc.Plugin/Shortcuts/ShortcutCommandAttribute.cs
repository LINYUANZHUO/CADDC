namespace YiZhiMao.Dc.Plugin.Shortcuts;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ShortcutCommandAttribute(
    string category,
    string description,
    string defaultAlias = "") : Attribute
{
    public string Category { get; } = category;

    public string Description { get; } = description;

    public string DefaultAlias { get; } = defaultAlias;
}
