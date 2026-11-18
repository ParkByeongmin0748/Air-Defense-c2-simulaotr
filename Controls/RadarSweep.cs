using System.Windows;
using System.Windows.Media;
using AirDefenseC2.Models;

namespace AirDefenseC2.Controls;

/// <summary>
/// 레이더 스캔 빔과 잔상(trail).
/// 방위 0° 기준으로 한 번만 그리고, XAML 의 RotateTransform(SweepAngle 바인딩)으로 회전시킨다.
/// </summary>
public sealed class RadarSweep : FrameworkElement
{
    private const double TrailDeg = 70;
    private const double SliceDeg = 1.0;

    protected override Size MeasureOverride(Size availableSize) =>
        new(RadarGeometry.CanvasSize, RadarGeometry.CanvasSize);

    protected override void OnRender(DrawingContext dc)
    {
        double R = RadarGeometry.RadiusPx;
        var center = new Point(RadarGeometry.Center, RadarGeometry.Center);

        for (double a = -TrailDeg; a < 0; a += SliceDeg)
        {
            double k = (a + TrailDeg) / TrailDeg;           // 0(꼬리) → 1(빔)
            byte alpha = (byte)(120 * k * k);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x3C, 0xE0, 0x78));
            brush.Freeze();
            dc.DrawGeometry(brush, null, Wedge(center, a, a + SliceDeg + 0.25, R));
        }

        var beamPen = new Pen(new SolidColorBrush(Color.FromRgb(0x7D, 0xFF, 0xA6)), 2.2);
        beamPen.Freeze();
        dc.DrawLine(beamPen, center, RadarGrid.PointAt(0, R));
    }

    private static Geometry Wedge(Point center, double a1, double a2, double r)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(center, true, true);
            ctx.LineTo(RadarGrid.PointAt(a1, r), false, false);
            ctx.ArcTo(RadarGrid.PointAt(a2, r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        }
        g.Freeze();
        return g;
    }
}
