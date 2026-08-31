# IoTPowerShellAgent

**A C# Windows Service for remotely executing PowerShell through Azure IoT Hub.**

`IoTPowerShellAgent` is a Windows-based execution agent that hosts PowerShell inside a .NET application and exposes controlled script execution through Azure IoT Hub direct methods.

The agent is implemented entirely in C#, using the PowerShell SDK to host and execute PowerShell rather than relying on an external `powershell.exe` process for each request.

The project is designed for infrastructure and automation scenarios where a persistent Windows execution agent needs to receive work remotely, execute PowerShell locally, and return structured results and telemetry.

> ⚠️ **Security Warning:** The default installer configures the service to run as `NT AUTHORITY\SYSTEM`. Any script submitted to the agent executes with SYSTEM-level privileges. Access to the agent must be treated as privileged remote code execution. See [Security model](#security-model) for deployment recommendations.

## Architecture

```text
                         Azure
                           │
                    ┌──────▼──────┐
                    │   IoT Hub   │
                    └──────┬──────┘
                           │
                     Direct Methods
                           │
                           ▼
              ┌────────────────────────┐
              │  IoTPowerShellAgent    │
              │                        │
              │   C# / .NET 8          │
              │          │             │
              │          ▼             │
              │   PowerShell SDK       │
              │          │             │
              │          ▼             │
              │    PowerShell Host     │
              │          │             │
              │          ▼             │
              │    Script Execution    │
              │                        │
              │   ┌────────────────┐   │
              │   │ Telemetry      │   │
              │   │ JSON Results   │   │
              │   │ Metrics        │   │
              │   │ Twin State     │   │
              │   └────────────────┘   │
              └────────────────────────┘
                           │
                           ▼
                    Local Windows OS
```

The service maintains a persistent application process and hosts PowerShell through the PowerShell SDK. This allows PowerShell execution, telemetry, process monitoring, and Azure IoT integration to be managed within the same .NET application.

## Why C#?

The agent is intentionally implemented as a C# application rather than a PowerShell script.

This provides a persistent .NET host for:

- Windows Service lifecycle management
- Azure IoT Hub connectivity
- PowerShell runspace management
- structured execution results
- process and system telemetry
- cancellation and execution timeouts
- native Windows and .NET APIs
- long-running service operation

PowerShell remains the execution language exposed to the caller, while C# provides the host and execution infrastructure around it.

## Core capabilities

### Remote PowerShell execution

PowerShell scripts can be submitted through Azure IoT Hub direct methods and executed locally by the agent.

### Azure IoT Hub integration

The agent supports:

- Direct Methods
- Device Twins
- Module Twins
- telemetry
- device/module identity
- remote execution workflows

### PowerShell SDK hosting

PowerShell is hosted directly inside the .NET process, allowing the application to manage execution, streams, cancellation, and results without treating PowerShell as an external command-line process.

### Structured execution results

PowerShell output streams are captured and represented as structured execution results.

Supported streams include:

- Output
- Error
- Warning
- Verbose
- Debug
- Information
- Progress

### JSON serialization

The project includes an embedded PowerShell JSON conversion module for serializing PowerShell-specific and complex .NET objects into representations suitable for workflow and API consumption.

### Performance and system telemetry

The agent collects process and system metrics using native Windows/.NET APIs and P/Invoke where required.

### Windows Service

The same application can operate as:

- an interactive debugging environment
- a console application
- a Windows Service

This allows the execution engine to be tested independently before being deployed as a persistent service.

## Project structure

```text
src/IoTPowerShellAgent/

├── Core/
│   └── Domain models and core services
│
├── PowerShell/
│   └── PowerShell hosting and execution
│
├── IoT/
│   └── Azure IoT Hub integration
│
├── Services/
│   └── Windows Service implementation
│
└── Utilities/
    └── Serialization, process management,
        telemetry and supporting functionality
```

See [`docs/PROJECT_STRUCTURE.md`](docs/PROJECT_STRUCTURE.md) for a detailed breakdown.

## Requirements

- .NET 8 SDK or later
- Windows 10/11 or Windows Server 2016+
- PowerShell 5.1 or later
- Azure IoT Hub for remote execution

The agent is a Windows application because its execution model relies on Windows Service functionality and Windows PowerShell/system APIs.

## Configuration

The agent can be configured through `appsettings.json` and the application's supported configuration providers.

Example:

```json
{
  "IoTHubConnectionString": "HostName=your-iothub.azure-devices.net;DeviceId=your-device-id;SharedAccessKey=your-key",
  "DeviceId": "your-device-id",
  "ScriptTimeoutSeconds": 300
}
```

**Do not commit production credentials to configuration files or source control.**

For production deployments, credentials should be supplied through an appropriate secret-management or protected configuration mechanism.

For IoT Edge deployments, configure the appropriate `ModuleId`.

## Building

```bash
dotnet build src/IoTPowerShellAgent/IoTPowerShellAgent.csproj
```

Or from the repository root:

```bash
dotnet build
```

## Running

### Debug mode

Debug mode runs the execution environment interactively without requiring an IoT Hub connection.

```bash
dotnet run -- --debug
```

Available commands include:

```text
Get-Date
Get-Process | Select-Object -First 5 Name, CPU
base64:<encoded-script>
metrics
sample
help
exit
```

This mode is intended for development and troubleshooting.

### Test mode

Execute a single local test without establishing an IoT Hub connection:

```bash
dotnet run -- --test
dotnet run -- --test --script="Get-Date"
dotnet run -- --test --script=<base64> --base64
dotnet run -- --test --metrics
```

### Console mode

Run the service host interactively:

```bash
dotnet run
```

When configured, the application connects to Azure IoT Hub and processes incoming requests.

### Windows Service

Build a Release deployment and install the service using the provided installer:

```bash
IoTPowerShellAgent.exe install [orgId]
```

The default installation configures the service account as:

```text
NT AUTHORITY\SYSTEM
```

Start the service:

```bash
IoTPowerShellAgent.exe start [orgId]
```

Check service state:

```bash
IoTPowerShellAgent.exe status
```

Because the service executes PowerShell using the service account's privileges, service installation and IoT Hub access should be treated as privileged operations.

## Remote execution

The agent accepts PowerShell execution requests through Azure IoT Hub direct methods.

Example request:

```json
{
  "methodName": "ExecuteScript",
  "payload": {
    "Script": "Get-Process | Select-Object -First 5",
    "IsInlinePowershell": false
  }
}
```

Example response:

```json
{
  "Success": true,
  "Output": "...",
  "ErrorMessage": ""
}
```

The exact request and response schema is documented in the backend documentation.

## Security model

`IoTPowerShellAgent` provides a privileged remote execution capability. By default, the Windows Service runs as `NT AUTHORITY\SYSTEM`. This is intentional for environments requiring privileged local administration, but it means any compromise of the execution boundary could result in SYSTEM-level code execution on the host.

Because of this, the agent's security model depends heavily on protecting the communication and authorization boundary around Azure IoT Hub. Consider the following deployment requirements:

- **Least Privilege:** Deployments that do not require SYSTEM privileges should configure the Windows Service to use a more restricted service identity.
- **Access Control:** Strictly restrict which Azure identities can invoke execution (via Direct Methods) on the device.
- **Credential Protection:** Protect IoT Hub credentials and avoid storing secrets in source control.
- **Authentication:** Use device or module authentication appropriate to the deployment.
- **Visibility:** Audit executed scripts and monitor execution telemetry.
- **Network Hardening:** Restrict network access to the host where practical.

## Azure backend

The `backend/` directory contains the Azure infrastructure required to support the agent.

This includes the project's infrastructure components such as:

- Azure IoT Hub
- Storage
- Event Grid
- Function App
- supporting resources

See [`backend/README.md`](backend/README.md) for deployment instructions and architecture.

## Design goals

The project is designed around several principles:

**Persistent execution host**

Maintain a long-running .NET process rather than repeatedly starting a PowerShell process for every operation.

**Separation of concerns**

C# provides the service, transport, lifecycle, telemetry, and execution infrastructure while PowerShell provides the automation language.

**Structured results**

Treat PowerShell execution as a structured operation with explicit streams, status, errors, and metadata rather than returning raw console output alone.

**Remote manageability**

Use Azure IoT Hub as the remote communication and device-management layer.

**Operational visibility**

Expose execution state and system telemetry so the agent can be monitored as part of a larger automation platform.

## Technical Highlights

- **C# / .NET 8** application implementing the complete agent runtime
- **PowerShell SDK hosting** with PowerShell executed inside managed .NET runspaces
- **Windows Service** architecture with interactive console and test execution modes
- **Azure IoT Hub Direct Methods** for remote execution
- **Device and Module Twin integration** for device state and configuration
- **Structured PowerShell stream handling** across Output, Error, Warning, Verbose, Debug, Information, and Progress
- **Execution timeouts and cancellation** for long-running scripts
- **Native Windows / .NET telemetry** with P/Invoke where required
- **Structured JSON serialization** for PowerShell and .NET execution results
- **Infrastructure as Code** using Azure Bicep for the supporting Azure backend
- **Separation between transport, execution, service lifecycle, telemetry, and domain components**

## License

MIT License Calvindd2f

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md) for guidelines and instructions on how to contribute to the project.
