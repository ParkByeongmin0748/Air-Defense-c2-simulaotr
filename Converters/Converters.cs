using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AirDefenseC2.Models;

namespace AirDefenseC2.Converters;

internal static class Palette
{
    public static readonly Brush Green = Make(0x65, 0xE8, 0x91);
    public static readonly Brush Amber = Make(0xFF, 0xBE, 0x45);
    public static readonly Brush Red = Make(0xFF, 0x62, 0x58);
    public static readonly Brush Grey = Make(0x9F, 0xB4, 0xA8);
    public static readonly Brush Dim = Make(0x73, 0x99, 0x87);
    public static readonly Brush Cyan = Make(0x4F, 0xC3, 0xF7);
    public static readonly Brush Text = Make(0xD5, 0xE8, 0xD8);

    private static Brush Make(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>월드 좌표(km) → 캔버스 좌표(px). ConverterParameter = "X" 또는 "Y"</summary>
public sealed class KmToCanvasConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double km = value is double d ? d : 0;
        return string.Equals(parameter as string, "Y", StringComparison.OrdinalIgnoreCase)
            ? RadarGeometry.ToCanvasY(km)
            : RadarGeometry.ToCanvasX(km);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>항적 점 목록(km) → Polyline 용 PointCollection(px)</summary>
public sealed class TrailToPointsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pc = new PointCollection();
        if (value is IEnumerable<Point> pts)
        {
            foreach (var p in pts)
                pc.Add(new Point(RadarGeometry.ToCanvasX(p.X), RadarGeometry.ToCanvasY(p.Y)));
        }
        pc.Freeze();
        return pc;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ThreatToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ThreatLevel.High => Palette.Red,
        ThreatLevel.Medium => Palette.Amber,
        ThreatLevel.Low => Palette.Green,
        _ => Palette.Grey
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class TargetStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        TargetStatus.Engaging => Palette.Amber,
        TargetStatus.Destroyed => Palette.Green,
        TargetStatus.Leaked => Palette.Red,
        TargetStatus.Lost => Palette.Dim,
        TargetStatus.Detected => Palette.Cyan,
        _ => Palette.Text
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class LauncherStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        LauncherStatus.Ready => Palette.Green,
        LauncherStatus.Engaging => Palette.Red,
        LauncherStatus.Reloading => Palette.Amber,
        _ => Palette.Grey
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>개수가 0 이면 Visible (빈 목록 안내 문구용)</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int n && n == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        LogLevel.System => Palette.Cyan,
        LogLevel.Info => Palette.Text,
        LogLevel.Warning => Palette.Amber,
        LogLevel.Critical => Palette.Red,
        LogLevel.Success => Palette.Green,
        _ => Palette.Grey
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
