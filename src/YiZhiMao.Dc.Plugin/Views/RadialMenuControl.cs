using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Views;

internal sealed class RadialMenuControl : FrameworkElement
{
    private readonly RadialMenuOptions options;
    private RadialMenuSlot? hoveredSlot;

    public RadialMenuControl(RadialMenuOptions options)
    {
        this.options = options;
        Cursor = Cursors.Hand;
        SnapsToDevicePixels = true;
    }

    public event Action<RadialMenuItem>? CommandSelected;

    public event Action? CloseRequested;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        DrawRing(drawingContext, center, 1, options.Radius * 0.16, options.Radius * 0.44, Color.FromRgb(15, 57, 91));
        DrawRing(drawingContext, center, 2, options.Radius * 0.44, options.Radius * 0.70, Color.FromRgb(18, 91, 145));
        if (options.ShowThirdRing)
        {
            DrawRing(drawingContext, center, 3, options.Radius * 0.70, options.Radius, Color.FromRgb(0, 174, 213));
        }

        drawingContext.DrawEllipse(
            new SolidColorBrush(Color.FromRgb(7, 28, 43)),
            new Pen(new SolidColorBrush(Color.FromRgb(54, 218, 242)), 1),
            center,
            options.Radius * 0.16,
            options.Radius * 0.16);
        var closeText = CreateText("×", 24, FontWeights.Light);
        drawingContext.DrawText(closeText, new Point(center.X - closeText.Width / 2, center.Y - closeText.Height / 2));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        hoveredSlot = HitTest(e.GetPosition(this));
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        hoveredSlot = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var point = e.GetPosition(this);
        var x = point.X - ActualWidth / 2;
        var y = point.Y - ActualHeight / 2;
        if (Math.Sqrt(x * x + y * y) <= options.Radius * 0.16)
        {
            CloseRequested?.Invoke();
            return;
        }

        var slot = RadialMenuHitTester.HitTest(x, y, options.Radius, options.ShowThirdRing);
        if (slot is not { } value)
        {
            return;
        }

        var item = options.Items.FirstOrDefault(candidate =>
            candidate.Ring == value.Ring && candidate.Position == value.Position);
        if (item is not null)
        {
            CommandSelected?.Invoke(item);
        }
    }

    private void DrawRing(
        DrawingContext context,
        Point center,
        int ring,
        double innerRadius,
        double outerRadius,
        Color baseColor)
    {
        var items = options.Items.Where(item => item.Ring == ring).OrderBy(item => item.Position).ToArray();
        var segmentAngle = 360d / items.Length;
        for (var index = 0; index < items.Length; index++)
        {
            var startAngle = -90 - segmentAngle / 2 + index * segmentAngle;
            var endAngle = startAngle + segmentAngle;
            var selected = hoveredSlot == new RadialMenuSlot(ring, index + 1);
            var fill = new SolidColorBrush(selected ? Lighten(baseColor, 35) : baseColor);
            var geometry = CreateSector(center, innerRadius, outerRadius, startAngle, endAngle);
            context.DrawGeometry(fill, new Pen(new SolidColorBrush(Color.FromArgb(180, 120, 228, 244)), 0.8), geometry);

            var middleAngle = (startAngle + endAngle) / 2 * Math.PI / 180;
            var textRadius = (innerRadius + outerRadius) / 2;
            var label = CreateText(items[index].Name, ring == 3 ? 11 : 12, FontWeights.SemiBold);
            var textPoint = new Point(
                center.X + Math.Cos(middleAngle) * textRadius - label.Width / 2,
                center.Y + Math.Sin(middleAngle) * textRadius - label.Height / 2);
            context.DrawText(label, textPoint);
        }
    }

    private RadialMenuSlot? HitTest(Point point) => RadialMenuHitTester.HitTest(
        point.X - ActualWidth / 2,
        point.Y - ActualHeight / 2,
        options.Radius,
        options.ShowThirdRing);

    private static StreamGeometry CreateSector(
        Point center,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle)
    {
        var outerStart = PointOnCircle(center, outerRadius, startAngle);
        var outerEnd = PointOnCircle(center, outerRadius, endAngle);
        var innerEnd = PointOnCircle(center, innerRadius, endAngle);
        var innerStart = PointOnCircle(center, innerRadius, startAngle);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(outerStart, true, true);
            context.ArcTo(outerEnd, new Size(outerRadius, outerRadius), 0, false, SweepDirection.Clockwise, true, false);
            context.LineTo(innerEnd, true, false);
            context.ArcTo(innerStart, new Size(innerRadius, innerRadius), 0, false, SweepDirection.Counterclockwise, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * radius, center.Y + Math.Sin(radians) * radius);
    }

    private FormattedText CreateText(string text, double size, FontWeight weight) => new(
        text,
        CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
        size,
        Brushes.White,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Color Lighten(Color color, byte amount) => Color.FromRgb(
        (byte)Math.Min(255, color.R + amount),
        (byte)Math.Min(255, color.G + amount),
        (byte)Math.Min(255, color.B + amount));
}
