namespace ForgeStudio.Circuit.Core.Security;

public sealed class ToolExecutionPolicy
{
    private readonly HashSet<string> _allowedTools = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _allowedArgs = new(StringComparer.OrdinalIgnoreCase);

    public void AllowTool(string toolId, IEnumerable<string> allowedArguments)
    {
        if (string.IsNullOrWhiteSpace(toolId)) throw new ArgumentException("Tool id is required.", nameof(toolId));
        _allowedTools.Add(toolId);
        _allowedArgs[toolId] = allowedArguments.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAllowed(string toolId, IEnumerable<string> args)
    {
        if (!_allowedTools.Contains(toolId)) return false;
        var allowed = _allowedArgs[toolId];
        return args.All(arg => !arg.Contains('&') && !arg.Contains('|') && (arg.StartsWith("--", StringComparison.Ordinal) ? allowed.Contains(arg) : true));
    }
}
