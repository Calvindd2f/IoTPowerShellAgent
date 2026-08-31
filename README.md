# IoTPowerShellAgent - PowerShell Executor Service

A Windows Service for executing PowerShell scripts remotely via Azure IoT Hub. This service provides secure, scalable PowerShell execution capabilities with comprehensive logging and telemetry.

## Features

- **PowerShell Execution**: Execute PowerShell scripts through Azure IoT Hub direct methods
- **Azure IoT Hub Integration**: Full support for device/module twins, telemetry, and direct methods
- **Windows Service**: Runs as a Windows Service with console mode for debugging
- **Comprehensive Logging**: Streams all PowerShell output types (Verbose, Debug, Warning, Error, Information, Progress)
- **JSON API Support**: Built-in JSON conversion utilities for PowerShell objects (embedded module)
- **Performance Monitoring**: Process and system performance tracking using P/Invoke
- **Activity Node Support**: Configurable activity node mode for workflow integration

## Azure Infrastructure

The complete Azure backend infrastructure (IoT Hub, Storage, Event Grid, Function App, etc.) can be deployed via the Bicep templates located in the `backend/` directory. See the [Backend Documentation](backend/README.md) for deployment instructions and architectural details.

## Project Structure

The project is organized into a clean, modular structure:

```
src/IoTPowerShellAgent/
├── Core/          # Domain models and core services
├── PowerShell/    # PowerShell host and executor
├── IoT/           # Azure IoT Hub integration
├── Services/      # Windows Service implementation
└── Utilities/     # JSON conversion, process management, etc.
```

See [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for detailed structure information.

## Prerequisites

- .NET 8.0 SDK or later
- Windows 10/11 or Windows Server 2016+
- Azure IoT Hub account (for IoT Hub integration)
- PowerShell 5.1 or later (included with Windows)

## Configuration

1. Copy `config/appsettings.json` and configure your settings:

```json
{
  "IoTHubConnectionString": "HostName=your-iothub.azure-devices.net;DeviceId=your-device-id;SharedAccessKey=your-key",
  "DeviceId": "your-device-id",
  "ScriptTimeoutSeconds": 300
}
```

2. For IoT Edge modules, set the `ModuleId` property.

## Building

```bash
cd src/IoTPowerShellAgent
dotnet build
```

Or from the root directory:

```bash
dotnet build src/IoTPowerShellAgent/IoTPowerShellAgent.csproj
```

## Running

### Debug Mode (Interactive Testing)

Interactive testing mode that allows you to test PowerShell execution without connecting to IoT Hub:

```bash
cd src/IoTPowerShellAgent
dotnet run -- --debug
# or
dotnet run -- -d
```

This mode provides an interactive shell where you can:
- Execute PowerShell commands directly
- Test base64-encoded scripts
- View environment metrics
- Run sample test scripts

**Example Usage:**
```
PS> Get-Date
PS> Get-Process | Select-Object -First 5 Name, CPU
PS> base64:RwBlAHQALQBEAGEAdABlAA==
PS> metrics
PS> sample
PS> help
PS> exit
```

### Test Mode (One-Time Execution)

Run a single test without IoT Hub connection:

```bash
dotnet run -- --test                           # Run sample test
dotnet run -- --test --script="Get-Date"       # Test custom script
dotnet run -- --test --script=<base64> --base64 # Test base64 script
dotnet run -- --test --metrics                # Test environment metrics
```

### Console Mode (Development)

```bash
cd src/IoTPowerShellAgent
dotnet run
```

This mode runs the service as a console application, connecting to IoT Hub if configured. Use `--debug` for testing without IoT Hub.

### Windows Service Mode (Production)

1. Build the project in Release mode
2. Install the service using the installer:
   ```bash
   IoTPowerShellAgent.exe install [orgId]
   ```
   The installer automatically configures the service to run as `NT AUTHORITY\SYSTEM`, ensuring PowerShell scripts execute with SYSTEM privileges.
3. Update the configuration file with your IoT Hub connection string
4. Start the service:
   ```bash
   IoTPowerShellAgent.exe start [orgId]
   ```

**Note**: The service is configured to run as `NT AUTHORITY\SYSTEM` by default. All PowerShell scripts executed through the service will run with SYSTEM privileges. Use the `status` command to verify the service account configuration.

## Usage via IoT Hub

### Execute Script via Direct Method

```json
{
  "methodName": "ExecuteScript",
  "payload": {
    "Script": "Get-Process | Select-Object -First 5",
    "IsInlinePowershell": false
  }
}
```

### Response

```json
{
  "Success": true,
  "Output": "...",
  "ErrorMessage": ""
}
```

## JSON Conversion Module

The service includes an embedded JSON conversion module (formerly `powershellruntimeextension`) that provides advanced JSON serialization for PowerShell objects. This enables:

- Complex object serialization
- PowerShell-specific type handling
- API integration for workflows
- Direct result redirection to external APIs

Example usage in PowerShell scripts:

```powershell
$result = Get-Process | Select-Object Name, CPU
# Results can be automatically converted to JSON via PowerShellExecutionResult.ToJson()
```

## Security Considerations

- **Service Account**: The service runs as `NT AUTHORITY\SYSTEM` by default, giving executed PowerShell scripts full system privileges. This is intentional for maximum script execution capabilities, but requires careful access control to the IoT Hub.
- Store connection strings securely
- Limit script execution permissions through IoT Hub access control
- Monitor activity logs for suspicious activity
- Use Azure IoT Hub device authentication
- Review and audit all executed scripts
- Only grant access to trusted devices/modules in Azure IoT Hub

## Troubleshooting

- **Service won't start**: Check Event Viewer for errors
- **IoT Hub connection fails**: Verify connection string and network connectivity
- **Scripts timeout**: Adjust `ScriptTimeoutSeconds` in configuration

## License

[Add your license information here]

## Contributing

[Add contribution guidelines if applicable]
