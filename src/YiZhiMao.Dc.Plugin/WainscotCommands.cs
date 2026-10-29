using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

public static class WainscotCommands
{
    private const string WainscotLayerName = "DC_墙板";

    [CommandMethod("DC_CMD_WAINSCOT_GRILLE_DRAW", CommandFlags.Modal)]
    [ShortcutCommand("墙板", "生成格栅板", "DCGS")]
    public static void DrawGrille()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var result = document.Editor.GetPoint("\n请选择格栅正视图左下角插入点: ");
        if (result.Status != PromptStatus.OK)
        {
            document.Editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var options = WainscotRuntimeSettings.Grille;
        TryWriteGrille(document.Database, document.Editor, result.Value, options.Width, options.Height, options);
    }

    [CommandMethod("DC_CMD_WAINSCOT_GRILLE_FILL", CommandFlags.Modal)]
    [ShortcutCommand("墙板", "内空填充格栅", "DCGST")]
    public static void FillGrille()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var point = editor.GetPoint("\n请点击格栅内空区域的一个点: ");
        if (point.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var boundaries = editor.TraceBoundary(point.Value, detectIslands: true);
        try
        {
            var boundary = boundaries.Cast<DBObject>().OfType<Entity>().FirstOrDefault();
            if (boundary is null)
            {
                editor.WriteMessage("\n[错误] 未找到闭合区域，请确保点击点位于闭合内空中。");
                return;
            }

            var extents = boundary.GeometricExtents;
            var width = extents.MaxPoint.X - extents.MinPoint.X;
            var height = extents.MaxPoint.Y - extents.MinPoint.Y;
            TryWriteGrille(
                document.Database,
                editor,
                extents.MinPoint,
                width,
                height,
                WainscotRuntimeSettings.Grille);
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 内空识别失败：{exception.Message}");
        }
        finally
        {
            foreach (DBObject boundary in boundaries)
            {
                boundary.Dispose();
            }
        }
    }

    [CommandMethod("DC_CMD_WAINSCOT_PERFORATED_DRAW", CommandFlags.Modal)]
    [ShortcutCommand("墙板", "生成洞洞板", "DCDB")]
    public static void DrawPerforatedPanel()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var result = document.Editor.GetPoint("\n请选择洞洞板正视图左下角插入点: ");
        if (result.Status != PromptStatus.OK)
        {
            document.Editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var options = WainscotRuntimeSettings.PerforatedPanel;
        TryWritePerforatedPanel(
            document.Database,
            document.Editor,
            result.Value,
            options.Width,
            options.Height,
            options);
    }

    [CommandMethod("DC_CMD_WAINSCOT_PERFORATED_FILL", CommandFlags.Modal)]
    [ShortcutCommand("墙板", "内空填充洞洞板", "DCDBT")]
    public static void FillPerforatedPanel()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var point = editor.GetPoint("\n请点击洞洞板内空区域的一个点: ");
        if (point.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var boundaries = editor.TraceBoundary(point.Value, detectIslands: true);
        try
        {
            var boundary = boundaries.Cast<DBObject>().OfType<Entity>().FirstOrDefault();
            if (boundary is null)
            {
                editor.WriteMessage("\n[错误] 未找到闭合区域，请确保点击点位于闭合内空中。");
                return;
            }

            var extents = boundary.GeometricExtents;
            TryWritePerforatedPanel(
                document.Database,
                editor,
                extents.MinPoint,
                extents.MaxPoint.X - extents.MinPoint.X,
                extents.MaxPoint.Y - extents.MinPoint.Y,
                WainscotRuntimeSettings.PerforatedPanel);
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 内空识别失败：{exception.Message}");
        }
        finally
        {
            foreach (DBObject boundary in boundaries)
            {
                boundary.Dispose();
            }
        }
    }

    [CommandMethod("DC_CMD_WAINSCOT_BACKGROUND_DRAW", CommandFlags.Modal)]
    [ShortcutCommand("墙板", "生成背景墙", "DCBJQ")]
    public static void DrawBackgroundWall()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var result = document.Editor.GetPoint("\n请选择背景墙正视图左下角插入点: ");
        if (result.Status != PromptStatus.OK)
        {
            document.Editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        AppendBackgroundWall(document.Database, document.Editor, result.Value, WainscotRuntimeSettings.BackgroundWall);
    }

    private static void TryWriteGrille(
        Database database,
        Editor editor,
        Point3d basePoint,
        double width,
        double height,
        GrilleOptions options)
    {
        try
        {
            var layout = GrilleLayoutCalculator.Calculate(
                basePoint.X,
                basePoint.Y,
                width,
                height,
                options);
            using var transaction = database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            EnsureLayer(database, transaction);

            AppendRectangle(space, transaction, layout.Frame);
            foreach (var strip in layout.FrontStrips)
            {
                var stripId = AppendRectangle(space, transaction, strip);
                AppendHatch(space, transaction, stripId, options.HatchPattern);
            }

            foreach (var rectangle in layout.PlanRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            foreach (var rectangle in layout.SideRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            transaction.Commit();
            editor.WriteMessage(
                $"\n[一只猫dc] 格栅板已生成：{width:0.###} × {height:0.###} mm，共 {layout.FrontStrips.Count} 条。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 生成格栅板失败：{exception.Message}");
        }
    }

    private static void TryWritePerforatedPanel(
        Database database,
        Editor editor,
        Point3d basePoint,
        double width,
        double height,
        PerforatedPanelOptions options)
    {
        try
        {
            var layout = PerforatedPanelLayoutCalculator.Calculate(
                basePoint.X,
                basePoint.Y,
                width,
                height,
                options);
            using var transaction = database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            EnsureLayer(database, transaction);

            AppendRectangle(space, transaction, layout.Frame);
            foreach (var hole in layout.Holes)
            {
                AppendCircle(space, transaction, hole);
            }

            foreach (var rectangle in layout.PlanRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            foreach (var rectangle in layout.SideRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            var dimensionStyle = (DimStyleTableRecord)transaction.GetObject(
                database.Dimstyle,
                OpenMode.ForRead);
            var dimensionTextHeight = dimensionStyle.Dimtxt * 0.5;
            var dimensionArrowSize = dimensionStyle.Dimasz / 6d;
            var dimensionExtensionBeyondLine = dimensionStyle.Dimexe / 5d;
            var dimensionOriginOffset = Math.Max(dimensionStyle.Dimexo, dimensionArrowSize);
            foreach (var dimension in layout.LinearDimensions)
            {
                AppendEntity(
                    space,
                    transaction,
                    new AlignedDimension(
                        new Point3d(dimension.StartX, dimension.StartY, 0),
                        new Point3d(dimension.EndX, dimension.EndY, 0),
                        new Point3d(dimension.LineX, dimension.LineY, 0),
                        string.Empty,
                        database.Dimstyle)
                    {
                        Layer = WainscotLayerName,
                        Dimtxt = dimensionTextHeight,
                        Dimasz = dimensionArrowSize,
                        Dimexe = dimensionExtensionBeyondLine,
                        Dimexo = dimensionOriginOffset
                    });
            }

            var diameter = layout.DiameterDimension;
            var diameterDirection = Math.Sqrt(0.5);
            AppendEntity(
                space,
                transaction,
                new DiametricDimension(
                    new Point3d(
                        diameter.CenterX + diameter.Radius * diameterDirection,
                        diameter.CenterY + diameter.Radius * diameterDirection,
                        0),
                    new Point3d(
                        diameter.CenterX - diameter.Radius * diameterDirection,
                        diameter.CenterY - diameter.Radius * diameterDirection,
                        0),
                    Math.Max(diameter.Radius * 2, dimensionTextHeight * 2),
                    string.Empty,
                    database.Dimstyle)
                {
                    Layer = WainscotLayerName,
                    Dimtxt = dimensionTextHeight,
                    Dimasz = dimensionArrowSize,
                    Dimexe = dimensionExtensionBeyondLine,
                    Dimexo = dimensionOriginOffset
                });

            transaction.Commit();
            editor.WriteMessage(
                $"\n[一只猫dc] 洞洞板已生成：{width:0.###} × {height:0.###} mm，共 {layout.Holes.Count} 个孔。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 生成洞洞板失败：{exception.Message}");
        }
    }

    private static void AppendBackgroundWall(
        Database database,
        Editor editor,
        Point3d basePoint,
        BackgroundWallOptions options)
    {
        try
        {
            var layout = BackgroundWallLayoutCalculator.Calculate(basePoint.X, basePoint.Y, options);
            using var transaction = database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            EnsureLayer(database, transaction);

            foreach (var panel in layout.FrontPanels)
            {
                AppendRectangle(space, transaction, new BackgroundWallRectangle(
                    panel.X,
                    panel.Y,
                    panel.Width,
                    panel.Height));
            }

            foreach (var rectangle in layout.PlanRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            foreach (var slot in layout.PlanSlots)
            {
                AppendRectangle(space, transaction, slot);
            }

            foreach (var rectangle in layout.SideRectangles)
            {
                AppendRectangle(space, transaction, rectangle);
            }

            transaction.Commit();
            editor.WriteMessage(
                $"\n[一只猫dc] 背景墙已生成：{layout.Frame.Width:0.###} × {layout.Frame.Height:0.###} mm，共 {layout.FrontPanels.Count} 块板。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 生成背景墙失败：{exception.Message}");
        }
    }

    private static ObjectId AppendRectangle(
        BlockTableRecord space,
        Transaction transaction,
        GrilleRectangle rectangle)
    {
        var polyline = new Polyline(4) { Closed = true, Layer = WainscotLayerName };
        polyline.AddVertexAt(0, new Point2d(rectangle.X, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(rectangle.X + rectangle.Width, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(rectangle.X, rectangle.Y + rectangle.Height), 0, 0, 0);
        var objectId = space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        return objectId;
    }

    private static ObjectId AppendRectangle(
        BlockTableRecord space,
        Transaction transaction,
        PerforatedPanelRectangle rectangle)
    {
        var polyline = new Polyline(4) { Closed = true, Layer = WainscotLayerName };
        polyline.AddVertexAt(0, new Point2d(rectangle.X, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(rectangle.X + rectangle.Width, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(rectangle.X, rectangle.Y + rectangle.Height), 0, 0, 0);
        var objectId = space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        return objectId;
    }

    private static ObjectId AppendRectangle(
        BlockTableRecord space,
        Transaction transaction,
        BackgroundWallRectangle rectangle)
    {
        var polyline = new Polyline(4) { Closed = true, Layer = WainscotLayerName };
        polyline.AddVertexAt(0, new Point2d(rectangle.X, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(rectangle.X + rectangle.Width, rectangle.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(rectangle.X, rectangle.Y + rectangle.Height), 0, 0, 0);
        var objectId = space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        return objectId;
    }

    private static void AppendCircle(
        BlockTableRecord space,
        Transaction transaction,
        PerforatedHole hole)
    {
        var circle = new Circle(
            new Point3d(hole.X, hole.Y, 0),
            Vector3d.ZAxis,
            hole.Radius)
        {
            Layer = WainscotLayerName
        };
        space.AppendEntity(circle);
        transaction.AddNewlyCreatedDBObject(circle, true);
    }

    private static void AppendEntity(
        BlockTableRecord space,
        Transaction transaction,
        Entity entity)
    {
        space.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
    }

    private static void AppendHatch(
        BlockTableRecord space,
        Transaction transaction,
        ObjectId boundaryId,
        string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) ||
            string.Equals(pattern, "无填充", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hatch = new Hatch
        {
            Layer = WainscotLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 8),
            PatternScale = string.Equals(pattern, "SOLID", StringComparison.OrdinalIgnoreCase)
                ? 1
                : DrawingLayerRuntimeSettings.WallHatchScale
        };
        space.AppendEntity(hatch);
        transaction.AddNewlyCreatedDBObject(hatch, true);
        hatch.SetHatchPattern(HatchPatternType.PreDefined, pattern);
        hatch.Associative = true;
        hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection([boundaryId]));
        hatch.EvaluateHatch(true);
    }

    private static void EnsureLayer(Database database, Transaction transaction)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layers.Has(WainscotLayerName))
        {
            return;
        }

        layers.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = WainscotLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
        };
        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }
}
