using ServiceManager.Helpers;

namespace ServiceManager.Commands;

public abstract class CommandBase
{
    protected ServiceHelper ServiceHelper { get; init; }
    public string[] Names { get; init; } = null!;
    public string? ArgumentsSyntax { get; init; }
    public string? Description { get; init; }

    protected CommandBase(ServiceHelper serviceHelper)
    {
        ServiceHelper = serviceHelper;
    }

    public abstract Task<bool> Run(string args);
}