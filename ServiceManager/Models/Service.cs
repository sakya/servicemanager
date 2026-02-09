namespace ServiceManager.Models;

public class Service
{
    public enum ServiceTypes
    {
        Normal,
        Terminal
    }

    public class CommandDefinition
    {
        public string Command { get; set; } = null!;
        public string? Arguments { get; set; }
    }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ServiceTypes Type { get; set; } = ServiceTypes.Normal;
    public string? WorkingDir { get; set; }
    public CommandDefinition[] Commands { get; set; } = [];
    public CommandDefinition[] StopCommands { get; set; } = [];

    public int MaxLogFiles  { get; set; } = 10;
    public int MaxLogFileSize  { get; set; } = 32;
    public bool AutoStart { get; set; } = true;
}