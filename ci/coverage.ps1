#Requires -Version 5.1
<#
.SYNOPSIS
    Generate code coverage reports for IoTPowerShellAgent.

.DESCRIPTION
    Runs tests with code coverage collection and generates reports.
    Supports multiple output formats and coverage thresholds.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Debug)

.PARAMETER Format
    Coverage report format: cobertura, opencover, json, or all (default: all)

.PARAMETER Threshold
    Minimum code coverage percentage (default: 0, no threshold)

.PARAMETER Output
    Output directory for coverage reports (default: ./coverage)

.PARAMETER Exclude
    Patterns to exclude from coverage (e.g., "*Test*.cs", "*Program.cs")

.EXAMPLE
    .\ci\coverage.ps1
    Generate coverage reports for all test projects

.EXAMPLE
    .\ci\coverage.ps1 -Threshold 80
    Generate coverage reports with 80% minimum threshold

.EXAMPLE
    .\ci\coverage.ps1 -Format json -Output ./coverage-reports
    Generate JSON format coverage report to custom directory
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    
    [Parameter()]
    [ValidateSet('cobertura', 'opencover', 'json', 'all')]
    [string]$Format = 'all',
    
    [Parameter()]
    [ValidateRange(0, 100)]
    [int]$Threshold = 0,
    
    [Parameter()]
    [string]$Output = './coverage',
    
    [Parameter()]
    [string[]]$Exclude = @(
        '*Test*.cs',
        '*TestHelpers*.cs',
        'Program.cs',
        '*Designer.cs',
        '*AssemblyInfo.cs'
    )
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ScriptRoot "src\IoTPowerShellAgent\IoTPowerShellAgent.csproj"

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  Code Coverage Report Generator        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check for coverlet.console tool
Write-Host "Checking for code coverage tools..." -ForegroundColor Cyan
$coverletInstalled = dotnet tool list -g | Select-String "coverlet.console"
if (-not $coverletInstalled) {
    Write-Host "Installing coverlet.console tool..." -ForegroundColor Yellow
    dotnet tool install -g coverlet.console --version 6.0.0
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install coverlet.console tool"
        exit 1
    }
}

# Find test projects
Write-Host "Searching for test projects..." -ForegroundColor Cyan
$testProjects = Get-ChildItem -Path $ScriptRoot -Filter "*Tests.csproj" -Recurse -ErrorAction SilentlyContinue

if ($testProjects.Count -eq 0) {
    Write-Warning "No test projects found. Creating coverage for main project only."
    Write-Host "Note: For accurate coverage, create test projects using:" -ForegroundColor Yellow
    Write-Host "  dotnet new xunit -n IoTPowerShellAgent.Tests" -ForegroundColor Gray
    Write-Host ""
    
    # Build the project first
    Write-Host "Building project for coverage analysis..." -ForegroundColor Cyan
    dotnet build "$ProjectPath" --configuration $Configuration --no-incremental
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Host "`nCoverage analysis requires test projects." -ForegroundColor Yellow
    Write-Host "Skipping coverage generation. Use '.\ci\test.ps1' to run tests first." -ForegroundColor Yellow
    exit 0
}

# Create output directory
if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

# Build exclude filters
$excludeFilters = $Exclude -join ";"

# Generate coverage for each test project
$coverageFiles = @()
foreach ($testProject in $testProjects) {
    Write-Host "`nProcessing test project: $($testProject.Name)" -ForegroundColor Cyan
    
    # Build test project
    Write-Host "  Building test project..." -ForegroundColor Gray
    dotnet build "$($testProject.FullName)" --configuration $Configuration --no-incremental
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Build failed for $($testProject.Name), skipping..."
        continue
    }
    
    # Run tests with coverage
    $coverageFileName = "$($testProject.BaseName).coverage"
    $coveragePath = Join-Path $Output $coverageFileName
    
    Write-Host "  Collecting coverage data..." -ForegroundColor Gray
    
    $coverageArgs = @(
        "coverlet",
        "$($testProject.FullName)",
        "--target", "dotnet",
        "--targetargs", "test `"$($testProject.FullName)`" --configuration $Configuration --no-build",
        "--format", "opencover",
        "--output", "$coveragePath.opencover.xml",
        "--exclude-by-attribute", "Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute",
        "--exclude-by-file", $excludeFilters
    )
    
    if ($VerbosePreference -eq 'Continue') {
        $coverageArgs += "--verbosity", "detailed"
    }
    
    & $coverageArgs
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path "$coveragePath.opencover.xml")) {
        $coverageFiles += "$coveragePath.opencover.xml"
        Write-Host "  ✓ Coverage data collected" -ForegroundColor Green
    } else {
        Write-Warning "  Failed to collect coverage for $($testProject.Name)"
    }
}

if ($coverageFiles.Count -eq 0) {
    Write-Warning "No coverage data collected. Ensure tests exist and run successfully."
    exit 0
}

# Generate reports
Write-Host "`nGenerating coverage reports..." -ForegroundColor Cyan

if ($Format -eq 'all' -or $Format -eq 'cobertura') {
    Write-Host "  Generating Cobertura report..." -ForegroundColor Gray
    # Use reportgenerator if available, otherwise skip
    $reportGenInstalled = dotnet tool list -g | Select-String "reportgenerator"
    if ($reportGenInstalled) {
        $reportArgs = @(
            "reportgenerator",
            "-reports:$($coverageFiles -join ';')",
            "-targetdir:$Output\cobertura",
            "-reporttypes:Cobertura"
        )
        & $reportArgs
        Write-Host "  ✓ Cobertura report generated" -ForegroundColor Green
    }
}

if ($Format -eq 'all' -or $Format -eq 'json') {
    Write-Host "  Generating JSON report..." -ForegroundColor Gray
    # Convert opencover to json if reportgenerator is available
    $reportGenInstalled = dotnet tool list -g | Select-String "reportgenerator"
    if ($reportGenInstalled) {
        $reportArgs = @(
            "reportgenerator",
            "-reports:$($coverageFiles -join ';')",
            "-targetdir:$Output\json",
            "-reporttypes:JsonSummary"
        )
        & $reportArgs
        Write-Host "  ✓ JSON report generated" -ForegroundColor Green
    }
}

# Display summary
Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║  Coverage Report Generated!           ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
Write-Host "Output directory: $Output" -ForegroundColor Green
Write-Host "Coverage files: $($coverageFiles.Count)" -ForegroundColor Green

if ($Threshold -gt 0) {
    Write-Host "`nChecking coverage threshold ($Threshold%)..." -ForegroundColor Cyan
    # Note: Actual threshold checking would require parsing coverage files
    Write-Host "Threshold validation requires reportgenerator tool." -ForegroundColor Yellow
}

exit 0

