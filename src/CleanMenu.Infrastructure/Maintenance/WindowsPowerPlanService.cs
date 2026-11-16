using System.Runtime.InteropServices;
using CleanMenu.Core.Maintenance;

namespace CleanMenu.Infrastructure.Maintenance;

/// <summary>Power plans through the Windows power API (powrprof.dll). No admin rights or hidden processes.</summary>
public sealed partial class WindowsPowerPlanService : IPowerPlanService
{
    private const uint AccessScheme = 16;
    private const uint NoMoreItems = 259;

    public Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<PowerPlan>>(() =>
        {
            var active = ActiveScheme();
            var plans = new List<PowerPlan>();
            for (uint index = 0; ; index++)
            {
                var buffer = new byte[16];
                var size = (uint)buffer.Length;
                var status = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, AccessScheme, index, buffer, ref size);
                if (status == NoMoreItems || status != 0)
                {
                    break;
                }

                var id = new Guid(buffer);
                plans.Add(new PowerPlan(id, FriendlyName(id) ?? id.ToString(), id == active));
            }

            return plans;
        }, cancellationToken);

    public Task<bool> ActivateAsync(Guid planId, CancellationToken cancellationToken = default) =>
        Task.Run(() => PowerSetActiveScheme(IntPtr.Zero, ref planId) == 0, cancellationToken);

    private static Guid? ActiveScheme()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var pointer) != 0)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStructure<Guid>(pointer);
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    private static string? FriendlyName(Guid scheme)
    {
        uint size = 0;
        if (PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, null, ref size) != 0 || size == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        return PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size) == 0
            ? System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0')
            : null;
    }

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerEnumerate(IntPtr rootKey, IntPtr schemeGuid, IntPtr subGroup, uint accessFlags, uint index, [Out] byte[] buffer, ref uint bufferSize);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerReadFriendlyName(IntPtr rootKey, ref Guid schemeGuid, IntPtr subGroup, IntPtr setting, [Out] byte[]? buffer, ref uint bufferSize);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerGetActiveScheme(IntPtr rootKey, out IntPtr activePolicy);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerSetActiveScheme(IntPtr rootKey, ref Guid schemeGuid);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr memory);
}
