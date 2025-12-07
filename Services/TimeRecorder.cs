using System.Windows.Threading;
using HocusFocus.Models;

namespace HocusFocus.Services;

/// <summary>
/// 시간 기록을 관리하는 메인 서비스
/// </summary>
public class TimeRecorder : IDisposable
{
    private readonly WindowTracker _windowTracker;
    private readonly IdleDetector _idleDetector;
    private readonly DataStorage _dataStorage;
    private readonly DispatcherTimer _timer;
    
    private AppData _appData;
    private string? _currentTrackedProcess;
    private DateTime _lastSaveTime;
    private bool _isTracking;
    
    // 딴짓시간, 잠수시간 (오늘 기록에서 로드)
    private long _slackingSeconds;
    private long _idleSeconds;

    public event Action<string?, bool>? TrackingStatusChanged;
    public event Action<long>? TodayTimeUpdated;
    public event Action<long, long>? SessionTimesUpdated;

    public TimeRecorder()
    {
        _windowTracker = new WindowTracker();
        _idleDetector = new IdleDetector();
        _dataStorage = new DataStorage();
        _appData = new AppData();
        _lastSaveTime = DateTime.Now;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
    }

    public AppData AppData => _appData;
    public bool IsTracking => _isTracking;
    public string? CurrentTrackedProcess => _currentTrackedProcess;
    public long SlackingSeconds => _slackingSeconds;
    public long IdleSeconds => _idleSeconds;

    /// <summary>
    /// 데이터 로드 및 추적 시작
    /// </summary>
    public async Task StartAsync()
    {
        _appData = await _dataStorage.LoadAsync();
        
        // 오늘의 딴짓/잠수 시간 로드
        var todayRecord = _appData.GetTodayRecord();
        _slackingSeconds = todayRecord.SlackingSeconds;
        _idleSeconds = todayRecord.IdleSeconds;
        
        _isTracking = true;
        _timer.Start();
    }

    /// <summary>
    /// 추적 중지
    /// </summary>
    public void Stop()
    {
        _isTracking = false;
        _timer.Stop();
        _dataStorage.Save(_appData);
    }

    /// <summary>
    /// 추적 일시정지/재개
    /// </summary>
    public void TogglePause()
    {
        _isTracking = !_isTracking;
        if (!_isTracking)
        {
            _currentTrackedProcess = null;
            TrackingStatusChanged?.Invoke(null, false);
        }
    }

    /// <summary>
    /// 앱 추가
    /// </summary>
    public void AddTrackedApp(string processName, string displayName)
    {
        if (_appData.TrackedApps.Any(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase)))
            return;

        _appData.TrackedApps.Add(new TrackedApp(processName, displayName));
        SaveAsync();
    }

    /// <summary>
    /// 앱 제거
    /// </summary>
    public void RemoveTrackedApp(string processName)
    {
        var app = _appData.TrackedApps.FirstOrDefault(a => 
            a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        
        if (app != null)
        {
            _appData.TrackedApps.Remove(app);
            SaveAsync();
        }
    }

    /// <summary>
    /// 유휴 시간 임계값 설정 (초)
    /// </summary>
    public void SetIdleThreshold(int seconds)
    {
        _appData.IdleThresholdSeconds = seconds;
        SaveAsync();
    }

    /// <summary>
    /// 닫을 때 트레이로 최소화 설정
    /// </summary>
    public void SetMinimizeToTrayOnClose(bool value)
    {
        _appData.MinimizeToTrayOnClose = value;
        SaveAsync();
    }

    /// <summary>
    /// 미니 모드 불투명도 설정 (10~100)
    /// </summary>
    public void SetMiniModeOpacity(int value)
    {
        _appData.MiniModeOpacityPercent = Math.Clamp(value, 10, 100);
        SaveAsync();
    }

    /// <summary>
    /// 오늘의 총 집중 시간 가져오기
    /// </summary>
    public TimeSpan GetTodayTotalTime()
    {
        return _appData.GetTodayRecord().GetTotalTimeSpan();
    }

    /// <summary>
    /// 오늘의 앱별 사용 시간 가져오기
    /// </summary>
    public Dictionary<string, TimeSpan> GetTodayAppTimes()
    {
        var record = _appData.GetTodayRecord();
        return record.AppSeconds.ToDictionary(
            kvp => kvp.Key,
            kvp => TimeSpan.FromSeconds(kvp.Value)
        );
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!_isTracking)
            return;

        var activeProcess = _windowTracker.GetActiveProcessName();
        var isIdle = _idleDetector.IsIdle(_appData.IdleThresholdSeconds);

        // 유휴 상태면 잠수시간 증가
        if (isIdle)
        {
            _idleSeconds++;
            _appData.GetTodayRecord().AddIdleTime(1);
            SessionTimesUpdated?.Invoke(_slackingSeconds, _idleSeconds);
            
            // 항상 유휴 상태 알림 (볼드 표시용)
            if (_currentTrackedProcess != null)
            {
                _currentTrackedProcess = null;
            }
            TrackingStatusChanged?.Invoke(null, true);
            return;
        }

        // 추적 대상 앱인지 확인
        var isTrackedApp = activeProcess != null && 
            _appData.TrackedApps.Any(a => 
                a.IsEnabled && 
                a.ProcessName.Equals(activeProcess, StringComparison.OrdinalIgnoreCase));

        if (isTrackedApp && activeProcess != null)
        {
            // 시간 기록
            _appData.GetTodayRecord().AddTime(activeProcess, 1);
            
            if (_currentTrackedProcess != activeProcess)
            {
                _currentTrackedProcess = activeProcess;
                TrackingStatusChanged?.Invoke(activeProcess, false);
            }

            TodayTimeUpdated?.Invoke(_appData.GetTodayRecord().GetTotalTime());
        }
        else
        {
            // 추적 대상이 아닌 앱 사용 중 = 딴짓시간
            _slackingSeconds++;
            _appData.GetTodayRecord().AddSlackingTime(1);
            SessionTimesUpdated?.Invoke(_slackingSeconds, _idleSeconds);
            
            if (_currentTrackedProcess != null)
            {
                _currentTrackedProcess = null;
                TrackingStatusChanged?.Invoke(null, false);
            }
        }

        // 30초마다 자동 저장
        if ((DateTime.Now - _lastSaveTime).TotalSeconds >= 30)
        {
            SaveAsync();
            _lastSaveTime = DateTime.Now;
        }
    }

    private async void SaveAsync()
    {
        await _dataStorage.SaveAsync(_appData);
    }

    public void Dispose()
    {
        _timer.Stop();
        _dataStorage.Save(_appData);
    }
}

