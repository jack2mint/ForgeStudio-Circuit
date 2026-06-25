namespace ForgeStudio.Circuit.Core.Serial;

public interface ISerialPortService : IAsyncDisposable
{
    bool IsConnected { get; }
    string? PortName { get; }
    IReadOnlyList<string> GetAvailablePorts();
    Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken);
    Task WriteAsync(string text, CancellationToken cancellationToken);
    Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken);
    Task<string> ReadExistingSafeAsync(int maxBytes, CancellationToken cancellationToken);
    Task<string> ReadUntilAsync(string marker, TimeSpan timeout, int maxBytes, CancellationToken cancellationToken);
    Task<string> ReadUntilAnyAsync(IReadOnlyCollection<string> markers, TimeSpan timeout, int maxBytes, CancellationToken cancellationToken);
    Task DisconnectAsync();
}
