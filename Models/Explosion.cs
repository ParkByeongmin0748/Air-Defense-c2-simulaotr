using CommunityToolkit.Mvvm.ComponentModel;

namespace AirDefenseC2.Models;

/// <summary>요격/피격 시 표시되는 폭발 효과</summary>
public partial class Explosion : ObservableObject
{
    public const double Duration = 1.6; // real s

    public Explosion(double x, double y, ExplosionKind kind)
    {
        X = x;
        Y = y;
        Kind = kind;
    }

    public double X { get; }
    public double Y { get; }
    public ExplosionKind Kind { get; }
    public double Age { get; private set; }

    [ObservableProperty] private double _scale = 0.2;
    [ObservableProperty] private double _opacity = 1.0;

    /// <returns>아직 표시 중이면 true</returns>
    public bool Update(double dtReal)
    {
        Age += dtReal;
        double k = Math.Min(1.0, Age / Duration);
        Scale = 0.2 + k * (Kind == ExplosionKind.Miss ? 0.8 : 1.6);
        Opacity = 1.0 - k;
        return Age < Duration;
    }
}
