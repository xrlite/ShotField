# unity_gate.ps1 - shared library for the two-tier ShotField gate.
# Dot-source it:  . "$PSScriptRoot\unity_gate.ps1"
# It talks to the Unity editor bridge (same server the AI agents use) when online,
# and gracefully falls back to headless batchmode if the editor is offline.
#
# Endpoint resolution order:
#   1. $env:SHOTFIELD_UNITY_MCP
#   2. mcpServers["shotfield-unity"].url in <workspace root>/.qoder/settings.local.json
# The token therefore stays outside the git repo (AGENTS rule 7 / no secrets in VCS).

function Get-GateEndpoint {
  if ($env:SHOTFIELD_UNITY_MCP) { return $env:SHOTFIELD_UNITY_MCP }
  $cfg = Join-Path (Join-Path $PSScriptRoot "..\..") ".qoder\settings.local.json"
  if (-not (Test-Path -LiteralPath $cfg)) {
    return $null
  }
  $json = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
  $url = $json.mcpServers.'shotfield-unity'.url
  return $url
}

function Test-GateBridge {
  param([string]$Url)
  if (-not $Url) { return $false }
  try {
    $uri = [System.Uri]$Url
    $tcp = New-Object System.Net.Sockets.TcpClient
    $iar = $tcp.BeginConnect($uri.Host, $uri.Port, $null, $null)
    $ok = $iar.AsyncWaitHandle.WaitOne(80, $false)
    $tcp.Close()
    return [bool]$ok
  } catch {
    return $false
  }
}

function Get-UnityExePath {
  $defaultPath = "D:\unity\6000.6.5f1\Editor\Unity.exe"
  if (Test-Path -LiteralPath $defaultPath) { return $defaultPath }
  if ($env:UNITY_EXE -and (Test-Path -LiteralPath $env:UNITY_EXE)) { return $env:UNITY_EXE }
  return "Unity.exe"
}

function Send-GateRpc {
  param([string]$Url, [string]$Body, [string]$Sid, [int]$TimeoutSec = 240)
  $h = @{ "Accept" = "application/json, text/event-stream" }
  if ($Sid) { $h["Mcp-Session-Id"] = $Sid }
  $r = Invoke-WebRequest -Uri $Url -Method POST -Body $Body -ContentType "application/json" `
        -Headers $h -TimeoutSec $TimeoutSec -UseBasicParsing
  $sidOut = ($r.Headers["Mcp-Session-Id"] -join "")
  $text = $r.Content
  if ($text -match '(?s)data:\s*(\{.*\})') { $text = $Matches[1].Trim() }
  return @{ sid = $sidOut; json = ($text | ConvertFrom-Json) }
}

function Start-GateSession {
  param([string]$Url)
  $init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"shotfield-gate","version":"1"}}}'
  $r = Send-GateRpc -Url $Url -Body $init
  $sid = $r.sid
  try { Send-GateRpc -Url $Url -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' -Sid $sid | Out-Null } catch { }
  return $sid
}

function Invoke-GateTool {
  param([string]$Url, [string]$Sid, [string]$Tool, [hashtable]$Arguments = @{})
  $payload = @{ jsonrpc = "2.0"; id = 2; method = "tools/call"; params = @{ name = $Tool; arguments = $Arguments } } |
              ConvertTo-Json -Depth 30 -Compress
  $r = Send-GateRpc -Url $Url -Body $payload -Sid $Sid
  if ($r.json.error) { return @{ ok = $false; text = ($r.json.error | ConvertTo-Json -Compress) } }
  $content = $r.json.result.content
  $text = ""
  if ($content) { $text = ($content | ForEach-Object { $_.text }) -join "`n" }
  $parsed = $null
  try { $parsed = $text | ConvertFrom-Json } catch { }
  return @{ ok = $true; text = $text; data = $parsed }
}

function Get-GateErrorCount {
  param([string]$Url, [string]$Sid)
  $res = Invoke-GateTool -Url $Url -Sid $Sid -Tool "get_console_logs" -Arguments @{
    log_type = "error"; source = "cache"; count = 60; group_duplicates = $false; include_stack_trace = $false
  }
  if (-not $res.ok) { return @{ count = -1; sample = @($res.text) } }
  $txt = $res.text
  $count = ([regex]::Matches($txt, "\[ERROR\]")).Count
  $msgs = @()
  foreach ($line in ($txt -split "\r?\n|\n")) {
    if ($line -match '\[ERROR\]\s*(.+)$') { $msgs += ($Matches[1] -replace '\s+', ' ') }
  }
  if ($count -eq 0 -and $txt -match '\(\s*(\d+)\s+entries') { $count = [int]$Matches[1] }
  return @{ count = $count; sample = $msgs }
}

function Get-GateCursor {
  param([string]$Url, [string]$Sid)
  $res = Invoke-GateTool -Url $Url -Sid $Sid -Tool "get_console_logs" -Arguments @{
    log_type = "all"; source = "console"; count = 1
  }
  if ($res.text -match 'latest_seq:\s*(\d+)') { return [long]$Matches[1] }
  return 0
}

# Verdict rules, deliberately strict so that silence can never read as success:
#   FAIL        - the verifier logged an error line, or Unity logged any error during the run
#   PASS        - a tag+PASS line was observed and no error was logged (only when the map has a tag)
#   NO-ERROR    - the run logged nothing tagged and nothing failed (weakest tier, still counted green)
#   NO-VERDICT  - the menu item itself refused to run -> treated as gate failure by callers
function Invoke-Verifier {
  param([string]$Url, [string]$Sid, [hashtable]$Entry)

  $cursor = Get-GateCursor -Url $Url -Sid $Sid

  $run = $null
  if ($Entry.code) {
    $snippet = $Entry.code + ' return "ran";'
    $run = Invoke-GateTool -Url $Url -Sid $Sid -Tool "execute_code" -Arguments @{ code = $snippet }
    if (-not $run.ok -or ($run.text -match '"success"\s*:\s*false')) {
      return [pscustomobject]@{ id = $Entry.id; verdict = "NO-VERDICT"; detail = ("code path failed: " + $run.text.Substring(0, [Math]::Min(200, $run.text.Length))) }
    }
  } else {
    $run = Invoke-GateTool -Url $Url -Sid $Sid -Tool "execute_menu_item" -Arguments @{
      menu_path = $Entry.menu; allow_modal = $false
    }
    if (-not $run.ok -or ($run.text -match '"success"\s*:\s*false')) {
      $why = if ($run.data -and $run.data.error) { $run.data.error } else { $run.text }
      return [pscustomobject]@{ id = $Entry.id; verdict = "NO-VERDICT"; detail = "$why" }
    }
  }
  Start-Sleep -Seconds 2

  $err = Invoke-GateTool -Url $Url -Sid $Sid -Tool "get_console_logs" -Arguments @{
    log_type = "error"; source = "console"; since_seq = $cursor; count = 60; group_duplicates = $false
  }
  $errCount = ([regex]::Matches($err.text, "\[ERROR\]")).Count
  if ($errCount -eq 0 -and $err.text -match '\(\s*(\d+)\s+entries') { $errCount = [int]$Matches[1] }
  $tag = $Entry.tag
  $tagLine = ""
  $tagVerdict = ""
  if ($tag) {
    $found = Invoke-GateTool -Url $Url -Sid $Sid -Tool "get_console_logs" -Arguments @{
      log_type = "all"; source = "console"; since_seq = $cursor; count = 120; group_duplicates = $false
    }
    $txt = $found.text
    $idx = $txt.IndexOf($tag)
    if ($idx -ge 0) {
      $take = [Math]::Min(160, $txt.Length - $idx)
      $tagLine = ($txt.Substring($idx, $take) -replace '\r|\n|\"', ' ' -replace '\s+', ' ').Trim()
      $head = $txt.Substring($idx, $take)
      if ($head -match 'FAIL') { $tagVerdict = "FAIL" }
      elseif ($head -match 'PASS') { $tagVerdict = "PASS" }
    }
  }

  $detail = if ($tagLine) { $tagLine } elseif ($errCount -gt 0) { if ($err.sample.Count -gt 0) { $err.sample[0] } else { "error(s) in console history" } } else { "no tagged log" }

  if ($tag) {
    if ($tagVerdict -eq "FAIL") {
      return [pscustomobject]@{ id = $Entry.id; verdict = "FAIL"; detail = $detail }
    }
    if ($tagVerdict -eq "PASS") {
      return [pscustomobject]@{ id = $Entry.id; verdict = "PASS"; detail = $detail }
    }
    return [pscustomobject]@{ id = $Entry.id; verdict = "NO-VERDICT"; detail = ("tag ran but printed neither PASS nor FAIL: " + $detail) }
  }
  return [pscustomobject]@{ id = $Entry.id; verdict = "NO-ERROR"; detail = $detail }
}

function Invoke-VerifierBatch {
  param([string]$RepoRoot, [hashtable]$Entry)
  if (-not $Entry.method) {
    return [pscustomobject]@{ id = $Entry.id; verdict = "NO-VERDICT"; detail = "no batch method registered in map" }
  }
  $unity = Get-UnityExePath
  $tmpLog = [System.IO.Path]::GetTempFileName()
  try {
    $proc = Start-Process -FilePath $unity -ArgumentList "-batchmode", "-quit", "-projectPath", $RepoRoot, "-executeMethod", $Entry.method, "-logFile", $tmpLog -Wait -PassThru -NoNewWindow
    $logText = ""
    if (Test-Path -LiteralPath $tmpLog) {
      $logText = [System.IO.File]::ReadAllText($tmpLog)
    }
    $tag = $Entry.tag
    $tagLine = ""
    $tagVerdict = ""
    if ($tag -and $logText) {
      $idx = $logText.IndexOf($tag)
      if ($idx -ge 0) {
        $take = [Math]::Min(160, $logText.Length - $idx)
        $tagLine = ($logText.Substring($idx, $take) -replace '\r|\n|\"', ' ' -replace '\s+', ' ').Trim()
        $head = $logText.Substring($idx, $take)
        if ($head -match 'FAIL') { $tagVerdict = "FAIL" }
        elseif ($head -match 'PASS') { $tagVerdict = "PASS" }
      }
    }
    $detail = if ($tagLine) { $tagLine } elseif ($proc.ExitCode -ne 0) { "Unity exited with code " + $proc.ExitCode } else { "no tagged log" }
    if ($tag) {
      if ($tagVerdict -eq "FAIL") { return [pscustomobject]@{ id = $Entry.id; verdict = "FAIL"; detail = $detail } }
      if ($tagVerdict -eq "PASS") { return [pscustomobject]@{ id = $Entry.id; verdict = "PASS"; detail = $detail } }
      if ($proc.ExitCode -ne 0) { return [pscustomobject]@{ id = $Entry.id; verdict = "FAIL"; detail = ("batch failed (code " + $proc.ExitCode + "): " + $detail) } }
      return [pscustomobject]@{ id = $Entry.id; verdict = "NO-VERDICT"; detail = ("batch ran but printed neither PASS nor FAIL: " + $detail) }
    }
    if ($proc.ExitCode -eq 0) { return [pscustomobject]@{ id = $Entry.id; verdict = "NO-ERROR"; detail = $detail } }
    return [pscustomobject]@{ id = $Entry.id; verdict = "FAIL"; detail = ("batch exit code " + $proc.ExitCode) }
  } finally {
    if (Test-Path -LiteralPath $tmpLog) { Remove-Item -LiteralPath $tmpLog -Force -ErrorAction SilentlyContinue }
  }
}

function Load-VerifierMap {
  $path = Join-Path $PSScriptRoot "verifiers.map.json"
  $raw = Get-Content -LiteralPath $path -Raw -Encoding UTF8
  $doc = $raw | ConvertFrom-Json
  $list = @()
  foreach ($v in $doc.verifiers) {
    $e = @{ id = $v.id; menu = $v.menu }
    if ($v.tag) { $e.tag = $v.tag }
    if ($v.code) { $e.code = $v.code }
    if ($v.method) { $e.method = $v.method }
    $e.watch = @($v.watch)
    $list += $e
  }
  return $list
}
