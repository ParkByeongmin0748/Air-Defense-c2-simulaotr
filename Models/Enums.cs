namespace AirDefenseC2.Models;

/// <summary>표적 종류</summary>
public enum TargetType
{
    Missile,
    Drone,
    Aircraft
}

/// <summary>위협도</summary>
public enum ThreatLevel
{
    Unknown,
    Low,
    Medium,
    High
}

/// <summary>표적 상태</summary>
public enum TargetStatus
{
    Undetected,   // 아직 레이더에 잡히지 않음
    Detected,     // 최초 탐지
    Tracking,     // 추적 중
    Engaging,     // 교전 중 (요격탄 할당/비행)
    Destroyed,    // 격추
    Leaked,       // 방어지역 침투
    Lost          // 탐지 범위 이탈
}

/// <summary>표적 처리(킬체인) 단계</summary>
public enum ProcessStage
{
    None = 0,
    Detect = 1,
    Identify = 2,
    ThreatEval = 3,
    WeaponAssign = 4,
    Intercept = 5,
    Assess = 6
}

/// <summary>발사대 상태</summary>
public enum LauncherStatus
{
    Ready,
    Engaging,
    Reloading
}

/// <summary>폭발 표시 종류</summary>
public enum ExplosionKind
{
    Kill,    // 요격 성공
    Miss,    // 요격 실패 / 자폭
    Impact   // 방어지역 피격
}

/// <summary>이벤트 로그 등급</summary>
public enum LogLevel
{
    System,
    Info,
    Warning,
    Critical,
    Success
}

/// <summary>프로세스 단계 표시 상태</summary>
public enum StepState
{
    Pending,
    Active,
    Done,
    Failed
}
