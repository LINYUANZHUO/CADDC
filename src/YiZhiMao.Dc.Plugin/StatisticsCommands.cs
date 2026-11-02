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
    public static void ExportParts()
    {
        if (!TryQueueExternal(LeishenBomBridge.PartsCommand))
        {
            Export(StatisticsMode.Parts, "部件统计");
        }
    }

    [CommandMethod("DC_CMD_DOOR_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "普通门板统计", "DCTJM")]
    public static void ExportDoors()
    {
        if (!TryQueueExternal(LeishenBomBridge.DoorCommand))
        {
            Export(StatisticsMode.Doors, "普通门板统计");
        }
    }

    [CommandMethod("DC_CMD_FRAME_DOOR_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "边框门板统计", "DCBKM")]
    public static void ExportFrameDoors()
    {
        if (!TryQueueExternal(LeishenBomBridge.FrameDoorCommand))
        {
            Export(StatisticsMode.FrameDoors, "边框门板统计");
        }
    }

    [CommandMethod("DC_CMD_CABINET_BOM", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "柜体拆单", "DCTJG")]
    public static void ExportCabinetBom()
    {
        if (!TryQueueExternal(LeishenBomBridge.CabinetBomCommand))
        {
            Export(StatisticsMode.Cabinet, "柜体拆单");
        }
    }

    [CommandMethod("DC_CMD_BOARD_STATISTICS", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "板件统计", "DCBJT")]
    public static void ExportBoards()
    {
        if (!TryQueueExternal(LeishenBomBridge.BoardNestingCommand))
        {
            Export(StatisticsMode.Boards, "板件统计");
        }
    }

    [CommandMethod("DC_CMD_BOM_VISUAL_COPY", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "外观图超级复制", "DCCOO")]
    public static void CopyBomAppearance()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        if (!LeishenBomBridge.TryQueueCommand(document, LeishenBomBridge.VisualCopyCommand, out var error))
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 外观图超级复制不可用：{error}");
        }
    }

    [CommandMethod("DC_CMD_WHOLE_HOUSE_BOM", CommandFlags.Modal)]
    [ShortcutCommand("拆单统计", "一键全屋拆单", "DCQW")]
    public static void ExportWholeHouse()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        if (TryQueueExternal(LeishenBomBridge.WholeHouseCommand))
        {
            return;
        }

        var selectionOptions = new PromptSelectionOptions
        {
            MessageForAdding = "\n请框选参与一键全屋拆单的对象，回车确认: "
        };
        var selection = document.Editor.GetSelection(selectionOptions);
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var wholeHouseOptions = ToolboxSettingsRuntime.Settings.WholeHouseStatistics;
        var rows = DrawingStatistics.Aggregate(Collect(
            document.Database,
            selection.Value.GetObjectIds(),
            StatisticsMode.WholeHouse,
            wholeHouseOptions));
        if (rows.Count == 0)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 选择范围内没有符合当前项目配置的拆单对象。");
            return;
        }

        var outputOptions = new PromptKeywordOptions("\n请选择【一键全屋拆单】输出方式")
        {
            AllowNone = false,
            AppendKeywordsToMessage = true
        };
        outputOptions.Keywords.Add("Table", "T", "放置CAD表格(T)");
        outputOptions.Keywords.Add("Excel", "E", "导出Excel总表(E)");
        var output = document.Editor.GetKeywords(outputOptions);
        if (output.Status != PromptStatus.OK)
        {
            return;
        }

        if (string.Equals(output.StringResult, "Table", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(output.StringResult, "T", StringComparison.OrdinalIgnoreCase))
        {
            PlaceCadTable(document, rows, wholeHouseOptions);
            return;
        }

        ExportWholeHouseExcel(document, rows);
    }

    private static bool TryQueueExternal(string command)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return false;
        }

        if (LeishenBomBridge.TryQueueCommand(document, command, out var error))
        {
            return true;
        }

        document.Editor.WriteMessage($"\n[一只猫dc] 成熟拆单模块不可用，已切换 CADDC 基础统计：{error}");
        return false;
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

    private static IReadOnlyList<DrawingPart> Collect(
        Database database,
        IEnumerable<ObjectId> objectIds,
        StatisticsMode mode,
        WholeHouseStatisticsOptions? wholeHouseOptions = null)
    {
        var result = new List<DrawingPart>();
        using var transaction = database.TransactionManager.StartTransaction();
        var expandedObjectIds = ExpandWithHiddenBackboards(database, transaction, objectIds);
        foreach (var objectId in expandedObjectIds)
        {
            if (objectId.IsNull || transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            var layer = entity.Layer ?? string.Empty;
            var name = GetName(entity, transaction);
            var boardProperty = GetBoardProperty(entity);
            var category = Classify(layer, name, mode);
            category = boardProperty?.Category ?? category;
            if (category is null)
            {
                continue;
            }

            double width;
            double height;
            double thickness;
            if (boardProperty?.HasExplicitSize == true)
            {
                width = boardProperty.Width!.Value;
                height = boardProperty.Height!.Value;
                thickness = boardProperty.Thickness!.Value;
            }
            else if (!TryGetSize(entity, out width, out height, out thickness))
            {
                continue;
            }

            if (mode == StatisticsMode.WholeHouse &&
                wholeHouseOptions is not null &&
                !ShouldIncludeWholeHouse(category, boardProperty, wholeHouseOptions))
            {
                continue;
            }

            result.Add(new DrawingPart(
                category,
                boardProperty?.PartName ?? ResolvePartName(layer, name, category),
                width,
                height,
                thickness));
        }

        transaction.Commit();
        return result;
    }

    private static IReadOnlyList<ObjectId> ExpandWithHiddenBackboards(
        Database database,
        Transaction transaction,
        IEnumerable<ObjectId> objectIds)
    {
        var selected = objectIds.Where(id => !id.IsNull).Distinct().ToList();
        if (!TryGetCombinedExtents(transaction, selected, out var selectedExtents))
        {
            return selected;
        }

        var selectedSet = new HashSet<ObjectId>(selected);
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId objectId in space)
        {
            if (selectedSet.Contains(objectId) ||
                transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity ||
                !string.Equals(entity.Layer, "0-正视图背板", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var layer = (LayerTableRecord)transaction.GetObject(entity.LayerId, OpenMode.ForRead);
            if (!layer.IsOff || GetBoardProperty(entity)?.Category != "背板")
            {
                continue;
            }

            try
            {
                if (IntersectsInPlan(selectedExtents, entity.GeometricExtents))
                {
                    selected.Add(objectId);
                    selectedSet.Add(objectId);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Ignore entities without usable extents, matching the existing statistics behavior.
            }
        }

        return selected;
    }

    private static bool TryGetCombinedExtents(
        Transaction transaction,
        IEnumerable<ObjectId> objectIds,
        out Extents3d combined)
    {
        combined = default;
        var hasExtents = false;
        foreach (var objectId in objectIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            try
            {
                if (!hasExtents)
                {
                    combined = entity.GeometricExtents;
                    hasExtents = true;
                }
                else
                {
                    combined.AddExtents(entity.GeometricExtents);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // A selection may include non-graphical objects; they do not define a frame range.
            }
        }

        return hasExtents;
    }

    private static bool IntersectsInPlan(Extents3d first, Extents3d second) =>
        first.MaxPoint.X >= second.MinPoint.X &&
        second.MaxPoint.X >= first.MinPoint.X &&
        first.MaxPoint.Y >= second.MinPoint.Y &&
        second.MaxPoint.Y >= first.MinPoint.Y;

    private static void PlaceCadTable(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        IReadOnlyList<DrawingStatisticsRow> rows,
        WholeHouseStatisticsOptions options)
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
        var headers = new[] { "类别", "名称", "宽度(mm)", "高度(mm)", "厚度(mm)", "数量", "面积(m²)" };
        var rowHeight = options.TableScaleFactor;
        var columnWidth = rowHeight * 3.75;
        var pageCount = (rows.Count + options.MaxRowsPerPage - 1) / options.MaxRowsPerPage;
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var pageRows = rows
                .Skip(pageIndex * options.MaxRowsPerPage)
                .Take(options.MaxRowsPerPage)
                .ToArray();
            var table = new Table
            {
                TableStyle = document.Database.Tablestyle,
                Position = point.Value + new Autodesk.AutoCAD.Geometry.Vector3d(
                    pageIndex * (columnWidth * headers.Length + rowHeight * 2),
                    0,
                    0)
            };
            table.SetSize(pageRows.Length + 2, headers.Length);
            table.SetRowHeight(rowHeight);
            table.SetColumnWidth(columnWidth);
            table.Cells[0, 0].TextString = pageCount == 1
                ? "一只猫dc 一键全屋拆单"
                : $"一只猫dc 一键全屋拆单 ({pageIndex + 1}/{pageCount})";
            table.MergeCells(CellRange.Create(table, 0, 0, 0, headers.Length - 1));
            for (var column = 0; column < headers.Length; column++)
            {
                table.Cells[1, column].TextString = headers[column];
            }

            for (var index = 0; index < pageRows.Length; index++)
            {
                var row = pageRows[index];
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
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已放置 {pageCount} 张、共 {rows.Count} 行全屋拆单CAD表格。");
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
        if (Compatibility.Contains(layer, "背板-俯视辅助图", StringComparison.OrdinalIgnoreCase) ||
            Compatibility.Contains(layer, "柜体辅助图", StringComparison.OrdinalIgnoreCase) ||
            Compatibility.Contains(layer, "柜体虚线", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var combined = layer + " " + name;
        var isDoor = Compatibility.Contains(combined, "门板", StringComparison.OrdinalIgnoreCase) ||
                     Compatibility.Contains(combined, "抽面", StringComparison.OrdinalIgnoreCase);
        var isFrameDoor = isDoor && (Compatibility.Contains(combined, "边框", StringComparison.OrdinalIgnoreCase) ||
                                     Compatibility.Contains(combined, "玻璃", StringComparison.OrdinalIgnoreCase));
        var isBoard = Compatibility.Contains(combined, "柜体", StringComparison.OrdinalIgnoreCase) ||
                      Compatibility.Contains(combined, "层板", StringComparison.OrdinalIgnoreCase) ||
                      Compatibility.Contains(combined, "背板", StringComparison.OrdinalIgnoreCase) ||
                      Compatibility.Contains(combined, "板件", StringComparison.OrdinalIgnoreCase);
        var isHangingRod = Compatibility.Contains(combined, "挂衣杆", StringComparison.OrdinalIgnoreCase);
        var isLightStrip = Compatibility.Contains(combined, "灯带", StringComparison.OrdinalIgnoreCase);
        var isConcealedHandle = Compatibility.Contains(combined, "暗拉手", StringComparison.OrdinalIgnoreCase);
        var isHandle = !isConcealedHandle && Compatibility.Contains(combined, "拉手", StringComparison.OrdinalIgnoreCase);
        var isHinge = Compatibility.Contains(combined, "铰链", StringComparison.OrdinalIgnoreCase);
        var isSlide = Compatibility.Contains(combined, "滑道", StringComparison.OrdinalIgnoreCase) ||
                      Compatibility.Contains(combined, "滑轨", StringComparison.OrdinalIgnoreCase);
        var isPart = isBoard || isDoor || isHangingRod || isLightStrip ||
                     isConcealedHandle || isHandle || isHinge || isSlide;

        return mode switch
        {
            StatisticsMode.WholeHouse when isFrameDoor => "边框门板",
            StatisticsMode.WholeHouse when isDoor => "普通门板",
            StatisticsMode.WholeHouse when isBoard => "柜体板件",
            StatisticsMode.WholeHouse when isHangingRod => "挂衣杆",
            StatisticsMode.WholeHouse when isLightStrip => "灯带",
            StatisticsMode.WholeHouse when isConcealedHandle => "暗拉手",
            StatisticsMode.WholeHouse when isHandle => "拉手",
            StatisticsMode.WholeHouse when isHinge => "铰链",
            StatisticsMode.WholeHouse when isSlide => "滑道",
            StatisticsMode.WholeHouse when isPart => "五金部件",
            StatisticsMode.Parts when isPart => "部件",
            StatisticsMode.Doors when isDoor && !isFrameDoor => "普通门板",
            StatisticsMode.FrameDoors when isFrameDoor => "边框门板",
            StatisticsMode.Cabinet when isBoard => "柜体板件",
            StatisticsMode.Boards when isBoard => "板件",
            _ => null
        };
    }

    private static bool ShouldIncludeWholeHouse(
        string category,
        BoardPropertyInfo? boardProperty,
        WholeHouseStatisticsOptions options)
    {
        if (boardProperty is not null)
        {
            return options.IncludeBoardProperties;
        }

        return category switch
        {
            "柜体板件" => options.IncludeCabinetBoards,
            "普通门板" => options.IncludeStandardDoors,
            "边框门板" => options.IncludeFrameDoors,
            "挂衣杆" => options.IncludeHangingRods,
            "灯带" => options.IncludeLightStrips,
            "拉手" => options.IncludeHandles,
            "暗拉手" => options.IncludeConcealedHandles,
            "铰链" => options.IncludeHinges,
            "滑道" => options.IncludeSlides,
            "五金部件" => options.IncludeHangingRods || options.IncludeLightStrips ||
                         options.IncludeHandles || options.IncludeConcealedHandles ||
                         options.IncludeHinges || options.IncludeSlides,
            _ => false
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

    private static BoardPropertyInfo? GetBoardProperty(Entity entity)
    {
        using var xdata = entity.GetXDataForApplication(BoardCommands.BoardPropertyAppName);
        if (xdata is null)
        {
            return null;
        }

        var values = xdata.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => value.Value as string)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
        if (values.Length == 0 ||
            values[0] is not ("普通板件" or "侧收口" or "顶收口" or "见光板" or "夹心层板" or "背板"))
        {
            return null;
        }

        var partName = values.Skip(1).FirstOrDefault(value => !value.StartsWith("SIZE|", StringComparison.Ordinal));
        var sizeValue = values.Skip(1).FirstOrDefault(value => value.StartsWith("SIZE|", StringComparison.Ordinal));
        double? width = null;
        double? height = null;
        double? thickness = null;
        if (sizeValue is not null)
        {
            var segments = sizeValue.Split('|');
            if (segments.Length == 4 &&
                double.TryParse(segments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedWidth) &&
                double.TryParse(segments[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHeight) &&
                double.TryParse(segments[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedThickness) &&
                parsedWidth > 0 && parsedHeight > 0 && parsedThickness > 0)
            {
                width = parsedWidth;
                height = parsedHeight;
                thickness = parsedThickness;
            }
        }

        return new BoardPropertyInfo(values[0], partName, width, height, thickness);
    }

    private sealed record BoardPropertyInfo(
        string Category,
        string? PartName,
        double? Width,
        double? Height,
        double? Thickness)
    {
        public bool HasExplicitSize => Width.HasValue && Height.HasValue && Thickness.HasValue;
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
                if (Compatibility.Contains(polyline.Layer, "柜体结构", StringComparison.OrdinalIgnoreCase))
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
