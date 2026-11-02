namespace YiZhiMao.Dc.Core;

public readonly record struct DrawerFacePoint(double X, double Y);

public readonly record struct DrawerFaceLine(DrawerFacePoint Start, DrawerFacePoint End);

public readonly record struct DrawerFaceRectangle(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Top => Y + Height;
}

public sealed record DrawerFaceLayout(
    IReadOnlyList<DrawerFaceRectangle> Faces,
    IReadOnlyList<DrawerFacePoint> HandleCenters,
    double ActualHorizontalGap,
    double ActualVerticalGap,
    double FaceWidth,
    double FaceHeight,
    int RowCount,
    int ColumnCount)
{
    public double ActualGap => RowCount > 1 ? ActualVerticalGap : ActualHorizontalGap;
}

public static class DrawerFaceLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static DrawerFaceLayout Calculate(
        DrawerFacePoint first,
        DrawerFacePoint second,
        DrawerFaceOptions options)
    {
        Compatibility.NotNull(options, nameof(options));
        var left = Math.Min(first.X, second.X) - options.LeftExtension;
        var right = Math.Max(first.X, second.X) + options.RightExtension;
        var bottom = Math.Min(first.Y, second.Y) - options.BottomExtension;
        var top = Math.Max(first.Y, second.Y) + options.TopExtension;
        var width = right - left;
        var height = top - bottom;
        if (!Compatibility.IsFinite(width) || !Compatibility.IsFinite(height) || width <= Tolerance || height <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(second), "柜体内空必须形成有效矩形。");
        }

        if (options.RowCount <= 0 || options.ColumnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "抽面行数和列数必须大于 0。");
        }

        var horizontal = DoorPanelDivisionCalculator.Calculate(
            width,
            options.ColumnCount,
            options.Gap,
            DoorPanelCalculationMode.GapFixedDoorDistributed);
        var vertical = DoorPanelDivisionCalculator.Calculate(
            height,
            options.RowCount,
            options.Handleless ? options.HandlelessGap : options.Gap,
            DoorPanelCalculationMode.GapFixedDoorDistributed);
        var faces = new List<DrawerFaceRectangle>(options.RowCount * options.ColumnCount);
        var centers = new List<DrawerFacePoint>(faces.Capacity);
        var y = bottom;
        foreach (var faceHeight in vertical.PanelWidths)
        {
            var x = left;
            foreach (var faceWidth in horizontal.PanelWidths)
            {
                var face = new DrawerFaceRectangle(x, y, faceWidth, faceHeight);
                faces.Add(face);
                centers.Add(new DrawerFacePoint(face.X + face.Width / 2, face.Y + face.Height / 2));
                x += faceWidth + horizontal.ActualGap;
            }

            y += faceHeight + vertical.ActualGap;
        }

        var firstFace = faces[0];
        return new DrawerFaceLayout(
            faces,
            centers,
            horizontal.ActualGap,
            vertical.ActualGap,
            firstFace.Width,
            firstFace.Height,
            options.RowCount,
            options.ColumnCount);
    }

    public static IReadOnlyList<DrawerFaceLine> CalculateOpeningArrow(DrawerFaceRectangle face)
    {
        if (!Compatibility.IsFinite(face.Width) || !Compatibility.IsFinite(face.Height) ||
            face.Width <= Tolerance || face.Height <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(face), "抽面必须形成有效矩形。");
        }

        var tip = new DrawerFacePoint(face.X + face.Width / 2, face.Y);
        return
        [
            new DrawerFaceLine(new DrawerFacePoint(face.X, face.Top), tip),
            new DrawerFaceLine(new DrawerFacePoint(face.Right, face.Top), tip)
        ];
    }
}
