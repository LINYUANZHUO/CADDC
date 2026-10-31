using System.Windows;
using System.Windows.Media;

namespace YiZhiMao.Dc.Plugin.Views;

public sealed class PointStyleIcon : FrameworkElement
{
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(int),
        typeof(PointStyleIcon),
        new FrameworkPropertyMetadata(35, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Mode
    {
        get => (int)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radius = Math.Max(3, Math.Min(ActualWidth, ActualHeight) * 0.32);
        var pen = new Pen(Brushes.Black, 1.1);
        var frame = Mode & 96;

        if ((frame & 32) != 0)
        {
            drawingContext.DrawEllipse(null, pen, center, radius, radius);
        }

        if ((frame & 64) != 0)
        {
            drawingContext.DrawRectangle(
                null,
                pen,
                new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
        }

        var markerRadius = radius * 0.88;
        switch (Mode & 31)
        {
            case 0:
                drawingContext.DrawEllipse(Brushes.Black, null, center, 1.5, 1.5);
                break;
            case 2:
                drawingContext.DrawLine(
                    pen,
                    new Point(center.X - markerRadius, center.Y),
                    new Point(center.X + markerRadius, center.Y));
                drawingContext.DrawLine(
                    pen,
                    new Point(center.X, center.Y - markerRadius),
                    new Point(center.X, center.Y + markerRadius));
                break;
            case 3:
                drawingContext.DrawLine(
                    pen,
                    new Point(center.X - markerRadius, center.Y - markerRadius),
                    new Point(center.X + markerRadius, center.Y + markerRadius));
                drawingContext.DrawLine(
                    pen,
                    new Point(center.X - markerRadius, center.Y + markerRadius),
                    new Point(center.X + markerRadius, center.Y - markerRadius));
                break;
            case 4:
                drawingContext.DrawLine(
                    pen,
                    new Point(center.X, center.Y - markerRadius),
                    new Point(center.X, center.Y + markerRadius));
                break;
        }
    }
}
