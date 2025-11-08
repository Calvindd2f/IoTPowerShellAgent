#Requires -Version 5.1
<#
.SYNOPSIS
    Publish IoTPowerShellAgent for deployment.

.DESCRIPTION
    Creates production-ready publish artifacts for Windows Service deployment.
    Supports self-contained and framework-dependent deployments.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER Runtime
    Target runtime identifier (default: win-x64)

.PARAMETER Output
    Output directory for published artifacts (default: ./publish)

.PARAMETER SelfContained
    Create self-contained deployment (includes .NET runtime)

.PARAMETER SingleFile
    Publish as single-file executable

.PARAMETER Trimmed
    Enable assembly trimming (reduces size)

.PARAMETER ReadyToRun
    Enable ReadyToRun compilation (improves startup performance)

.EXAMPLE
    .\ci\publish.ps1
    Publish framework-dependent deployment for win-x64

.EXAMPLE
    .\ci\publish.ps1 -SelfContained -SingleFile -Trimmed
    Publish optimized self-contained single-file deployment

.EXAMPLE
    .\ci\publish.ps1 -Runtime win-arm64 -Output ./publish-arm64
    Publish for Windows ARM64 architecture
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    
    [Parameter()]
    [string]$Output = './publish',
    
    [Parameter()]
    [switch]$SelfContained,
    
    [Parameter()]
    [switch]$SingleFile,
    
    [Parameter()]
    [switch]$Trimmed,
    
    [Parameter()]
    [switch]$ReadyToRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\IoTPowerShellAgent.csproj"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Publish Script                       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Validate project file
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

# Create output directory
$publishPath = Join-Path $Output $Runtime
if (Test-Path $publishPath) {
    Write-Host "Cleaning existing publish directory..." -ForegroundColor Yellow
    Remove-Item -Path $publishPath -Recurse -Force
}
New-Item -ItemType Directory -Path $publishPath -Force | Out-Null

Write-Host "Publishing configuration:" -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration" -ForegroundColor Gray
Write-Host "  Runtime: $Runtime" -ForegroundColor Gray
Write-Host "  Output: $publishPath" -ForegroundColor Gray
Write-Host "  Self-contained: $SelfContained" -ForegroundColor Gray
Write-Host "  Single file: $SingleFile" -ForegroundColor Gray
Write-Host "  Trimmed: $Trimmed" -ForegroundColor Gray
Write-Host "  ReadyToRun: $ReadyToRun" -ForegroundColor Gray
Write-Host ""

# Build publish arguments
$publishArgs = @(
    "publish",
    "`"$ProjectPath`"",
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--output", "`"$publishPath`"",
    "--no-build" # We'll build separately for better control
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
} else {
    $publishArgs += "--self-contained", "false"
}

if ($SingleFile) {
    $publishArgs += "/p:PublishSingleFile=true"
}

if ($Trimmed) {
    $publishArgs += "/p:PublishTrimmed=true"
}

if ($ReadyToRun) {
    $publishArgs += "/p:PublishReadyToRun=true"
}

# Performance optimizations
$publishArgs += "/p:DebugType=none" # Reduce size in Release
$publishArgs += "/p:DebugSymbols=false" # No symbols in publish
$publishArgs += "/p:IncludeNativeLibrariesForSelfExtract=true" # For single-file

# Build first
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build "$ProjectPath" --configuration $Configuration --runtime $Runtime --no-incremental
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Publish
Write-Host "`nPublishing application..." -ForegroundColor Cyan
$startTime = Get-Date
& dotnet $publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed"
    exit 1
}

$duration = (Get-Date) - $startTime

# Calculate publish size
$publishSize = (Get-ChildItem -Path $publishPath -Recurse -File | 
    Measure-Object -Property Length -Sum).Sum
$publishSizeMB = [math]::Round($publishSize / 1MB, 2)

# Copy configuration file if it exists
$configSource = Join-Path $ScriptRoot "config\appsettings.json"
if (Test-Path $configSource) {
    $configDest = Join-Path $publishPath "appsettings.json"
    Copy-Item -Path $configSource -Destination $configDest -Force
    Write-Host "  ✓ Configuration file copied" -ForegroundColor Green
}

# Create deployment notes
$notesPath = Join-Path $publishPath "DEPLOYMENT_NOTES.txt"
$notes = @"
IoTPowerShellAgent Deployment Package
=====================================

Published: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
Configuration: $Configuration
Runtime: $Runtime
Self-contained: $SelfContained
Single file: $SingleFile
Trimmed: $Trimmed
ReadyToRun: $ReadyToRun

Package Size: $publishSizeMB MB

Installation:
1. Copy all files to target system
2. Run: IoTPowerShellAgent.exe install [orgId]
3. Configure appsettings.json with IoT Hub credentials
4. Run: IoTPowerShellAgent.exe start [orgId]

For more information, see docs/README.md
"@
Set-Content -Path $notesPath -Value $notes

Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Publish Completed Successfully!      ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
Write-Host "Output directory: $publishPath" -ForegroundColor Green
Write-Host "Package size: $publishSizeMB MB" -ForegroundColor Green
Write-Host "Publish time: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
Write-Host "`nDeployment notes: $notesPath" -ForegroundColor Gray

exit 0

