namespace HocusFocus.Models;

/// <summary>
/// 일별 앱 사용 시간 기록
/// </summary>
public class DailyRecord
{
    public DateOnly Date { get; set; }
    public Dictionary<string, long> AppSeconds { get; set; } = new();
    public long SlackingSeconds { get; set; } = 0;  // 딴짓시간
    public long IdleSeconds { get; set; } = 0;      // 잠수시간

    public DailyRecord() 
    {
        Date = DateOnly.FromDateTime(DateTime.Today);
    }

    public DailyRecord(DateOnly date)
    {
        Date = date;
    }

    /// <summary>
    /// 특정 앱의 사용 시간(초) 추가
    /// </summary>
    public void AddTime(string processName, long seconds)
    {
        if (AppSeconds.ContainsKey(processName))
            AppSeconds[processName] += seconds;
        else
            AppSeconds[processName] = seconds;
    }

    /// <summary>
    /// 딴짓시간 추가
    /// </summary>
    public void AddSlackingTime(long seconds)
    {
        SlackingSeconds += seconds;
    }

    /// <summary>
    /// 잠수시간 추가
    /// </summary>
    public void AddIdleTime(long seconds)
    {
        IdleSeconds += seconds;
    }

    /// <summary>
    /// 특정 앱의 총 사용 시간(초) 반환
    /// </summary>
    public long GetAppTime(string processName)
    {
        return AppSeconds.TryGetValue(processName, out var seconds) ? seconds : 0;
    }

    /// <summary>
    /// 총 집중 시간(초) 반환
    /// </summary>
    public long GetTotalTime()
    {
        return AppSeconds.Values.Sum();
    }

    /// <summary>
    /// 총 집중 시간을 TimeSpan으로 반환
    /// </summary>
    public TimeSpan GetTotalTimeSpan()
    {
        return TimeSpan.FromSeconds(GetTotalTime());
    }
}

