using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HocusFocus.Models;
using HocusFocus.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace HocusFocus.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly TimeRecorder _timeRecorder;
    private readonly WindowTracker _windowTracker;

    [ObservableProperty]
    private string _todayTime = "00:00:00";

    [ObservableProperty]
    private string _slackingTime = "00:00:00";

    [ObservableProperty]
    private string _idleTime = "00:00:00";

    // 시간 비율 (Grid Width용)
    [ObservableProperty]
    private double _focusRatio = 1;

    [ObservableProperty]
    private double _slackingRatio = 0;

    [ObservableProperty]
    private double _idleRatio = 0;

    // 미니 모드
    [ObservableProperty]
    private bool _isMiniMode = false;

    [ObservableProperty]
    private string _currentStatus = "대기 중";

    // 현재 추적 유형 (볼드 표시용)
    [ObservableProperty]
    private bool _isFocusing = false;

    [ObservableProperty]
    private bool _isSlacking = false;

    [ObservableProperty]
    private bool _isIdling = false;

    [ObservableProperty]
    private bool _isTracking = true;

    [ObservableProperty]
    private int _idleThresholdSeconds = 5;

    [ObservableProperty]
    private bool _minimizeToTrayOnClose = false;

    // 미니 모드 불투명도 (UI용: 0.0~1.0)
    [ObservableProperty]
    private double _miniModeOpacity = 0.5;

    // 미니 모드 불투명도 (설정용: 10~100)
    [ObservableProperty]
    private int _miniModeOpacityPercent = 50;

    // 미니 모드 창 위치
    public double? MiniModeLeft => _timeRecorder.AppData.MiniModeLeft;
    public double? MiniModeTop => _timeRecorder.AppData.MiniModeTop;

    [ObservableProperty]
    private ObservableCollection<TrackedAppViewModel> _trackedApps = new();

    [ObservableProperty]
    private ObservableCollection<RunningAppViewModel> _runningApps = new();

    [ObservableProperty]
    private ObservableCollection<AppTimeViewModel> _todayAppTimes = new();

    [ObservableProperty]
    private ISeries[] _weeklyChartSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _weeklyChartXAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _weeklyChartYAxes = Array.Empty<Axis>();

    // 차트 범례 텍스트 색상
    public SolidColorPaint WeeklyChartLegendPaint { get; } = new SolidColorPaint(SKColor.Parse("#94a3b8"));


    public MainViewModel()
    {
        _timeRecorder = new TimeRecorder();
        _windowTracker = new WindowTracker();

        _timeRecorder.TrackingStatusChanged += OnTrackingStatusChanged;
        _timeRecorder.TodayTimeUpdated += OnTodayTimeUpdated;
        _timeRecorder.SessionTimesUpdated += OnSessionTimesUpdated;
    }

    public async Task InitializeAsync()
    {
        await _timeRecorder.StartAsync();
        
        IdleThresholdSeconds = _timeRecorder.AppData.IdleThresholdSeconds;
        MinimizeToTrayOnClose = _timeRecorder.AppData.MinimizeToTrayOnClose;
        MiniModeOpacityPercent = _timeRecorder.AppData.MiniModeOpacityPercent;
        MiniModeOpacity = MiniModeOpacityPercent / 100.0;
        
        // 오늘의 딴짓/잠수 시간 로드
        SlackingTime = TimeSpan.FromSeconds(_timeRecorder.SlackingSeconds).ToString(@"hh\:mm\:ss");
        IdleTime = TimeSpan.FromSeconds(_timeRecorder.IdleSeconds).ToString(@"hh\:mm\:ss");
        UpdateTimeRatios(_timeRecorder.SlackingSeconds, _timeRecorder.IdleSeconds);
        
        RefreshTrackedApps();
        RefreshRunningApps();
        RefreshTodayAppTimes();
        RefreshWeeklyChart();
        UpdateTodayTime();
    }

    private void OnTrackingStatusChanged(string? processName, bool isIdle)
    {
        // 추적 유형 업데이트
        IsFocusing = false;
        IsSlacking = false;
        IsIdling = false;
        
        if (isIdle)
        {
            IsIdling = true;
            CurrentStatus = "⏸️ 잠수 상태";
        }
        else if (processName != null)
        {
            IsFocusing = true;
            var app = _timeRecorder.AppData.TrackedApps
                .FirstOrDefault(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            CurrentStatus = $"🎯 {app?.DisplayName ?? processName}";
        }
        else
        {
            IsSlacking = true;
            CurrentStatus = "⏹️ 추적 대상 아님";
        }
    }

    private void OnTodayTimeUpdated(long totalSeconds)
    {
        UpdateTodayTime();
        RefreshTodayAppTimes();
        RefreshWeeklyChart();
    }

    private void OnSessionTimesUpdated(long slackingSeconds, long idleSeconds)
    {
        SlackingTime = TimeSpan.FromSeconds(slackingSeconds).ToString(@"hh\:mm\:ss");
        IdleTime = TimeSpan.FromSeconds(idleSeconds).ToString(@"hh\:mm\:ss");
        UpdateTimeRatios(slackingSeconds, idleSeconds);
    }

    private void UpdateTimeRatios(long slackingSeconds, long idleSeconds)
    {
        var focusSeconds = (long)_timeRecorder.GetTodayTotalTime().TotalSeconds;
        var total = focusSeconds + slackingSeconds + idleSeconds;
        
        if (total > 0)
        {
            FocusRatio = Math.Max(0.01, (double)focusSeconds / total);
            SlackingRatio = (double)slackingSeconds / total;
            IdleRatio = (double)idleSeconds / total;
        }
        else
        {
            FocusRatio = 1;
            SlackingRatio = 0;
            IdleRatio = 0;
        }
    }

    private void UpdateTodayTime()
    {
        var time = _timeRecorder.GetTodayTotalTime();
        TodayTime = time.ToString(@"hh\:mm\:ss");
    }

    [RelayCommand]
    private void ToggleTracking()
    {
        _timeRecorder.TogglePause();
        IsTracking = _timeRecorder.IsTracking;
        
        if (!IsTracking)
        {
            CurrentStatus = "⏸️ 일시정지됨";
            IsFocusing = false;
            IsSlacking = false;
            IsIdling = false;
        }
        else
        {
            CurrentStatus = "대기 중";
        }
    }

    [RelayCommand]
    private void RefreshRunningApps()
    {
        var processes = _windowTracker.GetRunningProcesses();
        RunningApps.Clear();
        
        foreach (var (processName, title) in processes)
        {
            var isTracked = _timeRecorder.AppData.TrackedApps
                .Any(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            
            RunningApps.Add(new RunningAppViewModel
            {
                ProcessName = processName,
                Title = title,
                IsTracked = isTracked
            });
        }
    }

    [RelayCommand]
    private void AddTrackedApp(RunningAppViewModel? app)
    {
        if (app == null) return;
        
        _timeRecorder.AddTrackedApp(app.ProcessName, app.ProcessName);
        app.IsTracked = true;
        RefreshTrackedApps();
    }

    [RelayCommand]
    private void RemoveTrackedApp(TrackedAppViewModel? app)
    {
        if (app == null) return;
        
        _timeRecorder.RemoveTrackedApp(app.ProcessName);
        RefreshTrackedApps();
        RefreshRunningApps();
    }

    partial void OnIdleThresholdSecondsChanged(int value)
    {
        _timeRecorder.SetIdleThreshold(value);
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        _timeRecorder.SetMinimizeToTrayOnClose(value);
    }

    partial void OnMiniModeOpacityPercentChanged(int value)
    {
        var clampedValue = Math.Clamp(value, 10, 100);
        MiniModeOpacity = clampedValue / 100.0;
        _timeRecorder.SetMiniModeOpacity(clampedValue);
    }

    public void SaveMiniModePosition(double left, double top)
    {
        _timeRecorder.SetMiniModePosition(left, top);
    }

    private void RefreshTrackedApps()
    {
        TrackedApps.Clear();
        foreach (var app in _timeRecorder.AppData.TrackedApps)
        {
            TrackedApps.Add(new TrackedAppViewModel
            {
                ProcessName = app.ProcessName,
                DisplayName = app.DisplayName,
                IsEnabled = app.IsEnabled
            });
        }
    }

    private void RefreshTodayAppTimes()
    {
        var times = _timeRecorder.GetTodayAppTimes();
        TodayAppTimes.Clear();

        foreach (var (processName, time) in times.OrderByDescending(x => x.Value))
        {
            var app = _timeRecorder.AppData.TrackedApps
                .FirstOrDefault(a => a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
            
            TodayAppTimes.Add(new AppTimeViewModel
            {
                AppName = app?.DisplayName ?? processName,
                Time = time.ToString(@"hh\:mm\:ss"),
                TotalSeconds = (long)time.TotalSeconds
            });
        }
    }

    // 앱별 색상 팔레트
    private static readonly string[] AppColors = new[]
    {
        "#8b5cf6", // Purple
        "#6366f1", // Indigo
        "#3b82f6", // Blue
        "#06b6d4", // Cyan
        "#10b981", // Green
        "#f59e0b", // Amber
        "#ef4444", // Red
        "#ec4899", // Pink
        "#f97316", // Orange
        "#84cc16", // Lime
    };

    private void RefreshWeeklyChart()
    {
        var records = _timeRecorder.AppData.GetRecentRecords(7);
        var labels = new List<string>();
        
        // 날짜 레이블 생성
        for (int i = 6; i >= 0; i--)
        {
            var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-i));
            labels.Add(date.ToString("MM/dd"));
        }

        // 모든 앱 이름 수집 (7일간 기록이 있는 앱들)
        var allApps = new HashSet<string>();
        foreach (var record in records)
        {
            foreach (var appName in record.AppSeconds.Keys)
            {
                allApps.Add(appName);
            }
        }

        // 각 앱별로 시리즈 생성
        var seriesList = new List<ISeries>();
        var colorIndex = 0;

        foreach (var appName in allApps.OrderByDescending(app => 
            records.Sum(r => r.AppSeconds.TryGetValue(app, out var s) ? s : 0)))
        {
            var values = new List<double>();
            var hasSignificantTime = false; // 1분 이상 사용한 날이 있는지 확인
            
            for (int i = 6; i >= 0; i--)
            {
                var date = DateOnly.FromDateTime(DateTime.Today.AddDays(-i));
                var record = records.FirstOrDefault(r => r.Date == date);
                var hours = 0.0;
                
                if (record != null && record.AppSeconds.TryGetValue(appName, out var seconds))
                {
                    // 1분(60초) 미만이면 0으로 처리
                    if (seconds >= 60)
                    {
                        hours = seconds / 3600.0;
                        hasSignificantTime = true;
                    }
                }
                
                values.Add(Math.Round(hours, 2));
            }

            // 1분 이상 사용한 날이 하나도 없으면 이 앱은 차트에 표시하지 않음
            if (!hasSignificantTime)
                continue;

            // 앱의 표시 이름 가져오기
            var trackedApp = _timeRecorder.AppData.TrackedApps
                .FirstOrDefault(a => a.ProcessName.Equals(appName, StringComparison.OrdinalIgnoreCase));
            var displayName = trackedApp?.DisplayName ?? appName;
            
            // Name이 비어있으면 프로세스 이름 사용
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = appName;
            }

            var color = AppColors[colorIndex % AppColors.Length];
            var series = new StackedColumnSeries<double>
            {
                Values = values,
                Fill = new SolidColorPaint(SKColor.Parse(color)),
                Stroke = null,
                MaxBarWidth = 35
            };
            // Name 속성을 명시적으로 설정 (LiveCharts2 범례 표시용)
            series.Name = displayName;
            seriesList.Add(series);
            
            colorIndex++;
        }

        // 앱이 없으면 빈 시리즈 추가 (범례 숨김용)
        if (seriesList.Count == 0)
        {
            seriesList.Add(new StackedColumnSeries<double>
            {
                Values = new double[] { 0, 0, 0, 0, 0, 0, 0 },
                Fill = new SolidColorPaint(SKColor.Parse("#6366f1")),
                Stroke = null,
                MaxBarWidth = 35,
                Name = "" // 빈 이름으로 범례 숨김
            });
        }

        WeeklyChartSeries = seriesList.ToArray();

        WeeklyChartXAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94a3b8")),
                TextSize = 12
            }
        };

        WeeklyChartYAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#94a3b8")),
                TextSize = 12,
                Labeler = value => $"{value:0.#}h"
            }
        };
    }

    public void Shutdown()
    {
        _timeRecorder.Stop();
        _timeRecorder.Dispose();
    }
}

public partial class TrackedAppViewModel : ObservableObject
{
    [ObservableProperty]
    private string _processName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;
}

public partial class RunningAppViewModel : ObservableObject
{
    [ObservableProperty]
    private string _processName = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isTracked;
}

public partial class AppTimeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private long _totalSeconds;
}

