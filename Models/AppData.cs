namespace HocusFocus.Models;

/// <summary>
/// 전체 앱 데이터 (저장/로드용)
/// </summary>
public class AppData
{
    public List<TrackedApp> TrackedApps { get; set; } = new();
    public int IdleThresholdSeconds { get; set; } = 5; // 기본 5초 (잠수 감지 시간)
    public bool MinimizeToTrayOnClose { get; set; } = false; // 닫을 때 트레이로 최소화 (기본값: false = 완전 종료)
    public int MiniModeOpacityPercent { get; set; } = 50; // 미니 모드 불투명도 (10 ~ 100)
    public double? MiniModeLeft { get; set; } = null; // 미니 모드 창 위치 X
    public double? MiniModeTop { get; set; } = null; // 미니 모드 창 위치 Y
    public Dictionary<string, DailyRecord> DailyRecords { get; set; } = new();

    /// <summary>
    /// 오늘의 기록 가져오기 (없으면 생성)
    /// </summary>
    public DailyRecord GetTodayRecord()
    {
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        
        if (!DailyRecords.ContainsKey(today))
        {
            DailyRecords[today] = new DailyRecord(DateOnly.FromDateTime(DateTime.Today));
        }
        
        return DailyRecords[today];
    }

    /// <summary>
    /// 특정 날짜의 기록 가져오기
    /// </summary>
    public DailyRecord? GetRecord(DateOnly date)
    {
        var key = date.ToString("yyyy-MM-dd");
        return DailyRecords.TryGetValue(key, out var record) ? record : null;
    }

    /// <summary>
    /// 최근 N일간의 기록 가져오기
    /// </summary>
    public List<DailyRecord> GetRecentRecords(int days)
    {
        var result = new List<DailyRecord>();
        var today = DateOnly.FromDateTime(DateTime.Today);

        for (int i = 0; i < days; i++)
        {
            var date = today.AddDays(-i);
            var record = GetRecord(date);
            if (record != null)
            {
                result.Add(record);
            }
        }

        return result;
    }
}

