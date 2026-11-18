using CommunityToolkit.Mvvm.ComponentModel;

namespace AirDefenseC2.Models;

/// <summary>가상 발사대</summary>
public partial class Launcher : ObservableObject
{
    public const double ReloadTime = 15.0;   // 재장전 시간 (sim s)
    public const double FireInterval = 2.0;  // 연속 발사 간격 (sim s)

    public Launcher(string id, string name, double x, double y, int maxAmmo, double engagementRangeKm)
    {
        Id = id;
        Name = name;
        X = x;
        Y = y;
        MaxAmmo = maxAmmo;
        EngagementRangeKm = engagementRangeKm;
        _ammo = maxAmmo;
    }

    public string Id { get; }
    public string Name { get; }
    public double X { get; }
    public double Y { get; }
    public int MaxAmmo { get; }
    public double EngagementRangeKm { get; }

    /// <summary>교전 반경 원 지름 (화면 px)</summary>
    public double RangeDiameterPx => EngagementRangeKm * 2 * RadarGeometry.PxPerKm;
    public double RangeOffsetPx => -RangeDiameterPx / 2;

    public double CooldownRemaining { get; set; }
    public double ReloadRemaining { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmmoText))]
    private int _ammo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private LauncherStatus _status;

    [ObservableProperty] private double _reloadProgress;
    [ObservableProperty] private int _activeInterceptors;

    public string AmmoText => $"{Ammo}/{MaxAmmo}발";

    public string StatusText => Status switch
    {
        LauncherStatus.Ready => "대기",
        LauncherStatus.Engaging => "교전 중",
        LauncherStatus.Reloading => "재장전",
        _ => "-"
    };

    public bool CanFire => Status != LauncherStatus.Reloading && Ammo > 0 && CooldownRemaining <= 0;

    public double DistanceTo(Target t) => RadarGeometry.Range(t.TrueX - X, t.TrueY - Y);
}
