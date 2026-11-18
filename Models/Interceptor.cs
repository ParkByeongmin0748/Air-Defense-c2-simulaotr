using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AirDefenseC2.Models;

/// <summary>요격 미사일</summary>
public partial class Interceptor : ObservableObject
{
    private const int MaxTrail = 40;
    private const double TrailInterval = 0.4; // sim s
    private readonly List<Point> _history = new();
    private double _trailTimer;

    public Interceptor(string id, Target target, Launcher launcher, double speedKmS)
    {
        Id = id;
        Target = target;
        Launcher = launcher;
        Speed = speedKmS;
        _x = launcher.X;
        _y = launcher.Y;
    }

    public string Id { get; }
    public Target Target { get; }
    public Launcher Launcher { get; }
    public double Speed { get; }
    public double LaunchDelay { get; set; }
    public double FlightTime { get; set; }

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private bool _isLaunched;
    [ObservableProperty] private IReadOnlyList<Point> _trail = Array.Empty<Point>();

    public void RecordTrail(double dt, bool force = false)
    {
        _trailTimer += dt;
        if (!force && _trailTimer < TrailInterval) return;
        _trailTimer = 0;

        _history.Add(new Point(X, Y));
        if (_history.Count > MaxTrail) _history.RemoveAt(0);
        Trail = _history.ToArray();
    }
}
