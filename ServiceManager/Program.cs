using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.Cmp;
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
    private static readonly BlockingCollection<string> Queue = new();
    private static readonly List<CommandBase> Commands = [];
    private static readonly Dictionary<string, SshTunnel?> SshTunnels = [];

    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"ServiceManager v.{Assembly.GetExecutingAssembly().GetName().Version}");
        Console.WriteLine("Copyright © 2026 by Paolo Iommarini");
        Console.WriteLine();

        var settingsPath = args.Length > 0 ?
            args[0] :
            Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        if (!File.Exists(settingsPath)) {
            ConsoleHelper.WriteLineError("Settings file not found");
            return -1;
        }
        Console.WriteLine($"Settings file: {settingsPath}");
        Console.WriteLine();

        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(settingsPath, optional: false, reloadOnChange: true);
        Configuration = builder.Build();

        var loggerConfig = new LoggerConfiguration()
            .WriteTo.File(
                Path.Join(Configuration["logPath"], "ServiceManager-.log"),
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
        if (sshTunnelsConfigs?.Length > 0) {
            ConsoleHelper.WriteLineHighlight("Starting SSH tunnels");
            foreach (var sshTunnelConfig in sshTunnelsConfigs) {
                Console.Write($"{sshTunnelConfig.Name}");
                Console.CursorLeft = 30;
                ConsoleHelper.WriteHighlight("Connecting");
                try {
                    var t = new SshTunnel(
                        sshTunnelConfig.LocalPort, sshTunnelConfig.RemotePort,
                        sshTunnelConfig.Host,
                        sshTunnelConfig.UserName, sshTunnelConfig.Password);
                    await t.Connect();

                    Console.CursorLeft = 40;
                    ConsoleHelper.WriteLineSuccess("Ok");
                    SshTunnels[sshTunnelConfig.Name] = t;
                } catch {
                    Console.CursorLeft = 40;
                    ConsoleHelper.WriteLineError("Failed");
                    SshTunnels[sshTunnelConfig.Name] = null;
                }
            }
        }

        // Auto start services
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

        ConsoleHelper.WriteLineHighlight("Starting services:");
        foreach (var service in serviceHelper.Services) {
            if (service.AutoStart) {
                await serviceHelper.Start(service);
            }
        }
        Console.WriteLine();

        // User input thread
        new Thread(() =>
        {
            while (!cts.Token.IsCancellationRequested) {
                var line = ConsoleHelper.ReadLine(cts.Token, 2);
                if (!string.IsNullOrEmpty(line)) {
                    Queue.Add(line);
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
                if (Queue.TryTake(out var input)) {
                    ConsoleHelper.InputDisabled = true;
                    userInput = true;
                    Console.WriteLine();
                    input = input.ToLower().Trim();

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

        Commands.Add(new HelpCommand(serviceHelper, Commands));
    }

    private static CommandBase? GetCommand(string name)
    {
        return Commands.FirstOrDefault(c => c.Names.Contains(name));
    }
}