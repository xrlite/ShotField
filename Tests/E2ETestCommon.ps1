# E2ETestCommon.ps1
# Common framework, assertions, Unity JSON-RPC client, AST scanner, and TAP engine for ShotField E2E testing.
# Encoded in UTF-8 with BOM for compatibility with Windows PowerShell 5.1 and pwsh.

[CmdletBinding()]
param()

# Global Test Framework State
if (-not $Global:E2ETestRegistry) {
    $Global:E2ETestRegistry = [System.Collections.Generic.List[PSCustomObject]]::new()
}
if (-not $Global:E2ETestResults) {
    $Global:E2ETestResults = [System.Collections.Generic.List[PSCustomObject]]::new()
}

# -----------------------------------------------------------------------------
# 1. Assertion Helper Functions
# -----------------------------------------------------------------------------

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message = "Expected condition to be True"
    )
    if (-not $Condition) {
        throw [System.InvalidOperationException]"[Assert-True Failed] $Message (Actual: False)"
    }
}

function Assert-False {
    param(
        [bool]$Condition,
        [string]$Message = "Expected condition to be False"
    )
    if ($Condition) {
        throw [System.InvalidOperationException]"[Assert-False Failed] $Message (Actual: True)"
    }
}

function Assert-Equal {
    param(
        $Expected,
        $Actual,
        [string]$Message = "Expected values to be equal"
    )
    if ($Expected -ne $Actual) {
        throw [System.InvalidOperationException]"[Assert-Equal Failed] $Message | Expected: <$Expected>, Actual: <$Actual>"
    }
}

function Assert-Match {
    param(
        [string]$Pattern,
        [string]$Value,
        [string]$Message = "Expected value to match regex pattern"
    )
    if ($null -eq $Value -or $Value -notmatch $Pattern) {
        throw [System.InvalidOperationException]"[Assert-Match Failed] $Message | Pattern: <$Pattern>, Actual: <$Value>"
    }
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Message = "Expected path to exist on disk"
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw [System.IO.FileNotFoundException]"[Assert-PathExists Failed] $Message | Missing: <$Path>"
    }
}

function Assert-WithinTolerance {
    param(
        [double]$Expected,
        [double]$Actual,
        [double]$Tolerance = 0.001,
        [string]$Message = "Expected actual value to be within tolerance"
    )
    $diff = [System.Math]::Abs($Expected - $Actual)
    if ($diff -gt $Tolerance) {
        throw [System.InvalidOperationException]"[Assert-WithinTolerance Failed] $Message | Expected: $Expected, Actual: $Actual, Diff: $diff > Tolerance: $Tolerance"
    }
}

# -----------------------------------------------------------------------------
# 2. Unity JSON-RPC & Bridge Helper Functions
# -----------------------------------------------------------------------------

function Get-E2EGateEndpoint {
    param(
        [string]$PicoRoot = "F:\pico"
    )
    if ($env:SHOTFIELD_UNITY_MCP) {
        return $env:SHOTFIELD_UNITY_MCP
    }
    $cfg = Join-Path $PicoRoot ".qoder\settings.local.json"
    if (Test-Path -LiteralPath $cfg) {
        try {
            $json = Get-Content -LiteralPath $cfg -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($json.mcpServers -and $json.mcpServers.'shotfield-unity' -and $json.mcpServers.'shotfield-unity'.url) {
                return $json.mcpServers.'shotfield-unity'.url
            }
        } catch { }
    }
    return "http://127.0.0.1:8765"
}

function Send-E2EGateRpc {
    param(
        [string]$Url,
        [string]$Body,
        [string]$Sid = "",
        [int]$TimeoutSec = 10
    )
    $headers = @{ "Accept" = "application/json, text/event-stream" }
    if ($Sid) { $headers["Mcp-Session-Id"] = $Sid }
    try {
        $r = Invoke-WebRequest -Uri $Url -Method POST -Body $Body -ContentType "application/json" `
            -Headers $headers -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
        $sidOut = ($r.Headers["Mcp-Session-Id"] -join "")
        $text = $r.Content
        if ($text -match '(?s)data:\s*(\{.*\})') { $text = $Matches[1].Trim() }
        $parsed = $null
        try { $parsed = $text | ConvertFrom-Json } catch { }
        return @{ ok = $true; sid = $sidOut; text = $text; json = $parsed }
    } catch {
        return @{ ok = $false; error = $_.Exception.Message; sid = $Sid; text = ""; json = $null }
    }
}

function Start-E2EGateSession {
    param([string]$Url)
    $init = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"shotfield-e2e","version":"1"}}}'
    $r = Send-E2EGateRpc -Url $Url -Body $init -TimeoutSec 10
    if (-not $r.ok) { return $null }
    $sid = $r.sid
    try {
        Send-E2EGateRpc -Url $Url -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}' -Sid $sid -TimeoutSec 5 | Out-Null
    } catch { }
    return $sid
}

function Invoke-E2EGateTool {
    param(
        [string]$Url,
        [string]$Sid,
        [string]$Tool,
        [hashtable]$Arguments = @{}
    )
    $payload = @{
        jsonrpc = "2.0";
        id = [System.Guid]::NewGuid().ToString();
        method = "tools/call";
        params = @{ name = $Tool; arguments = $Arguments }
    } | ConvertTo-Json -Depth 30 -Compress

    $r = Send-E2EGateRpc -Url $Url -Body $payload -Sid $Sid -TimeoutSec 30
    if (-not $r.ok) {
        return @{ ok = $false; text = $r.error; data = $null }
    }
    if ($r.json.error) {
        return @{ ok = $false; text = ($r.json.error | ConvertTo-Json -Compress); data = $r.json.error }
    }
    $content = $r.json.result.content
    $text = ""
    if ($content) { $text = ($content | ForEach-Object { $_.text }) -join "`n" }
    $parsed = $null
    try { $parsed = $text | ConvertFrom-Json } catch { }
    return @{ ok = $true; text = $text; data = $parsed }
}

function Invoke-E2EGateCode {
    param(
        [string]$Url,
        [string]$Sid,
        [string]$CSharpCode
    )
    return Invoke-E2EGateTool -Url $Url -Sid $Sid -Tool "execute_code" -Arguments @{ code = $CSharpCode }
}

function Get-E2EConsoleLogs {
    param(
        [string]$Url,
        [string]$Sid,
        [string]$LogType = "error",
        [int]$Count = 60
    )
    return Invoke-E2EGateTool -Url $Url -Sid $Sid -Tool "get_console_logs" -Arguments @{
        log_type = $LogType; source = "console"; count = $Count; group_duplicates = $false
    }
}

function Get-E2EEditorDialog {
    param(
        [string]$Url,
        [string]$Sid
    )
    return Invoke-E2EGateTool -Url $Url -Sid $Sid -Tool "get_editor_dialog" -Arguments @{}
}

# -----------------------------------------------------------------------------
# 3. AST & Regex Code Scanning Utilities
# -----------------------------------------------------------------------------

function Scan-PrivateColorDeclarations {
    param(
        [string]$DirectoryPath
    )
    # Searches for private Color AmberColor or hardcoded private color definitions
    $findings = [System.Collections.Generic.List[PSCustomObject]]::new()
    if (-not (Test-Path -LiteralPath $DirectoryPath)) { return $findings }

    $files = Get-ChildItem -LiteralPath $DirectoryPath -Recurse -Filter *.cs -File
    $regexPrivateAmber = [regex]'(?i)(?:private|protected|internal)\s+(?:static\s+)?(?:readonly\s+)?Color\s+AmberColor'
    $regexPrivateColor = [regex]'(?i)(?:private|protected)\s+(?:static\s+)?(?:readonly\s+)?Color\s+([A-Za-z0-9_]+)\s*=\s*new\s+Color\('

    foreach ($file in $files) {
        $lines = Get-Content -LiteralPath $file.FullName -Encoding UTF8
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($regexPrivateAmber.IsMatch($line)) {
                $findings.Add([PSCustomObject]@{
                    File = $file.FullName
                    LineNumber = $i + 1
                    Type = "PrivateAmberColor"
                    Code = $line.Trim()
                })
            }
            elseif ($regexPrivateColor.IsMatch($line)) {
                # Ignore PrevisPalette.cs and SemanticPalette.cs which define palette colors
                if ($file.Name -notin @("PrevisPalette.cs", "SemanticPalette.cs", "VerifyAssert.cs")) {
                    $findings.Add([PSCustomObject]@{
                        File = $file.FullName
                        LineNumber = $i + 1
                        Type = "PrivateColorConstant"
                        Code = $line.Trim()
                    })
                }
            }
        }
    }
    return $findings
}

function Scan-MagicColorComparisons {
    param(
        [string]$DirectoryPath
    )
    # Detects loose float comparisons like col.r > 0.8f or wallCol.r - 0.847f in verifiers
    $findings = [System.Collections.Generic.List[PSCustomObject]]::new()
    if (-not (Test-Path -LiteralPath $DirectoryPath)) { return $findings }

    $files = Get-ChildItem -LiteralPath $DirectoryPath -Recurse -Filter *.cs -File
    $regexMagic = [regex]'(?:col[A-Za-z0-9_]*\.[rgb]\s*[><=]\s*0\.\d+f|wallCol\.[rgb]\s*-\s*0\.\d+f|floorCol\.[rgb]\s*-\s*0\.\d+f)'

    foreach ($file in $files) {
        $lines = Get-Content -LiteralPath $file.FullName -Encoding UTF8
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ($regexMagic.IsMatch($line)) {
                $findings.Add([PSCustomObject]@{
                    File = $file.FullName
                    LineNumber = $i + 1
                    Type = "MagicFloatColorComparison"
                    Code = $line.Trim()
                })
            }
        }
    }
    return $findings
}

function Scan-FileLinesWithinBudget {
    param(
        [string]$DirectoryPath,
        [int]$LineBudget = 200
    )
    $oversize = [System.Collections.Generic.List[PSCustomObject]]::new()
    if (-not (Test-Path -LiteralPath $DirectoryPath)) { return $oversize }

    $files = Get-ChildItem -LiteralPath $DirectoryPath -Recurse -Filter *.cs -File
    foreach ($f in $files) {
        $count = (Get-Content -LiteralPath $f.FullName -Encoding UTF8 | Measure-Object).Count
        if ($count -gt $LineBudget) {
            $oversize.Add([PSCustomObject]@{
                Path = $f.FullName
                Lines = $count
                Over = $count - $LineBudget
            })
        }
    }
    return $oversize
}

function Scan-OrphanAssetsWithoutMeta {
    param(
        [string]$DirectoryPath
    )
    $orphans = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $DirectoryPath)) { return $orphans }

    $files = Get-ChildItem -LiteralPath $DirectoryPath -Recurse -File | Where-Object { $_.Extension -ne ".meta" }
    foreach ($f in $files) {
        $meta = $f.FullName + ".meta"
        if (-not (Test-Path -LiteralPath $meta)) {
            $orphans.Add($f.FullName)
        }
    }
    return $orphans
}

function Test-FileHasUtf8Bom {
    param(
        [string]$FilePath
    )
    if (-not (Test-Path -LiteralPath $FilePath)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    if ($bytes.Length -lt 3) { return $false }
    return ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
}

# -----------------------------------------------------------------------------
# 4. Git Status and YAML Utilities
# -----------------------------------------------------------------------------

function Get-GitChangedFiles {
    param(
        [string]$RepoRoot,
        [string]$Base = "HEAD"
    )
    $changed = @()
    Push-Location $RepoRoot
    try {
        $tracked = git diff --name-only $Base 2>$null
        $staged  = git diff --name-only --cached 2>$null
        $others  = git ls-files --others --exclude-standard 2>$null
        $changed = @(@($tracked) + @($staged) + @($others) | Where-Object { $_ } | Select-Object -Unique)
    } finally {
        Pop-Location
    }
    return @($changed | ForEach-Object { $_ -replace '\\', '/' })
}

function Get-GitUntrackedAssets {
    param(
        [string]$RepoRoot
    )
    $untracked = @()
    Push-Location $RepoRoot
    try {
        $others = git ls-files --others --exclude-standard Assets/ 2>$null
        $untracked = @($others | Where-Object { $_ -match '\.(asset|mat|prefab|unity)$' })
    } finally {
        Pop-Location
    }
    return $untracked
}

function Get-YamlProperty {
    param(
        [string]$FilePath,
        [string]$PropertyName
    )
    if (-not (Test-Path -LiteralPath $FilePath)) { return $null }
    $pattern = "(?m)^\s*" + [regex]::Escape($PropertyName) + "\s*:\s*(.+)$"
    $content = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
    if ($content -match $pattern) {
        return $Matches[1].Trim()
    }
    return $null
}

# -----------------------------------------------------------------------------
# 5. Test Registry and TAP Execution Engine
# -----------------------------------------------------------------------------

function Register-E2ETest {
    param(
        [string]$Id,
        [int]$Tier,
        [string]$Requirement,
        [string]$Name,
        [string]$Description,
        [scriptblock]$ScriptBlock
    )
    $existing = $Global:E2ETestRegistry | Where-Object { $_.Id -eq $Id }
    if ($existing) {
        $Global:E2ETestRegistry.Remove($existing) | Out-Null
    }
    $testObj = [PSCustomObject]@{
        Id = $Id
        Tier = $Tier
        Requirement = $Requirement
        Name = $Name
        Description = $Description
        ScriptBlock = $ScriptBlock
    }
    $Global:E2ETestRegistry.Add($testObj)
}

function Invoke-E2ETestCase {
    param(
        [PSCustomObject]$Test,
        [hashtable]$Context
    )
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "PASS"
    $errorMsg = ""
    $outputData = $null

    try {
        & $Test.ScriptBlock $Context
    } catch {
        $status = "FAIL"
        $errorMsg = $_.Exception.Message
    } finally {
        $sw.Stop()
    }

    $res = [PSCustomObject]@{
        Id = $Test.Id
        Tier = $Test.Tier
        Requirement = $Test.Requirement
        Name = $Test.Name
        Description = $Test.Description
        Status = $status
        DurationMs = [System.Math]::Round($sw.Elapsed.TotalMilliseconds, 2)
        Error = $errorMsg
    }
    $Global:E2ETestResults.Add($res)
    return $res
}

function Save-E2EJsonReport {
    param(
        [System.Collections.Generic.List[PSCustomObject]]$Results,
        [string]$OutputPath,
        [hashtable]$Metadata = @{}
    )
    $passed = ($Results | Where-Object { $_.Status -eq "PASS" }).Count
    $failed = ($Results | Where-Object { $_.Status -eq "FAIL" }).Count
    $total = $Results.Count

    $reportObj = [PSCustomObject]@{
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        suite = "ShotField E2E Test Suite"
        metadata = $Metadata
        summary = [PSCustomObject]@{
            total = $total
            passed = $passed
            failed = $failed
            pass_rate = if ($total -gt 0) { [System.Math]::Round(($passed / $total) * 100, 2) } else { 0 }
            verdict = if ($failed -eq 0 -and $total -gt 0) { "PASS" } else { "FAIL" }
        }
        results = $Results
    }

    $dir = Split-Path -Parent $OutputPath
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $json = $reportObj | ConvertTo-Json -Depth 10
    $encoding = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($OutputPath, $json, $encoding)
}


