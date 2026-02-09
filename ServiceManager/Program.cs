using System.Collections.Concurrent;
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
    private static readonly BlockingCollection<string> Queue = new();
    private static readonly List<CommandBase> Commands = [];

    private static async Task<int> Main(string[] args)
    {
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
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

        // Run services
        var services = Configuration.GetRequiredSection("services").Get<Service[]>();
        if (services == null || services.Length == 0) {
            Logger.Error("No services found");
            return -1;
        }

        ConsoleHelper.WriteLineHighlight("Available services:");
        var i = 0;
        foreach (var service in services) {
            Console.WriteLine($"[{++i}] {service.Name}");
        }
        Console.WriteLine();

        var serviceHelper = new ServiceHelper(services);
        InitCommands(serviceHelper, cts);

        ConsoleHelper.WriteLineHighlight("Starting services:");
        foreach (var service in serviceHelper.Services) {
            if (service.AutoStart) {
                Console.Write("  ");
                await serviceHelper.Start(service);
            }
        }
        Console.WriteLine();

        // User input thread
        new Thread(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var line = Console.ReadLine();
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
                    userInput = true;
                    Console.WriteLine();
                    input = input.ToLower().Trim();
                    Logger?.Information("User input: {Input}", input);

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
            }
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
        Commands.Add(new StatusCommand(serviceHelper));
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