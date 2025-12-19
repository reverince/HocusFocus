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
    private string? _currentTrackedProcess = "__INIT__"; // 초기값 (첫 번째 틱에서 상태 이벤트 발생 보장)
    private DateTime _lastSaveTime;
    private bool _isTracking;
    private bool _wasIdle = false; // 이전 틱에서 잠수 상태였는지 추적
    private DateOnly _currentDate; // 현재 추적 중인 날짜 (자정 감지용)
    
    // 딴짓시간, 잠수시간 (오늘 기록에서 로드)
    private long _slackingSeconds;
    private long _idleSeconds;
    
    // 자정(날짜 변경) 이벤트
    public event Action? DayChanged;

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
        _currentDate = DateOnly.FromDateTime(DateTime.Today);
        
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
            _wasIdle = false;
            TrackingStatusChanged?.Invoke(null, false);
        }
        else
        {
            // 재개 시 상태 초기화 (다음 틱에서 상태 이벤트 발생 보장)
            _currentTrackedProcess = "__INIT__";
            _wasIdle = false;
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
    /// 잠수 감지 시간 임계값 설정 (초)
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
    /// 미니 모드 창 위치 설정
    /// </summary>
    public void SetMiniModePosition(double left, double top)
    {
        _appData.MiniModeLeft = left;
        _appData.MiniModeTop = top;
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

        // 자정 감지: 날짜가 바뀌면 딴짓/잠수 시간 리셋
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today != _currentDate)
        {
            _currentDate = today;
            _slackingSeconds = 0;
            _idleSeconds = 0;
            _currentTrackedProcess = "__INIT__"; // 상태 초기화
            _wasIdle = false;
            DayChanged?.Invoke();
            return;
        }

        var activeProcess = _windowTracker.GetActiveProcessName();
        var isIdle = _idleDetector.IsIdle(_appData.IdleThresholdSeconds);

        // 잠수 감지
        if (isIdle)
        {
            var wasIdleBefore = _wasIdle;
            
            // 잠수 상태로 전환될 때: 잠수 감지 시간만큼 한 번에 증가
            if (!wasIdleBefore)
            {
                var thresholdSeconds = _appData.IdleThresholdSeconds;
                
                // 이전 상태에 따라 시간 보정 (잠수 감지 시간만큼 이미 잠수였으므로)
                if (_currentTrackedProcess != null && _currentTrackedProcess != "__INIT__")
                {
                    // 집중 중이었음 → 해당 앱 시간에서 감소
                    _appData.GetTodayRecord().SubtractTime(_currentTrackedProcess, thresholdSeconds);
                    TodayTimeUpdated?.Invoke(_appData.GetTodayRecord().GetTotalTime());
                }
                else if (_currentTrackedProcess == null)
                {
                    // 딴짓 중이었음 → 딴짓 타이머 감소
                    _slackingSeconds = Math.Max(0, _slackingSeconds - thresholdSeconds);
                    _appData.GetTodayRecord().SubtractSlackingTime(thresholdSeconds);
                }
                // _currentTrackedProcess == "__INIT__"인 경우는 초기 상태이므로 감소 없음
                
                _idleSeconds += thresholdSeconds;
                _appData.GetTodayRecord().AddIdleTime(thresholdSeconds);
                _wasIdle = true;
                
                _currentTrackedProcess = null;
                TrackingStatusChanged?.Invoke(null, true);
            }
            else
            {
                // 잠수 상태 유지 중: 1초씩 증가
                _idleSeconds++;
                _appData.GetTodayRecord().AddIdleTime(1);
            }
            
            SessionTimesUpdated?.Invoke(_slackingSeconds, _idleSeconds);
            return;
        }
        
        var wasIdleBeforeThisTick = _wasIdle;
        _wasIdle = false;

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
            
            // 상태 변경 감지: 집중/잠수 → 딴짓 전환 시 이벤트 발생
            var needsUpdate = _currentTrackedProcess != null || wasIdleBeforeThisTick;
            _currentTrackedProcess = null;
            
            if (needsUpdate)
            {
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

