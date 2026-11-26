param(
    [int]$Tier = 0,
    [string]$Milestone = "ALL",
    [string]$Filter = "",
    [switch]$IncludeEmulator,
    [switch]$RequireEmulator,
    [switch]$IncludeUnity,
    [switch]$AllowSkips,
    [string]$JsonReport = ""
)

$ErrorActionPreference = "Stop"

if ($Tier -notin @(0, 1, 2, 3, 4)) {
    Write-Error "Invalid -Tier '$Tier'. Expected 0, 1, 2, 3, or 4."
    exit 2
}

$msUpper = $Milestone.ToUpperInvariant()
if ($msUpper -notin @("M1", "M2", "M3", "M4", "M5", "ALL")) {
    Write-Error "Invalid -Milestone '$Milestone'. Expected M1, M2, M3, M4, M5, or ALL."
    exit 2
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$ProjectParent = Split-Path -Parent $RepoRoot
$PythonExe = Join-Path $ProjectParent "Tools\venv\Scripts\python.exe"
$RunnerPy = Join-Path $PSScriptRoot "run_e2e.py"

if (-not (Test-Path -LiteralPath $PythonExe)) {
    Write-Error "Python venv executable not found: $PythonExe"
    exit 2
}

$pyArgs = @(
    $RunnerPy,
    "--tier", $Tier.ToString(),
    "--milestone", $msUpper
)

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $pyArgs += @("--filter", $Filter)
}
if ($IncludeEmulator) {
    $pyArgs += "--include-emulator"
}
if ($RequireEmulator) {
    $pyArgs += "--require-emulator"
}
if ($IncludeUnity) {
    $pyArgs += "--include-unity"
}
if ($AllowSkips) {
    $pyArgs += "--allow-skips"
}
if (-not [string]::IsNullOrWhiteSpace($JsonReport)) {
    $pyArgs += @("--json-report", $JsonReport)
}

& $PythonExe @pyArgs
exit $LASTEXITCODE
