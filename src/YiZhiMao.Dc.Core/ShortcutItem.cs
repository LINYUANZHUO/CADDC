namespace YiZhiMao.Dc.Core;

public sealed class ShortcutItem
{
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string CoreCommand { get; set; } = string.Empty;

    public string DefaultAlias { get; set; } = string.Empty;

    public string CustomAlias { get; set; } = string.Empty;

    public bool DisableDefault { get; set; }

    public bool IsUserDefined { get; set; }

    public static ShortcutItem CreateNewMapping()
    {
        return new ShortcutItem
        {
            Category = "自定义",
            Description = "新功能",
            CoreCommand = "NEWCMD",
            DefaultAlias = "NEWCMD",
            CustomAlias = "NEWCMD",
            IsUserDefined = true
        };
    }

    public ShortcutItem Clone()
    {
        return new ShortcutItem
        {
            Category = Category,
            Description = Description,
            CoreCommand = CoreCommand,
            DefaultAlias = DefaultAlias,
            CustomAlias = CustomAlias,
            DisableDefault = DisableDefault,
            IsUserDefined = IsUserDefined
        };
    }
}
