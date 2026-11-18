using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using AirDefenseC2.Models;
using AirDefenseC2.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AirDefenseC2.ViewModels;

/// <summary>
/// 메인 화면 ViewModel.
/// 갱신 타이머로 시뮬레이션 엔진을 구동하고, 레이더/표적 목록/프로세스/이벤트 로그에 바인딩할 상태를 제공한다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int MaxLogEntries = 300;
    private static readonly double[] TimeScales = { 1, 2, 4, 8 };

    private readonly SimulationEngine _engine = new();
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTick;
    private int _logSeq;

    public MainViewModel()
    {
        ProcessSteps = new ObservableCollection<ProcessStep>
        {
            new(1, "탐지"),
            new(2, "식별"),
            new(3, "위협평가"),
            new(4, "무장할당"),
            new(5, "요격"),
            new(6, "결과")
        };

        _engine.LogRaised += AddLog;
        _engine.TargetRemoved += OnTargetRemoved;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;

        AddLog(LogLevel.System, "방공 C2 시뮬레이터 가동 - 레이더 탐색 개시");
        IsRunning = true;
        RefreshStatus();
        _timer.Start();
    }

    // ---- 컬렉션 (레이더 / 목록 바인딩) ----
    public ObservableCollection<Target> Targets => _engine.Targets;
    public ObservableCollection<Launcher> Launchers => _engine.Launchers;
    public ObservableCollection<Interceptor> Interceptors => _engine.Interceptors;
    public ObservableCollection<Explosion> Explosions => _engine.Explosions;
    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<ProcessStep> ProcessSteps { get; }

    // ---- 선택 표적 ----
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EngageCommand))]
    private Target? _selectedTarget;

    partial void OnSelectedTargetChanged(Target? oldValue, Target? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        UpdateProcess();
    }

    // ---- 상태 표시 ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunButtonText), nameof(RunStateText))]
    private bool _isRunning;

    [ObservableProperty] private double _sweepAngle;
    [ObservableProperty] private double _scanProgress;
    [ObservableProperty] private int _scanCount;
    [ObservableProperty] private string _simTimeText = "T+00:00";
    [ObservableProperty] private int _trackCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _threatCount;
    [ObservableProperty] private int _engagingCount;
    [ObservableProperty] private int _inFlightCount;
    [ObservableProperty] private int _killCount;
    [ObservableProperty] private int _missCount;
    [ObservableProperty] private int _leakCount;
    [ObservableProperty] private int _firedCount;
    [ObservableProperty] private int _detectedTotal;
    [ObservableProperty] private string _selectedSummary = "";

    public string RunButtonText => IsRunning ? "❚❚ 일시정지" : "▶ 시작";
    public string RunStateText => IsRunning ? "RUNNING" : "PAUSED";
    public string TimeScaleText => $"배속 x{_engine.TimeScale:0}";
    public string AutoEngageText => AutoEngage ? "자동교전 ON" : "자동교전 OFF";
    public string RadarInfoText =>
        $"스캔 주기 {SimulationEngine.SweepPeriodSec:0.0}s · 탐지거리 {RadarGeometry.MaxRangeKm:0}km";

    public bool AutoEngage
    {
        get => _engine.AutoEngage;
        set
        {
            if (_engine.AutoEngage == value) return;
            _engine.AutoEngage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoEngageText));
            AddLog(LogLevel.System, value ? "자동 교전 모드 활성화 (HIGH 위협 자동 요격)" : "자동 교전 모드 해제 (수동 교전)");
        }
    }

    // =====================================================================
    //  갱신 루프
    // =====================================================================
    private void OnTick(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = now - _lastTick;
        _lastTick = now;

        if (!IsRunning) return;

        _engine.Tick(dt);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        SweepAngle = _engine.SweepAngle;
        ScanProgress = _engine.SweepAngle / 360.0 * 100.0;
        ScanCount = _engine.ScanCount;
        SimTimeText = LogEntry.FormatSimTime(_engine.SimTime);

        int track = 0, warn = 0, threat = 0, engaging = 0;
        foreach (var t in Targets)
        {
            if (!t.IsActive) continue;
            track++;
            if (t.Threat == ThreatLevel.Medium) warn++;
            if (t.Threat == ThreatLevel.High) threat++;
            if (t.Status == TargetStatus.Engaging) engaging++;
        }
        TrackCount = track;
        WarningCount = warn;
        ThreatCount = threat;
        EngagingCount = engaging;
        InFlightCount = Interceptors.Count(m => m.IsLaunched);

        KillCount = _engine.Kills;
        MissCount = _engine.Misses;
        LeakCount = _engine.Leaks;
        FiredCount = _engine.InterceptorsFired;
        DetectedTotal = _engine.DetectedTotal;

        UpdateProcess();
        EngageCommand.NotifyCanExecuteChanged();
    }

    /// <summary>선택 표적의 킬체인 진행 단계를 PROCESS 패널에 반영</summary>
    private void UpdateProcess()
    {
        var t = SelectedTarget;
        if (t is null)
        {
            foreach (var s in ProcessSteps)
            {
                s.State = StepState.Pending;
                s.Detail = "-";
            }
            SelectedSummary = "표적을 선택하세요 (TARGET LIST 또는 레이더 화면 클릭)";
            return;
        }

        int cur = (int)t.Stage;
        foreach (var s in ProcessSteps)
            s.State = s.Index < cur ? StepState.Done : s.Index == cur ? StepState.Active : StepState.Pending;

        var result = ProcessSteps[5];
        switch (t.Status)
        {
            case TargetStatus.Destroyed:
                result.State = StepState.Done;
                break;
            case TargetStatus.Leaked:
                result.State = StepState.Failed;
                break;
        }

        ProcessSteps[0].Detail = $"{t.Bearing:000}° / {t.Range:0}km";
        ProcessSteps[1].Detail = t.IsIdentified ? t.TypeName : "식별 중";
        ProcessSteps[2].Detail = t.ThreatEvaluated ? t.ThreatText : "-";
        ProcessSteps[3].Detail = t.Stage >= ProcessStage.WeaponAssign && t.AssignedLauncher is not null
            ? t.AssignedLauncher.Id : "-";
        ProcessSteps[4].Detail = t.Stage == ProcessStage.Intercept ? "비행 중"
            : t.Stage == ProcessStage.WeaponAssign ? "발사 준비" : t.Status == TargetStatus.Destroyed ? "명중" : "-";
        result.Detail = t.Status switch
        {
            TargetStatus.Destroyed => "격추",
            TargetStatus.Leaked => "침투",
            TargetStatus.Lost => "소실",
            _ => "-"
        };

        SelectedSummary =
            $"{t.Id} · {t.TypeName} · {t.StatusText} · 방위 {t.Bearing:000}° · 거리 {t.Range:0.0}km · 위협 {t.ThreatText}";
    }

    private void OnTargetRemoved(Target t)
    {
        if (ReferenceEquals(SelectedTarget, t)) SelectedTarget = null;
    }

    private void AddLog(LogLevel level, string message)
    {
        Logs.Insert(0, new LogEntry(++_logSeq, DateTime.Now, _engine.SimTime, level, message));
        while (Logs.Count > MaxLogEntries) Logs.RemoveAt(Logs.Count - 1);
    }

    // =====================================================================
    //  커맨드
    // =====================================================================
    [RelayCommand]
    private void ToggleRun()
    {
        IsRunning = !IsRunning;
        AddLog(LogLevel.System, IsRunning ? "시뮬레이션 재개" : "시뮬레이션 일시정지");
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedTarget = null;
        _engine.Reset();
        Logs.Clear();
        _logSeq = 0;
        AddLog(LogLevel.System, "시뮬레이션 초기화 - 레이더 탐색 재개시");
        RefreshStatus();
    }

    [RelayCommand]
    private void CycleTimeScale()
    {
        int idx = Array.IndexOf(TimeScales, _engine.TimeScale);
        _engine.TimeScale = TimeScales[(idx + 1) % TimeScales.Length];
        OnPropertyChanged(nameof(TimeScaleText));
        AddLog(LogLevel.System, $"시뮬레이션 배속 x{_engine.TimeScale:0}");
    }

    [RelayCommand]
    private void Spawn(TargetType type) => _engine.Spawn(type);

    [RelayCommand(CanExecute = nameof(CanEngage))]
    private void Engage()
    {
        if (SelectedTarget is not null) _engine.Engage(SelectedTarget, manual: true);
    }

    private bool CanEngage() => SelectedTarget is { IsActive: true } t && t.Status != TargetStatus.Engaging;

    [RelayCommand]
    private void SelectTarget(Target? target) => SelectedTarget = target;

    [RelayCommand]
    private void ClearSelection() => SelectedTarget = null;

    [RelayCommand]
    private void ClearLog() => Logs.Clear();
}
