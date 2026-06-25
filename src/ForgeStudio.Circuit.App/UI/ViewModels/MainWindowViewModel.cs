using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeStudio.Circuit.Core.Boards;
using ForgeStudio.Circuit.Core.Devices;
using ForgeStudio.Circuit.Core.MicroPython;
using ForgeStudio.Circuit.Core.Security;
using ForgeStudio.Circuit.Core.Serial;

namespace ForgeStudio.Circuit.App.UI.ViewModels;

public sealed record ProblemItem(string Severity, string Area, string Message, string Hint)
{
    public override string ToString() => $"[{Severity}] {Area}: {Message}  {Hint}";
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ISerialPortService _serialPortService;
    private readonly IBoardProfileService _boardProfileService;
    private readonly IMicroPythonDeviceService _deviceService;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly string _projectRoot;
    private CancellationTokenSource? _operationCts;

    [ObservableProperty] private string? selectedPort;
    [ObservableProperty] private string? selectedLocalFile;
    [ObservableProperty] private DeviceFileItem? selectedDeviceFile;
    [ObservableProperty] private string? selectedCircuitPythonDrive;
    [ObservableProperty] private string deviceStatus = "DISCONNECTED";
    [ObservableProperty] private string connectionMode = "None";
    [ObservableProperty] private string connectionStateText = ConnectionState.Disconnected.ToString();
    [ObservableProperty] private string devicePath = "/";
    [ObservableProperty] private string deviceTargetPath = "main.py";
    [ObservableProperty] private string replInput = "print('ping from ForgeStudio Circuit')";
    [ObservableProperty] private string editorText = "# ForgeStudio Circuit\n# Write MicroPython or CircuitPython here.\n\nprint('Hello from ForgeStudio Circuit')\n";
    [ObservableProperty] private string consoleText = "[ForgeStudio Circuit] REPL ready. Connect a board to begin.\n";
    [ObservableProperty] private string editorFileName = "main.py";
    [ObservableProperty] private string currentLanguage = "Python / MicroPython";
    [ObservableProperty] private string editorAssistText = "Python mode: indentation matters. Use main.py for startup code, boot.py for boot configuration, and config.py/json for settings.";
    [ObservableProperty] private string editorState = "Ready";
    [ObservableProperty] private string connectionHint = "Select a COM port, connect, then device files refresh automatically.";
    [ObservableProperty] private string selectedBoardName = string.Empty;
    [ObservableProperty] private string editorHighlightingName = "Python";
    [ObservableProperty] private string editorCaretStatus = "Lines 1  |  0 chars";
    [ObservableProperty] private string findText = string.Empty;
    [ObservableProperty] private string selectedSnippet = "print";
    [ObservableProperty] private bool isDeviceBusy;

    public ObservableCollection<string> Ports { get; } = new();
    public ObservableCollection<string> LocalFiles { get; } = new();
    public ObservableCollection<DeviceFileItem> DeviceFiles { get; } = new();
    public ObservableCollection<string> BoardNames { get; } = new();
    public ObservableCollection<string> CircuitPythonDrives { get; } = new();
    public ObservableCollection<ProblemItem> Problems { get; } = new();
    public ObservableCollection<int> EditorLineNumbers { get; } = new();

    public ObservableCollection<string> Snippets { get; } = new(new[]
    {
        "print",
        "try/except",
        "while True",
        "machine.Pin",
        "DHT read",
        "JSON config",
        "ESP-NOW safe import"
    });

    private MainWindowViewModel(ISerialPortService serialPortService, IBoardProfileService boardProfileService)
    {
        _serialPortService = serialPortService;
        _boardProfileService = boardProfileService;
        _deviceService = new MicroPythonDeviceService(_serialPortService);
        _projectRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ForgeStudio Circuit", "Projects");

        RefreshPorts();
        RefreshCircuitPythonDrives();
        EnsureProjectWorkspace();
        LoadLocalFiles();
        SetPlaceholderDeviceFiles("Connect device to list files");
        foreach (var board in _boardProfileService.GetProfiles())
        {
            BoardNames.Add(board.DisplayName);
        }
        SelectedBoardName = BoardNames.FirstOrDefault() ?? string.Empty;
        UpdateEditorLanguage(DeviceTargetPath);
        UpdateEditorMetrics();
    }

    public static MainWindowViewModel CreateDefault()
    {
        return new MainWindowViewModel(new SerialPortService(), BoardProfileService.CreateDefault());
    }

    partial void OnEditorTextChanged(string value)
    {
        UpdateEditorMetrics();
        if (!EditorState.Contains("Unsaved", StringComparison.OrdinalIgnoreCase))
        {
            EditorState = "Unsaved changes";
        }
    }

    partial void OnSelectedDeviceFileChanged(DeviceFileItem? value)
    {
        if (value is not null && !value.IsDirectory && !value.Path.Contains(" ", StringComparison.Ordinal))
        {
            DeviceTargetPath = value.Path;
        }
    }

    partial void OnDeviceTargetPathChanged(string value)
    {
        UpdateEditorLanguage(value);
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        Ports.Clear();
        foreach (var port in _serialPortService.GetAvailablePorts())
        {
            Ports.Add(port);
        }
        SelectedPort ??= Ports.FirstOrDefault();
        ConnectionHint = Ports.Count == 0 ? "No serial ports found. Plug in a board, then refresh." : $"{Ports.Count} serial port(s) detected.";
    }

    [RelayCommand]
    private void RefreshCircuitPythonDrives()
    {
        CircuitPythonDrives.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(static d => d.IsReady && d.VolumeLabel.Equals("CIRCUITPY", StringComparison.OrdinalIgnoreCase)))
        {
            CircuitPythonDrives.Add(drive.RootDirectory.FullName);
        }
        SelectedCircuitPythonDrive ??= CircuitPythonDrives.FirstOrDefault();
    }

    [RelayCommand]
    private void MountCircuitPythonDrive()
    {
        if (string.IsNullOrWhiteSpace(SelectedCircuitPythonDrive) || !Directory.Exists(SelectedCircuitPythonDrive))
        {
            AddProblem("Warning", "CircuitPython", "No CIRCUITPY drive selected.", "Plug in a CircuitPython board and press Scan Drives.");
            return;
        }

        ConnectionMode = "CircuitPython USB Drive";
        DeviceStatus = "CIRCUITPY MOUNTED";
        ConnectionStateText = ConnectionState.ConnectedSerial.ToString();
        DevicePath = SelectedCircuitPythonDrive;
        LoadCircuitPythonFiles(SelectedCircuitPythonDrive);
        AppendConsole($"Mounted CircuitPython drive: {SelectedCircuitPythonDrive}");
    }

    [RelayCommand]
    private void LoadLocalFiles()
    {
        LocalFiles.Clear();
        EnsureProjectWorkspace();
        var candidates = Directory.EnumerateFiles(_projectRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static file => file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Where(IsVisibleProjectFile)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in candidates)
        {
            LocalFiles.Add(file);
        }
        SelectedLocalFile ??= LocalFiles.FirstOrDefault();
    }

    [RelayCommand]
    private void OpenLocalFile()
    {
        if (string.IsNullOrWhiteSpace(SelectedLocalFile))
        {
            return;
        }
        var fullPath = Path.Combine(_projectRoot, SelectedLocalFile);
        if (!File.Exists(fullPath))
        {
            AddProblem("Warning", "Project", $"Local file not found: {SelectedLocalFile}", "Refresh the project file list.");
            return;
        }
        EditorText = File.ReadAllText(fullPath);
        EditorFileName = SelectedLocalFile;
        DeviceTargetPath = SelectedLocalFile;
        EditorState = "Local file opened";
        AppendConsole($"Opened local file '{SelectedLocalFile}'.");
    }

    [RelayCommand]
    private void NewEditorFile()
    {
        EditorFileName = "untitled.py";
        DeviceTargetPath = "untitled.py";
        EditorText = "# New ForgeStudio Circuit file\n\nprint('ready')\n";
        EditorState = "New file";
        AppendConsole("Created new editor buffer.");
    }

    [RelayCommand]
    private void SaveLocalFile()
    {
        EnsureProjectWorkspace();
        var safeName = Path.GetFileName(string.IsNullOrWhiteSpace(EditorFileName) ? DeviceTargetPath : EditorFileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "main.py";
        }

        if (!IsVisibleProjectFile(safeName))
        {
            AddProblem("Warning", "Project", "Blocked unsafe or unsupported local file name.", "Use .py, .json, .toml, .md, or .txt files.");
            return;
        }

        var fullPath = Path.Combine(_projectRoot, safeName);
        File.WriteAllText(fullPath, EditorText);
        EditorFileName = safeName;
        DeviceTargetPath = safeName;
        EditorState = "Saved locally";
        LoadLocalFiles();
        AppendConsole($"Saved local file '{safeName}'.");
    }

    [RelayCommand]
    private void FormatIndent()
    {
        var lines = EditorText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].Replace("\t", "    ", StringComparison.Ordinal).TrimEnd();
        }

        EditorText = string.Join(Environment.NewLine, lines);
        EditorState = "Indent cleaned";
        AppendConsole("Editor indentation normalized to spaces and trailing whitespace removed.");
    }

    [RelayCommand]
    private void ToggleComment()
    {
        var lines = EditorText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var meaningful = lines.Where(static line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var shouldUncomment = meaningful.Length > 0 && meaningful.All(static line => line.TrimStart().StartsWith('#'));

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var leading = lines[i].Length - lines[i].TrimStart().Length;
            if (shouldUncomment)
            {
                var trimmed = lines[i][leading..];
                lines[i] = lines[i][..leading] + (trimmed.StartsWith("# ", StringComparison.Ordinal) ? trimmed[2..] : trimmed.TrimStart('#'));
            }
            else
            {
                lines[i] = lines[i][..leading] + "# " + lines[i][leading..];
            }
        }

        EditorText = string.Join(Environment.NewLine, lines);
        EditorState = shouldUncomment ? "Uncommented buffer" : "Commented buffer";
    }

    [RelayCommand]
    private void FindNext()
    {
        if (string.IsNullOrWhiteSpace(FindText))
        {
            EditorState = "Find text is empty";
            return;
        }

        var index = EditorText.IndexOf(FindText, StringComparison.OrdinalIgnoreCase);
        EditorState = index < 0 ? $"Not found: {FindText}" : $"Found '{FindText}' at char {index}";
        AppendConsole(EditorState);
    }

    [RelayCommand]
    private void InsertSnippet()
    {
        var snippet = SelectedSnippet switch
        {
            "try/except" => "try:\n    pass\nexcept Exception as ex:\n    print(ex)\n",
            "while True" => "import time\nwhile True:\n    # loop body\n    time.sleep(1)\n",
            "machine.Pin" => "from machine import Pin\nled = Pin(2, Pin.OUT)\nled.value(1)\n",
            "DHT read" => "import dht\nfrom machine import Pin\nsensor = dht.DHT11(Pin(4))\nsensor.measure()\nprint(sensor.temperature(), sensor.humidity())\n",
            "JSON config" => "import json\nconfig = {\"device\": \"board\", \"enabled\": True}\nprint(json.dumps(config))\n",
            "ESP-NOW safe import" => "try:\n    import espnow\nexcept Exception:\n    espnow = None\n",
            _ => "print('ForgeStudio Circuit')\n"
        };

        EditorText = EditorText.EndsWith(Environment.NewLine, StringComparison.Ordinal) || EditorText.EndsWith("\n", StringComparison.Ordinal)
            ? EditorText + snippet
            : EditorText + Environment.NewLine + snippet;
        EditorState = $"Inserted snippet: {SelectedSnippet}";
    }

    [RelayCommand]
    private void ValidateEditor()
    {
        var issues = new List<string>();
        if (EditorText.Length > 128000)
        {
            issues.Add("large editor buffer may be slow over raw REPL");
        }
        if (CurrentLanguage.Contains("JSON", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _ = JsonDocument.Parse(EditorText);
            }
            catch (JsonException ex)
            {
                issues.Add($"JSON error: {ex.Message}");
            }
        }
        if (!SecurityValidators.IsSafeDevicePath(DeviceTargetPath))
        {
            issues.Add("device path must be relative and must not contain traversal or unsafe characters");
        }

        EditorState = issues.Count == 0 ? "Validation passed" : "Validation warnings";
        if (issues.Count == 0)
        {
            AppendConsole("Editor validation passed.");
            return;
        }

        foreach (var issue in issues)
        {
            AddProblem("Warning", "Editor", issue, "Fix the highlighted issue before saving/running.");
        }
        AppendConsole("Editor validation: " + string.Join("; ", issues));
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        await RunDeviceOperationAsync("Connect", TimeSpan.FromSeconds(20), async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                throw new InvalidOperationException("No serial port selected.");
            }

            SetConnectionState(ConnectionState.Connecting, "CONNECTING...", "Serial", "Opening serial port...");
            await _serialPortService.ConnectAsync(SelectedPort, 115200, token);
            SetConnectionState(ConnectionState.ConnectedSerial, $"CONNECTED: {SelectedPort}", "Serial Monitor", "Connected. Trying MicroPython raw REPL...");
            AppendConsole($"Connected to {SelectedPort} at 115200 baud.");
            await Task.Delay(600, token);

            SetConnectionState(ConnectionState.EnteringRawRepl, "RAW REPL CHECK...", "MicroPython Probe", "Checking raw REPL support...");
            var rawReady = await _deviceService.TryEnterRawReplAsync(token);
            if (rawReady)
            {
                SetConnectionState(ConnectionState.ConnectedRawRepl, $"CONNECTED: {SelectedPort}", "MicroPython Raw REPL", "Raw REPL ready. Reading device filesystem...");
                await LoadDeviceFilesCoreAsync(token);
            }
            else
            {
                SetConnectionState(ConnectionState.ErrorRecoverable, $"CONNECTED: {SelectedPort}", "Serial Monitor", "Board connected, but raw REPL did not respond. Use Stop, Soft Reboot, or Read Files.");
                SetPlaceholderDeviceFiles("Raw REPL not ready - press Stop or Soft Reboot, then Read Files");
                AddProblem("Warning", "MicroPython", "Board did not enter raw REPL during connect.", "Circuit remains connected in Serial Monitor mode. Try Stop, Soft Reboot, then Read Files.");
            }
        });
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await RunDeviceOperationAsync("Disconnect", TimeSpan.FromSeconds(8), async token =>
        {
            _operationCts?.CancelAfter(TimeSpan.FromSeconds(2));
            SetConnectionState(ConnectionState.Disconnecting, "DISCONNECTING...", ConnectionMode, "Closing serial/device session...");
            await _serialPortService.DisconnectAsync();
            SetConnectionState(ConnectionState.Disconnected, "DISCONNECTED", "None", "Disconnected.");
            SetPlaceholderDeviceFiles("Connect device to list files");
            AppendConsole("Disconnected.");
            await Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task LoadDeviceFilesAsync()
    {
        await RunDeviceOperationAsync("Read device files", TimeSpan.FromSeconds(25), LoadDeviceFilesCoreAsync);
    }

    private async Task LoadDeviceFilesCoreAsync(CancellationToken token)
    {
        if (ConnectionMode.Equals("CircuitPython USB Drive", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(SelectedCircuitPythonDrive))
        {
            LoadCircuitPythonFiles(DevicePath);
            return;
        }

        EnsureConnectedOrThrow();
        SetConnectionState(ConnectionState.BusyReadingFiles, "READING FILES...", ConnectionMode, "Reading MicroPython filesystem using raw REPL...");
        var files = await _deviceService.ListFilesAsync(DevicePath, token);
        DeviceFiles.Clear();
        foreach (var file in files)
        {
            DeviceFiles.Add(file);
        }
        if (files.Count == 0)
        {
            SetPlaceholderDeviceFiles("Device filesystem is empty");
        }
        SetConnectionState(ConnectionState.ConnectedRawRepl, $"CONNECTED: {_serialPortService.PortName}", "MicroPython Raw REPL", $"Device files loaded from '{DevicePath}'.");
        AppendConsole($"Device files refreshed from '{DevicePath}'.");
    }

    [RelayCommand]
    private async Task OpenDeviceFileAsync()
    {
        await RunDeviceOperationAsync("Open device file", TimeSpan.FromSeconds(25), async token =>
        {
            if (SelectedDeviceFile is null || SelectedDeviceFile.Path.StartsWith("Connect", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (SelectedDeviceFile.IsDirectory)
            {
                DevicePath = SelectedDeviceFile.Path;
                await LoadDeviceFilesCoreAsync(token);
                return;
            }

            SetConnectionState(ConnectionState.BusyReadingFiles, "READING FILE...", ConnectionMode, $"Opening {SelectedDeviceFile.Path}...");
            if (ConnectionMode.Equals("CircuitPython USB Drive", StringComparison.OrdinalIgnoreCase))
            {
                var fullPath = Path.GetFullPath(SelectedDeviceFile.Path);
                if (new FileInfo(fullPath).Length > 262144)
                {
                    throw new InvalidOperationException("File exceeds the safe editor read limit of 256 KB.");
                }
                EditorText = await File.ReadAllTextAsync(fullPath, token);
            }
            else
            {
                EnsureConnectedOrThrow();
                EditorText = await _deviceService.ReadTextFileAsync(SelectedDeviceFile.Path, token);
            }

            DeviceTargetPath = SelectedDeviceFile.Path;
            EditorFileName = SelectedDeviceFile.Name;
            EditorState = "Device file opened";
            SetConnectionState(ConnectionMode.Equals("CircuitPython USB Drive", StringComparison.OrdinalIgnoreCase) ? ConnectionState.ConnectedSerial : ConnectionState.ConnectedRawRepl, DeviceStatusForConnected(), ConnectionMode, $"Opened '{SelectedDeviceFile.Path}'.");
            AppendConsole($"Opened device file '{SelectedDeviceFile.Path}'.");
        });
    }

    [RelayCommand]
    private async Task SaveEditorToDeviceAsync()
    {
        await RunDeviceOperationAsync("Save to device", TimeSpan.FromSeconds(30), async token =>
        {
            if (!SecurityValidators.IsSafeDevicePath(DeviceTargetPath) && !ConnectionMode.Equals("CircuitPython USB Drive", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Unsafe device path blocked.");
            }

            SetConnectionState(ConnectionState.BusyWritingFile, "SAVING FILE...", ConnectionMode, $"Saving {DeviceTargetPath}...");
            if (ConnectionMode.Equals("CircuitPython USB Drive", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(SelectedCircuitPythonDrive))
            {
                var root = Path.GetFullPath(SelectedCircuitPythonDrive);
                var safeName = Path.GetFileName(DeviceTargetPath);
                if (string.IsNullOrWhiteSpace(safeName))
                {
                    safeName = "code.py";
                }
                var fullPath = Path.GetFullPath(Path.Combine(root, safeName));
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Blocked CircuitPython path traversal.");
                }
                await File.WriteAllTextAsync(fullPath, EditorText, token);
                LoadCircuitPythonFiles(root);
            }
            else
            {
                EnsureConnectedOrThrow();
                await _deviceService.WriteTextFileAsync(DeviceTargetPath, EditorText, token);
                await LoadDeviceFilesCoreAsync(token);
            }

            EditorFileName = Path.GetFileName(DeviceTargetPath);
            EditorState = "Saved to device";
            AppendConsole($"Saved editor to device path '{DeviceTargetPath}'.");
        });
    }

    [RelayCommand]
    private async Task RunEditorAsync()
    {
        await RunDeviceOperationAsync("Run editor", TimeSpan.FromSeconds(30), async token =>
        {
            EnsureConnectedOrThrow();
            SetConnectionState(ConnectionState.RunningCode, "RUNNING...", ConnectionMode, "Running editor buffer over MicroPython raw REPL...");
            var output = await _deviceService.RunCodeAsync(EditorText, token);
            SetConnectionState(ConnectionState.ConnectedRawRepl, DeviceStatusForConnected(), "MicroPython Raw REPL", "Run completed.");
            AppendConsole(output.Length == 0 ? "Run completed with no output." : output);
        });
    }

    [RelayCommand]
    private async Task RunSelectionAsync()
    {
        await RunDeviceOperationAsync("Run selection", TimeSpan.FromSeconds(20), async token =>
        {
            EnsureConnectedOrThrow();
            var code = string.IsNullOrWhiteSpace(FindText) ? EditorText : FindText;
            var output = await _deviceService.RunCodeAsync(code, token);
            AppendConsole(output.Length == 0 ? "Selection run completed with no output." : output);
        });
    }

    [RelayCommand]
    private async Task SendReplAsync()
    {
        await RunDeviceOperationAsync("Send REPL", TimeSpan.FromSeconds(20), async token =>
        {
            EnsureConnectedOrThrow();
            var output = await _deviceService.RunCodeAsync(ReplInput, token);
            AppendConsole($">>> {ReplInput}");
            AppendConsole(output.Length == 0 ? "OK" : output);
        });
    }

    [RelayCommand]
    private async Task StopProgramAsync()
    {
        await RunDeviceOperationAsync("Stop", TimeSpan.FromSeconds(8), async token =>
        {
            EnsureConnectedOrThrow();
            await _deviceService.StopProgramAsync(token);
            SetConnectionState(ConnectionState.ConnectedSerial, DeviceStatusForConnected(), "Serial Monitor", "Interrupt sent. Press Read Files to re-enter raw REPL.");
            AppendConsole("Stop/interrupt sent.");
        });
    }

    [RelayCommand]
    private async Task SoftRebootAsync()
    {
        await RunDeviceOperationAsync("Soft reboot", TimeSpan.FromSeconds(20), async token =>
        {
            EnsureConnectedOrThrow();
            await _deviceService.SoftRebootAsync(token);
            SetConnectionState(ConnectionState.ConnectedSerial, DeviceStatusForConnected(), "Serial Monitor", "Soft reboot sent. Rechecking raw REPL...");
            AppendConsole("Soft reboot sent. Refreshing files...");
            await Task.Delay(1000, token);
            await LoadDeviceFilesCoreAsync(token);
        });
    }

    [RelayCommand]
    private async Task GoDeviceParentAsync()
    {
        await RunDeviceOperationAsync("Parent directory", TimeSpan.FromSeconds(12), async token =>
        {
            if (DevicePath == "/" || string.IsNullOrWhiteSpace(DevicePath))
            {
                return;
            }
            var trimmed = DevicePath.Trim('/');
            var slash = trimmed.LastIndexOf('/', (int)StringComparison.Ordinal);
            DevicePath = slash < 0 ? "/" : trimmed[..slash];
            await LoadDeviceFilesCoreAsync(token);
        });
    }

    [RelayCommand]
    private void ClearProblems()
    {
        Problems.Clear();
    }

    [RelayCommand]
    private void ClearConsole()
    {
        ConsoleText = string.Empty;
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        var diagnostics = $"Status={DeviceStatus}; Mode={ConnectionMode}; State={ConnectionStateText}; Port={_serialPortService.PortName ?? "none"}; Problems={Problems.Count}";
        AppendConsole("Diagnostics: " + diagnostics);
    }

    private async Task RunDeviceOperationAsync(string operationName, TimeSpan timeout, Func<CancellationToken, Task> operation)
    {
        if (!await _operationLock.WaitAsync(0))
        {
            AddProblem("Info", operationName, "Device is busy.", "Wait for the current operation to finish, then retry.");
            return;
        }

        IsDeviceBusy = true;
        _operationCts?.Dispose();
        _operationCts = new CancellationTokenSource(timeout);
        try
        {
            await operation(_operationCts.Token);
        }
        catch (OperationCanceledException)
        {
            SetConnectionState(ConnectionState.ErrorRecoverable, DeviceStatusForConnected(), ConnectionMode, $"{operationName} timed out or was cancelled.");
            AddProblem("Warning", operationName, "Operation timed out or was cancelled.", "Try again, or disconnect/reconnect if the board is busy.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or TimeoutException)
        {
            SetConnectionState(ConnectionState.ErrorRecoverable, _serialPortService.IsConnected ? DeviceStatusForConnected() : "DISCONNECTED", _serialPortService.IsConnected ? ConnectionMode : "None", ex.Message);
            AddProblem("Error", operationName, ex.Message, "The app stayed alive; review REPL/log output and retry safely.");
            AppendConsole($"{operationName} failed: {ex.Message}");
        }
        finally
        {
            IsDeviceBusy = false;
            _operationLock.Release();
        }
    }

    private void EnsureConnectedOrThrow()
    {
        if (_serialPortService.IsConnected)
        {
            return;
        }
        SetConnectionState(ConnectionState.Disconnected, "DISCONNECTED", "None", "Device is not connected.");
        throw new InvalidOperationException("Device is not connected.");
    }

    private void LoadCircuitPythonFiles(string path)
    {
        var root = string.IsNullOrWhiteSpace(SelectedCircuitPythonDrive) ? path : SelectedCircuitPythonDrive;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            throw new InvalidOperationException("CircuitPython drive is not available.");
        }

        var current = string.IsNullOrWhiteSpace(path) || path == "/" ? root : path;
        var currentFull = Path.GetFullPath(current);
        var rootFull = Path.GetFullPath(root);
        if (!currentFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Blocked CircuitPython path traversal.");
        }

        DeviceFiles.Clear();
        foreach (var directory in Directory.EnumerateDirectories(currentFull).OrderBy(static d => d, StringComparer.OrdinalIgnoreCase))
        {
            var info = new DirectoryInfo(directory);
            DeviceFiles.Add(new DeviceFileItem(info.Name, info.FullName, true, -1));
        }
        foreach (var file in Directory.EnumerateFiles(currentFull).Where(IsSupportedEditorFile).OrderBy(static f => f, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            DeviceFiles.Add(new DeviceFileItem(info.Name, info.FullName, false, info.Length));
        }
        if (DeviceFiles.Count == 0)
        {
            SetPlaceholderDeviceFiles("CircuitPython drive folder is empty");
        }
        SetConnectionState(ConnectionState.ConnectedSerial, "CIRCUITPY MOUNTED", "CircuitPython USB Drive", $"Loaded files from {currentFull}.");
    }

    private void SetConnectionState(ConnectionState state, string status, string mode, string hint)
    {
        ConnectionStateText = state.ToString();
        DeviceStatus = status;
        ConnectionMode = mode;
        ConnectionHint = hint;
    }

    private string DeviceStatusForConnected()
    {
        return _serialPortService.IsConnected ? $"CONNECTED: {_serialPortService.PortName}" : DeviceStatus;
    }

    private void SetPlaceholderDeviceFiles(string message)
    {
        DeviceFiles.Clear();
        DeviceFiles.Add(new DeviceFileItem(message, message, false, -1));
    }

    private void AddProblem(string severity, string area, string message, string hint)
    {
        Problems.Insert(0, new ProblemItem(severity, area, message, hint));
        while (Problems.Count > 80)
        {
            Problems.RemoveAt(Problems.Count - 1);
        }
    }

    private void AppendConsole(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }
        ConsoleText += message.EndsWith('\n') ? message : message + Environment.NewLine;
        if (ConsoleText.Length > 200000)
        {
            ConsoleText = ConsoleText[^120000..];
        }
    }

    private void EnsureProjectWorkspace()
    {
        Directory.CreateDirectory(_projectRoot);
        var sample = Path.Combine(_projectRoot, "main.py");
        if (!File.Exists(sample))
        {
            File.WriteAllText(sample, "# ForgeStudio Circuit local workspace\nprint('hello from local main.py')\n");
        }
    }

    private void UpdateEditorMetrics()
    {
        var normalized = EditorText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lineCount = normalized.Length == 0 ? 1 : normalized.Count(static c => c == '\n') + 1;
        EditorCaretStatus = $"Lines {lineCount}  |  {EditorText.Length} chars";

        EditorLineNumbers.Clear();
        for (var i = 1; i <= Math.Min(lineCount, 5000); i++)
        {
            EditorLineNumbers.Add(i);
        }
    }

    private void UpdateEditorLanguage(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".py":
                CurrentLanguage = "Python / MicroPython";
                EditorHighlightingName = "Python";
                EditorAssistText = "Python IDE mode: line numbers, language detection, snippets, find, comment toggle, indent cleanup, validation, run, local save, and device save are enabled. Syntax coloring is handled by the internal editor-safe theme layer without external editor packages.";
                break;
            case ".json":
                CurrentLanguage = "JSON";
                EditorHighlightingName = "JavaScript";
                EditorAssistText = "JSON IDE mode: validation, line numbers, find, and device/local save are enabled. Use double-quoted keys/strings and avoid trailing commas.";
                break;
            case ".toml":
            case ".ini":
                CurrentLanguage = "TOML / Config";
                EditorHighlightingName = "INI Files";
                EditorAssistText = "TOML/config mode: key/value editing, sections, find, snippets, save local, save device, and validation guardrails.";
                break;
            case ".md":
                CurrentLanguage = "Markdown";
                EditorHighlightingName = "XML";
                EditorAssistText = "Markdown notes mode: use headings, fenced code blocks, and concise device documentation.";
                break;
            default:
                CurrentLanguage = "Plain Text";
                EditorHighlightingName = string.Empty;
                EditorAssistText = "Plain text mode: line numbers, find, snippets, local save, device save, and run/validation controls are available.";
                break;
        }
    }

    private static bool IsVisibleProjectFile(string name)
    {
        if (name.Contains(".deps.", StringComparison.OrdinalIgnoreCase)
            || name.Contains(".runtimeconfig", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsSupportedEditorFile(name);
    }

    private static bool IsSupportedEditorFile(string path)
    {
        return path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }
}
