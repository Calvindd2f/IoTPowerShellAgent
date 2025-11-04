# IoTPowerShellAgent Project Structure

## Overview
IoTPowerShellAgent is a PowerShell executor service integrated with Azure IoT Hub, designed to run as a Windows Service.

## Directory Structure

```
iot_powershell_agent/
├── src/
│   └── IoTPowerShellAgent/              # Main project source code
│       ├── Core/                # Core domain models and services
│       │   ├── PowerShellExecutionResult.cs
│       │   ├── SettingsService.cs
│       │   ├── LogOutputType.cs
│       │   └── EHExceptionRecord.cs
│       ├── PowerShell/          # PowerShell host implementations
│       │   ├── PowerShellExecutor.cs
│       │   ├── DefaultHost.cs
│       │   ├── DefaultHostUserInterface.cs
│       │   └── DefaultHostRawUserInterface.cs
│       ├── IoT/                 # Azure IoT Hub integration
│       │   └── IoTHubService.cs
│       ├── Services/            # Windows Service implementation
│       │   └── PowerShellExecutorService.cs
│       ├── Utilities/           # Utility classes and helpers
│       │   ├── JsonObject.cs           # JSON conversion utilities (embedded from powershellruntimeextension)
│       │   ├── ConvertToJsonContext.cs
│       │   ├── ProcessUtil.cs
│       │   └── WindowsApiInterop.cs
│       ├── Program.cs           # Application entry point
│       └── IoTPowerShellAgent.csproj    # Project file
├── config/                      # Configuration files
│   └── appsettings.json
├── docs/                        # Documentation
│   ├── README.md
│   └── PROJECT_STRUCTURE.md
└── .gitignore                   # Git ignore rules

```

## Namespace Organization

- **IoTPowerShellAgent** - Root namespace (Program.cs)
- **IoTPowerShellAgent.Core** - Core domain models, settings, and result types
- **IoTPowerShellAgent.PowerShell** - PowerShell host and executor implementations
- **IoTPowerShellAgent.IoT** - Azure IoT Hub client and integration
- **IoTPowerShellAgent.Services** - Windows Service implementation
- **IoTPowerShellAgent.Utilities** - Utility classes including JSON conversion, process management, and Windows API interop

## Key Components

### Core
- **PowerShellExecutionResult**: Execution result model with JSON conversion support
- **SettingsService**: Singleton service for application configuration
- **LogOutputType**: Enumeration for log output types

### PowerShell
- **PowerShellExecutor**: Main executor for PowerShell scripts with stream handling
- **DefaultHost**: Custom PSHost implementation
- **DefaultHostUserInterface**: Custom PSHostUserInterface for non-interactive execution

### IoT
- **IoTHubService**: Manages Azure IoT Hub connections, direct methods, and telemetry

### Utilities
- **JsonObject**: Embedded PowerShell JSON conversion utilities (formerly in powershellruntimeextension)
  - Converts PowerShell objects to/from JSON
  - Supports complex PowerShell object structures
  - Used for API integration and workflow systems
- **ProcessUtil**: Process management and performance monitoring using P/Invoke
- **WindowsApiInterop**: Windows API declarations for performance-critical operations

## Configuration

Configuration is managed through `config/appsettings.json` and loaded via `SettingsService`. The service will check multiple locations for the configuration file to support different deployment scenarios.

## Building

```bash
dotnet build src/IoTPowerShellAgent/IoTPowerShellAgent.csproj
```

## Deployment

The service can run in two modes:
1. **Windows Service** - Production mode (requires service installation)
2. **Console Mode** - Development/debugging mode (when run interactively)

