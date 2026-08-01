# ========================================================================
# OctoTask - Restore Task Manager Script
# -----------------------------------------------------------------------
# Restores the original Windows Task Manager by applying the backup
# Debugger value that was saved during installation.
# Requires administrator privileges.
# ========================================================================
# usage: powershell -ExecutionPolicy Bypass -File scripts\restore-taskmgr.ps1
#        -InstallDir "C:\Program Files\OctoTask"  (override install path)
# ========================================================================

#Requires -RunAsAdministrator

param(
    [string]$InstallDir = "C:\Program Files\OctoTask"
)

function Write-Status([string]$msg) {
    Write-Host "[OctoTask] $msg" -ForegroundColor Cyan
}

function Write-Error([string]$msg) {
    Write-Host "[OctoTask] ERROR: $msg" -ForegroundColor Red
}

$destExe = Join-Path $InstallDir "OctoTask.exe"
$backupPath = Join-Path $InstallDir "taskmgr_backup.reg"

# --- Check if OctoTask is installed ---
if (-not (Test-Path $destExe)) {
    Write-Status "OctoTask.exe not found at $destExe"
    Write-Status "Will attempt direct registry restore from backup file."
}

# --- Check for backup file ---
if (Test-Path $backupPath) {
    Write-Status "Found backup file: $backupPath"
    Write-Status "Restoring Task Manager from backup..."

    if (Test-Path $destExe) {
        & $destExe --restore
        if ($LASTEXITCODE -eq 0) {
            Write-Status "Restore completed successfully via OctoTask CLI."
            exit 0
        } else {
            Write-Error "OctoTask CLI restore failed (exit code $LASTEXITCODE). Falling back to manual restore."
        }
    }

    # Manual fallback: parse the .reg file and apply
    Write-Status "Applying .reg file manually..."
    reg import $backupPath
    if ($LASTEXITCODE -eq 0) {
        Write-Status "Restore completed via reg import."
    } else {
        Write-Error "reg import failed (exit code $LASTEXITCODE)."
        Write-Host "  -> Try running: reg import `"$backupPath`""
    }
} else {
    # No backup — just remove the hook
    Write-Status "No backup file found. Removing IFEO Debugger value..."
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\taskmgr.exe"

    if (Test-Path $regPath) {
        Remove-ItemProperty -Path $regPath -Name "Debugger" -ErrorAction SilentlyContinue
    }

    Write-Status "Task Manager hook removed. Default Task Manager will be used."
}

Write-Status "Restore complete!"
exit 0
