using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class DimensionStyleSyncService
{
    private const string PreferredStyleName = "雷神标注";

    public static bool TrySynchronize(
        Document document,
        SmartAnnotationOptions options,
        out string message)
    {
        if (!options.UseTemplateStyle)
        {
            message = "使用当前图纸标注样式。";
            return true;
        }

        var templatePath = DimensionTemplateLibraryService.Resolve(options.TemplateName);
        if (templatePath is null)
        {
            message = $"未找到标注样式模板：{options.TemplateName}";
            return false;
        }

        return TrySynchronize(document, templatePath, PreferredStyleName, out message);
    }

    internal static bool TrySynchronize(
        Document document,
        string templatePath,
        string preferredStyleName,
        out string message)
    {
        try
        {
            var targetDatabase = document.Database;
            if (IsSameDrawing(targetDatabase.Filename, templatePath))
            {
                return TryActivateTargetStyle(targetDatabase, preferredStyleName, out message);
            }

            using var sourceDatabase = new Database(false, true);
            sourceDatabase.ReadDwgFile(
                templatePath,
                FileOpenMode.OpenForReadAndAllShare,
                allowCPConversion: true,
                password: null);
            sourceDatabase.CloseInput(true);

            var sourceIds = new ObjectIdCollection();
            var sourceNames = new List<string>();
            using (var sourceTransaction = sourceDatabase.TransactionManager.StartOpenCloseTransaction())
            {
                var sourceTable = (DimStyleTable)sourceTransaction.GetObject(
                    sourceDatabase.DimStyleTableId,
                    OpenMode.ForRead);
                foreach (ObjectId sourceId in sourceTable)
                {
                    var record = (DimStyleTableRecord)sourceTransaction.GetObject(sourceId, OpenMode.ForRead);
                    if (string.Equals(record.Name, "Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    sourceIds.Add(sourceId);
                    sourceNames.Add(record.Name);
                }
            }

            if (sourceIds.Count == 0)
            {
                message = $"模板中没有可同步的标注样式：{Path.GetFileName(templatePath)}";
                return false;
            }

            var mapping = new IdMapping();
            sourceDatabase.WblockCloneObjects(
                sourceIds,
                targetDatabase.DimStyleTableId,
                mapping,
                DuplicateRecordCloning.Replace,
                deferTranslation: false);

            var activeStyle = sourceNames.FirstOrDefault(name =>
                                  string.Equals(name, preferredStyleName, StringComparison.OrdinalIgnoreCase))
                              ?? sourceNames[0];
            if (!TryActivateTargetStyle(targetDatabase, activeStyle, out message))
            {
                return false;
            }

            message = $"已从 {Path.GetFileName(templatePath)} 同步 {sourceIds.Count} 个标注样式，当前样式：{activeStyle}。";
            return true;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception exception)
        {
            message = $"{exception.ErrorStatus}: {exception.Message}";
            return false;
        }
        catch (System.Exception exception)
        {
            message = exception.Message;
            return false;
        }
    }

    private static bool TryActivateTargetStyle(Database targetDatabase, string styleName, out string message)
    {
        using var transaction = targetDatabase.TransactionManager.StartOpenCloseTransaction();
        var targetTable = (DimStyleTable)transaction.GetObject(
            targetDatabase.DimStyleTableId,
            OpenMode.ForRead);
        if (!targetTable.Has(styleName))
        {
            message = $"当前图纸中未找到标注样式：{styleName}";
            return false;
        }

        targetDatabase.Dimstyle = targetTable[styleName];
        message = $"当前标注样式：{styleName}。";
        return true;
    }

    private static bool IsSameDrawing(string? targetPath, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(targetPath),
            Path.GetFullPath(sourcePath),
            StringComparison.OrdinalIgnoreCase);
    }
}
