using ForgeStudio.Circuit.Core.Security;
using Xunit;

namespace ForgeStudio.Circuit.Tests;

public sealed class SecurityValidatorTests
{
    [Theory]
    [InlineData("COM1")]
    [InlineData("COM12")]
    [InlineData("/dev/ttyUSB0")]
    [InlineData("/dev/ttyACM0")]
    public void AllowsExpectedSerialPorts(string port) => Assert.True(SecurityValidators.IsSafeSerialPortName(port));

    [Theory]
    [InlineData("COM0")]
    [InlineData("COM9999")]
    [InlineData("bad && calc")]
    [InlineData("../COM3")]
    public void BlocksUnsafeSerialPorts(string port) => Assert.False(SecurityValidators.IsSafeSerialPortName(port));

    [Theory]
    [InlineData("main.py")]
    [InlineData("lib/sensor.py")]
    public void AllowsSafeDevicePaths(string path) => Assert.True(SecurityValidators.IsSafeDevicePath(path));

    [Theory]
    [InlineData("../main.py")]
    [InlineData("C:/Windows/system.ini")]
    [InlineData("/etc/passwd")]
    public void BlocksUnsafeDevicePaths(string path) => Assert.False(SecurityValidators.IsSafeDevicePath(path));

    [Fact]
    public void RedactsSecrets()
    {
        var output = SecurityValidators.RedactSecrets("token=abc123 Bearer deadbeef");
        Assert.DoesNotContain("abc123", output);
        Assert.DoesNotContain("deadbeef", output);
    }
}
