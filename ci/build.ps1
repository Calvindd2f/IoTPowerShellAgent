#Requires -Version 5.1
<#
.SYNOPSIS
    Build script for IoTPowerShellAgent project.

.DESCRIPTION
    Comprehensive build script that supports multiple configurations and build targets.
    Optimized for .NET 8.0 Windows Service projects with performance-first approach.

.PARAMETER Configuration
    Build configuration: Debug, Release, or All (default: Release)

.PARAMETER TargetFramework
    Target framework to build (default: net8.0-windows)

.PARAMETER NoRestore
    Skip restoring NuGet packages

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\ci\build.ps1
    Builds the project in Release configuration

.EXAMPLE
    .\ci\build.ps1 -Configuration Debug
    Builds the project in Debug configuration

.EXAMPLE
    .\ci\build.ps1 -Configuration All
    Builds both Debug and Release configurations
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release', 'All')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [string]$TargetFramework = 'net8.0-windows',
    
    [Parameter()]
    [switch]$NoRestore,
    
    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\IoTPowerShellAgent.csproj"

# Validate .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Cyan
$dotnetVersion = dotnet --version
if (-not $dotnetVersion) {
    Write-Error ".NET SDK not found. Please install .NET 8.0 SDK or later."
    exit 1
}
Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Green

# Validate project file exists
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

# Build function
function Build-Project {
    param(
        [string]$Config,
        [string]$Framework
    )
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "Building Configuration: $Config" -ForegroundColor Cyan
    Write-Host "Target Framework: $Framework" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
    
    $buildArgs = @(
        "build",
        "`"$ProjectPath`"",
        "--configuration", $Config,
        "--framework", $Framework,
        "--no-incremental"
    )
    
    if ($NoRestore) {
        $buildArgs += "--no-restore"
    }
    
    if ($Verbose) {
        $buildArgs += "--verbosity", "detailed"
    } else {
        $buildArgs += "--verbosity", "minimal"
    }
    
    # Performance: Use parallel builds
    $buildArgs += "/p:BuildInParallel=true"
    
    # Performance: Enable deterministic builds for reproducibility
    $buildArgs += "/p:Deterministic=true"
    
    # Performance: Enable source link for better debugging
    $buildArgs += "/p:EnableSourceLink=true"
    
    # Performance: Enable assembly info generation
    $buildArgs += "/p:GenerateAssemblyInfo=true"
    
    $startTime = Get-Date
    $result = & dotnet $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for configuration: $Config"
        exit $LASTEXITCODE
    }
    
    $duration = (Get-Date) - $startTime
    Write-Host "`nBuild completed successfully in $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
    
    # Output build artifacts location
    $outputPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\bin\$Config\$Framework"
    if (Test-Path $outputPath) {
        Write-Host "Output directory: $outputPath" -ForegroundColor Gray
        $exePath = Join-Path $outputPath "IoTPowerShellAgent.exe"
        if (Test-Path $exePath) {
            $fileInfo = Get-Item $exePath
            Write-Host "Executable size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
        }
    }
}

# Main build logic
try {
    Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  IoTPowerShellAgent Build Script      ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
    
    $overallStartTime = Get-Date
    
    if ($Configuration -eq 'All') {
        Build-Project -Config 'Debug' -Framework $TargetFramework
        Build-Project -Config 'Release' -Framework $TargetFramework
    } else {
        Build-Project -Config $Configuration -Framework $TargetFramework
    }
    
    $overallDuration = (Get-Date) - $overallStartTime
    Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  Build Completed Successfully!        ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host "Total build time: $($overallDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
    
    exit 0
}
catch {
    Write-Error "Build script failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}

