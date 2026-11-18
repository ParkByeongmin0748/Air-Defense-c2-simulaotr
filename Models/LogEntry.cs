namespace AirDefenseC2.Models;

/// <summary>이벤트 로그 항목</summary>
public sealed class LogEntry
{
    public LogEntry(int seq, DateTime wallTime, double simTime, LogLevel level, string message)
    {
        Seq = seq;
        WallTime = wallTime.ToString("HH:mm:ss");
        SimTime = FormatSimTime(simTime);
        Level = level;
        Message = message;
    }

    public int Seq { get; }
    public string WallTime { get; }
    public string SimTime { get; }
    public LogLevel Level { get; }
    public string Message { get; }

    public string LevelText => Level switch
    {
        LogLevel.System => "SYS",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Critical => "ALERT",
        LogLevel.Success => "KILL",
        _ => "-"
    };

    public static string FormatSimTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"T+{(int)ts.TotalMinutes:00}:{ts.Seconds:00}";
    }
}
