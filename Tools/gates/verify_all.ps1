# verify_all.ps1 - tier 2 of the ShotField gate: run every verifier in
# verifiers.map.json against the live editor, then write one evidence log.
#
#   powershell -File 04_Project\巡检\verify_all.ps1
#   powershell -File 04_Project\巡检\verify_all.ps1 -Only palette,navmesh
#
# Exit code 0 only when every selected verifier ends PASS or NO-ERROR.
# The log lands in 05_验证记录\2026-11\门禁日志\ and is text, so it can go to git.

param(
  [string[]]$Only = @(),
  [string]$LogDir = ""
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "unity_gate.ps1")

$map = Load-VerifierMap
if ($Only.Count -gt 0) {
  $expandedOnly = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
  $map = @($map | Where-Object { $expandedOnly -contains $_.id })
  if ($map.Count -eq 0) { Write-Host "No verifier matches -Only " ($Only -join ","); exit 2 }
}

if (-not $LogDir) {
  # Resolve the evidence folder without hardcoding its non-ASCII name: this
  # script is read by Windows PowerShell as ANSI, so Chinese literals here are unsafe.
  $wsRoot = (Get-Item (Join-Path $PSScriptRoot "..\..")).FullName
  $rec = Get-ChildItem -LiteralPath $wsRoot -Directory | Where-Object { $_.Name -like "05_*" } | Select-Object -First 1
  if (-not $rec) { throw "No 05_* evidence folder under $wsRoot" }
  $month = Get-Date -Format "yyyy-MM"
  $LogDir = (Join-Path $rec.FullName ($month + "\gate_logs"))
}
if (-not (Test-Path -LiteralPath $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path $LogDir ("verify_all_" + $stamp + ".log")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\ShotField")).Path
$url = try { Get-GateEndpoint } catch { $null }
$bridgeUp = if ($url) { Test-GateBridge -Url $url } else { $false }
$sid = if ($bridgeUp) { Start-GateSession -Url $url } else { $null }

Write-Host ""
Write-Host "ShotField gate / tier 2 (full sweep)"
if ($bridgeUp) {
  Write-Host ("  mode     : live editor bridge (" + ($url -replace '/t/.*', '/t/<redacted>/') + ")")
} else {
  Write-Host "  mode     : headless batchmode (live bridge offline)"
}
Write-Host ("  run      : " + $map.Count + " verifier(s)")

$lines = @()
$lines += "ShotField verify_all " + $stamp
$lines += if ($bridgeUp) { "unity_bridge " + ($url -replace '/t/.*', '/t/<redacted>/') } else { "unity_batchmode headless" }

$fail = 0
$noError = 0
foreach ($entry in $map) {
  $sw = [System.Diagnostics.Stopwatch]::StartNew()
  try {
    $r = if ($bridgeUp) {
      Invoke-Verifier -Url $url -Sid $sid -Entry $entry
    } else {
      Invoke-VerifierBatch -RepoRoot $repoRoot -Entry $entry
    }
  } catch {
    $r = [pscustomobject]@{ id = $entry.id; verdict = "NO-VERDICT"; detail = $_.Exception.Message }
  }
  $sw.Stop()
  if ($r.verdict -eq "FAIL" -or $r.verdict -eq "NO-VERDICT") { $fail++ }
  if ($r.verdict -eq "NO-ERROR") { $noError++ }
  $line = "{0,-14} {1,-10} {2,5:N0}s  {3}" -f $r.verdict, $r.id, $sw.Elapsed.TotalSeconds, $r.detail
  Write-Host ("  " + $line)
  $lines += $line
}

$lines += ""
$lines += "summary: " + $map.Count + " run, " + $fail + " failed/no-verdict, " + $noError + " no-error (unverified verdict)"
Write-Host ""
Write-Host ("  " + $lines[-1])
Write-Host ("  log: " + $logPath)
$lines | Set-Content -LiteralPath $logPath -Encoding UTF8

if ($fail -eq 0) { exit 0 }
exit 1
