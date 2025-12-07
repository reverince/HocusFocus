using System.Runtime.InteropServices;

namespace HocusFocus.Services;

/// <summary>
/// 사용자 잠수 시간을 감지하는 서비스
/// </summary>
public class IdleDetector
{
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    /// <summary>
    /// 마지막 사용자 입력 이후 경과 시간(초) 반환
    /// </summary>
    public int GetIdleTimeSeconds()
    {
        var lastInput = new LASTINPUTINFO();
        lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);

        if (!GetLastInputInfo(ref lastInput))
            return 0;

        var idleTime = Environment.TickCount - (int)lastInput.dwTime;
        return Math.Max(0, idleTime / 1000);
    }

    /// <summary>
    /// 지정된 시간(초) 이상 잠수 상태인지 확인
    /// </summary>
    public bool IsIdle(int thresholdSeconds)
    {
        return GetIdleTimeSeconds() >= thresholdSeconds;
    }
}

