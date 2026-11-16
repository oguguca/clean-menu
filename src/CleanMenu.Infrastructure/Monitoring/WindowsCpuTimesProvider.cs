using System.ComponentModel;
using System.Runtime.InteropServices;
using CleanMenu.Core.Monitoring;
using CleanMenu.Infrastructure.Native;

namespace CleanMenu.Infrastructure.Monitoring;

/// <summary>System CPU times from GetSystemTimes. No admin rights or performance counters needed.</summary>
public sealed class WindowsCpuTimesProvider : ICpuTimesProvider
{
    public CpuTimes GetTimes()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new CpuTimes(idle.Value, kernel.Value, user.Value);
    }
}
