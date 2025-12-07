namespace HocusFocus.Models;

/// <summary>
/// 추적 대상 애플리케이션 정보
/// </summary>
public class TrackedApp
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public bool IsEnabled { get; set; } = true;

    public TrackedApp() { }

    public TrackedApp(string processName, string displayName)
    {
        ProcessName = processName;
        DisplayName = displayName;
    }
}

