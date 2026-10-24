using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace UniversalControl.Windows;

/// <summary>A private high-resolution timer; no system-wide timer-resolution changes.</summary>
public sealed class MouseReportTimer : IDisposable
{
    private readonly SafeWaitHandle _handle;
    private int _interval;
    public nint Handle => _handle.DangerousGetHandle();
    public MouseReportTimer()
    {
        _handle = CreateWaitableTimerEx(0, null, 2, 0x00100002); // High resolution; synchronize + modify.
        if (_handle.IsInvalid) { _handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    public void Start(int intervalMs)
    {
        intervalMs = Math.Clamp(intervalMs, 8, 100);
        if (_interval == intervalMs) return;
        long due = -intervalMs * 10_000L;
        if (!SetWaitableTimerEx(_handle, in due, intervalMs, 0, 0, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        _interval = intervalMs;
    }
    public void Stop() { if (_interval != 0) CancelWaitableTimer(_handle); _interval = 0; }
    public bool Wait(int timeoutMs) => WaitForSingleObject(_handle, (uint)timeoutMs) == 0;
    public void Dispose() { Stop(); _handle.Dispose(); }
    [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimerEx(nint attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetWaitableTimerEx(SafeWaitHandle timer, in long due, int period, nint routine, nint argument, nint context, uint tolerableDelay);
    [DllImport("kernel32.dll")] private static extern bool CancelWaitableTimer(SafeWaitHandle timer);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);
}
