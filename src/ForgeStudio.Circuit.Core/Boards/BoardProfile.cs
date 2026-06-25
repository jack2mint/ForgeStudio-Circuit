namespace ForgeStudio.Circuit.Core.Boards;

public sealed record BoardProfile(
    string Id,
    string DisplayName,
    string Family,
    IReadOnlyList<string> Runtime,
    IReadOnlyList<string> Connection,
    int DefaultBaud,
    bool DestructiveToolsEnabled);
