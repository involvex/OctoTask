# ========================================================================
# OctoTask - Uninstall Script
# -----------------------------------------------------------------------
# Removes OctoTask from the system and restores the original Task Manager.
# Requires administrator privileges.
# ========================================================================
# usage: powershell -ExecutionPolicy Bypass -File scripts\uninstall.ps1
#        -Restore           Restore original Task Manager (default)
#        -NoRestore         Remove hook without restoring backup
# ========================================================================

#Requires -RunAsAdministrator

param(
    [switch]$Restore = $true,
    [switch]$NoRestore,
    [string]$InstallDir = "C:\Program Files\OctoTask"
)

function Write-Status([string]$msg) {
    Write-Host "[OctoTask] $msg" -ForegroundColor Cyan
}

function Write-Error([string]$msg) {
    Write-Host "[OctoTask] ERROR: $msg" -ForegroundColor Red
}

$destExe = Join-Path $InstallDir "OctoTask.exe"

# --- Remove IFEO hook (restore or uninstall) ---
if (Test-Path $destExe) {
    try {
        if ($NoRestore) {
            Write-Status "Removing hook (no restore)..."
            & $destExe --uninstall
        } else {
            Write-Status "Removing hook and restoring original Task Manager..."
            & $destExe --restore
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Error "CLI operation failed with exit code $LASTEXITCODE"
        }
    } catch {
        Write-Error "CLI operation failed: $_"
    }
} else {
    Write-Status "OctoTask.exe not found at $destExe — skipping CLI operation."
    Write-Status "Manually remove the IFEO Debugger entry if it still exists."
}

# --- Delete install directory ---
if (Test-Path $InstallDir) {
    try {
        Write-Status "Removing install directory: $InstallDir"
        Remove-Item -Path $InstallDir -Recurse -Force
    } catch {
        Write-Error "Failed to remove install directory: $_"
    }
}

# --- Final verification ---
$regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe"
if (Test-Path $regPath) {
    $debugger = (Get-ItemProperty -Path $regPath -Name "Debugger" -ErrorAction SilentlyContinue).Debugger
    if ($debugger) {
        Write-Error "Debugger value still exists in IFEO key: $debugger"
        Write-Host "  -> Delete manually: reg delete ""$regPath"" /v Debugger /f"
    } else {
        Write-Status "IFEO key clean — Task Manager is restored."
    }
} else {
    Write-Status "IFEO key not found — Task Manager is clean."
}

Write-Status "Uninstall complete!"
exit 0
