#Requires -Version 5.1
<#
.SYNOPSIS
    Main CI pipeline runner for IoTPowerShellAgent.

.DESCRIPTION
    Executes a complete CI pipeline: clean, build, test, coverage, lint, and publish.
    Designed for CI/CD integration with configurable steps.

.PARAMETER Steps
    Pipeline steps to execute: clean, build, test, coverage, lint, publish, or all (default: all)

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER SkipPublish
    Skip publish step even if included in Steps

.PARAMETER CoverageThreshold
    Minimum code coverage percentage (default: 0, no threshold)

.PARAMETER FailOnWarnings
    Treat warnings as errors in lint step

.EXAMPLE
    .\ci\ci.ps1
    Run full CI pipeline

.EXAMPLE
    .\ci\ci.ps1 -Steps "clean,build,test" -Configuration Debug
    Run only clean, build, and test steps in Debug

.EXAMPLE
    .\ci\ci.ps1 -CoverageThreshold 80 -FailOnWarnings
    Run full pipeline with 80% coverage threshold and fail on warnings
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$Steps = 'all',
    
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$SkipPublish,
    
    [Parameter()]
    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 0,
    
    [Parameter()]
    [switch]$FailOnWarnings
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$ScriptStartTime = Get-Date

Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  IoTPowerShellAgent CI Pipeline                       ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Steps: $Steps" -ForegroundColor Gray
Write-Host ""

# Parse steps
$stepList = @()
if ($Steps -eq 'all') {
    $stepList = @('clean', 'build', 'test', 'coverage', 'lint', 'publish')
} else {
    $stepList = $Steps -split ',' | ForEach-Object { $_.Trim() }
}

# Remove publish if SkipPublish is specified
if ($SkipPublish) {
    $stepList = $stepList | Where-Object { $_ -ne 'publish' }
}

# Step execution function
function Invoke-CIStep {
    param(
        [string]$StepName,
        [string]$ScriptPath,
        [string[]]$Arguments
    )
    
    Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "Step: $StepName" -ForegroundColor Cyan
    Write-Host "════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
    
    $stepStartTime = Get-Date
    
    try {
        $processArgs = @{
            FilePath = "powershell.exe"
            ArgumentList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath) + $Arguments
            NoNewWindow = $true
            Wait = $true
            PassThru = $true
        }
        
        $process = Start-Process @processArgs
        
        if ($process.ExitCode -ne 0) {
            Write-Error "Step '$StepName' failed with exit code $($process.ExitCode)"
            return $false
        }
        
        $stepDuration = (Get-Date) - $stepStartTime
        Write-Host ""
        Write-Host "✓ Step '$StepName' completed in $($stepDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Green
        Write-Host ""
        
        return $true
    }
    catch {
        Write-Error "Step '$StepName' failed: $_"
        return $false
    }
}

# Execute pipeline steps
$failedSteps = @()
$successfulSteps = @()

foreach ($step in $stepList) {
    $stepScript = Join-Path $PSScriptRoot "$step.ps1"
    
    if (-not (Test-Path $stepScript)) {
        Write-Warning "Step script not found: $stepScript. Skipping..."
        continue
    }
    
    $stepArgs = @()
    
    # Add common arguments based on step
    switch ($step) {
        'build' {
            $stepArgs += "-Configuration", $Configuration
        }
        'test' {
            $stepArgs += "-Configuration", $Configuration, "-Logger", "trx"
        }
        'coverage' {
            $stepArgs += "-Configuration", $Configuration
            if ($CoverageThreshold -gt 0) {
                $stepArgs += "-Threshold", $CoverageThreshold.ToString()
            }
        }
        'lint' {
            $stepArgs += "-Configuration", $Configuration
            if ($FailOnWarnings) {
                $stepArgs += "-TreatWarningsAsErrors"
            }
        }
        'publish' {
            $stepArgs += "-Configuration", $Configuration, "-SelfContained", "-SingleFile", "-Trimmed"
        }
    }
    
    $success = Invoke-CIStep -StepName $step -ScriptPath $stepScript -Arguments $stepArgs
    
    if ($success) {
        $successfulSteps += $step
    } else {
        $failedSteps += $step
        if ($step -in @('build', 'test', 'lint')) {
            Write-Error "Critical step '$step' failed. Stopping pipeline."
            break
        }
    }
}

# Pipeline summary
$totalDuration = (Get-Date) - $ScriptStartTime

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor $(if ($failedSteps.Count -eq 0) { "Green" } else { "Red" })
Write-Host "║  CI Pipeline Summary                                  ║" -ForegroundColor $(if ($failedSteps.Count -eq 0) { "Green" } else { "Red" })
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor $(if ($failedSteps.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""
Write-Host "Total duration: $($totalDuration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Gray
Write-Host "Successful steps: $($successfulSteps.Count)" -ForegroundColor Green
Write-Host "  $($successfulSteps -join ', ')" -ForegroundColor Green

if ($failedSteps.Count -gt 0) {
    Write-Host "Failed steps: $($failedSteps.Count)" -ForegroundColor Red
    Write-Host "  $($failedSteps -join ', ')" -ForegroundColor Red
    Write-Host ""
    Write-Host "Pipeline failed. Review errors above." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "All pipeline steps completed successfully!" -ForegroundColor Green
    exit 0
}

