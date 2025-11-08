#Requires -Version 5.1
<#
.SYNOPSIS
    Run tests for IoTPowerShellAgent project.

.DESCRIPTION
    Discovers and runs all test projects with comprehensive reporting.
    Supports filtering, parallel execution, and multiple output formats.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug)

.PARAMETER Filter
    Test filter expression (e.g., "FullyQualifiedName~PowerShellExecutor")

.PARAMETER Logger
    Test logger format: console, trx, html, or all (default: console)

.PARAMETER Output
    Output directory for test results (default: ./TestResults)

.PARAMETER NoBuild
    Skip building before running tests

.PARAMETER Verbose
    Enable verbose test output

.EXAMPLE
    .\ci\test.ps1
    Run all tests in Debug configuration

.EXAMPLE
    .\ci\test.ps1 -Configuration Release -Filter "FullyQualifiedName~PowerShell"
    Run tests matching filter in Release configuration

.EXAMPLE
    .\ci\test.ps1 -Logger all -Output ./test-results
    Run tests with all logger formats to custom directory
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    
    [Parameter()]
    [string]$Filter,
    
    [Parameter()]
    [ValidateSet('console', 'trx', 'html', 'all')]
    [string]$Logger = 'console',
    
    [Parameter()]
    [string]$Output = './TestResults',
    
    [Parameter()]
    [switch]$NoBuild,
    
    [Parameter()]
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Test Runner                          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Find test projects
Write-Host "Discovering test projects..." -ForegroundColor Cyan
$testProjects = Get-ChildItem -Path $ScriptRoot -Filter "*Tests.csproj" -Recurse -ErrorAction SilentlyContinue

if ($testProjects.Count -eq 0) {
    Write-Warning "No test projects found."
    Write-Host "`nTo create a test project, run:" -ForegroundColor Yellow
    Write-Host "  dotnet new xunit -n IoTPowerShellAgent.Tests -o tests/IoTPowerShellAgent.Tests" -ForegroundColor Gray
    Write-Host "  dotnet add tests/IoTPowerShellAgent.Tests/IoTPowerShellAgent.Tests.csproj reference src/IoTPowerShellAgent/IoTPowerShellAgent.csproj" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Running functional tests via debug mode instead..." -ForegroundColor Yellow
    Write-Host ""
    
    # Run functional test via debug mode
    $projectPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\IoTPowerShellAgent.csproj"
    if (Test-Path $projectPath) {
        Write-Host "Executing: dotnet run --project `"$projectPath`" -- --test --metrics" -ForegroundColor Gray
        dotnet run --project "$projectPath" -- --test --metrics
        exit $LASTEXITCODE
    } else {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }
}

Write-Host "Found $($testProjects.Count) test project(s)" -ForegroundColor Green
Write-Host ""

# Create output directory
if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

# Build test projects if needed
if (-not $NoBuild) {
    Write-Host "Building test projects..." -ForegroundColor Cyan
    foreach ($testProject in $testProjects) {
        Write-Host "  Building $($testProject.Name)..." -ForegroundColor Gray
        dotnet build "$($testProject.FullName)" --configuration $Configuration --no-incremental
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed for $($testProject.Name)"
            exit 1
        }
    }
    Write-Host ""
}

# Run tests
$testResults = @()
$totalTests = 0
$passedTests = 0
$failedTests = 0
$skippedTests = 0

foreach ($testProject in $testProjects) {
    Write-Host "Running tests: $($testProject.Name)" -ForegroundColor Cyan
    
    $testArgs = @(
        "test",
        "`"$($testProject.FullName)`"",
        "--configuration", $Configuration,
        "--no-build",
        "--nologo"
    )
    
    if ($Filter) {
        $testArgs += "--filter", "`"$Filter`""
    }
    
    # Add loggers
    if ($Logger -eq 'all' -or $Logger -eq 'console') {
        $testArgs += "--logger", "console;verbosity=normal"
    }
    
    if ($Logger -eq 'all' -or $Logger -eq 'trx') {
        $trxFile = Join-Path $Output "$($testProject.BaseName).trx"
        $testArgs += "--logger", "`"trx;LogFileName=$trxFile`""
    }
    
    if ($Logger -eq 'all' -or $Logger -eq 'html') {
        $htmlFile = Join-Path $Output "$($testProject.BaseName).html"
        $testArgs += "--logger", "`"html;LogFileName=$htmlFile`""
    }
    
    if ($Verbose) {
        $testArgs += "--verbosity", "detailed"
    }
    
    # Performance: Run tests in parallel
    $testArgs += "--", "/p:ParallelizeTestCollections=true"
    
    $startTime = Get-Date
    & dotnet $testArgs
    $duration = (Get-Date) - $startTime
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Tests passed in $($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Tests failed" -ForegroundColor Red
        $testResults += @{
            Project = $testProject.Name
            Status = "Failed"
            Duration = $duration
        }
    }
    
    Write-Host ""
}

# Summary
Write-Host "╔════════════════════════════════════════╗" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "║  Test Run Summary                      ║" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "Test projects: $($testProjects.Count)" -ForegroundColor Gray
Write-Host "Results directory: $Output" -ForegroundColor Gray

if ($testResults.Count -gt 0) {
    Write-Host "`nFailed test projects:" -ForegroundColor Red
    foreach ($result in $testResults) {
        Write-Host "  - $($result.Project)" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
    exit 0
}

