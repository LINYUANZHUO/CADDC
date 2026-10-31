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
        var palette = ResolvePalette();
        var labelBrush = ResolveThemeBrush("ThemeSurfaceBrush", Brushes.White);

        DrawRing(drawingContext, center, 1, options.Radius * 0.16, options.Radius * 0.44, palette.Inner, palette.Accent, palette.Divider, labelBrush);
        DrawRing(drawingContext, center, 2, options.Radius * 0.44, options.Radius * 0.70, palette.Middle, palette.Accent, palette.Divider, labelBrush);
        if (options.ShowThirdRing)
        {
            DrawRing(drawingContext, center, 3, options.Radius * 0.70, options.Radius, palette.Outer, palette.Accent, palette.Divider, labelBrush);
        }

        drawingContext.DrawEllipse(
            new SolidColorBrush(palette.Inner),
            new Pen(new SolidColorBrush(palette.Accent), 1.2),
            center,
            options.Radius * 0.16,
            options.Radius * 0.16);
        var closeText = CreateText("×", 24, FontWeights.Light, labelBrush);
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
        if (item is not null && !string.IsNullOrWhiteSpace(item.CommandAlias))
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
        Color baseColor,
        Color accentColor,
        Color dividerColor,
        Brush labelBrush)
    {
        var items = options.Items.Where(item => item.Ring == ring).OrderBy(item => item.Position).ToArray();
        var segmentAngle = 360d / items.Length;
        for (var index = 0; index < items.Length; index++)
        {
            var startAngle = -90 - segmentAngle / 2 + index * segmentAngle;
            var endAngle = startAngle + segmentAngle;
            var selected = hoveredSlot == new RadialMenuSlot(ring, index + 1);
            var fill = new SolidColorBrush(selected ? accentColor : baseColor);
            var geometry = CreateSector(center, innerRadius, outerRadius, startAngle, endAngle);
            var strokeColor = selected
                ? accentColor
                : Color.FromArgb(178, dividerColor.R, dividerColor.G, dividerColor.B);
            context.DrawGeometry(
                fill,
                new Pen(new SolidColorBrush(strokeColor), selected ? 1.4 : 0.8),
                geometry);

            var middleAngle = (startAngle + endAngle) / 2 * Math.PI / 180;
            var textRadius = (innerRadius + outerRadius) / 2;
            var label = CreateText(items[index].Name, ring == 3 ? 11 : 12, FontWeights.SemiBold, labelBrush);
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

    private FormattedText CreateText(string text, double size, FontWeight weight, Brush foreground) => new(
        text,
        CultureInfo.CurrentUICulture,
        FlowDirection.LeftToRight,
        new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
        size,
        foreground,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Color Blend(Color first, Color second, double secondWeight)
    {
        var firstWeight = 1 - secondWeight;
        return Color.FromRgb(
            (byte)Math.Round(first.R * firstWeight + second.R * secondWeight),
            (byte)Math.Round(first.G * firstWeight + second.G * secondWeight),
            (byte)Math.Round(first.B * firstWeight + second.B * secondWeight));
    }

    private RadialPalette ResolvePalette()
    {
        if (string.Equals(options.Skin, RadialMenuOptions.CadDarkSkin, StringComparison.Ordinal))
        {
            return new RadialPalette(
                ResolveThemeColor("RadialCadDarkInnerBrush", Color.FromRgb(31, 37, 44)),
                ResolveThemeColor("RadialCadDarkMiddleBrush", Color.FromRgb(42, 52, 61)),
                ResolveThemeColor("RadialCadDarkOuterBrush", Color.FromRgb(55, 69, 80)),
                ResolveThemeColor("RadialCadDarkAccentBrush", Color.FromRgb(45, 186, 210)),
                ResolveThemeColor("RadialCadDarkDividerBrush", Color.FromRgb(110, 125, 140)));
        }

        if (string.Equals(options.Skin, RadialMenuOptions.PurpleGraySkin, StringComparison.Ordinal))
        {
            return new RadialPalette(
                ResolveThemeColor("RadialPurpleGrayInnerBrush", Color.FromRgb(55, 50, 63)),
                ResolveThemeColor("RadialPurpleGrayMiddleBrush", Color.FromRgb(75, 67, 87)),
                ResolveThemeColor("RadialPurpleGrayOuterBrush", Color.FromRgb(98, 88, 111)),
                ResolveThemeColor("RadialPurpleGrayAccentBrush", Color.FromRgb(154, 112, 209)),
                ResolveThemeColor("RadialPurpleGrayDividerBrush", Color.FromRgb(184, 174, 197)));
        }

        var inner = ResolveThemeColor("ThemeTextPrimaryBrush", Color.FromRgb(32, 36, 42));
        var outer = ResolveThemeColor("ThemeTextSecondaryBrush", Color.FromRgb(104, 112, 124));
        return new RadialPalette(
            inner,
            Blend(inner, outer, 0.48),
            outer,
            ResolveThemeColor("ThemeAccentBrush", Color.FromRgb(239, 94, 37)),
            ResolveThemeColor("ThemeBorderBrush", Color.FromRgb(217, 221, 227)));
    }

    private Color ResolveThemeColor(string key, Color fallback) =>
        TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;

    private Brush ResolveThemeBrush(string key, Brush fallback) =>
        TryFindResource(key) is Brush brush ? brush : fallback;

    private sealed class RadialPalette
    {
        public RadialPalette(Color inner, Color middle, Color outer, Color accent, Color divider)
        {
            Inner = inner;
            Middle = middle;
            Outer = outer;
            Accent = accent;
            Divider = divider;
        }

        public Color Inner { get; }
        public Color Middle { get; }
        public Color Outer { get; }
        public Color Accent { get; }
        public Color Divider { get; }
    }
}
