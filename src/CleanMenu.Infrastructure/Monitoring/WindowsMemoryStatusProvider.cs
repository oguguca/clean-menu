using System.ComponentModel;
using System.Runtime.InteropServices;
using CleanMenu.Core.Monitoring;
using CleanMenu.Infrastructure.Native;

namespace CleanMenu.Infrastructure.Monitoring;

/// <summary>Physical memory from GlobalMemoryStatusEx (same source as Task Manager).</summary>
public sealed class WindowsMemoryStatusProvider : IMemoryStatusProvider
{
    public MemoryStatus GetStatus()
    {
        var status = new NativeMethods.MemoryStatusEx { Length = (uint)Marshal.SizeOf<NativeMethods.MemoryStatusEx>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new MemoryStatus(checked((long)status.TotalPhys), checked((long)status.AvailPhys));
    }
}
