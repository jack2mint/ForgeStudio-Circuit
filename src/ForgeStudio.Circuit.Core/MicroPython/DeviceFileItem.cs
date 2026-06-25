namespace ForgeStudio.Circuit.Core.MicroPython;

public sealed record DeviceFileItem(string Name, string Path, bool IsDirectory, long SizeBytes)
{
    public override string ToString()
    {
        var icon = IsDirectory ? "▸" : "•";
        var size = IsDirectory || SizeBytes < 0 ? string.Empty : $"  {SizeBytes} B";
        return $"{icon} {Path}{size}";
    }
}
