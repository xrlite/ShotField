# ShotField credential-leak gate (secret_scan)
#
# Why this exists: AGENTS rule 7 keeps binaries out of git, and .gitignore keeps the local
# credential file out of git. Neither of them stops Unity from packing Assets/Resources/**
# into the APK. So "no key in git" is NOT the right criterion -- the right criterion is
# "no key in any artifact we ship". This script checks both.
#
# PowerShell 5.1 reads .ps1 as ANSI when there is no BOM, which turns Chinese literals into
# mojibake and breaks parsing. This file is therefore deliberately ASCII-only; numbered
# workspace folders are resolved by wildcard instead of by name.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File 巡检/secret_scan.ps1
#   powershell -File 巡检/secret_scan.ps1 -Targets resources
#   powershell -File 巡检/secret_scan.ps1 -Targets tracked,resources -Strict   # exit 1 on hit
#
# Output prints only the first 8 characters of any match. Never log the full value.

param(
    [string[]]$Targets = @("tracked", "resources", "apks"),
    [switch]$Strict
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

# -Targets 从外部（bash / -Command）传进来时常常是单条 "a,b" 字符串而不是数组；
# 另外 switch 匹配不到的目标会被静默跳过，于是「什么都没扫」也会打印 PASS。
# 这里先拆逗号，再把未知目标当错误，杜绝空扫假绿。
$Targets = @((($Targets -join ",") -split "[,; ]+") | Where-Object { $_ } | ForEach-Object { $_.ToLower() })
$Known = @("tracked", "resources", "apks", "logs")
foreach ($t in $Targets) {
    if ($Known -notcontains $t) {
        Write-Output ("SECRET-SCAN=ERROR unknown target: " + $t + " (known: " + ($Known -join ",") + ")")
        exit 2
    }
}
if ($Targets.Count -eq 0) { $Targets = $Known }
$planned = @($Targets)

$ProjectRoot = Split-Path -Parent $PSScriptRoot          # .../04_Project
$RepoRoot    = Join-Path $ProjectRoot "ShotField"        # .../04_Project/ShotField
$Workspace   = Split-Path -Parent $ProjectRoot           # workspace root

$Patterns = @(
    'sk-[A-Za-z0-9_\-]{20,}',
    'tsk_[A-Za-z0-9_\-]{20,}',
    'ghp_[A-Za-z0-9_\-]{20,}',
    'github_pat_[A-Za-z0-9_\-]{20,}',
    'xoxb-[A-Za-z0-9_\-]{20,}',
    'AIza[0-9A-Za-z_\-]{30,}',
    'ark-[a-f0-9]{24,}',
    'Bearer\s+[A-Za-z0-9_\-\.]{20,}',
    'eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}',
    '(?i)(?<![A-Za-z])(secret|token|apikey|api_key|authorization)[ \t]*[:=][ \t]*(?:"[^"\s]{16,}"|''[^''\s]{16,}''|(?=[A-Za-z0-9_-]*\d)[A-Za-z0-9_-]{16,31}|[A-Za-z0-9_-]{32,})'
)
$Rx = [regex]($Patterns -join '|')

$hits = New-Object System.Collections.ArrayList
$script:scanned = 0

function Test-File([string]$scope, [string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return }
    $item = Get-Item -LiteralPath $path
    if ($item.Length -gt 300MB) { return }
    $script:scanned++
    if ($item.Name -ieq "ai_credentials.json") {
        [void]$hits.Add([pscustomobject]@{ Scope = $scope; File = $path; Line = 1; Key = "forbidden-credential-file" })
        return
    }
    try { $bytes = [System.IO.File]::ReadAllBytes($path) } catch { return }
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    foreach ($m in $Rx.Matches($text)) {
        $v = $m.Value
        $masked = if ($v.Length -gt 8) { $v.Substring(0, 8) + '***' } else { '***' }
        $line = ($text.Substring(0, $m.Index) -split "`n").Count
        [void]$hits.Add([pscustomobject]@{ Scope = $scope; File = $path; Line = $line; Key = $masked })
    }
}

function Resolve-NumberedDir([string]$prefix) {
    Get-ChildItem -LiteralPath $Workspace -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like ($prefix + '_*') } |
        Select-Object -First 1 -ExpandProperty FullName
}

foreach ($t in $Targets) {
    switch ($t.ToLower()) {
        "tracked" {
            Push-Location $RepoRoot
            # PS 5.1 在 ErrorActionPreference=Stop 下，原生命令往 stderr 写一点东西就会抛终止错误，
            # 把整个分支打死（实测 scannedFiles=0 却打印 PASS 的前身）。这里临时降级。
            $prev = $ErrorActionPreference
            $ErrorActionPreference = "SilentlyContinue"
            # core.quotePath=false 防止 git 把非 ASCII 路径转成八进制转义并加引号，
            # 那种带引号的路径会让 Test-Path 直接抛错（实测踩过：Assets/Settings/Build Profiles/Android™.asset）。
            $files = @(& git -c core.quotePath=false ls-files)
            $ErrorActionPreference = $prev
            Pop-Location
            $files = @($files | Where-Object { $_ -and $_ -notlike '"*' })
            foreach ($f in $files) {
                if ($f -like "*.dll" -or $f -like "*.png" -or $f -like "*.mp4" -or $f -like "*.apk") { continue }
                Test-File "git-tracked" (Join-Path $RepoRoot $f)
            }
        }
        "resources" {
            # Unity packs everything under Assets/Resources into the player build.
            $res = Join-Path $RepoRoot "Assets/Resources"
            if (Test-Path -LiteralPath $res) {
                Get-ChildItem -LiteralPath $res -Recurse -File | ForEach-Object {
                    Test-File "packed-into-APK" $_.FullName
                }
            }
            $ship = Resolve-NumberedDir "07"
            if ($ship) {
                Get-ChildItem -LiteralPath $ship -Recurse -File -ErrorAction SilentlyContinue |
                    ForEach-Object { Test-File "submission-package" $_.FullName }
            }
        }
        "apks" {
            $apks = @()
            $bld = Join-Path $RepoRoot "build"
            if (Test-Path -LiteralPath $bld) {
                $apks += Get-ChildItem -LiteralPath $bld -Recurse -Filter *.apk -ErrorAction SilentlyContinue
            }
            $ev = Resolve-NumberedDir "05"
            if ($ev) {
                $apks += Get-ChildItem -LiteralPath $ev -Recurse -Filter *.apk -ErrorAction SilentlyContinue |
                    Sort-Object LastWriteTime -Descending | Select-Object -First 2
            }
            $tmp = Join-Path $env:TEMP ("sf_secret_" + [Guid]::NewGuid().ToString("N").Substring(0, 8))
            foreach ($a in ($apks | Sort-Object FullName -Unique)) {
                $dest = Join-Path $tmp ([IO.Path]::GetFileNameWithoutExtension($a.Name))
                New-Item -ItemType Directory -Force -Path $dest | Out-Null
                try {
                    [System.IO.Compression.ZipFile]::ExtractToDirectory($a.FullName, $dest)
                    Get-ChildItem -LiteralPath $dest -Recurse -File -ErrorAction SilentlyContinue |
                        ForEach-Object { Test-File ("apk:" + $a.Name) $_.FullName }
                } finally {
                    Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue
        }
        "logs" {
            # Post-build logs and logcat dumps may carry the Gradle env dump; scan as shipped evidence.
            $ev = Resolve-NumberedDir "05"
            if ($ev) {
                Get-ChildItem -LiteralPath $ev -Recurse -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Extension -eq ".log" -or $_.Extension -eq ".txt" } |
                    ForEach-Object { Test-File "build-log" $_.FullName }
            }
            $repoLogs = Join-Path $RepoRoot "Logs"
            if (Test-Path -LiteralPath $repoLogs) {
                Get-ChildItem -LiteralPath $repoLogs -Recurse -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Extension -eq ".log" -or $_.Extension -eq ".txt" } |
                    ForEach-Object { Test-File "build-log" $_.FullName }
            }
        }
    }
}

Write-Output "SHOTFIELD SECRET SCAN"
Write-Output ("time=" + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Write-Output ("targets=" + ($planned -join ","))
Write-Output ("scannedFiles=" + $script:scanned)
Write-Output ("hitCount=" + $hits.Count)

if ($script:scanned -eq 0) {
    Write-Output "SECRET-SCAN=ERROR nothing was read; a PASS here would be false"
    exit 2
}

if ($hits.Count -eq 0) {
    Write-Output ("SECRET-SCAN=PASS (" + $script:scanned + " files read, no credential pattern)")
    exit 0
}

foreach ($g in ($hits | Group-Object Scope)) {
    Write-Output ("SCOPE=" + $g.Name + " count=" + $g.Count)
    $g.Group | Select-Object -First 10 | ForEach-Object {
        Write-Output ("  " + $_.File + ":" + $_.Line + "  " + $_.Key)
    }
}

$inGit = @($hits | Where-Object { $_.Scope -eq "git-tracked" })
$inArtifact = @($hits | Where-Object { $_.Scope -like "packed-into-APK*" -or $_.Scope -like "apk:*" -or $_.Scope -eq "submission-package" })

if ($inGit.Count -gt 0) {
    Write-Output "GRADE=P0-IN-GIT (public repo exposure: revoke the key and scrub history)"
} elseif ($inArtifact.Count -gt 0) {
    Write-Output "GRADE=P0-IN-BUILD-INPUT (git is clean but the next APK will carry the key; move config out of Assets/Resources or read it from persistentDataPath / environment variable only)"
} else {
    Write-Output "GRADE=P1-LOCAL-ONLY"
}
Write-Output "SECRET-SCAN=FAIL"
if ($Strict) { exit 1 } else { exit 0 }
