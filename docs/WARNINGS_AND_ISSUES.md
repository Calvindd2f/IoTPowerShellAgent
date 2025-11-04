# Warnings and Known Issues

## Build Warnings Summary

### Fixed Warnings ✅
- **CS1998 (Async method lacks await)**: Fixed by converting async methods to return `Task` directly where no await was needed.

### Harmless Warnings (Can Be Ignored) ⚠️

#### Nullable Reference Type Warnings (CS8600, CS8603, CS8604, CS8625, etc.)
**Location**: `Utilities/JsonObject.cs`, `PowerShell/PowerShellExecutor.cs`

**Explanation**: These warnings occur because:
1. `JsonObject.cs` is legacy/embedded code that predates C#'s nullable reference types
2. The code safely handles null values at runtime
3. Adding nullable annotations throughout would require extensive refactoring

**Impact**: None - the code handles null values correctly at runtime.

#### Nullability Mismatch Warnings (CS8622)
**Location**: Event handler signatures in `PowerShellExecutor.cs`

**Explanation**: The PowerShell SDK's event handler signatures don't match C#'s strict nullability requirements. This is a known issue with PowerShell SDK compatibility.

**Impact**: None - event handlers work correctly despite the warnings.

## Known Issues

### PowerShell Module Loading Error

**Error Message**:
```
The 'Write-Output' command was found in the module 'Microsoft.PowerShell.Utility', 
but the module could not be loaded due to the following error: 
[Could not load type 'System.Management.Automation.PSSnapIn' from assembly 
'System.Management.Automation, Version=7.4.0.500...]
```

**Status**: Under investigation

**Possible Causes**:
1. PowerShell 7.x SDK compatibility issue with module loading
2. Local development environment configuration
3. Missing PowerShell runtime dependencies

**Workarounds**:
1. **Use expressions instead of cmdlets**:
   ```powershell
   # Instead of: Write-Output "Hello"
   # Use: "Hello"
   ```

2. **Use PowerShell expressions that don't require modules**:
   ```powershell
   # These work:
   1 + 1
   "Hello World"
   $var = "test"
   ```

3. **Test on production environment**: This may be environment-specific and work correctly when deployed.

4. **Use alternative cmdlets**: Some cmdlets may work while others don't. Test your specific use case.

**Future Resolution**:
- Consider updating to a newer version of `System.Management.Automation` when available
- Investigate PowerShell module pre-loading strategies
- Consider PowerShell 5.1 compatibility mode if available

## Recommendations

1. **For Development**: Ignore nullable warnings in `JsonObject.cs` - they're in legacy code and don't affect functionality.

2. **For Testing**: Use simple PowerShell expressions or test scripts that don't rely heavily on cmdlet module loading.

3. **For Production**: Test PowerShell scripts on your target environment. The module loading issue may not occur in production if it's environment-specific.

4. **Code Quality**: The nullable warnings could be addressed in a future refactoring of `JsonObject.cs`, but it's low priority since the code functions correctly.
