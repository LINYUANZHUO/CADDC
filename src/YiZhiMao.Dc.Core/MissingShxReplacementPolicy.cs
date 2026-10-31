namespace YiZhiMao.Dc.Core;

public enum ShxReferenceKind
{
    TextFont,
    BigFont,
    ShapeFile
}

public enum MissingShxAction
{
    None,
    UseLibrary,
    Replace,
    ReportOnly
}

public sealed record MissingShxDecision(
    MissingShxAction Action,
    string? ReplacementFile = null);

public static class MissingShxReplacementPolicy
{
    public const string DefaultTextFont = "simplex.shx";
    public const string DefaultBigFont = "gbcbig.shx";

    public static MissingShxDecision Decide(
        string? fileName,
        ShxReferenceKind kind,
        bool isAvailable,
        bool isInLibrary)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetExtension(fileName), ".shx", StringComparison.OrdinalIgnoreCase) ||
            isAvailable)
        {
            return new MissingShxDecision(MissingShxAction.None);
        }

        if (isInLibrary)
        {
            return new MissingShxDecision(
                MissingShxAction.UseLibrary,
                Path.GetFileName(fileName));
        }

        return kind switch
        {
            ShxReferenceKind.TextFont => new MissingShxDecision(
                MissingShxAction.Replace,
                DefaultTextFont),
            ShxReferenceKind.BigFont => new MissingShxDecision(
                MissingShxAction.Replace,
                DefaultBigFont),
            ShxReferenceKind.ShapeFile => new MissingShxDecision(MissingShxAction.ReportOnly),
            _ => new MissingShxDecision(MissingShxAction.None)
        };
    }
}
