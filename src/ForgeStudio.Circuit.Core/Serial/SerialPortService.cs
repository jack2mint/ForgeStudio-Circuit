using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using ForgeStudio.Circuit.Core.Security;

namespace ForgeStudio.Circuit.Core.Serial;

public sealed class SerialPortService : ISerialPortService
{
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private SerialPort? _port;

    public bool IsConnected => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;

    public IReadOnlyList<string> GetAvailablePorts()
    {
        return SerialPort.GetPortNames()
            .Where(SecurityValidators.IsSafeSerialPortName)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!SecurityValidators.IsSafeSerialPortName(portName))
        {
            throw new InvalidOperationException("Unsafe or invalid serial port name.");
        }
        if (baudRate is < 1200 or > 4000000)
        {
            throw new InvalidOperationException("Baud rate is outside the allowed range.");
        }

        try
        {
            _port?.Close();
        }
        catch (IOException)
        {
            // Ignore stale driver errors while replacing a port.
        }
        _port?.Dispose();
        _port = new SerialPort(portName, baudRate)
        {
            Encoding = Encoding.UTF8,
            ReadTimeout = 250,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true,
            NewLine = "\r\n"
        };
        _port.Open();
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
        return Task.CompletedTask;
    }

    public async Task WriteAsync(string text, CancellationToken cancellationToken)
    {
        if (text.Length > 65536)
        {
            throw new InvalidOperationException("Serial write payload is too large.");
        }
        await WriteBytesAsync(Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteBytesAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_port?.IsOpen != true)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }
        if (bytes.Length > 262144)
        {
            throw new InvalidOperationException("Serial write payload is too large.");
        }

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _port.Write(bytes, 0, bytes.Length);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public Task<string> ReadExistingSafeAsync(int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_port?.IsOpen != true)
        {
            return Task.FromResult(string.Empty);
        }
        if (maxBytes is < 1 or > 1048576)
        {
            throw new InvalidOperationException("Invalid serial read limit.");
        }
        var data = _port.ReadExisting();
        if (Encoding.UTF8.GetByteCount(data) > maxBytes)
        {
            data = data[..Math.Min(data.Length, maxBytes)];
        }
        return Task.FromResult(SecurityValidators.SanitizeConsoleText(data));
    }

    public Task<string> ReadUntilAsync(string marker, TimeSpan timeout, int maxBytes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(marker))
        {
            throw new InvalidOperationException("Read marker cannot be empty.");
        }

        return ReadUntilAnyAsync(new[] { marker }, timeout, maxBytes, cancellationToken);
    }

    public async Task<string> ReadUntilAnyAsync(IReadOnlyCollection<string> markers, TimeSpan timeout, int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_port?.IsOpen != true)
        {
            throw new InvalidOperationException("Serial port is not connected.");
        }
        if (markers.Count == 0 || markers.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException("Read markers cannot be empty.");
        }
        if (maxBytes is < 1 or > 1048576)
        {
            throw new InvalidOperationException("Invalid serial read limit.");
        }

        var buffer = new StringBuilder();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string chunk;
            try
            {
                chunk = _port.ReadExisting();
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException("Serial port was closed during read.", ex);
            }

            if (!string.IsNullOrEmpty(chunk))
            {
                buffer.Append(chunk);
                var current = buffer.ToString();
                if (Encoding.UTF8.GetByteCount(current) > maxBytes)
                {
                    throw new InvalidOperationException("Serial response exceeded safe read limit.");
                }
                if (markers.Any(marker => current.Contains(marker, StringComparison.Ordinal)))
                {
                    break;
                }
            }
            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        return SecurityValidators.SanitizeConsoleText(buffer.ToString());
    }

    public async Task DisconnectAsync()
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_port is not null)
            {
                try
                {
                    if (_port.IsOpen)
                    {
                        _port.DiscardInBuffer();
                        _port.DiscardOutBuffer();
                        _port.Close();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Port already closed by driver/runtime.
                }
                catch (IOException)
                {
                    // USB serial devices can disappear while connected. Treat as disconnected.
                }
                finally
                {
                    _port.Dispose();
                    _port = null;
                }
            }
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _ioLock.Dispose();
    }
}
