using ForgeStudio.Circuit.Core.Security;

namespace ForgeStudio.Circuit.Core.FileSystem;

public sealed class DevicePathService
{
    public string NormalizeDevicePath(string input)
    {
        if (!SecurityValidators.IsSafeDevicePath(input))
        {
            throw new InvalidOperationException("Unsafe device path was blocked.");
        }
        return input.Replace('\\', '/').TrimStart('/');
    }
}
