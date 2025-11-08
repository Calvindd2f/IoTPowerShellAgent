#Requires -Version 5.1
<#
.SYNOPSIS
    Run code analysis and linting for IoTPowerShellAgent.

.DESCRIPTION
    Performs static code analysis using .NET analyzers and style checks.
    Reports code quality issues and style violations.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER TreatWarningsAsErrors
    Treat warnings as errors

.PARAMETER Output
    Output file for analysis results (default: ./analysis-results.txt)

.EXAMPLE
    .\ci\lint.ps1
    Run code analysis and display results

.EXAMPLE
    .\ci\lint.ps1 -TreatWarningsAsErrors
    Run analysis and fail on warnings
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$TreatWarningsAsErrors,
    
    [Parameter()]
    [string]$Output = './analysis-results.txt'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\IoTPowerShellAgent.csproj"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Code Analysis & Linting              ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Validate project file
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project file not found: $ProjectPath"
    exit 1
}

# Build with analysis
Write-Host "Building with code analysis..." -ForegroundColor Cyan

$buildArgs = @(
    "build",
    "`"$ProjectPath`"",
    "--configuration", $Configuration,
    "--no-incremental"
)

if ($TreatWarningsAsErrors) {
    $buildArgs += "/p:TreatWarningsAsErrors=true"
}

# Enable code analysis
$buildArgs += "/p:RunAnalyzersDuringBuild=true"
$buildArgs += "/p:EnableNETAnalyzers=true"

# Run build
$startTime = Get-Date
& dotnet $buildArgs 2>&1 | Tee-Object -FilePath $Output

$duration = (Get-Date) - $startTime
$exitCode = $LASTEXITCODE

# Analyze results
$outputContent = Get-Content $Output -Raw -ErrorAction SilentlyContinue
$warningCount = 0
$errorCount = 0

if ($outputContent) {
    $warningCount = ([regex]::Matches($outputContent, "warning", "IgnoreCase")).Count
    $errorCount = ([regex]::Matches($outputContent, "error", "IgnoreCase")).Count
}

Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor $(if ($exitCode -eq 0 -and $errorCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "║  Analysis Complete                    ║" -ForegroundColor $(if ($exitCode -eq 0 -and $errorCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor $(if ($exitCode -eq 0 -and $errorCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "Analysis time: $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Gray
Write-Host "Warnings: $warningCount" -ForegroundColor $(if ($warningCount -eq 0) { "Green" } else { "Yellow" })
Write-Host "Errors: $errorCount" -ForegroundColor $(if ($errorCount -eq 0) { "Green" } else { "Red" })
Write-Host "Results file: $Output" -ForegroundColor Gray

if ($exitCode -ne 0 -or ($TreatWarningsAsErrors -and $warningCount -gt 0)) {
    Write-Host "`nCode analysis found issues. Review $Output for details." -ForegroundColor Red
    exit 1
}

exit 0

