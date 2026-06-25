using System.Text.RegularExpressions;

namespace ForgeStudio.Circuit.Core.Security;

public static partial class SecurityValidators
{
    public static bool IsSafeSerialPortName(string? portName)
    {
        if (string.IsNullOrWhiteSpace(portName) || portName.Length > 32)
        {
            return false;
        }
        return WindowsComRegex().IsMatch(portName) || UnixSerialRegex().IsMatch(portName);
    }

    public static bool IsSafeDeviceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            return false;
        }
        if (name.Contains('\0', StringComparison.Ordinal) || name.Contains("/", StringComparison.Ordinal) || name.Contains("\\", StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }
        return !name.Any(char.IsControl);
    }

    public static bool IsSafeDevicePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 240)
        {
            return false;
        }
        if (path.Contains('\0', StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }
        if (Path.IsPathRooted(path))
        {
            return false;
        }
        return !path.Any(char.IsControl);
    }

    public static string SanitizeConsoleText(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var cleaned = new string(input.Where(c => !char.IsControl(c) || c is '\r' or '\n' or '\t').ToArray());
        return cleaned.Length > 1048576 ? cleaned[..1048576] : cleaned;
    }

    public static string RedactSecrets(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        var redacted = SecretRegex().Replace(input, "$1=[REDACTED]");
        return BearerRegex().Replace(redacted, "Bearer [REDACTED]");
    }

    [GeneratedRegex("^COM[1-9][0-9]{0,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsComRegex();

    [GeneratedRegex("^/dev/(ttyUSB|ttyACM|cu\\.|tty\\.)[A-Za-z0-9_.-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex UnixSerialRegex();

    [GeneratedRegex("(?i)(password|passwd|token|secret|api[_-]?key)\\s*=\\s*[^\\s;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretRegex();

    [GeneratedRegex("(?i)Bearer\\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();
}
