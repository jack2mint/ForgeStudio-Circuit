using System.Globalization;
using System.Text;
using ForgeStudio.Circuit.Core.FileSystem;
using ForgeStudio.Circuit.Core.Security;
using ForgeStudio.Circuit.Core.Serial;

namespace ForgeStudio.Circuit.Core.MicroPython;

public sealed class MicroPythonDeviceService : IMicroPythonDeviceService
{
    private const byte CtrlA = 0x01;
    private const byte CtrlB = 0x02;
    private const byte CtrlC = 0x03;
    private const byte CtrlD = 0x04;
    private readonly ISerialPortService _serial;
    private readonly DevicePathService _pathService = new();
    private readonly SemaphoreSlim _deviceLock = new(1, 1);

    public MicroPythonDeviceService(ISerialPortService serial)
    {
        _serial = serial;
    }

    public async Task<IReadOnlyList<DeviceFileItem>> ListFilesAsync(string path, CancellationToken cancellationToken)
    {
        path = NormalizePathOrRoot(path);
        var script = $$"""
import os
_p='{{EscapePythonString(path)}}'
try:
    for _e in os.ilistdir(_p):
        _n=_e[0]
        _t=_e[1] if len(_e)>1 else 0
        _s=_e[3] if len(_e)>3 else -1
        print('FSC_FILE|%s|%s|%s' % (_n, _t, _s))
except Exception as _ex:
    print('FSC_ERROR|%s' % _ex)
""";
        var output = await ExecuteRawAsync(script, cancellationToken).ConfigureAwait(false);
        ThrowIfDeviceError(output);

        var results = new List<DeviceFileItem>();
        foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("FSC_FILE|", StringComparison.Ordinal))
            {
                continue;
            }
            var parts = line.Split('|');
            if (parts.Length < 4)
            {
                continue;
            }
            var name = parts[1];
            if (!SecurityValidators.IsSafeDeviceName(name))
            {
                continue;
            }
            var type = ParseLong(parts[2]);
            var size = ParseLong(parts[3]);
            var isDirectory = (type & 0x4000) == 0x4000;
            var childPath = string.IsNullOrWhiteSpace(path) ? name : $"{path.TrimEnd('/')}/{name}";
            if (!SecurityValidators.IsSafeDevicePath(childPath))
            {
                continue;
            }
            results.Add(new DeviceFileItem(name, childPath, isDirectory, size));
        }

        return results.OrderByDescending(static f => f.IsDirectory)
            .ThenBy(static f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string> ReadTextFileAsync(string path, CancellationToken cancellationToken)
    {
        path = _pathService.NormalizeDevicePath(path);
        var script = $$"""
_p='{{EscapePythonString(path)}}'
try:
    with open(_p, 'r') as _f:
        print('FSC_BEGIN_FILE')
        print(_f.read())
        print('FSC_END_FILE')
except Exception as _ex:
    print('FSC_ERROR|%s' % _ex)
""";
        var output = await ExecuteRawAsync(script, cancellationToken).ConfigureAwait(false);
        ThrowIfDeviceError(output);
        const string start = "FSC_BEGIN_FILE";
        const string end = "FSC_END_FILE";
        var startIndex = output.IndexOf(start, StringComparison.Ordinal);
        var endIndex = output.IndexOf(end, StringComparison.Ordinal);
        if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
        {
            throw new InvalidOperationException("Device did not return a readable file payload.");
        }
        var contentStart = startIndex + start.Length;
        var content = output[contentStart..endIndex].TrimStart('\r', '\n');
        return content.TrimEnd('\r', '\n');
    }

    public async Task WriteTextFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        path = _pathService.NormalizeDevicePath(path);
        if (Encoding.UTF8.GetByteCount(content) > 262144)
        {
            throw new InvalidOperationException("File content exceeds the safe dev-build write limit of 256 KB.");
        }
        var script = $$"""
_p='{{EscapePythonString(path)}}'
_data={{ToPythonBytesLiteral(content)}}
try:
    with open(_p, 'wb') as _f:
        _f.write(_data)
    print('FSC_OK|write')
except Exception as _ex:
    print('FSC_ERROR|%s' % _ex)
""";
        var output = await ExecuteRawAsync(script, cancellationToken).ConfigureAwait(false);
        ThrowIfDeviceError(output);
    }

    public async Task DeleteFileAsync(string path, CancellationToken cancellationToken)
    {
        path = _pathService.NormalizeDevicePath(path);
        var script = $$"""
import os
_p='{{EscapePythonString(path)}}'
try:
    os.remove(_p)
    print('FSC_OK|delete')
except Exception as _ex:
    print('FSC_ERROR|%s' % _ex)
""";
        var output = await ExecuteRawAsync(script, cancellationToken).ConfigureAwait(false);
        ThrowIfDeviceError(output);
    }

    public Task<string> RunCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(code) > 262144)
        {
            throw new InvalidOperationException("Code payload exceeds the safe dev-build run limit of 256 KB.");
        }
        return ExecuteRawAsync(code, cancellationToken);
    }

    public async Task<bool> TryEnterRawReplAsync(CancellationToken cancellationToken)
    {
        if (!_serial.IsConnected)
        {
            return false;
        }

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TryEnterRawReplCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    public async Task StopProgramAsync(CancellationToken cancellationToken)
    {
        if (!_serial.IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        await _serial.WriteBytesAsync(new[] { CtrlB }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        await _serial.WriteBytesAsync(new[] { CtrlC, CtrlC }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        _ = await _serial.ReadExistingSafeAsync(65536, cancellationToken).ConfigureAwait(false);
    }

    public async Task SoftRebootAsync(CancellationToken cancellationToken)
    {
        if (!_serial.IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        await _serial.WriteBytesAsync(new[] { CtrlB }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        await _serial.WriteBytesAsync(new[] { CtrlC, CtrlC, CtrlD }, cancellationToken).ConfigureAwait(false);
        await Task.Delay(750, cancellationToken).ConfigureAwait(false);
        _ = await _serial.ReadExistingSafeAsync(65536, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExecuteRawAsync(string code, CancellationToken cancellationToken)
    {
        if (!_serial.IsConnected)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        await _deviceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rawReady = await TryEnterRawReplCoreAsync(cancellationToken).ConfigureAwait(false);
            if (!rawReady)
            {
                throw new InvalidOperationException("Board did not enter MicroPython raw REPL mode. Circuit stayed connected in Serial Monitor mode; try Stop, Soft Reboot, or reconnect after the board settles.");
            }

            await _serial.WriteAsync(code.Replace("\r\n", "\n", StringComparison.Ordinal), cancellationToken).ConfigureAwait(false);
            await _serial.WriteBytesAsync(new[] { CtrlD }, cancellationToken).ConfigureAwait(false);
            var response = await _serial.ReadUntilAsync(">", TimeSpan.FromSeconds(10), 1048576, cancellationToken).ConfigureAwait(false);
            await _serial.WriteBytesAsync(new[] { CtrlB }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            _ = await _serial.ReadExistingSafeAsync(65536, cancellationToken).ConfigureAwait(false);
            return CleanRawReplResponse(response);
        }
        finally
        {
            _deviceLock.Release();
        }
    }

    private async Task<bool> TryEnterRawReplCoreAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Normal REPL first, then interrupt twice. This handles boards running main.py loops.
            await _serial.WriteBytesAsync(new[] { CtrlB }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(80 + attempt * 40, cancellationToken).ConfigureAwait(false);
            await _serial.WriteBytesAsync(new[] { CtrlC }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(120 + attempt * 50, cancellationToken).ConfigureAwait(false);
            await _serial.WriteBytesAsync(new[] { CtrlC }, cancellationToken).ConfigureAwait(false);
            await Task.Delay(180 + attempt * 70, cancellationToken).ConfigureAwait(false);
            _ = await _serial.ReadExistingSafeAsync(131072, cancellationToken).ConfigureAwait(false);

            // Enter raw REPL and wait for the raw prompt. Different firmware builds return
            // slightly different banners, so accept the prompt only with raw/banner context.
            await _serial.WriteBytesAsync(new[] { CtrlA }, cancellationToken).ConfigureAwait(false);
            var prompt = await _serial.ReadUntilAnyAsync(
                new[] { "raw REPL", "raw REPL; CTRL-B to exit" },
                TimeSpan.FromSeconds(attempt <= 2 ? 4 : 6),
                131072,
                cancellationToken).ConfigureAwait(false);

            if (prompt.Contains("raw REPL", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string CleanRawReplResponse(string response)
    {
        var cleaned = response.Replace("OK", string.Empty, StringComparison.Ordinal);
        cleaned = cleaned.Replace("\u0004", "\n", StringComparison.Ordinal);
        cleaned = cleaned.Replace(">", string.Empty, StringComparison.Ordinal);
        return SecurityValidators.SanitizeConsoleText(cleaned).Trim();
    }

    private static void ThrowIfDeviceError(string output)
    {
        var line = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(static l => l.StartsWith("FSC_ERROR|", StringComparison.Ordinal));
        if (line is not null)
        {
            throw new InvalidOperationException(line.Replace("FSC_ERROR|", "Device error: ", StringComparison.Ordinal));
        }
    }

    private static string NormalizePathOrRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }
        return new DevicePathService().NormalizeDevicePath(path);
    }

    private static long ParseLong(string text)
    {
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    }

    private static string EscapePythonString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string ToPythonBytesLiteral(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hex = string.Concat(bytes.Select(static b => $"\\x{b:x2}"));
        return $"b'{hex}'";
    }
}
