using CleanMenu.Core.Startup;

namespace CleanMenu.Infrastructure.Startup;

/// <summary>
/// Reads scheduled tasks that start at logon through the Task Scheduler COM API (read-only).
/// Windows' own tasks (under \Microsoft) are skipped: they are part of the system.
/// </summary>
internal static class LogonTaskReader
{
    private const int LogonTrigger = 9;   // TASK_TRIGGER_LOGON
    private const int ExecAction = 0;     // TASK_ACTION_EXEC

    public static IReadOnlyList<StartupEntry> Read()
    {
        var result = new List<StartupEntry>();
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type is null)
            {
                return result;
            }

            dynamic service = Activator.CreateInstance(type)!;
            service.Connect();
            Visit(service.GetFolder("\\"), result, depth: 0);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException
                                       or UnauthorizedAccessException or InvalidCastException)
        {
            // Task Scheduler unavailable: tasks are simply not listed.
        }

        return result;
    }

    private static void Visit(dynamic folder, List<StartupEntry> result, int depth)
    {
        string path = folder.Path;
        if (depth > 8 || path.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            foreach (dynamic task in folder.GetTasks(0))
            {
                if (!HasLogonTrigger(task.Definition))
                {
                    continue;
                }

                string command = string.Empty;
                string? exe = null;
                foreach (dynamic action in task.Definition.Actions)
                {
                    if ((int)action.Type == ExecAction)
                    {
                        exe = Environment.ExpandEnvironmentVariables((string)action.Path).Trim('"');
                        command = $"{action.Path} {action.Arguments}".Trim();
                        break;
                    }
                }

                result.Add(new StartupEntry((string)task.Name, command, exe, WindowsStartupManager.Publisher(exe), StartupSource.ScheduledTask, (bool)task.Enabled));
            }

            foreach (dynamic sub in folder.GetFolders(0))
            {
                Visit(sub, result, depth + 1);
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            // Folder not readable for this user.
        }
    }

    private static bool HasLogonTrigger(dynamic definition)
    {
        foreach (dynamic trigger in definition.Triggers)
        {
            if ((int)trigger.Type == LogonTrigger)
            {
                return true;
            }
        }

        return false;
    }
}
