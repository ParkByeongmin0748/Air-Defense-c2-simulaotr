using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AirDefenseC2.Models;

namespace AirDefenseC2.Controls;

/// <summary>
/// 레이더 배경: 탐색 영역, 거리 동심원, 방위선, 방위 눈금/라벨을 그린다.
/// </summary>
public sealed class RadarGrid : FrameworkElement
{
    private static readonly Brush RingBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x28, 0x58, 0x3C)));
    private static readonly Brush StrongBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x7A, 0x52)));
    private static readonly Brush LabelBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5E, 0x9C, 0x74)));
    private static readonly Pen RingPen = Freeze(new Pen(RingBrush, 1));
    private static readonly Pen StrongPen = Freeze(new Pen(StrongBrush, 1.6));
    private static readonly Pen DimPen = Freeze(new Pen(RingBrush, 0.7) { DashStyle = new DashStyle(new double[] { 4, 4 }, 0) });
    private static readonly Typeface Face = new("Consolas");

    private static readonly Brush CoverageBrush = Freeze(new RadialGradientBrush(
        Color.FromArgb(0x30, 0x1B, 0x5E, 0x35),
        Color.FromArgb(0x60, 0x12, 0x3A, 0x22)));

    protected override Size MeasureOverride(Size availableSize) =>
        new(RadarGeometry.CanvasSize, RadarGeometry.CanvasSize);

    protected override void OnRender(DrawingContext dc)
    {
        double c = RadarGeometry.Center;
        double R = RadarGeometry.RadiusPx;
        var center = new Point(c, c);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // 레이더 탐색 영역
        dc.DrawEllipse(CoverageBrush, StrongPen, center, R, R);

        // 거리 동심원 + 라벨
        for (int i = 1; i <= 4; i++)
        {
            double r = R * i / 4;
            if (i < 4) dc.DrawEllipse(null, RingPen, center, r, r);
            var text = MakeText($"{RadarGeometry.MaxRangeKm * i / 4:0}km", 10, LabelBrush, dpi);
            dc.DrawText(text, new Point(c + 4, c - r + 2));
        }

        // 방위선 (30° 간격)
        for (int deg = 0; deg < 360; deg += 30)
            dc.DrawLine(deg % 90 == 0 ? RingPen : DimPen, center, PointAt(deg, R));

        // 방위 눈금 (5° 간격)
        for (int deg = 0; deg < 360; deg += 5)
        {
            double inner = R - (deg % 30 == 0 ? 10 : deg % 10 == 0 ? 6 : 3);
            dc.DrawLine(RingPen, PointAt(deg, inner), PointAt(deg, R));
        }

        // 방위 라벨
        for (int deg = 0; deg < 360; deg += 30)
        {
            string label = deg switch { 0 => "N", 90 => "E", 180 => "S", 270 => "W", _ => deg.ToString("000") };
            var text = MakeText(label, deg % 90 == 0 ? 12 : 10, deg % 90 == 0 ? StrongBrush : LabelBrush, dpi);
            var p = PointAt(deg, R - 20);
            dc.DrawText(text, new Point(p.X - text.Width / 2, p.Y - text.Height / 2));
        }
    }

    internal static Point PointAt(double bearingDeg, double radiusPx)
    {
        double rad = RadarGeometry.ToRad(bearingDeg);
        return new Point(RadarGeometry.Center + Math.Sin(rad) * radiusPx,
                         RadarGeometry.Center - Math.Cos(rad) * radiusPx);
    }

    private static FormattedText MakeText(string s, double size, Brush brush, double dpi) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, brush, dpi);

    private static T Freeze<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }
}
