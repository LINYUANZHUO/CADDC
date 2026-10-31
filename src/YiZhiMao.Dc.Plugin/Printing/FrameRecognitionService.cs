using System.Collections;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Printing;

internal static class FrameRecognitionService
{
    private const double Tolerance = 0.001;

    public static IReadOnlyList<CadPrintJob> SelectFrames(Document document, PrintSettings settings)
    {
        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择打印图框："
        });
        if (selection.Status != PromptStatus.OK)
        {
            return [];
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var frames = new List<PrintFrame>();
        var lines = new List<(ObjectId Id, Point3d Start, Point3d End)>();
        var frameObjectIds = new HashSet<ObjectId>();

        foreach (var objectId in selection.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
            {
                continue;
            }

            if (settings.DetectClosedPolylines && entity is Polyline polyline && TryGetPolylineFrame(polyline, out var polylineFrame))
            {
                frames.Add(polylineFrame with { SourceKey = objectId.Handle.ToString() });
                frameObjectIds.Add(objectId);
            }
            else if (settings.DetectLooseLines && entity is Line line)
            {
                lines.Add((objectId, line.StartPoint, line.EndPoint));
            }
            else if (settings.DetectBlocks && entity is BlockReference && TryGetEntityFrame(entity, out var blockFrame))
            {
                frames.Add(blockFrame with { SourceKey = objectId.Handle.ToString() });
                frameObjectIds.Add(objectId);
            }
        }

        if (settings.DetectLooseLines)
        {
            frames.AddRange(FindLineRectangles(lines, frameObjectIds));
        }

        var prepared = PrintFrameProcessor.Prepare(frames);
        if (settings.IgnoreEmptyFrames)
        {
            prepared = prepared.Where(frame => HasContent(document.Database.CurrentSpaceId, frame, frameObjectIds, transaction)).ToList();
        }

        var layoutId = LayoutManager.Current.GetLayoutId(LayoutManager.Current.CurrentLayout);
        var layoutName = LayoutManager.Current.CurrentLayout;
        transaction.Commit();
        return prepared.Select((frame, index) => new CadPrintJob
        {
            Index = index + 1,
            LayoutId = layoutId,
            LayoutName = layoutName,
            Frame = frame,
            Paper = settings.Paper
        }).ToList();
    }

    public static CadPrintJob? GetCurrentPaperSpaceLayout(Database database, PrintSettings settings)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var currentSpace = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        var layout = (Layout)transaction.GetObject(currentSpace.LayoutId, OpenMode.ForRead);
        if (layout.ModelType)
        {
            return null;
        }

        var job = new CadPrintJob
        {
            Index = 1,
            LayoutId = layout.ObjectId,
            LayoutName = layout.LayoutName,
            Paper = settings.Paper
        };
        transaction.Commit();
        return job;
    }

    public static CadPrintJob? GetCurrentOrActivateFirstPaperSpaceLayout(
        Document document,
        PrintSettings settings)
    {
        var current = GetCurrentPaperSpaceLayout(document.Database, settings);
        if (current is not null)
        {
            return current;
        }

        var firstLayoutName = GetFirstPaperSpaceLayoutName(document.Database);
        if (string.IsNullOrWhiteSpace(firstLayoutName))
        {
            return null;
        }

        LayoutManager.Current.CurrentLayout = firstLayoutName;
        document.Editor.Regen();
        return GetCurrentPaperSpaceLayout(document.Database, settings);
    }

    private static string? GetFirstPaperSpaceLayoutName(Database database)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var layouts = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
        string? firstName = null;
        var firstTabOrder = int.MaxValue;
        foreach (DictionaryEntry entry in layouts)
        {
            if (entry.Value is not ObjectId layoutId ||
                transaction.GetObject(layoutId, OpenMode.ForRead) is not Layout layout ||
                layout.ModelType ||
                layout.TabOrder >= firstTabOrder)
            {
                continue;
            }

            firstName = layout.LayoutName;
            firstTabOrder = layout.TabOrder;
        }

        transaction.Commit();
        return firstName;
    }

    private static bool TryGetPolylineFrame(Polyline polyline, out PrintFrame frame)
    {
        frame = default;
        if (!polyline.Closed || polyline.NumberOfVertices != 4)
        {
            return false;
        }

        for (var index = 0; index < polyline.NumberOfVertices; index++)
        {
            if (polyline.GetSegmentType(index) != SegmentType.Line)
            {
                return false;
            }
        }

        var points = Enumerable.Range(0, polyline.NumberOfVertices)
            .Select(polyline.GetPoint2dAt)
            .ToList();
        for (var index = 0; index < points.Count; index++)
        {
            var start = points[index];
            var end = points[(index + 1) % points.Count];
            var horizontal = Math.Abs(start.Y - end.Y) <= Tolerance;
            var vertical = Math.Abs(start.X - end.X) <= Tolerance;
            if (horizontal == vertical)
            {
                return false;
            }
        }

        var minX = points.Min(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxX = points.Max(point => point.X);
        var maxY = points.Max(point => point.Y);
        if (maxX - minX <= Tolerance || maxY - minY <= Tolerance)
        {
            return false;
        }

        var corners = new[]
        {
            new Point2d(minX, minY),
            new Point2d(maxX, minY),
            new Point2d(maxX, maxY),
            new Point2d(minX, maxY)
        };
        if (points.Any(point => !corners.Any(corner => point.GetDistanceTo(corner) <= Tolerance)))
        {
            return false;
        }

        frame = new PrintFrame(minX, minY, maxX, maxY);
        return true;
    }

    private static bool TryGetEntityFrame(Entity entity, out PrintFrame frame)
    {
        frame = default;
        try
        {
            var extents = entity.GeometricExtents;
            frame = new PrintFrame(extents.MinPoint.X, extents.MinPoint.Y, extents.MaxPoint.X, extents.MaxPoint.Y);
            return frame.Width > Tolerance && frame.Height > Tolerance;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return false;
        }
    }

    private static IEnumerable<PrintFrame> FindLineRectangles(
        IReadOnlyList<(ObjectId Id, Point3d Start, Point3d End)> lines,
        ISet<ObjectId> frameObjectIds)
    {
        var segments = lines
            .Select(line => new AxisAlignedSegment(line.Start.X, line.Start.Y, line.End.X, line.End.Y))
            .ToList();
        foreach (var match in AxisAlignedRectangleDetector.Find(segments, Tolerance))
        {
            var ids = new[]
            {
                lines[match.BottomIndex].Id,
                lines[match.TopIndex].Id,
                lines[match.LeftIndex].Id,
                lines[match.RightIndex].Id
            };
            frameObjectIds.UnionWith(ids);
            yield return new PrintFrame(
                match.MinX,
                match.MinY,
                match.MaxX,
                match.MaxY,
                string.Join(";", ids.Select(id => id.Handle.ToString())));
        }
    }

    private static bool HasContent(ObjectId spaceId, PrintFrame frame, ISet<ObjectId> frameObjectIds, Transaction transaction)
    {
        var space = (BlockTableRecord)transaction.GetObject(spaceId, OpenMode.ForRead);
        foreach (var objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
            {
                continue;
            }

            if (frameObjectIds.Contains(objectId))
            {
                if (entity is BlockReference blockReference && BlockContainsDrawingContent(blockReference, transaction))
                {
                    return true;
                }

                continue;
            }

            try
            {
                var extents = entity.GeometricExtents;
                if (extents.MaxPoint.X > frame.MinX + Tolerance &&
                    extents.MinPoint.X < frame.MaxX - Tolerance &&
                    extents.MaxPoint.Y > frame.MinY + Tolerance &&
                    extents.MinPoint.Y < frame.MaxY - Tolerance)
                {
                    return true;
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Entities without geometric extents do not make a frame printable.
            }
        }

        return false;
    }

    private static bool BlockContainsDrawingContent(BlockReference blockReference, Transaction transaction)
    {
        if (transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead) is not BlockTableRecord definition)
        {
            return false;
        }

        var lineLikeEntities = 0;
        foreach (var objectId in definition)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
            {
                continue;
            }

            if (entity is not Line && entity is not Polyline)
            {
                return true;
            }

            lineLikeEntities++;
        }

        return lineLikeEntities > 1;
    }
}
