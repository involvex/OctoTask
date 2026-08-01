# ========================================================================
# OctoTask - Install Script
# -----------------------------------------------------------------------
# Installs OctoTask as a Task Manager replacement with automatic backup.
# Requires administrator privileges.
# ========================================================================
# usage: powershell -ExecutionPolicy Bypass -File scripts\install.ps1
# ========================================================================

#Requires -RunAsAdministrator

param(
    [string]$SourcePath = ".\bin\Release\net10.0-windows\publish\OctoTask.exe",
    [string]$InstallDir = "C:\Program Files\OctoTask"
)

function Write-Status([string]$msg) {
    Write-Host "[OctoTask] $msg" -ForegroundColor Cyan
}

function Write-Error([string]$msg) {
    Write-Host "[OctoTask] ERROR: $msg" -ForegroundColor Red
}

# --- Validate source ---
if (-not (Test-Path $SourcePath)) {
    Write-Error "Executable not found at: $SourcePath"
    Write-Host "  -> Build first with: dotnet publish -c Release"
    exit 1
}

# --- Create install directory ---
try {
    if (-not (Test-Path $InstallDir)) {
        Write-Status "Creating install directory: $InstallDir"
        New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    }
} catch {
    Write-Error "Failed to create install directory: $_"
    exit 1
}

# --- Copy executable ---
$destExe = Join-Path $InstallDir "OctoTask.exe"
try {
    Write-Status "Copying OctoTask.exe to $destExe"
    Copy-Item -Path $SourcePath -Destination $destExe -Force
} catch {
    Write-Error "Failed to copy executable: $_"
    exit 1
}

# --- Install IFEO hook with backup ---
try {
    Write-Status "Installing Task Manager hook (IFEO Debugger)..."
    & $destExe --install
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Installation failed with exit code $LASTEXITCODE"
        exit 1
    }
} catch {
    Write-Error "Installation failed: $_"
    exit 1
}

# --- Verify ---
$backupPath = Join-Path $InstallDir "taskmgr_backup.reg"
if (Test-Path $backupPath) {
    Write-Status "Backup saved to: $backupPath"
} else {
    Write-Status "No prior Task Manager hook found — no backup needed."
}

Write-Status "Installation complete!"
Write-Host "  - Executable: $destExe"
Write-Host "  - Backup:     $backupPath (if prior hook existed)"
Write-Host "  - Restore:    Run 'uninstall.ps1 -Restore' to revert"
exit 0
