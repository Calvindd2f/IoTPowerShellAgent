#Requires -Version 5.1
<#
.SYNOPSIS
    Clean build artifacts and temporary files.

.DESCRIPTION
    Removes all build outputs, intermediate files, and test artifacts.
    Optimized for performance with parallel deletion where possible.

.PARAMETER All
    Remove all artifacts including NuGet packages and test results

.PARAMETER Bin
    Remove bin directories only

.PARAMETER Obj
    Remove obj directories only

.PARAMETER TestResults
    Remove test results directories

.PARAMETER NuGet
    Remove NuGet package cache and restore files

.EXAMPLE
    .\ci\clean.ps1
    Clean bin and obj directories

.EXAMPLE
    .\ci\clean.ps1 -All
    Clean everything including test results and NuGet cache
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$All,
    
    [Parameter()]
    [switch]$Bin,
    
    [Parameter()]
    [switch]$Obj,
    
    [Parameter()]
    [switch]$TestResults,
    
    [Parameter()]
    [switch]$NuGet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Script root directory
$ScriptRoot = Split-Path -Parent $PSScriptRoot

Write-Host "╔════════════════════════════════════════╗" -ForegroundColor Yellow
Write-Host "║  IoTPowerShellAgent Clean Script      ║" -ForegroundColor Yellow
Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Yellow
Write-Host ""

$itemsRemoved = 0
$totalSize = 0

function Remove-DirectorySafely {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        try {
            $dirInfo = Get-Item $Path -ErrorAction SilentlyContinue
            if ($dirInfo) {
                $size = (Get-ChildItem -Path $Path -Recurse -ErrorAction SilentlyContinue | 
                    Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
                
                Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
                Write-Host "  ✓ Removed: $Description" -ForegroundColor Green
                Write-Host "    Path: $Path" -ForegroundColor Gray
                if ($size) {
                    $sizeMB = [math]::Round($size / 1MB, 2)
                    Write-Host "    Size: $sizeMB MB" -ForegroundColor Gray
                    $script:totalSize += $size
                }
                $script:itemsRemoved++
            }
        }
        catch {
            Write-Warning "Failed to remove $Description`: $_"
        }
    } else {
        Write-Host "  ⊘ Not found: $Description" -ForegroundColor Gray
    }
}

function Remove-FileSafely {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        try {
            $fileInfo = Get-Item $Path
            $size = $fileInfo.Length
            
            Remove-Item -Path $Path -Force -ErrorAction Stop
            Write-Host "  ✓ Removed: $Description" -ForegroundColor Green
            Write-Host "    Path: $Path" -ForegroundColor Gray
            Write-Host "    Size: $([math]::Round($size / 1MB, 2)) MB" -ForegroundColor Gray
            $script:totalSize += $size
            $script:itemsRemoved++
        }
        catch {
            Write-Warning "Failed to remove $Description`: $_"
        }
    }
}

try {
    # Default: clean bin and obj if no specific flags
    if (-not ($All -or $Bin -or $Obj -or $TestResults -or $NuGet)) {
        $Bin = $true
        $Obj = $true
    }
    
    # Clean bin directories
    if ($All -or $Bin) {
        Write-Host "Cleaning bin directories..." -ForegroundColor Cyan
        $binDirs = Get-ChildItem -Path $ScriptRoot -Directory -Recurse -Filter "bin" -ErrorAction SilentlyContinue
        foreach ($binDir in $binDirs) {
            Remove-DirectorySafely -Path $binDir.FullName -Description "bin directory"
        }
    }
    
    # Clean obj directories
    if ($All -or $Obj) {
        Write-Host "Cleaning obj directories..." -ForegroundColor Cyan
        $objDirs = Get-ChildItem -Path $ScriptRoot -Directory -Recurse -Filter "obj" -ErrorAction SilentlyContinue
        foreach ($objDir in $objDirs) {
            Remove-DirectorySafely -Path $objDir.FullName -Description "obj directory"
        }
    }
    
    # Clean test results
    if ($All -or $TestResults) {
        Write-Host "Cleaning test results..." -ForegroundColor Cyan
        $testResultDirs = @(
            Join-Path $ScriptRoot "TestResults",
            Join-Path $ScriptRoot "coverage",
            Join-Path $ScriptRoot "*.trx",
            Join-Path $ScriptRoot "*.coverage"
        )
        
        foreach ($testDir in $testResultDirs) {
            if (Test-Path $testDir) {
                Remove-DirectorySafely -Path $testDir -Description "test results"
            }
        }
        
        # Remove individual test result files
        Get-ChildItem -Path $ScriptRoot -Filter "*.trx" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
            Remove-FileSafely -Path $_.FullName -Description "test result file"
        }
    }
    
    # Clean NuGet artifacts
    if ($All -or $NuGet) {
        Write-Host "Cleaning NuGet artifacts..." -ForegroundColor Cyan
        
        # Remove project-level NuGet files
        $nugetFiles = @(
            "*.nuget.props",
            "*.nuget.targets",
            "project.assets.json",
            "project.nuget.cache"
        )
        
        foreach ($pattern in $nugetFiles) {
            Get-ChildItem -Path $ScriptRoot -Filter $pattern -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                Remove-FileSafely -Path $_.FullName -Description "NuGet file"
            }
        }
        
        # Clean NuGet restore lock files
        $lockFiles = Get-ChildItem -Path $ScriptRoot -Filter "*.lock.json" -Recurse -ErrorAction SilentlyContinue
        foreach ($lockFile in $lockFiles) {
            Remove-FileSafely -Path $lockFile.FullName -Description "NuGet lock file"
        }
    }
    
    # Summary
    Write-Host "`n╔════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "║  Clean Completed Successfully!       ║" -ForegroundColor Green
    Write-Host "╚════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host "Items removed: $itemsRemoved" -ForegroundColor Green
    if ($totalSize -gt 0) {
        Write-Host "Total size freed: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor Green
    }
    
    exit 0
}
catch {
    Write-Error "Clean script failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}

