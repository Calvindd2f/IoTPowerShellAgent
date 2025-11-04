BeforeAll {
    # Import the module or load the assembly
    $assemblyPath = Join-Path $PSScriptRoot "..\..\..\IoTPowerShellAgent\bin\Debug\net8.0-windows\IoTPowerShellAgent.dll"
    $assemblyPath = [System.IO.Path]::GetFullPath($assemblyPath)
    if (Test-Path $assemblyPath) {
        # Register an AssemblyResolve handler so transitive dependencies (SMA, IoT SDK, etc.)
        # are found in the same output directory as the main assembly.
        $assemblyDir = [System.IO.Path]::GetDirectoryName($assemblyPath)
        $resolveHandler = [System.ResolveEventHandler]{
            param($sender, $args)
            $name = [System.Reflection.AssemblyName]::new($args.Name)
            $candidate = Join-Path $assemblyDir "$($name.Name).dll"
            if (Test-Path $candidate) {
                return [System.Reflection.Assembly]::LoadFrom($candidate)
            }
            return $null
        }
        [System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
        [System.Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null
    } else {
        Write-Warning "Assembly not found at: $assemblyPath"
    }

    # Import Pester
    Import-Module Pester -MinimumVersion 5.0
}

Describe "PowerShellExecutor Integration Tests" {
    BeforeEach {
        # Create a mock log callback if needed
        $script:logMessages = @()
    }

    Context "Script Execution" {
        It "Should execute a simple PowerShell command successfully" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Get-Date"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $true
            $result.ErrorMessage | Should -BeNullOrEmpty
            $result.Output | Should -Not -BeNullOrEmpty
        }

        It "Should handle script errors correctly" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Get-Command -Name NonExistentCommand12345"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $false
            $result.ErrorMessage | Should -Not -BeNullOrEmpty
        }

        It "Should capture error details with inner exceptions" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = @"
                try {
                    throw "Outer exception"
                } catch {
                    throw "Inner exception"
                }
"@

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $false
            $result.ErrorDetails | Should -Not -BeNullOrEmpty
            if ($result.ErrorDetails.Count -gt 0) {
                $result.ErrorDetails[0].Message | Should -Not -BeNullOrEmpty
            }
        }

        It "Should handle verbose output" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Write-Verbose 'Test verbose message' -Verbose"

            $result = $executor.ExecutePowerShell($script, $false)

            # Verbose output should be captured via log callback
            $result.Success | Should -Be $true
        }

        It "Should handle warning output" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Write-Warning 'Test warning message'"

            $result = $executor.ExecutePowerShell($script, $false)

            # Warning output should be captured via log callback
            $result.Success | Should -Be $true
        }

        It "Should handle error output" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Write-Error 'Test error message'"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $false
            $result.ErrorMessage | Should -Not -BeNullOrEmpty
        }

        It "Should handle empty script" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = ""

            { $executor.ExecutePowerShell($script, $false) } | Should -Throw
        }

        It "Should handle null script" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = $null

            { $executor.ExecutePowerShell($script, $false) } | Should -Throw
        }
    }

    Context "Async Execution" {
        It "Should execute PowerShell script asynchronously" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Start-Sleep -Seconds 1; Get-Date"

            $task = $executor.ExecutePowerShellAsync($script, $false, [System.Threading.CancellationToken]::None)
            $result = $task.GetAwaiter().GetResult()

            $result.Success | Should -Be $true
            $result.Output | Should -Not -BeNullOrEmpty
        }

        It "Should respect cancellation token" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Start-Sleep -Seconds 10"
            $cts = New-Object System.Threading.CancellationTokenSource
            $cts.CancelAfter(100) # Cancel after 100ms

            $task = $executor.ExecutePowerShellAsync($script, $false, $cts.Token)

            { $task.GetAwaiter().GetResult() } | Should -Throw
        }
    }

    Context "Output Serialization" {
        It "Should serialize simple output correctly" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "42"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $true
            $result.Output | Should -Be "42"
        }

        It "Should serialize complex objects to JSON" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "@{Name='Test'; Value=123}"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $true
            $result.Output | Should -Not -BeNullOrEmpty
            $result.Output | Should -Match "Name"
            $result.Output | Should -Match "Value"
        }

        It "Should serialize arrays correctly" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "@(1, 2, 3)"

            $result = $executor.ExecutePowerShell($script, $false)

            $result.Success | Should -Be $true
            $result.Output | Should -Not -BeNullOrEmpty
        }
    }

    Context "Module Preloading" {
        It "Should preload common PowerShell modules" {
            $executor = New-Object IoTPowerShellAgent.PowerShell.PowerShellExecutor
            $script = "Get-Command Get-Process | Select-Object -First 1"

            $result = $executor.ExecutePowerShell($script, $false)

            # Should succeed if modules are preloaded
            $result.Success | Should -Be $true
        }
    }
}

