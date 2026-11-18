namespace AirDefenseC2.Models;

/// <summary>
/// 레이더 좌표계 정의.
/// 월드 좌표: km 단위, 원점 = 레이더(방어 지역), +X = 동쪽, +Y = 북쪽.
/// 화면 좌표: 640x640 캔버스, 중심 (320, 320), 반경 300px = 100km.
/// </summary>
public static class RadarGeometry
{
    public const double CanvasSize = 640;
    public const double Center = CanvasSize / 2;
    public const double RadiusPx = 300;
    public const double MaxRangeKm = 100;
    public const double PxPerKm = RadiusPx / MaxRangeKm;

    public static double ToCanvasX(double xKm) => Center + xKm * PxPerKm;

    public static double ToCanvasY(double yKm) => Center - yKm * PxPerKm;

    /// <summary>원점 기준 방위각 (북쪽 0°, 시계방향)</summary>
    public static double Bearing(double x, double y) => NormalizeDeg(Math.Atan2(x, y) * 180.0 / Math.PI);

    public static double Range(double x, double y) => Math.Sqrt(x * x + y * y);

    public static double NormalizeDeg(double deg)
    {
        deg %= 360.0;
        return deg < 0 ? deg + 360.0 : deg;
    }

    public static double ToRad(double deg) => deg * Math.PI / 180.0;
}
