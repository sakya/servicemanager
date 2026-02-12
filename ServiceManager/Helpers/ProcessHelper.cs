using System.Diagnostics;
using System.Management;
using Serilog;

namespace ServiceManager.Helpers;

public static class ProcessHelper
{
    public static Process RunProcess(
        string name,
        string command,
        string  args,
        string? workingDir = null,
        string? logPath = null,
        int logFileSize = 32,
        int logFiles = 20)
    {
        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File(
                Path.Join(logPath, $"{name}-.log"),
                rollingInterval: RollingInterval.Day,
                buffered: false,
                fileSizeLimitBytes: logFileSize * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: logFiles);

        var logger = loggerConfig.CreateLogger();

        var path = workingDir ?? Path.GetDirectoryName(Path.GetFullPath(command));
        var si = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WorkingDirectory = path,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };

        var process = new Process();
        process.StartInfo = si;

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                logger.Information(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                logger.Error(eventArgs.Data);
        };

        process.Start();
        process.EnableRaisingEvents = true;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        return process;
    }

    public static async Task KillProcessTree(Process process)
    {
        var processes = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? GetChildProcesses(process)
            : GetChildProcessesLinux(process);
        foreach (var child in processes) {
            await KillProcessTree(child);
        }

        await KillProcess(process);
    }

    public static async Task RunCommand(string command, string? args)
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix) {
            args = $"-lc \"{command} {args} && exit\"";
            command = "bash";
        }

        var si = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        var process = new Process();
        process.StartInfo = si;

        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };

        process.Start();
        process.EnableRaisingEvents = true;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync();
        if (process.ExitCode != 0) {
            Program.Logger?.Warning("Command '{Command} {Args}' exit code: {ProcessExitCode}", command, args, process.ExitCode);
        }
        process.Dispose();
    }

    private static async Task KillProcess(Process process)
    {
        try {
            process.Kill(true);
            await process.WaitForExitAsync();
            process.Dispose();
        } catch (Exception ex) {
            Program.Logger?.Warning("Error killing process with id {ProcessId}: {ExMessage}", process.Id, ex.Message);
        }
    }

    private static IList<Process> GetChildProcesses(Process process)
    {
        try {
            using var mos =
                new ManagementObjectSearcher($"Select * From Win32_Process Where ParentProcessID={process.Id}");
            return mos.Get()
                .Cast<ManagementObject>()
                .Select(mo => Process.GetProcessById(Convert.ToInt32(mo["ProcessID"])))
                .ToList();
        } catch (Exception ex) {
            Program.Logger?.Warning("Error getting child for process with id {ProcessId}: {ExMessage}", process.Id, ex.Message);
            return [];
        }
    }

    private static IList<Process> GetChildProcessesLinux(Process parent)
    {
        var children = new List<Process>();

        foreach (var dir in Directory.EnumerateDirectories("/proc")) {
            var pidStr = Path.GetFileName(dir);
            if (!int.TryParse(pidStr, out var pid)) continue;

            try {
                var statusPath = Path.Combine(dir, "status");
                if (!File.Exists(statusPath)) continue;

                var lines = File.ReadAllLines(statusPath);
                var ppidLine = lines.FirstOrDefault(l => l.StartsWith("PPid:"));
                if (ppidLine == null) continue;

                var parts = ppidLine.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (int.TryParse(parts[1], out var ppid) && ppid == parent.Id) {
                    try {
                        children.Add(Process.GetProcessById(pid));
                    } catch {
                        // ignored
                    }
                }
            } catch {
                // ignored
            }
        }

        return children;
    }
}