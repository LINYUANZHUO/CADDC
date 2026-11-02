using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class CadAxisAlignedSegmentReader
{
    private const double Tolerance = 0.000001;

    public static IReadOnlyList<AxisAlignedSegment> CollectCurrentSpace(Database database)
    {
        var segments = new List<AxisAlignedSegment>();
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        var layerZeroId = layers["0"];
        ReadBlockTableRecord(
            space,
            Array.Empty<Matrix3d>(),
            ObjectId.Null,
            layerZeroId,
            transaction,
            new HashSet<ObjectId>(),
            segments);
        return segments;
    }

    private static void ReadBlockTableRecord(
        BlockTableRecord blockTableRecord,
        IReadOnlyList<Matrix3d> transformsToWorld,
        ObjectId inheritedLayerId,
        ObjectId layerZeroId,
        Transaction transaction,
        ISet<ObjectId> activeBlockRecords,
        ICollection<AxisAlignedSegment> segments)
    {
        if (!activeBlockRecords.Add(blockTableRecord.ObjectId))
        {
            return;
        }

        try
        {
            foreach (var objectId in blockTableRecord)
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity ||
                    !TryGetVisibleLayer(entity, inheritedLayerId, layerZeroId, transaction, out var effectiveLayerId))
                {
                    continue;
                }

                if (entity is BlockReference reference)
                {
                    var definitionId = reference.BlockTableRecord;
                    if (definitionId.IsNull || definitionId.IsErased || !definitionId.IsValid ||
                        transaction.GetObject(definitionId, OpenMode.ForRead, false) is not BlockTableRecord definition)
                    {
                        continue;
                    }

                    ReadBlockTableRecord(
                        definition,
                        PrependTransform(reference.BlockTransform, transformsToWorld),
                        effectiveLayerId,
                        layerZeroId,
                        transaction,
                        activeBlockRecords,
                        segments);
                    continue;
                }

                foreach (var segment in Read(entity, transformsToWorld))
                {
                    segments.Add(segment);
                }
            }
        }
        finally
        {
            activeBlockRecords.Remove(blockTableRecord.ObjectId);
        }
    }

    private static bool TryGetVisibleLayer(
        Entity entity,
        ObjectId inheritedLayerId,
        ObjectId layerZeroId,
        Transaction transaction,
        out ObjectId effectiveLayerId)
    {
        effectiveLayerId = entity.LayerId == layerZeroId && !inheritedLayerId.IsNull
            ? inheritedLayerId
            : entity.LayerId;
        if (!entity.Visible || effectiveLayerId.IsNull || effectiveLayerId.IsErased || !effectiveLayerId.IsValid ||
            transaction.GetObject(effectiveLayerId, OpenMode.ForRead, false) is not LayerTableRecord layer)
        {
            return false;
        }

        return !layer.IsOff && !layer.IsFrozen;
    }

    private static IReadOnlyList<Matrix3d> PrependTransform(
        Matrix3d transform,
        IReadOnlyList<Matrix3d> transformsToWorld)
    {
        var result = new Matrix3d[transformsToWorld.Count + 1];
        result[0] = transform;
        for (var index = 0; index < transformsToWorld.Count; index++)
        {
            result[index + 1] = transformsToWorld[index];
        }

        return result;
    }

    private static IEnumerable<AxisAlignedSegment> Read(
        Entity entity,
        IReadOnlyList<Matrix3d> transformsToWorld)
    {
        if (entity is Line line)
        {
            var start = TransformPoint(line.StartPoint, transformsToWorld);
            var end = TransformPoint(line.EndPoint, transformsToWorld);
            if (IsAxisAligned(start.X, start.Y, end.X, end.Y))
            {
                yield return new AxisAlignedSegment(
                    start.X,
                    start.Y,
                    end.X,
                    end.Y);
            }

            yield break;
        }

        if (entity is not Polyline polyline || polyline.NumberOfVertices < 2)
        {
            yield break;
        }

        var segmentCount = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance)
            {
                continue;
            }

            var next = (index + 1) % polyline.NumberOfVertices;
            var start = TransformPoint(polyline.GetPoint3dAt(index), transformsToWorld);
            var end = TransformPoint(polyline.GetPoint3dAt(next), transformsToWorld);
            if (IsAxisAligned(start.X, start.Y, end.X, end.Y))
            {
                yield return new AxisAlignedSegment(start.X, start.Y, end.X, end.Y);
            }
        }
    }

    private static Point3d TransformPoint(Point3d point, IReadOnlyList<Matrix3d> transformsToWorld)
    {
        foreach (var transform in transformsToWorld)
        {
            point = point.TransformBy(transform);
        }

        return point;
    }

    private static bool IsAxisAligned(double startX, double startY, double endX, double endY) =>
        (Math.Abs(startX - endX) <= Tolerance && Math.Abs(startY - endY) > Tolerance) ||
        (Math.Abs(startY - endY) <= Tolerance && Math.Abs(startX - endX) > Tolerance);
}
