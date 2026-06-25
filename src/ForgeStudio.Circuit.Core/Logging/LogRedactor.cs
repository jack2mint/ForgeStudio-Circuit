using ForgeStudio.Circuit.Core.Security;

namespace ForgeStudio.Circuit.Core.Logging;

public static class LogRedactor
{
    public static string Clean(string message) => SecurityValidators.RedactSecrets(message);
}
