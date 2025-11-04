# Pester test runner script
# Usage: .\run-pester.ps1

param(
    [string]$TestPath = "PowerShell\*.Tests.ps1",
    [switch]$CodeCoverage,
    [string]$OutputFormat = "NUnitXml",
    [string]$OutputFile = "PesterResults.xml"
)

# Ensure Pester is available
if (-not (Get-Module -ListAvailable -Name Pester)) {
    Write-Error "Pester module is not installed. Install it with: Install-Module -Name Pester -Force -SkipPublisherCheck"
    exit 1
}

Import-Module Pester -MinimumVersion 5.0

# Build the project first
Write-Host "Building project..." -ForegroundColor Cyan
$buildResult = dotnet build ..\IoTPowerShellAgent\IoTPowerShellAgent.csproj --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Please fix build errors before running tests."
    exit 1
}

# Configuration
$pesterConfig = New-PesterConfiguration
$pesterConfig.Run.Path = $TestPath
$pesterConfig.Run.PassThru = $true
$pesterConfig.Output.Verbosity = "Detailed"

if ($CodeCoverage) {
    $pesterConfig.CodeCoverage.Enabled = $true
    $pesterConfig.CodeCoverage.Path = "..\IoTPowerShellAgent\**\*.cs"
    $pesterConfig.CodeCoverage.OutputFormat = "JaCoCo"
    $pesterConfig.CodeCoverage.OutputPath = "CodeCoverage.xml"
}

# Output configuration
if ($OutputFormat -eq "NUnitXml") {
    $pesterConfig.TestResult.Enabled = $true
    $pesterConfig.TestResult.OutputFormat = "NUnitXml"
    $pesterConfig.TestResult.OutputPath = $OutputFile
}

Write-Host "Running Pester tests..." -ForegroundColor Cyan
$result = Invoke-Pester -Configuration $pesterConfig

if ($result.FailedCount -gt 0) {
    Write-Host "`n$($result.FailedCount) test(s) failed!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
    exit 0
}

