using System.Diagnostics;
using ServiceManager.Models;

namespace ServiceManager.Helpers;

public class ServiceHelper
{
    private readonly Service[] _services;
    private readonly List<Process?> _processes = [];

    public ServiceHelper(Service[] services)
    {
        _services = services;
        for (var i = 0; i < _services.Length; i++) {
            _processes.Add(null);
        }
    }

    public Service[] Services => _services;

    public async Task StartAll()
    {
        foreach (var service in _services) {
            await Start(service);
        }
    }

    public async Task StopAll()
    {
        foreach (var t in _services) {
            await Stop(t);
        }
    }

    public bool IsRunning(Service service)
    {
        var idx = _services.IndexOf(service);
        if (idx < 0) {
            Program.Logger?.Warning("Service {service} not found", service.Name);
            return false;
        }

        var proc = _processes[idx];
        return proc != null && !proc.HasExited;
    }

    public async Task Stop(Service service)
    {
        Console.Write($"Stopping service {service.Name}...");
        var idx = _services.IndexOf(service);
        if (idx < 0) {
            Program.Logger?.Warning("Service {service} not found", service.Name);
            ConsoleHelper.WriteLineWarning("NOT FOUND");
            return;
        }

        var proc = _processes[idx];
        if (proc == null || proc.HasExited) {
            Program.Logger?.Warning("Cannot stop service {service}, it is not running", service.Name);
            ConsoleHelper.WriteLineWarning("NOT RUNNING");
            return;
        }

        await ProcessHelper.KillProcessTree(proc);
        proc.Dispose();
        _processes[idx] = null;
        ConsoleHelper.WriteLineSuccess("OK");
    }

    public Service? GetService(string serviceName)
    {
        return Services.FirstOrDefault(s => s.Name.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase));
    }

    public async Task Start(Service service)
    {
        Console.Write($"Starting service {service.Name}...");
        var idx = _services.IndexOf(service);
        if (idx < 0) {
            Program.Logger?.Warning("Service {service} not found", service.Name);
            ConsoleHelper.WriteLineWarning("NOT FOUND");
            return;
        }

        var proc = _processes[idx];
        if (proc != null && !proc.HasExited) {
            Program.Logger?.Warning("Cannot start service {service}, it is running", service.Name);
            ConsoleHelper.WriteLineWarning("RUNNING");
            return;
        }

        Program.Logger?.Information("Starting service {service}", service.Name);
        try {
            switch (service.Type) {
                case Service.ServiceTypes.Normal:
                    var nProc = StartNormalService(service);
                    _processes[idx] = nProc;
                    break;
                case Service.ServiceTypes.Terminal:
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                        var tProc = StartTerminalServiceWindows(service);
                        _processes[idx] = tProc;
                    } else {
                        var tProc = StartTerminalService(service);
                        _processes[idx] = tProc;
                    }

                    break;
            }

            ConsoleHelper.WriteLineSuccess("OK");
        } catch (Exception ex) {
            Program.Logger?.Error(ex, "Error starting service {service}: {message}", service.Name, ex.Message);
            ConsoleHelper.WriteLineError("FAILED");
            _processes.Add(null);
        }
    }

    private Process? StartNormalService(Service service)
    {
        for (var i = 0; i < service.Commands.Length; i++) {
            var command = service.Commands[i];
            var proc = ProcessHelper.RunProcess(
                service.Name,
                command.Command,
                command.Arguments ?? string.Empty,
                service.WorkingDir,
                Program.Configuration["logPath"],
                service.MaxLogFileSize,
                service.MaxLogFiles);

            proc.Exited += (s, _) =>
            {
                if (s is Process p) {
                    Program.Logger?.Information("Process {Name} exited with code {ProcessExitCode}",
                        service.Name, p.ExitCode);
                }
            };

            if (i == service.Commands.Length - 1) {
                return proc;
            }
            proc.WaitForExit();
        }

        return null;
    }

    private Process StartTerminalServiceWindows(Service service)
    {
        var proc = ProcessHelper.RunProcess(service.Name, "cmd", string.Empty, service.WorkingDir, Program.Configuration["logPath"], service.MaxLogFileSize, service.MaxLogFiles);
        foreach (var command in service.Commands) {
            proc.StandardInput.WriteLine($"{command.Command} {command.Arguments}");
        }
        return proc;
    }

    private Process StartTerminalService(Service service)
    {
        var commands = new List<Service.CommandDefinition>(service.Commands);
        commands.Insert(0, new Service.CommandDefinition()
        {
            Command = ".",
            Arguments = "$HOME/.nvm/nvm.sh"
        });
        var cmd = string.Join(" && ",
            commands.Select(c =>
                $"{c.Command} {c.Arguments}".Trim()
            )
        );

        var proc = ProcessHelper.RunProcess(service.Name, "bash", $"-lc \"{cmd}\"", service.WorkingDir, Program.Configuration["logPath"], service.MaxLogFileSize, service.MaxLogFiles);
        return proc;
    }
}