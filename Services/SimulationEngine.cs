using System.Collections.ObjectModel;
using AirDefenseC2.Models;

namespace AirDefenseC2.Services;

/// <summary>
/// 방공 시뮬레이션 엔진.
/// 표적 생성/이동, 레이더 스캔(탐지·식별·위협평가), 무장 할당, 요격 미사일 유도, 결과 판정을 담당한다.
/// UI 스레드(DispatcherTimer)에서 Tick()이 호출된다.
/// </summary>
public sealed class SimulationEngine
{
    public const double SweepPeriodSec = 3.0;        // 레이더 1회전 주기 (실제 시간)
    public const double InterceptorSpeed = 1.6;      // km/s
    public const double InterceptorMaxFlight = 60;   // sim s
    public const double LaunchPrepTime = 1.5;        // sim s
    public const double LeakRadiusKm = 2.5;
    public const int MaxActiveTargets = 8;

    private readonly Random _rng = new();
    private readonly List<Target> _allTargets = new();
    private int _targetSeq;
    private int _interceptorSeq;
    private double _nextSpawnSim;
    private double _realTime;

    public ObservableCollection<Target> Targets { get; } = new();          // 탐지된 표적
    public ObservableCollection<Launcher> Launchers { get; } = new();
    public ObservableCollection<Interceptor> Interceptors { get; } = new();
    public ObservableCollection<Explosion> Explosions { get; } = new();

    public double SimTime { get; private set; }
    public double SweepAngle { get; private set; }
    public int ScanCount { get; private set; }
    public double TimeScale { get; set; } = 4;
    public bool AutoEngage { get; set; } = true;
    public bool AutoSpawn { get; set; } = true;

    public int DetectedTotal { get; private set; }
    public int Kills { get; private set; }
    public int Misses { get; private set; }
    public int Leaks { get; private set; }
    public int InterceptorsFired { get; private set; }

    public event Action<LogLevel, string>? LogRaised;
    public event Action<Target>? TargetRemoved;

    public SimulationEngine()
    {
        Reset();
    }

    public void Reset()
    {
        _allTargets.Clear();
        Targets.Clear();
        Interceptors.Clear();
        Explosions.Clear();
        Launchers.Clear();

        Launchers.Add(new Launcher("L1", "발사대-1 (서)", -18, -6, 6, 55));
        Launchers.Add(new Launcher("L2", "발사대-2 (동)", 18, -6, 6, 55));
        Launchers.Add(new Launcher("L3", "발사대-3 (북)", 0, 20, 6, 55));

        _targetSeq = 0;
        _interceptorSeq = 0;
        _nextSpawnSim = 2;
        _realTime = 0;
        SimTime = 0;
        SweepAngle = 0;
        ScanCount = 0;
        DetectedTotal = Kills = Misses = Leaks = InterceptorsFired = 0;
    }

    // =====================================================================
    //  메인 루프
    // =====================================================================
    public void Tick(double dtReal)
    {
        if (dtReal <= 0) return;
        dtReal = Math.Min(dtReal, 0.1);
        double dt = dtReal * TimeScale;

        _realTime += dtReal;
        SimTime += dt;

        if (AutoSpawn && SimTime >= _nextSpawnSim)
        {
            if (_allTargets.Count(t => t.IsAlive) < MaxActiveTargets)
                SpawnRandom();
            _nextSpawnSim = SimTime + Rand(8, 16);
        }

        foreach (var t in _allTargets)
            if (t.IsAlive) t.Move(dt);

        UpdateSweep(dtReal);
        UpdateLaunchers(dt);
        UpdateInterceptors(dt);
        CheckBoundaries();
        if (AutoEngage) RunAutoEngage();
        UpdateBlips();
        UpdateExplosions(dtReal);
        Cleanup();
    }

    // =====================================================================
    //  표적 생성
    // =====================================================================
    public Target SpawnRandom()
    {
        double r = _rng.NextDouble();
        var type = r < 0.3 ? TargetType.Missile : r < 0.65 ? TargetType.Aircraft : TargetType.Drone;
        return Spawn(type);
    }

    public Target Spawn(TargetType type)
    {
        double bearing = Rand(0, 360);
        double range = type == TargetType.Drone ? Rand(35, 55) : 104;   // 드론은 저고도 → 근거리에서 출현
        double x = range * Math.Sin(RadarGeometry.ToRad(bearing));
        double y = range * Math.Cos(RadarGeometry.ToRad(bearing));

        double offset = type switch
        {
            TargetType.Missile => Rand(-4, 4),
            TargetType.Aircraft => Rand(-25, 25),
            _ => Rand(-12, 12)
        };
        double heading = RadarGeometry.NormalizeDeg(bearing + 180 + offset);

        double speed = type switch
        {
            TargetType.Missile => Rand(0.85, 1.0),
            TargetType.Aircraft => Rand(0.24, 0.30),
            _ => Rand(0.05, 0.07)
        };
        double alt = type switch
        {
            TargetType.Missile => Rand(12, 20),
            TargetType.Aircraft => Rand(6, 11),
            _ => Rand(0.2, 1.0)
        };

        var t = new Target($"T-{++_targetSeq:000}", type, x, y, heading, speed, alt);
        _allTargets.Add(t);
        Log(LogLevel.System, $"[모의] 가상 표적 {t.Id} 투입 ({TypeKor(type)}, 방위 {bearing:000}°)");
        return t;
    }

    // =====================================================================
    //  레이더 스캔
    // =====================================================================
    private void UpdateSweep(double dtReal)
    {
        double prev = SweepAngle;
        double next = prev + 360.0 * dtReal / SweepPeriodSec;

        foreach (var t in _allTargets)
        {
            if (!t.IsAlive || t.TrueRange > RadarGeometry.MaxRangeKm) continue;

            double b = RadarGeometry.Bearing(t.TrueX, t.TrueY);
            if (b <= prev) b += 360;
            if (b <= next) Paint(t);
        }

        if (next >= 360)
        {
            next -= 360;
            ScanCount++;
        }
        SweepAngle = next;
    }

    /// <summary>레이더 빔이 표적을 비춤 → 플롯 갱신 + 탐지/식별/위협평가 단계 진행</summary>
    private void Paint(Target t)
    {
        t.PaintCount++;
        t.Plot();
        t.LastPaintReal = _realTime;
        t.BlipOpacity = 1.0;

        if (!t.IsDetected)
        {
            t.IsDetected = true;
            t.Status = TargetStatus.Detected;
            t.Stage = ProcessStage.Detect;
            Targets.Add(t);
            DetectedTotal++;
            Log(LogLevel.Info, $"[탐지] {t.Id} 신규 표적 탐지 - 방위 {t.Bearing:000}°, 거리 {t.Range:0.0}km");
            return;
        }

        if (!t.IsIdentified)
        {
            t.IsIdentified = true;
            if (t.Status == TargetStatus.Detected) t.Status = TargetStatus.Tracking;
            if (t.Stage < ProcessStage.Identify) t.Stage = ProcessStage.Identify;
            Log(LogLevel.Info, $"[식별] {t.Id} → {t.TypeName} (고도 {t.AltitudeText}, 속도 {t.SpeedText})");
            return;
        }

        var threat = EvaluateThreat(t);
        if (!t.ThreatEvaluated)
        {
            t.ThreatEvaluated = true;
            t.Threat = threat;
            if (t.Stage < ProcessStage.ThreatEval) t.Stage = ProcessStage.ThreatEval;
            var level = threat == ThreatLevel.High ? LogLevel.Critical
                      : threat == ThreatLevel.Medium ? LogLevel.Warning : LogLevel.Info;
            Log(level, $"[위협평가] {t.Id} {t.TypeName} 위협도 {t.ThreatText} (거리 {t.Range:0.0}km)");
        }
        else if (threat != t.Threat)
        {
            string old = t.ThreatText;
            t.Threat = threat;
            if (threat > ThreatLevel.Low)
                Log(threat == ThreatLevel.High ? LogLevel.Critical : LogLevel.Warning,
                    $"[위협평가] {t.Id} 위협도 변경 {old} → {t.ThreatText}");
        }
    }

    private static ThreatLevel EvaluateThreat(Target t)
    {
        double r = t.TrueRange;
        if (t.ClosestApproach() > 30) return ThreatLevel.Low; // 방어지역을 지나쳐 가는 항적

        return t.Type switch
        {
            TargetType.Missile => ThreatLevel.High,
            TargetType.Aircraft => r < 60 ? ThreatLevel.High : ThreatLevel.Medium,
            _ => r < 25 ? ThreatLevel.High : r < 45 ? ThreatLevel.Medium : ThreatLevel.Low
        };
    }

    // =====================================================================
    //  교전
    // =====================================================================
    private void RunAutoEngage()
    {
        foreach (var t in Targets)
        {
            if (t.Status == TargetStatus.Tracking && t.ThreatEvaluated && t.Threat == ThreatLevel.High)
                Engage(t, manual: false);
        }
    }

    /// <summary>표적에 발사대를 할당하고 요격 미사일을 발사 준비한다.</summary>
    public bool Engage(Target t, bool manual)
    {
        if (!t.IsActive)
        {
            if (manual) Log(LogLevel.Warning, $"[교전] {t.Id} 교전 불가 - 유효한 표적이 아님");
            return false;
        }
        if (t.Status == TargetStatus.Engaging)
        {
            if (manual) Log(LogLevel.Warning, $"[교전] {t.Id} 이미 교전 중");
            return false;
        }

        var launcher = Launchers
            .Where(l => l.CanFire && l.DistanceTo(t) <= l.EngagementRangeKm)
            .OrderBy(l => l.DistanceTo(t))
            .FirstOrDefault();

        if (launcher is null)
        {
            if (manual)
            {
                bool inRange = Launchers.Any(l => l.DistanceTo(t) <= l.EngagementRangeKm);
                Log(LogLevel.Warning, inRange
                    ? $"[교전] {t.Id} 가용 발사대 없음 (탄약 소진/재장전/발사 간격)"
                    : $"[교전] {t.Id} 모든 발사대 사거리 밖 (최근접 {Launchers.Min(l => l.DistanceTo(t)):0.0}km)");
            }
            return false;
        }

        launcher.Ammo--;
        launcher.CooldownRemaining = Launcher.FireInterval;
        launcher.ActiveInterceptors++;
        if (launcher.Status != LauncherStatus.Reloading) launcher.Status = LauncherStatus.Engaging;
        InterceptorsFired++;

        var m = new Interceptor($"M-{++_interceptorSeq:00}", t, launcher, InterceptorSpeed)
        {
            LaunchDelay = LaunchPrepTime
        };
        Interceptors.Add(m);

        t.Status = TargetStatus.Engaging;
        t.Stage = ProcessStage.WeaponAssign;
        t.AssignedLauncher = launcher;

        Log(LogLevel.Warning,
            $"[무장할당] {t.Id} ← {launcher.Name} ({(manual ? "수동" : "자동")} 교전), 잔여 {launcher.AmmoText}");

        if (launcher.Ammo == 0)
        {
            launcher.Status = LauncherStatus.Reloading;
            launcher.ReloadRemaining = Launcher.ReloadTime;
            launcher.ReloadProgress = 0;
            Log(LogLevel.System, $"[무장] {launcher.Name} 탄약 소진 → 재장전 시작");
        }
        return true;
    }

    private void UpdateLaunchers(double dt)
    {
        foreach (var l in Launchers)
        {
            if (l.CooldownRemaining > 0) l.CooldownRemaining -= dt;

            if (l.Status == LauncherStatus.Reloading)
            {
                l.ReloadRemaining -= dt;
                l.ReloadProgress = Math.Clamp(1 - l.ReloadRemaining / Launcher.ReloadTime, 0, 1);
                if (l.ReloadRemaining <= 0)
                {
                    l.Ammo = l.MaxAmmo;
                    l.ReloadProgress = 1;
                    l.Status = l.ActiveInterceptors > 0 ? LauncherStatus.Engaging : LauncherStatus.Ready;
                    Log(LogLevel.System, $"[무장] {l.Name} 재장전 완료 ({l.AmmoText})");
                }
            }
            else
            {
                l.Status = l.ActiveInterceptors > 0 ? LauncherStatus.Engaging : LauncherStatus.Ready;
            }
        }
    }

    private void UpdateInterceptors(double dt)
    {
        for (int i = Interceptors.Count - 1; i >= 0; i--)
        {
            var m = Interceptors[i];
            var t = m.Target;

            if (!m.IsLaunched)
            {
                if (!t.IsAlive)
                {
                    FinishInterceptor(m);
                    m.Launcher.Ammo = Math.Min(m.Launcher.MaxAmmo, m.Launcher.Ammo + 1);
                    Log(LogLevel.Info, $"[교전] {t.Id} 표적 소멸 → {m.Id} 발사 취소");
                    continue;
                }

                m.LaunchDelay -= dt;
                if (m.LaunchDelay > 0) continue;

                m.IsLaunched = true;
                m.RecordTrail(0, force: true);
                if (t.Status == TargetStatus.Engaging) t.Stage = ProcessStage.Intercept;
                Log(LogLevel.Warning, $"[발사] {m.Launcher.Name} → 요격미사일 {m.Id} 발사 (표적 {t.Id})");
            }

            if (!t.IsAlive)
            {
                FinishInterceptor(m);
                Explosions.Add(new Explosion(m.X, m.Y, ExplosionKind.Miss));
                Log(LogLevel.Info, $"[요격] {m.Id} 표적 소멸로 자폭");
                continue;
            }

            m.FlightTime += dt;
            double dx = t.TrueX - m.X;
            double dy = t.TrueY - m.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double step = m.Speed * dt;

            if (dist <= step + 0.4)
            {
                ResolveIntercept(m, t);
                continue;
            }

            // 선도 추적(lead pursuit): 예상 조우 지점을 향해 비행
            double tgo = dist / m.Speed;
            double ax = t.TrueX + t.VelX * tgo - m.X;
            double ay = t.TrueY + t.VelY * tgo - m.Y;
            double len = Math.Sqrt(ax * ax + ay * ay);
            if (len > 1e-6)
            {
                m.X += ax / len * step;
                m.Y += ay / len * step;
            }
            m.RecordTrail(dt);

            if (m.FlightTime > InterceptorMaxFlight)
            {
                FinishInterceptor(m);
                Misses++;
                Explosions.Add(new Explosion(m.X, m.Y, ExplosionKind.Miss));
                ReturnToTracking(t);
                Log(LogLevel.Warning, $"[요격 실패] {m.Id} 연료 소진 자폭 - {t.Id} 재교전 필요");
            }
        }
    }

    private void ResolveIntercept(Interceptor m, Target t)
    {
        m.X = t.TrueX;
        m.Y = t.TrueY;
        FinishInterceptor(m);

        double pk = t.Type switch
        {
            TargetType.Missile => 0.75,
            TargetType.Aircraft => 0.85,
            _ => 0.92
        };

        if (_rng.NextDouble() < pk)
        {
            t.Plot();
            t.Status = TargetStatus.Destroyed;
            t.Stage = ProcessStage.Assess;
            t.RemoveAtReal = _realTime + 6;
            Kills++;
            Explosions.Add(new Explosion(t.TrueX, t.TrueY, ExplosionKind.Kill));
            Log(LogLevel.Success,
                $"[요격 성공] {m.Launcher.Name}의 요격미사일 {m.Id}이(가) {t.Id}({t.TypeName})을(를) 요격했습니다.");
        }
        else
        {
            Misses++;
            Explosions.Add(new Explosion(t.TrueX, t.TrueY, ExplosionKind.Miss));
            ReturnToTracking(t);
            Log(LogLevel.Warning, $"[요격 실패] {m.Id}이(가) {t.Id} 요격에 실패 - 재교전 필요");
        }
    }

    private static void ReturnToTracking(Target t)
    {
        if (!t.IsAlive) return;
        t.Status = TargetStatus.Tracking;
        t.Stage = t.ThreatEvaluated ? ProcessStage.ThreatEval
                : t.IsIdentified ? ProcessStage.Identify : ProcessStage.Detect;
    }

    private void FinishInterceptor(Interceptor m)
    {
        Interceptors.Remove(m);
        m.Launcher.ActiveInterceptors = Math.Max(0, m.Launcher.ActiveInterceptors - 1);
    }

    // =====================================================================
    //  상태 점검 / 정리
    // =====================================================================
    private void CheckBoundaries()
    {
        foreach (var t in _allTargets)
        {
            if (!t.IsAlive) continue;
            double r = t.TrueRange;

            if (r < LeakRadiusKm)
            {
                if (!t.IsDetected)
                {
                    t.IsDetected = true;
                    Targets.Add(t);
                }
                t.Plot();
                t.Status = TargetStatus.Leaked;
                t.Stage = ProcessStage.Assess;
                t.RemoveAtReal = _realTime + 4;
                Leaks++;
                Explosions.Add(new Explosion(t.TrueX, t.TrueY, ExplosionKind.Impact));
                Log(LogLevel.Critical, $"[침투] {t.Id}({t.TypeName}) 방어지역 도달 - 방어 실패!");
            }
            else if (r > RadarGeometry.MaxRangeKm + 8 && (t.TrueX * t.VelX + t.TrueY * t.VelY) > 0)
            {
                t.Status = TargetStatus.Lost;
                if (t.IsDetected)
                {
                    t.RemoveAtReal = _realTime + 2;
                    Log(LogLevel.Info, $"[추적 소실] {t.Id} 탐지 범위 이탈");
                }
                else
                {
                    t.RemoveAtReal = _realTime;
                }
            }
        }
    }

    private void UpdateBlips()
    {
        foreach (var t in Targets)
        {
            if (t.IsAlive)
            {
                double age = _realTime - t.LastPaintReal;
                t.BlipOpacity = Math.Max(0.35, 1.0 - age / SweepPeriodSec * 0.65);
            }
            else
            {
                t.BlipOpacity = Math.Clamp((t.RemoveAtReal - _realTime) / 3.0, 0, 1);
            }
        }
    }

    private void UpdateExplosions(double dtReal)
    {
        for (int i = Explosions.Count - 1; i >= 0; i--)
            if (!Explosions[i].Update(dtReal)) Explosions.RemoveAt(i);
    }

    private void Cleanup()
    {
        for (int i = _allTargets.Count - 1; i >= 0; i--)
        {
            var t = _allTargets[i];
            if (t.IsAlive || _realTime < t.RemoveAtReal) continue;

            _allTargets.RemoveAt(i);
            if (Targets.Remove(t)) TargetRemoved?.Invoke(t);
        }
    }

    // =====================================================================
    //  유틸
    // =====================================================================
    private double Rand(double min, double max) => min + _rng.NextDouble() * (max - min);

    private void Log(LogLevel level, string message) => LogRaised?.Invoke(level, message);

    private static string TypeKor(TargetType type) => type switch
    {
        TargetType.Missile => "미사일",
        TargetType.Drone => "드론",
        _ => "항공기"
    };
}
