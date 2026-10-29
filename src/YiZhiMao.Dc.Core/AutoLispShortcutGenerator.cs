using System.Text;

namespace YiZhiMao.Dc.Core;

public static class AutoLispShortcutGenerator
{
    public static string Generate(ShortcutConfiguration configuration)
    {
        var errors = ShortcutValidator.Validate(configuration.ShortcutItems);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }

        var registrations = configuration.ShortcutItems
            .SelectMany(item => ShortcutValidator.GetEffectiveAliases(item)
                .Select(alias => new Registration(alias, item.CoreCommand)))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine(";;; 此文件由一只猫dc自动生成，请通过快捷键面板修改。");
        builder.AppendLine("(vl-load-com)");
        builder.AppendLine("(if (boundp '*dc-shortcut-aliases*)");
        builder.AppendLine("  (foreach dc:alias *dc-shortcut-aliases*");
        builder.AppendLine("    (set (read (strcat \"C:\" dc:alias)) nil)");
        builder.AppendLine("  )");
        builder.AppendLine(")");
        builder.Append("(setq *dc-shortcut-aliases* '(");
        builder.Append(string.Join(" ", registrations.Select(item => $"\"{item.Alias}\"")));
        builder.AppendLine("))");

        for (var index = 0; index < registrations.Count; index++)
        {
            var registration = registrations[index];
            var invocation = string.Equals(
                registration.CoreCommand,
                "DC_CMD_HINGE_LABEL",
                StringComparison.OrdinalIgnoreCase)
                ? $"(vla-SendCommand (vla-get-ActiveDocument (vlax-get-acad-object)) \".{registration.CoreCommand} \")"
                : $"(vl-cmdf \".{registration.CoreCommand}\")";
            builder.AppendLine($"(defun c:{registration.Alias} (/ ss)");
            builder.AppendLine("  (if (setq ss (ssget \"_I\"))");
            builder.AppendLine($"    (progn (sssetfirst nil ss) {invocation})");
            builder.AppendLine($"    {invocation}");
            builder.AppendLine("  )");
            builder.AppendLine("  (princ)");
            builder.AppendLine(")");
        }

        builder.AppendLine("(princ)");
        return builder.ToString();
    }

    private sealed record Registration(string Alias, string CoreCommand);
}
