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
  "ModuleId": "",
  "IsActivityNode": false,
  "ActivityLogThreshold": 1000,
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

### Console Mode (Development)

```bash
cd src/IoTPowerShellAgent
dotnet run
```

This mode allows you to see console output and debug the service.

### Windows Service Mode (Production)

1. Build the project in Release mode
2. Install as a Windows Service using `sc` or a service installer tool
3. Configure the service to run under an appropriate account

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

- Store connection strings securely
- Limit script execution permissions
- Monitor activity logs for suspicious activity
- Use Azure IoT Hub device authentication
- Review and audit all executed scripts

## Troubleshooting

- **Service won't start**: Check Event Viewer for errors
- **IoT Hub connection fails**: Verify connection string and network connectivity
- **Scripts timeout**: Adjust `ScriptTimeoutSeconds` in configuration
- **High memory usage**: Monitor via telemetry and adjust `ActivityLogThreshold`

## License

[Add your license information here]

## Contributing

[Add contribution guidelines if applicable]
