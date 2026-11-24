# run_e2e_tests.ps1
# Master automated test runner for ShotField E2E test suite (51 comprehensive tests).
# Encoded in UTF-8 with BOM for compatibility with Windows PowerShell 5.1 and pwsh.

[CmdletBinding()]
param(
    [ValidateSet(0, 1, 2, 3, 4)]
    [int]$Tier = 0,

    [string]$Filter = "",

    [string]$RepoRoot = "",

    [string]$PicoRoot = "",

    [switch]$JsonReport
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}
if ([string]::IsNullOrWhiteSpace($PicoRoot)) {
    $PicoRoot = (Resolve-Path (Join-Path $RepoRoot "..\..")).Path
}

# Parameter validation
if (-not (Test-Path -LiteralPath $RepoRoot)) {
    Write-Host "[ERROR] RepoRoot directory does not exist: $RepoRoot" -ForegroundColor Red
    exit 2
}
if (-not (Test-Path -LiteralPath $PicoRoot)) {
    Write-Host "[ERROR] PicoRoot directory does not exist: $PicoRoot" -ForegroundColor Red
    exit 2
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Reset global state
$Global:E2ETestRegistry = [System.Collections.Generic.List[PSCustomObject]]::new()
$Global:E2ETestResults = [System.Collections.Generic.List[PSCustomObject]]::new()

# Load Common Framework
$commonPath = Join-Path $scriptDir "E2ETestCommon.ps1"
if (-not (Test-Path -LiteralPath $commonPath)) {
    Write-Host "[ERROR] Missing common test framework: $commonPath" -ForegroundColor Red
    exit 2
}
. $commonPath

# Load Test Suites based on Tier
$tierScripts = @(
    @{ Tier = 1; Script = "Tier1_FeatureCoverage.ps1" },
    @{ Tier = 2; Script = "Tier2_BoundaryCorner.ps1" },
    @{ Tier = 3; Script = "Tier3_CrossFeature.ps1" },
    @{ Tier = 4; Script = "Tier4_RealWorldScenarios.ps1" }
)

foreach ($ts in $tierScripts) {
    if ($Tier -eq 0 -or $Tier -eq $ts.Tier) {
        $p = Join-Path $scriptDir $ts.Script
        if (Test-Path -LiteralPath $p) {
            . $p
        } else {
            Write-Host "[WARN] Test file not found: $p" -ForegroundColor Yellow
        }
    }
}

# Apply Filter if specified
$selectedTests = $Global:E2ETestRegistry
if ($Tier -ne 0) {
    $selectedTests = @($selectedTests | Where-Object { $_.Tier -eq $Tier })
}
if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $selectedTests = @($selectedTests | Where-Object { $_.Id -match $Filter -or $_.Name -match $Filter -or $_.Requirement -match $Filter })
}

if ($selectedTests.Count -eq 0) {
    Write-Host "[WARN] No tests matched criteria (Tier: $Tier, Filter: '$Filter')" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "                      SHOTFIELD E2E TEST RUNNER" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "  Repo Root    : $RepoRoot"
Write-Host "  Pico Root    : $PicoRoot"
Write-Host "  Selected     : $($selectedTests.Count) test(s)"
Write-Host "  Tier Filter  : $(if ($Tier -eq 0) { 'All (0)' } else { $Tier })"
Write-Host "  Regex Filter : $(if ($Filter) { $Filter } else { '<None>' })"
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""

# TAP Header
Write-Host "1..$($selectedTests.Count)"
Write-Host "# TAP (Test Anything Protocol) Stream Initiated"
Write-Host ""

$context = @{
    RepoRoot = $RepoRoot
    PicoRoot = $PicoRoot
}

$testIndex = 1
$failCount = 0

foreach ($test in $selectedTests) {
    $res = Invoke-E2ETestCase -Test $test -Context $context

    if ($res.Status -eq "PASS") {
        Write-Host ("ok {0} - {1}: {2} ({3} ms)" -f $testIndex, $res.Id, $res.Name, $res.DurationMs) -ForegroundColor Green
    } else {
        $failCount++
        Write-Host ("not ok {0} - {1}: {2} ({3} ms) # {4}" -f $testIndex, $res.Id, $res.Name, $res.DurationMs, $res.Error) -ForegroundColor Red
    }
    $testIndex++
}

Write-Host ""
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "                               TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Formatted Summary Table
$results = $Global:E2ETestResults
$tableFormat = "{0,-10} {1,-6} {2,-8} {3,-10} {4}"
Write-Host ($tableFormat -f "TEST ID", "TIER", "STATUS", "DURATION", "NAME")
Write-Host ("-" * 80)

foreach ($r in $results) {
    $durStr = "$($r.DurationMs)ms"
    $line = $tableFormat -f $r.Id, ("T" + $r.Tier), $r.Status, $durStr, $r.Name
    if ($r.Status -eq "PASS") {
        Write-Host $line -ForegroundColor Green
    } else {
        Write-Host $line -ForegroundColor Red
        if ($r.Error) {
            Write-Host ("    └─ ERROR: " + $r.Error) -ForegroundColor DarkRed
        }
    }
}

Write-Host ("-" * 80)
$totalRun = $results.Count
$passedRun = $totalRun - $failCount
$passRate = if ($totalRun -gt 0) { [System.Math]::Round(($passedRun / $totalRun) * 100, 2) } else { 0 }

Write-Host ("Total: {0} | Passed: {1} | Failed: {2} | Pass Rate: {3}%" -f $totalRun, $passedRun, $failCount, $passRate) -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host "================================================================================" -ForegroundColor Cyan

# JSON Report Generation
if ($JsonReport -or $true) {
    $month = Get-Date -Format "yyyy-MM"
    $logDir = Join-Path $PicoRoot "05_验证记录\$month\gate_logs"
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $jsonPath = Join-Path $logDir "e2e_test_report_$timestamp.json"

    try {
        Save-E2EJsonReport -Results $results -OutputPath $jsonPath -Metadata @{
            RepoRoot = $RepoRoot
            Tier = $Tier
            Filter = $Filter
        }
        Write-Host "JSON Report saved to: $jsonPath" -ForegroundColor Gray
    } catch {
        Write-Host "[WARN] Failed to write JSON report: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
if ($failCount -eq 0) {
    Write-Host "[RESULT] ALL $totalRun TESTS PASSED (100% OK)" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[RESULT] $failCount TEST(S) FAILED" -ForegroundColor Red
    exit 1
}
