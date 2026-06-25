namespace ForgeStudio.Circuit.Core.Devices;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    ConnectedSerial,
    EnteringRawRepl,
    ConnectedRawRepl,
    BusyReadingFiles,
    BusyWritingFile,
    RunningCode,
    ErrorRecoverable,
    Disconnecting
}
