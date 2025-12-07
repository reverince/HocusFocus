using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HocusFocus.Services;

/// <summary>
/// 현재 활성 창 정보를 추적하는 서비스
/// </summary>
public class WindowTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    /// <summary>
    /// 현재 활성 창의 프로세스 이름 가져오기
    /// </summary>
    public string? GetActiveProcessName()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            GetWindowThreadProcessId(hwnd, out int processId);
            
            if (processId == 0)
                return null;

            var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 현재 활성 창의 제목 가져오기
    /// </summary>
    public string? GetActiveWindowTitle()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            var sb = new StringBuilder(256);
            if (GetWindowText(hwnd, sb, 256) > 0)
                return sb.ToString();

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 현재 실행 중인 모든 프로세스 목록 가져오기 (창이 있는 프로세스만)
    /// </summary>
    public List<(string ProcessName, string Title)> GetRunningProcesses()
    {
        var result = new List<(string ProcessName, string Title)>();
        var seen = new HashSet<string>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero && 
                    !string.IsNullOrEmpty(process.MainWindowTitle) &&
                    !seen.Contains(process.ProcessName))
                {
                    seen.Add(process.ProcessName);
                    result.Add((process.ProcessName, process.MainWindowTitle));
                }
            }
            catch
            {
                // 접근 권한 없는 프로세스 무시
            }
        }

        return result.OrderBy(x => x.ProcessName).ToList();
    }
}

