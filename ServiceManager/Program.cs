using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using ServiceManager.Commands;
using ServiceManager.Helpers;
using ServiceManager.Models;

namespace ServiceManager;

class Program
{
    public static IConfigurationRoot Configuration = null!;
    public static Logger? Logger;

    private static readonly List<string> CommandsHistory = [];
    private static readonly BlockingCollection<string> Queue = new();
    private static readonly List<CommandBase> Commands = [];
    private static readonly Dictionary<string, SshTunnel?> SshTunnels = new(StringComparer.InvariantCultureIgnoreCase);

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"ServiceManager v.{Assembly.GetExecutingAssembly().GetName().Version}");
        Console.WriteLine(((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyCopyrightAttribute), false)!).Copyright);
        Console.WriteLine();

        var settingsPath = args.Length > 0 ?
            args[0] :
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (!File.Exists(settingsPath)) {
            ConsoleHelper.WriteLineError("Settings file not found");
            return -1;
        }

        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(settingsPath, optional: false, reloadOnChange: true);
        Configuration = builder.Build();

        var logPath = Configuration["logPath"] ?? "./logs";
        logPath = Path.GetFullPath(logPath);
        if (!Directory.Exists(logPath)) {
            Directory.CreateDirectory(logPath);
        }

        Console.WriteLine($"Settings file: {settingsPath}");
        Console.WriteLine($"Log directory: {logPath}");
        Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine();

        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File(
                Path.Join(logPath, "ServiceManager-.log"),
                rollingInterval: RollingInterval.Day,
                buffered: false,
                fileSizeLimitBytes: 32 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 20);

        Logger = loggerConfig.CreateLogger();

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("User cancelled");
            cts.Cancel();
        };

        var services = Configuration.GetRequiredSection("services").Get<Service[]>();
        if (services == null || services.Length == 0) {
            Logger.Error("No services defined in appsettings.json");
            return -1;
        }

        // Start SSH tunnels
        var sshTunnelsConfigs = Configuration.GetRequiredSection("sshTunnels").Get<SshTunnelConfig[]>();
        if (sshTunnelsConfigs != null) {
            foreach (var sshTunnelConfig in sshTunnelsConfigs) {
                SshTunnels[sshTunnelConfig.Name] = null;
            }
        }

        if (sshTunnelsConfigs?.Any(c => c.AutoConnect) == true) {
            ConsoleHelper.WriteLineHighlight("Starting SSH tunnels");
            foreach (var sshTunnelConfig in sshTunnelsConfigs) {
                if (!sshTunnelConfig.AutoConnect)
                    continue;

                Console.Write($"{sshTunnelConfig.Name}");
                Console.CursorLeft = 40;
                ConsoleHelper.WriteWarning("Connecting");
                try {
                    var t = new SshTunnel(
                        sshTunnelConfig.LocalPort, sshTunnelConfig.RemotePort,
                        sshTunnelConfig.Host,
                        sshTunnelConfig.UserName, sshTunnelConfig.Password,
                        Logger);
                    var connected = await t.Connect();

                    Console.CursorLeft = 40;
                    if (connected) {
                        ConsoleHelper.WriteLineSuccess($"{"Ok", -10}");
                    } else {
                        ConsoleHelper.WriteLineError($"{"Failed", -10}");
                    }

                    SshTunnels[sshTunnelConfig.Name] = t;
                } catch {
                    Console.CursorLeft = 40;
                    ConsoleHelper.WriteLineSuccess($"{"Failed", -10}");
                    SshTunnels[sshTunnelConfig.Name] = null;
                }
            }
            Console.WriteLine();
        }

        ConsoleHelper.WriteLineHighlight($"{"Service", -40}Auto start");
        var i = 0;
        foreach (var service in services) {
            Console.Write($"[{++i}] {service.Name}");
            Console.CursorLeft = 40;
            if (service.AutoStart) {
                ConsoleHelper.WriteLineSuccess("True");
            } else {
                ConsoleHelper.WriteLineWarning("False");
            }
        }
        Console.WriteLine();

        var serviceHelper = new ServiceHelper(services);
        InitCommands(serviceHelper, cts);

        // Auto start services
        if (services.Any(s => s.AutoStart)) {
            ConsoleHelper.WriteLineHighlight("Starting services:");
            foreach (var service in serviceHelper.Services) {
                if (service.AutoStart) {
                    await serviceHelper.Start(service);
                }
            }
            Console.WriteLine();
        }

        // User input thread
        new Thread(() =>
        {
            while (!cts.Token.IsCancellationRequested) {
                var line = ConsoleHelper.ReadLine(cts.Token, 2, CommandsHistory);
                if (!string.IsNullOrEmpty(line)) {
                    line = line.Trim();
                    Queue.Add(line);
                    var hIdx = GetCommandHistoryIndex(line);
                    if (hIdx < 0) {
                        CommandsHistory.Add(line);
                    } else {
                        CommandsHistory.RemoveAt(hIdx);
                        CommandsHistory.Add(line);
                    }
                } else {
                    PrintPrompt();
                }
            }
        })
        { IsBackground = true }.Start();

        PrintPrompt();
        var userInput = false;
        while (!cts.Token.IsCancellationRequested) {
            if (userInput) {
                PrintPrompt();
                userInput = false;
            }

            try {
                await Task.Delay(100, cts.Token);
                if (Queue.TryTake(out var oInput)) {
                    ConsoleHelper.InputDisabled = true;
                    userInput = true;
                    Console.WriteLine();
                    oInput = oInput.Trim();
                    var input = oInput.Trim();

                    var idx = input.IndexOf(' ');
                    var commandName = idx >= 0 ? input[..idx] : input;
                    var commandArgs = idx >= 0 ? input[(idx + 1)..] : string.Empty;
                    var command = GetCommand(commandName);
                    if (command == null) {
                        ConsoleHelper.WriteLineError($"Unknown command: {input}");
                        continue;
                    }

                    await command.Run(commandArgs);
                }
            } catch (OperationCanceledException) {
                // Ignored
            } finally {
                ConsoleHelper.InputDisabled = false;
            }
        }

        // Stop SSH tunnels
        foreach (var kvp in SshTunnels) {
            kvp.Value?.Dispose();
        }
        return 0;
    }

    private static void PrintPrompt()
    {
        Console.Write("> ");
    }

    private static void InitCommands(ServiceHelper serviceHelper, CancellationTokenSource cts)
    {
        Commands.Add(new ExitCommand(serviceHelper, cts));
        Commands.Add(new StatusCommand(serviceHelper, SshTunnels));
        Commands.Add(new StartCommand(serviceHelper));
        Commands.Add(new StopCommand(serviceHelper));
        Commands.Add(new RestartCommand(serviceHelper));

        Commands.Add(new ConnectCommand(serviceHelper, Configuration.GetRequiredSection("sshTunnels").Get<SshTunnelConfig[]>(), SshTunnels));
        Commands.Add(new DisconnectCommand(serviceHelper, Configuration.GetRequiredSection("sshTunnels").Get<SshTunnelConfig[]>(), SshTunnels));

        Commands.Add(new HelpCommand(serviceHelper, Commands));
    }

    private static CommandBase? GetCommand(string name)
    {
        return Commands.FirstOrDefault(c => c.Names.Contains(name));
    }

    private static int GetCommandHistoryIndex(string command)
    {
        var idx = 0;
        foreach (var ch in CommandsHistory) {
            if (string.Compare(ch, command, StringComparison.InvariantCultureIgnoreCase) == 0)
                return idx;
            idx++;
        }

        return -1;
    }
}