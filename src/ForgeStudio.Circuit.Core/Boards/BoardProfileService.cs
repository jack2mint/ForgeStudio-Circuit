namespace ForgeStudio.Circuit.Core.Boards;

public sealed class BoardProfileService : IBoardProfileService
{
    private readonly IReadOnlyList<BoardProfile> _profiles;

    private BoardProfileService(IReadOnlyList<BoardProfile> profiles)
    {
        _profiles = profiles;
    }

    public IReadOnlyList<BoardProfile> GetProfiles() => _profiles;

    public static BoardProfileService CreateDefault()
    {
        return new BoardProfileService(new[]
        {
            new BoardProfile("raspberry-pi-pico", "Raspberry Pi Pico", "RP2040", new[] { "MicroPython", "CircuitPython" }, new[] { "serial", "uf2" }, 115200, false),
            new BoardProfile("raspberry-pi-pico-w", "Raspberry Pi Pico W", "RP2040", new[] { "MicroPython", "CircuitPython" }, new[] { "serial", "uf2" }, 115200, false),
            new BoardProfile("esp32-wroom", "ESP32-WROOM", "ESP32", new[] { "MicroPython" }, new[] { "serial" }, 115200, false),
            new BoardProfile("esp8266", "ESP8266", "ESP8266", new[] { "MicroPython" }, new[] { "serial" }, 115200, false),
        });
    }
}
