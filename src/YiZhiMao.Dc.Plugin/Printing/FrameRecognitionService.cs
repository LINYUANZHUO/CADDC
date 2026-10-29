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

        return TryGetEntityFrame(polyline, out frame);
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
                var centerX = (extents.MinPoint.X + extents.MaxPoint.X) / 2;
                var centerY = (extents.MinPoint.Y + extents.MaxPoint.Y) / 2;
                if (centerX > frame.MinX + Tolerance && centerX < frame.MaxX - Tolerance &&
                    centerY > frame.MinY + Tolerance && centerY < frame.MaxY - Tolerance)
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

        foreach (var objectId in definition)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is Entity entity &&
                entity is not Line &&
                entity is not Polyline)
            {
                return true;
            }
        }

        return false;
    }
}
