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
    double ActualGap,
    double FaceWidth,
    double FaceHeight);

public static class DrawerFaceLayoutCalculator
{
    private const double Tolerance = 0.000001;

    public static DrawerFaceLayout Calculate(
        DrawerFacePoint first,
        DrawerFacePoint second,
        DrawerFaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var left = Math.Min(first.X, second.X) - options.LeftExtension;
        var right = Math.Max(first.X, second.X) + options.RightExtension;
        var bottom = Math.Min(first.Y, second.Y) - options.BottomExtension;
        var top = Math.Max(first.Y, second.Y) + options.TopExtension;
        var width = right - left;
        var height = top - bottom;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= Tolerance || height <= Tolerance)
        {
            throw new ArgumentOutOfRangeException(nameof(second), "柜体内空必须形成有效矩形。");
        }

        var totalLength = options.Orientation == DrawerFaceOrientation.Vertical ? height : width;
        var division = DoorPanelDivisionCalculator.CalculateWithGapCompensation(
            totalLength,
            options.Count,
            options.Gap,
            options.CalculationMode);
        var faces = new List<DrawerFaceRectangle>(options.Count);
        var centers = new List<DrawerFacePoint>(options.Count);
        var distance = 0d;
        foreach (var panelLength in division.PanelWidths)
        {
            var face = options.Orientation == DrawerFaceOrientation.Vertical
                ? new DrawerFaceRectangle(left, bottom + distance, width, panelLength)
                : new DrawerFaceRectangle(left + distance, bottom, panelLength, height);
            faces.Add(face);
            centers.Add(new DrawerFacePoint(face.X + face.Width / 2, face.Y + face.Height / 2));
            distance += panelLength + division.ActualGap;
        }

        var firstFace = faces[0];
        return new DrawerFaceLayout(
            faces,
            centers,
            division.ActualGap,
            firstFace.Width,
            firstFace.Height);
    }

    public static IReadOnlyList<DrawerFaceLine> CalculateOpeningArrow(DrawerFaceRectangle face)
    {
        if (!double.IsFinite(face.Width) || !double.IsFinite(face.Height) ||
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
