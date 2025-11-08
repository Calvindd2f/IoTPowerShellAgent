# CI Scripts Documentation

This directory contains comprehensive CI/CD scripts for building, testing, and deploying IoTPowerShellAgent.

## Scripts Overview

### `build.ps1`
Builds the IoTPowerShellAgent project with support for multiple configurations.

**Usage:**
```powershell
.\ci\build.ps1                          # Build Release configuration
.\ci\build.ps1 -Configuration Debug     # Build Debug configuration
.\ci\build.ps1 -Configuration All       # Build both Debug and Release
.\ci\build.ps1 -Verbose                 # Verbose output
```

**Features:**
- Multiple configuration support (Debug, Release, All)
- Parallel builds for performance
- Deterministic builds for reproducibility
- Source link support for debugging
- Build time and artifact size reporting

### `clean.ps1`
Removes build artifacts, intermediate files, and test results.

**Usage:**
```powershell
.\ci\clean.ps1                    # Clean bin and obj directories
.\ci\clean.ps1 -All               # Clean everything including NuGet cache
.\ci\clean.ps1 -Bin               # Clean bin directories only
.\ci\clean.ps1 -Obj               # Clean obj directories only
.\ci\clean.ps1 -TestResults       # Clean test results
.\ci\clean.ps1 -NuGet             # Clean NuGet artifacts
```

**Features:**
- Selective cleaning options
- Size reporting for freed space
- Safe error handling

### `test.ps1`
Runs all test projects with comprehensive reporting.

**Usage:**
```powershell
.\ci\test.ps1                                    # Run all tests
.\ci\test.ps1 -Configuration Release            # Run in Release mode
.\ci\test.ps1 -Filter "FullyQualifiedName~PowerShell"  # Filter tests
.\ci\test.ps1 -Logger all -Output ./test-results # Multiple output formats
```

**Features:**
- Automatic test project discovery
- Multiple logger formats (console, trx, html)
- Test filtering support
- Parallel test execution
- Fallback to functional tests if no test projects exist

### `coverage.ps1`
Generates code coverage reports for test projects.

**Usage:**
```powershell
.\ci\coverage.ps1                    # Generate all coverage formats
.\ci\coverage.ps1 -Threshold 80     # Require 80% coverage
.\ci\coverage.ps1 -Format json       # JSON format only
.\ci\coverage.ps1 -Output ./coverage # Custom output directory
```

**Features:**
- Multiple coverage formats (cobertura, opencover, json)
- Coverage threshold validation
- Automatic tool installation (coverlet.console)
- Exclude patterns for test helpers and generated code

### `publish.ps1`
Creates production-ready deployment packages.

**Usage:**
```powershell
.\ci\publish.ps1                              # Framework-dependent publish
.\ci\publish.ps1 -SelfContained -SingleFile  # Self-contained single-file
.\ci\publish.ps1 -Runtime win-arm64          # ARM64 architecture
.\ci\publish.ps1 -Trimmed -ReadyToRun       # Optimized build
```

**Features:**
- Multiple runtime support (win-x64, win-x86, win-arm64)
- Self-contained and framework-dependent options
- Single-file deployment
- Assembly trimming for size reduction
- ReadyToRun compilation for performance
- Automatic configuration file copying
- Deployment notes generation

### `lint.ps1`
Performs static code analysis and linting.

**Usage:**
```powershell
.\ci\lint.ps1                          # Run code analysis
.\ci\lint.ps1 -TreatWarningsAsErrors   # Fail on warnings
.\ci\lint.ps1 -Output ./analysis.txt  # Save results to file
```

**Features:**
- .NET analyzers integration
- Warning and error reporting
- Results export to file

## Common CI/CD Workflows

### Full Build and Test Pipeline
```powershell
# Clean previous builds
.\ci\clean.ps1

# Build Release configuration
.\ci\build.ps1 -Configuration Release

# Run tests
.\ci\test.ps1 -Configuration Release

# Generate coverage
.\ci\coverage.ps1 -Configuration Release -Threshold 80

# Run code analysis
.\ci\lint.ps1 -Configuration Release -TreatWarningsAsErrors

# Publish for deployment
.\ci\publish.ps1 -SelfContained -SingleFile -Trimmed
```

### Quick Development Build
```powershell
.\ci\clean.ps1
.\ci\build.ps1 -Configuration Debug
.\ci\test.ps1 -Configuration Debug
```

### Production Release
```powershell
.\ci\clean.ps1 -All
.\ci\build.ps1 -Configuration Release
.\ci\test.ps1 -Configuration Release -Logger all
.\ci\coverage.ps1 -Configuration Release -Threshold 80
.\ci\lint.ps1 -Configuration Release -TreatWarningsAsErrors
.\ci\publish.ps1 -SelfContained -SingleFile -Trimmed -ReadyToRun
```

## CI/CD Integration

### GitHub Actions Example
```yaml
name: CI

on: [push, pull_request]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Build
        run: .\ci\build.ps1 -Configuration Release
      
      - name: Test
        run: .\ci\test.ps1 -Configuration Release
      
      - name: Coverage
        run: .\ci\coverage.ps1 -Configuration Release
      
      - name: Lint
        run: .\ci\lint.ps1 -Configuration Release
      
      - name: Publish
        run: .\ci\publish.ps1 -SelfContained -SingleFile
```

### Azure DevOps Pipeline Example
```yaml
trigger:
  branches:
    include:
    - main
    - develop

pool:
  vmImage: 'windows-latest'

steps:
- task: UseDotNet@2
  inputs:
    packageType: 'sdk'
    version: '8.0.x'

- task: PowerShell@2
  displayName: 'Build'
  inputs:
    filePath: 'ci/build.ps1'
    arguments: '-Configuration Release'

- task: PowerShell@2
  displayName: 'Test'
  inputs:
    filePath: 'ci/test.ps1'
    arguments: '-Configuration Release -Logger trx'

- task: PowerShell@2
  displayName: 'Publish'
  inputs:
    filePath: 'ci/publish.ps1'
    arguments: '-SelfContained -SingleFile'
```

## Requirements

- **PowerShell 5.1+** (Windows PowerShell or PowerShell Core)
- **.NET 8.0 SDK** or later
- **Windows OS** (for Windows Service deployment)

## Performance Considerations

All scripts are optimized for performance:
- Parallel builds and test execution
- Incremental build support
- Efficient file operations
- Minimal output for CI environments

## Error Handling

All scripts:
- Use `$ErrorActionPreference = 'Stop'` for fail-fast behavior
- Provide clear error messages
- Exit with appropriate exit codes for CI/CD integration
- Include try-catch blocks for graceful error handling

## Best Practices

1. **Always clean before building** in CI environments
2. **Use Release configuration** for production builds
3. **Enable code coverage thresholds** to maintain quality
4. **Run linting with warnings as errors** in CI
5. **Use self-contained publishes** for easier deployment
6. **Enable ReadyToRun** for production deployments

## Troubleshooting

### Build Fails
- Ensure .NET 8.0 SDK is installed: `dotnet --version`
- Clean and rebuild: `.\ci\clean.ps1 -All; .\ci\build.ps1`

### Tests Not Found
- Create test projects: `dotnet new xunit -n IoTPowerShellAgent.Tests`
- The test script will fall back to functional tests if no test projects exist

### Coverage Tools Missing
- Coverage script automatically installs coverlet.console
- Ensure you have internet access for tool installation

### Publish Fails
- Ensure target runtime is installed: `dotnet --list-runtimes`
- For self-contained publishes, runtime is included

## Contributing

When adding new CI scripts:
1. Follow the existing script structure
2. Include comprehensive help documentation
3. Use consistent error handling
4. Add performance optimizations where possible
5. Update this README with usage examples

