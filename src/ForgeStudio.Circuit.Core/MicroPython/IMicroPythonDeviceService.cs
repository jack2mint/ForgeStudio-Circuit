namespace ForgeStudio.Circuit.Core.MicroPython;

public interface IMicroPythonDeviceService
{
    Task<bool> TryEnterRawReplAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceFileItem>> ListFilesAsync(string path, CancellationToken cancellationToken);
    Task<string> ReadTextFileAsync(string path, CancellationToken cancellationToken);
    Task WriteTextFileAsync(string path, string content, CancellationToken cancellationToken);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken);
    Task<string> RunCodeAsync(string code, CancellationToken cancellationToken);
    Task StopProgramAsync(CancellationToken cancellationToken);
    Task SoftRebootAsync(CancellationToken cancellationToken);
}
