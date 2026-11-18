using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirDefenseC2.Models;

/// <summary>
/// 가상 표적. 실제 위치(TrueX/TrueY)는 매 틱 갱신되고,
/// 화면에 표시되는 위치(X/Y)는 레이더 빔이 지나갈 때만 갱신된다 (레이더 갱신 주기).
/// </summary>
public partial class Target : ObservableObject
{
    private const int MaxTrail = 8;
    private readonly List<Point> _history = new();

    public Target(string id, TargetType type, double x, double y, double headingDeg, double speedKmS, double altitudeKm)
    {
        Id = id;
        Type = type;
        TrueX = x;
        TrueY = y;
        Heading = headingDeg;
        Speed = speedKmS;
        Altitude = altitudeKm;

        double rad = RadarGeometry.ToRad(headingDeg);
        VelX = speedKmS * Math.Sin(rad);
        VelY = speedKmS * Math.Cos(rad);

        // 진행 방향 지시선 (화면 px)
        double len = 12 + speedKmS * 30;
        LeaderX = Math.Sin(rad) * len;
        LeaderY = -Math.Cos(rad) * len;
    }

    public string Id { get; }
    public TargetType Type { get; }
    public double Heading { get; }
    /// <summary>속도 (km/s)</summary>
    public double Speed { get; }
    /// <summary>고도 (km)</summary>
    public double Altitude { get; }
    public double VelX { get; }
    public double VelY { get; }
    public double LeaderX { get; }
    public double LeaderY { get; }

    // ---- 시뮬레이션 내부 상태 ----
    public double TrueX { get; set; }
    public double TrueY { get; set; }
    public double TrueRange => RadarGeometry.Range(TrueX, TrueY);
    public bool IsDetected { get; set; }
    public bool ThreatEvaluated { get; set; }
    public int PaintCount { get; set; }
    public double LastPaintReal { get; set; }
    public double RemoveAtReal { get; set; } = double.MaxValue;
    public Launcher? AssignedLauncher { get; set; }

    // ---- 화면 표시용 (레이더 플롯) ----
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _bearing;
    [ObservableProperty] private double _range;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsActive))]
    private TargetStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThreatText))]
    private ThreatLevel _threat;

    [ObservableProperty] private ProcessStage _stage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeName))]
    private bool _isIdentified;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private double _blipOpacity = 1.0;
    [ObservableProperty] private IReadOnlyList<Point> _trail = Array.Empty<Point>();

    /// <summary>탐지되어 추적 중인(종료되지 않은) 표적</summary>
    public bool IsActive => Status is TargetStatus.Detected or TargetStatus.Tracking or TargetStatus.Engaging;

    /// <summary>시뮬레이션 상 아직 살아있는 표적 (미탐지 포함)</summary>
    public bool IsAlive => Status is TargetStatus.Undetected or TargetStatus.Detected or TargetStatus.Tracking or TargetStatus.Engaging;

    public string TypeName => !IsIdentified ? "미상" : Type switch
    {
        TargetType.Missile => "미사일",
        TargetType.Drone => "드론",
        TargetType.Aircraft => "항공기",
        _ => "미상"
    };

    public string StatusText => Status switch
    {
        TargetStatus.Undetected => "미탐지",
        TargetStatus.Detected => "탐지",
        TargetStatus.Tracking => "추적",
        TargetStatus.Engaging => "교전 중",
        TargetStatus.Destroyed => "격추",
        TargetStatus.Leaked => "침투",
        TargetStatus.Lost => "소실",
        _ => "-"
    };

    public string ThreatText => Threat switch
    {
        ThreatLevel.Low => "LOW",
        ThreatLevel.Medium => "MID",
        ThreatLevel.High => "HIGH",
        _ => "-"
    };

    public string AltitudeText => $"{Altitude:0.0}km";

    public string SpeedText => $"{Speed * 3600:0}km/h";

    public void Move(double dt)
    {
        TrueX += VelX * dt;
        TrueY += VelY * dt;
    }

    /// <summary>레이더 플롯: 실제 위치를 화면 표시 위치로 반영하고 항적을 남긴다.</summary>
    public void Plot()
    {
        if (IsDetected)
        {
            _history.Add(new Point(X, Y));
            if (_history.Count > MaxTrail) _history.RemoveAt(0);
        }

        X = TrueX;
        Y = TrueY;
        Bearing = RadarGeometry.Bearing(X, Y);
        Range = RadarGeometry.Range(X, Y);

        var pts = new List<Point>(_history) { new(X, Y) };
        Trail = pts;
    }

    /// <summary>원점에 대한 최근접 거리 (CPA, km)</summary>
    public double ClosestApproach()
    {
        double vv = VelX * VelX + VelY * VelY;
        if (vv < 1e-9) return TrueRange;
        double tca = -(TrueX * VelX + TrueY * VelY) / vv;
        if (tca < 0) return TrueRange;
        return RadarGeometry.Range(TrueX + VelX * tca, TrueY + VelY * tca);
    }
}
