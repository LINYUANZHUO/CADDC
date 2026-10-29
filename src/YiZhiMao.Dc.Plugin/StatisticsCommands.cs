using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Win32;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class StatisticsCommands
{
    private const string DefaultWholeHouseFileName = "Drawing1_柜拆单.xls";

    [CommandMethod("DC_CMD_PARTS_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "部件统计", "DCCD")]
    public static void ExportParts() => Export(StatisticsMode.Parts, "部件统计");

    [CommandMethod("DC_CMD_DOOR_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "普通门板统计", "DCTJM")]
    public static void ExportDoors() => Export(StatisticsMode.Doors, "普通门板统计");

    [CommandMethod("DC_CMD_FRAME_DOOR_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "边框门板统计", "DCBKM")]
    public static void ExportFrameDoors() => Export(StatisticsMode.FrameDoors, "边框门板统计");

    [CommandMethod("DC_CMD_CABINET_BOM", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "柜体拆单", "DCTJG")]
    public static void ExportCabinetBom() => Export(StatisticsMode.Cabinet, "柜体拆单");

    [CommandMethod("DC_CMD_BOARD_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "板件统计", "DCBJT")]
    public static void ExportBoards() => Export(StatisticsMode.Boards, "板件统计");

    [CommandMethod("DC_CMD_WHOLE_HOUSE_BOM", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "一键全屋拆单", "DCQW")]
    public static void ExportWholeHouse()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var rows = DrawingStatistics.Aggregate(CollectAll(document.Database, StatisticsMode.WholeHouse));
        if (rows.Count == 0)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 当前模型空间没有可用于全屋拆单的对象。");
            return;
        }

        var outputOptions = new PromptKeywordOptions("\n请选择【一键全屋拆单】输出方式")
        {
            AllowNone = false,
            AppendKeywordsToMessage = true
        };
        outputOptions.Keywords.Add("Table", "T", "放置CAD表格");
        outputOptions.Keywords.Add("Excel", "E", "导出Excel总表");
        var output = document.Editor.GetKeywords(outputOptions);
        if (output.Status != PromptStatus.OK)
        {
            return;
        }

        if (string.Equals(output.StringResult, "Table", StringComparison.OrdinalIgnoreCase))
        {
            PlaceCadTable(document, rows);
            return;
        }

        ExportWholeHouseExcel(document, rows);
    }

    private static void Export(StatisticsMode mode, string title)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var options = new PromptSelectionOptions
        {
            MessageForAdding = $"\n请选择参与{title}的对象，回车确认: "
        };
        var selection = document.Editor.GetSelection(options);
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var parts = Collect(document.Database, selection.Value.GetObjectIds(), mode);
        var rows = DrawingStatistics.Aggregate(parts);
        if (rows.Count == 0)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 选择范围内没有可用于{title}的对象。");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"导出{title}",
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"{title}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            AddExtension = true,
            DefaultExt = ".csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        DrawingStatistics.ExportCsv(dialog.FileName, rows);
        document.Editor.WriteMessage(
            $"\n[一只猫dc] {title}已导出 {rows.Sum(row => row.Quantity)} 件、{rows.Count} 种规格：{dialog.FileName}");
    }

    private static IReadOnlyList<DrawingPart> CollectAll(Database database, StatisticsMode mode)
    {
        var objectIds = new List<ObjectId>();
        using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            objectIds.AddRange(modelSpace.Cast<ObjectId>());
        }

        return Collect(database, objectIds, mode);
    }

    private static IReadOnlyList<DrawingPart> Collect(
        Database database,
        IEnumerable<ObjectId> objectIds,
        StatisticsMode mode)
    {
        var result = new List<DrawingPart>();
        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var objectId in objectIds)
        {
            if (objectId.IsNull || transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var layer = entity.Layer ?? string.Empty;
            var name = GetName(entity, transaction);
            var category = Classify(layer, name, mode);
            if (category is null || !TryGetSize(entity, out var width, out var height, out var thickness))
            {
                continue;
            }

            result.Add(new DrawingPart(category, ResolvePartName(layer, name, category), width, height, thickness));
        }

        transaction.Commit();
        return result;
    }

    private static void PlaceCadTable(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        IReadOnlyList<DrawingStatisticsRow> rows)
    {
        var point = document.Editor.GetPoint("\n请选择CAD拆单表格放置点: ");
        if (point.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        var table = new Table
        {
            TableStyle = document.Database.Tablestyle,
            Position = point.Value
        };
        table.SetSize(rows.Count + 2, 7);
        table.SetRowHeight(8);
        table.SetColumnWidth(30);
        table.Cells[0, 0].TextString = "一只猫dc 一键全屋拆单";
        table.MergeCells(CellRange.Create(table, 0, 0, 0, 6));
        var headers = new[] { "类别", "名称", "宽度(mm)", "高度(mm)", "厚度(mm)", "数量", "面积(m²)" };
        for (var column = 0; column < headers.Length; column++)
        {
            table.Cells[1, column].TextString = headers[column];
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var values = new[]
            {
                row.Category,
                row.Name,
                row.Width.ToString("0.##", CultureInfo.InvariantCulture),
                row.Height.ToString("0.##", CultureInfo.InvariantCulture),
                row.Thickness.ToString("0.##", CultureInfo.InvariantCulture),
                row.Quantity.ToString(CultureInfo.InvariantCulture),
                row.AreaSquareMeters.ToString("0.####", CultureInfo.InvariantCulture)
            };
            for (var column = 0; column < values.Length; column++)
            {
                table.Cells[index + 2, column].TextString = values[column];
            }
        }

        modelSpace.AppendEntity(table);
        transaction.AddNewlyCreatedDBObject(table, true);
        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已放置 {rows.Count} 行全屋拆单CAD表格。");
    }

    private static void ExportWholeHouseExcel(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        IReadOnlyList<DrawingStatisticsRow> rows)
    {
        var drawingName = Path.GetFileNameWithoutExtension(document.Name);
        var fileName = string.IsNullOrWhiteSpace(drawingName)
            ? DefaultWholeHouseFileName
            : $"{drawingName}_柜拆单.xls";
        var dialog = new SaveFileDialog
        {
            Title = "导出一键全屋拆单总表",
            Filter = "Excel 97-2003 工作簿 (*.xls)|*.xls",
            FileName = fileName,
            AddExtension = true,
            DefaultExt = ".xls",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        DrawingStatistics.ExportExcelXml(dialog.FileName, "柜拆单", rows);
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 全屋拆单已导出 {rows.Sum(row => row.Quantity)} 件、{rows.Count} 种规格：{dialog.FileName}");
    }

    private static string? Classify(string layer, string name, StatisticsMode mode)
    {
        var combined = layer + " " + name;
        var isDoor = combined.Contains("门板", StringComparison.OrdinalIgnoreCase) ||
                     combined.Contains("抽面", StringComparison.OrdinalIgnoreCase);
        var isFrameDoor = isDoor && (combined.Contains("边框", StringComparison.OrdinalIgnoreCase) ||
                                     combined.Contains("玻璃", StringComparison.OrdinalIgnoreCase));
        var isBoard = combined.Contains("柜体", StringComparison.OrdinalIgnoreCase) ||
                      combined.Contains("层板", StringComparison.OrdinalIgnoreCase) ||
                      combined.Contains("背板", StringComparison.OrdinalIgnoreCase) ||
                      combined.Contains("板件", StringComparison.OrdinalIgnoreCase);
        var isPart = isBoard || isDoor ||
                     combined.Contains("铰链", StringComparison.OrdinalIgnoreCase) ||
                     combined.Contains("拉手", StringComparison.OrdinalIgnoreCase) ||
                     combined.Contains("挂衣杆", StringComparison.OrdinalIgnoreCase) ||
                     combined.Contains("灯带", StringComparison.OrdinalIgnoreCase);

        return mode switch
        {
            StatisticsMode.WholeHouse when isFrameDoor => "边框门板",
            StatisticsMode.WholeHouse when isDoor => "普通门板",
            StatisticsMode.WholeHouse when isBoard => "柜体板件",
            StatisticsMode.WholeHouse when isPart => "五金部件",
            StatisticsMode.Parts when isPart => "部件",
            StatisticsMode.Doors when isDoor && !isFrameDoor => "普通门板",
            StatisticsMode.FrameDoors when isFrameDoor => "边框门板",
            StatisticsMode.Cabinet when isBoard => "柜体板件",
            StatisticsMode.Boards when isBoard => "板件",
            _ => null
        };
    }

    private static string ResolvePartName(string layer, string name, string fallback)
    {
        var isGenericEntityName = name is "矩形" or "圆形五金" or "线性部件";
        if (isGenericEntityName && !string.IsNullOrWhiteSpace(layer) && layer != "0")
        {
            return layer;
        }

        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "图元", StringComparison.Ordinal))
        {
            return name;
        }

        return !string.IsNullOrWhiteSpace(layer) && layer != "0" ? layer : fallback;
    }

    private static string GetName(Entity entity, Transaction transaction)
    {
        if (entity is not BlockReference blockReference)
        {
            return entity switch
            {
                Polyline => "矩形",
                Circle => "圆形五金",
                Line => "线性部件",
                _ => "图元"
            };
        }

        var definition = (BlockTableRecord)transaction.GetObject(
            blockReference.IsDynamicBlock ? blockReference.DynamicBlockTableRecord : blockReference.BlockTableRecord,
            OpenMode.ForRead);
        return definition.Name;
    }

    private static bool TryGetSize(Entity entity, out double width, out double height, out double thickness)
    {
        width = 0;
        height = 0;
        thickness = 0;
        try
        {
            var extents = entity.GeometricExtents;
            width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);
            height = Math.Abs(extents.MaxPoint.Y - extents.MinPoint.Y);
            thickness = Math.Abs(extents.MaxPoint.Z - extents.MinPoint.Z);
            if (entity is Polyline polyline && Math.Abs(polyline.Thickness) > 0.001)
            {
                var extrusion = Math.Abs(polyline.Thickness);
                if (polyline.Layer.Contains("柜体结构", StringComparison.OrdinalIgnoreCase))
                {
                    var length = Math.Max(width, height);
                    var boardThickness = Math.Min(width, height);
                    width = length;
                    height = extrusion;
                    thickness = boardThickness;
                }
                else
                {
                    thickness = extrusion;
                }
            }
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return false;
        }

        if (width <= 0.001 && height > 0.001)
        {
            width = height;
            height = 1;
        }
        else if (height <= 0.001 && width > 0.001)
        {
            height = 1;
        }

        if (thickness <= 0.001)
        {
            thickness = entity is Circle ? 0 : BoardRuntimeSettings.Thickness;
        }

        width = Math.Round(width, 2);
        height = Math.Round(height, 2);
        thickness = Math.Round(thickness, 2);
        return width > 0 && height > 0;
    }

    private enum StatisticsMode
    {
        WholeHouse,
        Parts,
        Doors,
        FrameDoors,
        Cabinet,
        Boards
    }
}
