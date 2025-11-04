# Testing Guide

## Local Testing Without IoT Hub

The project includes a local testing mode that allows you to test PowerShell execution without connecting to Azure IoT Hub.

### Quick Start

```bash
cd src/IoTPowerShellAgent
dotnet run -- --test
```

This will run a sample PowerShell script and display the results.

### Test Modes

#### 1. Sample Script Test
```bash
dotnet run -- --test
```
Executes a sample script that lists processes.

#### 2. Base64 Encoding Test
```bash
dotnet run -- --test --base64
```
Tests the base64 decoding functionality with an encoded script.

#### 3. Custom Script Test
```bash
dotnet run -- --test --script="Get-Date"
```
Executes a custom PowerShell script directly.

#### 4. Base64 Encoded Custom Script
```bash
# First encode your script
$script = "Get-Process | Select-Object -First 5"
$base64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($script))

# Then run it
dotnet run -- --test --script="$base64" --base64
```

## Testing with Azure IoT Hub

### Prerequisites
1. Azure IoT Hub instance
2. Device or module registered in IoT Hub
3. Connection string configured in `config/appsettings.json`

### Building for Production

```bash
cd src/IoTPowerShellAgent
dotnet build -c Release
```

### Running as Console (for Testing)

```bash
cd src/IoTPowerShellAgent
dotnet run
```

This starts the service in console mode where you can see logs and debug output.

### Testing IoT Hub Direct Methods

#### Using Azure CLI

```bash
az iot hub invoke-device-method \
  --hub-name <your-iothub-name> \
  --device-id <your-device-id> \
  --method-name ExecuteScript \
  --method-payload '{"Script":"Get-Date","IsInlinePowershell":false}'
```

#### Using Azure Portal

1. Navigate to your IoT Hub in Azure Portal
2. Go to **IoT devices** → Select your device
3. Click **Direct method**
4. Method name: `ExecuteScript`
5. Payload:
```json
{
  "Script": "Get-Process | Select-Object -First 5",
  "IsInlinePowershell": false
}
```

#### Using C# Test Client

Create a simple test application:

```csharp
using Microsoft.Azure.Devices;

var serviceClient = ServiceClient.CreateFromConnectionString("<your-service-connection-string>");
var methodInvocation = new CloudToDeviceMethod("ExecuteScript")
{
    ResponseTimeout = TimeSpan.FromSeconds(30)
};

var payload = new
{
    Script = "Get-Date",
    IsInlinePowershell = false
};

methodInvocation.SetPayloadJson(JsonSerializer.Serialize(payload));
var response = await serviceClient.InvokeDeviceMethodAsync("<device-id>", methodInvocation);
Console.WriteLine(response.GetPayloadAsJson());
```

## Base64 Encoding Support

### Is Base64 Required?

**No**, Azure IoT Hub does not require base64 encoding for direct method payloads. However, the service supports it for cases where:

1. Scripts contain special characters that need escaping
2. Scripts are large and need compression/encoding
3. You want to obfuscate script content

### When to Use Base64

- Scripts with complex quotes and escaping issues
- Multi-line scripts that are easier to manage as base64
- Integration with systems that prefer base64 encoding

### Example: Base64 Encoded Request

```json
{
  "Script": "V3JpdGUtSG9zdCAnSGVsbG8gV29ybGQhJzsgR2V0LURhdGU=",
  "IsInlinePowershell": false,
  "IsBase64Encoded": true
}
```

The service will automatically detect and decode base64 scripts if `IsBase64Encoded` is true or if the script appears to be base64 encoded.

## Monitoring and Debugging

### View Logs in Console Mode

When running in console mode (`dotnet run`), all logs are displayed directly in the console.

### View IoT Hub Telemetry

All execution logs are sent as telemetry to IoT Hub. You can view them in:

1. **Azure Portal** → IoT Hub → **Monitoring** → **Telemetry**
2. **Azure IoT Explorer** tool
3. **Azure CLI**: `az iot hub monitor-events --hub-name <name>`

### Device Twin Status

Service status and performance metrics are reported to the device twin. View them in:

1. **Azure Portal** → IoT Hub → **IoT devices** → **Device twin**
2. Look for `serviceStatus`, `memoryUsageMB`, `cpuUsagePercent`, etc.

## Troubleshooting

### Service Won't Start
- Check `config/appsettings.json` exists and is valid
- Verify IoT Hub connection string
- Check Event Viewer for errors

### Script Execution Fails
- Test locally first using `--test` mode
- Check script syntax in PowerShell ISE
- Review error messages in IoT Hub telemetry

### Base64 Decode Errors
- Ensure script is properly base64 encoded
- Verify UTF-8 encoding is used
- Try without base64 encoding first to isolate issues
