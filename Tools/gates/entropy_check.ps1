<#
.SYNOPSIS
    镜场 ShotField 熵巡检：把"代码腐烂"变成可读的数字。

.DESCRIPTION
    报五类指标：
      1. 超预算文件（>200 行）
      2. 重复方法名（跨文件同名定义，是"复制粘贴"的信号）
      3. 模块隔离（有无 asmdef，依赖方向是否能被编译期约束）
      4. 孤儿资产（.cs 缺 .meta，Unity 无法正确引用）
      5. 编译警告（从最新构建日志抓）

    只读，不改任何文件。退出码恒为 0，便于定时跑。

.EXAMPLE
    pwsh -File "<scriptPath>\entropy_check.ps1"
    pwsh -File "<scriptPath>\entropy_check.ps1" -OutFile "<evidencePath>\entropy.txt"
#>

param(
    [string]$ProjectPath = "",
    [string]$LogDir = "",
    [int]$LineBudget = 200,
    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\ShotField")).Path
}
if ([string]::IsNullOrWhiteSpace($LogDir)) {
    $workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $evidence = Get-ChildItem -LiteralPath $workspace -Directory | Where-Object { $_.Name -like "05_*" } | Select-Object -First 1
    $LogDir = if ($evidence) { Join-Path $evidence.FullName ((Get-Date -Format "yyyy-MM") + "\G0") } else { Join-Path $ProjectPath "Build" }
}
$report = [System.Collections.Generic.List[string]]::new()

function Add-Line([string]$text) {
    $report.Add($text)
}

$scriptsRoot = Join-Path $ProjectPath "Assets\Scripts"

Add-Line "=============================================="
Add-Line " 镜场 ShotField 熵巡检"
Add-Line (" 时间：" + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Add-Line (" 工程：" + $ProjectPath)
Add-Line "=============================================="
Add-Line ""

# ---------- 1. 超预算文件 ----------
Add-Line "[1] 超预算文件（单文件上限 $LineBudget 行）"
$allScripts = @()
if (Test-Path -LiteralPath $scriptsRoot) {
    $allScripts = Get-ChildItem -LiteralPath $scriptsRoot -Recurse -Filter *.cs -File
}

$oversize = @()
$totalLines = 0
foreach ($f in $allScripts) {
    $n = (Get-Content -LiteralPath $f.FullName -Encoding UTF8 | Measure-Object).Count
    $totalLines += $n
    if ($n -gt $LineBudget) {
        $rel = $f.FullName.Replace($ProjectPath + "\", "")
        $oversize += [PSCustomObject]@{ Lines = $n; Path = $rel }
    }
}

if ($oversize.Count -eq 0) {
    Add-Line ("  OK  无超预算文件（共 " + $allScripts.Count + " 个文件 / " + $totalLines + " 行）")
} else {
    foreach ($o in ($oversize | Sort-Object -Property Lines -Descending)) {
        Add-Line ("  !!  " + $o.Lines + " 行  " + $o.Path + "  (超 " + ($o.Lines - $LineBudget) + " 行)")
    }
    Add-Line ("  合计 " + $allScripts.Count + " 个文件 / " + $totalLines + " 行")
}
Add-Line ""

# ---------- 2. 重复方法名 ----------
Add-Line "[2] 重复方法定义（跨文件同名的私有/公有方法，是复制粘贴的信号）"
$methods = @{}
$methodRx = [regex]'(?m)^\s*(?:public|private|protected|internal)\s+(?:static\s+)?(?:virtual\s+)?(?:override\s+)?[A-Za-z_<>\[\]\.]+\s+([A-Za-z_][A-Za-z0-9_]*)\s*\('

foreach ($f in $allScripts) {
    $text = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
    foreach ($m in $methodRx.Matches($text)) {
        $name = $m.Groups[1].Value
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if ($name -in @("OnGUI", "OnEnable", "OnDisable", "Start", "Awake", "Update", "OnDestroy", "ToDto", "FromDto", "Clone", "Evaluate", "ToSpec")) { continue }
        if (-not $methods.ContainsKey($name)) { $methods[$name] = @() }
        $methods[$name] += $f.Name
    }
}

$dups = @()
foreach ($k in $methods.Keys) {
    $files = $methods[$k] | Sort-Object -Unique
    if ($files.Count -gt 1) { $dups += [PSCustomObject]@{ Name = $k; Files = ($files -join ", ") } }
}

if ($dups.Count -eq 0) {
    Add-Line "  OK  无跨文件同名方法"
} else {
    foreach ($d in $dups) {
        Add-Line ("  !!  " + $d.Name + "  出现在：" + $d.Files)
    }
    Add-Line ("  共 " + $dups.Count + " 组。注意：接口实现和重载会误报，人工确认后再判定")
}
Add-Line ""

# ---------- 3. 模块隔离 ----------
Add-Line "[3] 模块隔离（asmdef）"
$asmdefs = @()
if (Test-Path -LiteralPath (Join-Path $ProjectPath "Assets")) {
    $asmdefs = Get-ChildItem -LiteralPath (Join-Path $ProjectPath "Assets") -Recurse -Filter *.asmdef -File -ErrorAction SilentlyContinue
}

if ($asmdefs.Count -eq 0) {
    Add-Line "  !!  无 asmdef —— 所有脚本编进 Assembly-CSharp"
    Add-Line "     意味着：Core 可以引用 Constraints，Constraints 可以引用 Editor"
    Add-Line "     依赖方向只靠目录名约束，没有编译期拦截"
    Add-Line "     依据：ENGINEERING.md 第 1 节"
} else {
    Add-Line ("  OK  找到 " + $asmdefs.Count + " 个 asmdef：")
    foreach ($a in $asmdefs) {
        try {
            $json = Get-Content -LiteralPath $a.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            $refs = "无引用"
            if ($json.references -and $json.references.Count -gt 0) { $refs = ($json.references -join ", ") }
            $plat = "全平台"
            if ($json.includePlatforms -and $json.includePlatforms.Count -gt 0) { $plat = ($json.includePlatforms -join ", ") }
            Add-Line ("      " + $json.name + "  引用=[" + $refs + "]  平台=[" + $plat + "]")
            if ($a.DirectoryName -match "\\Editor$" -and $plat -ne "Editor") {
                Add-Line ("     !! " + $json.name + " 位于 Editor 目录但 includePlatforms 不是 Editor，Android 构建会失败")
            }
        } catch {
            Add-Line ("     !! 无法解析：" + $a.Name)
        }
    }
}
Add-Line ""

# ---------- 4. 孤儿资产 ----------
Add-Line "[4] 孤儿资产（.cs 缺 .meta）"
$noMeta = @()
foreach ($f in $allScripts) {
    $meta = $f.FullName + ".meta"
    if (-not (Test-Path -LiteralPath $meta)) {
        $noMeta += $f.FullName.Replace($ProjectPath + "\", "")
    }
}
if ($noMeta.Count -eq 0) {
    Add-Line "  OK  全部脚本都有 .meta"
} else {
    foreach ($p in $noMeta) { Add-Line ("  !!  缺 .meta：" + $p) }
    Add-Line "     处理：打开一次 Unity 让它生成，再提交"
}
Add-Line ""

# ---------- 5. 编译警告 ----------
Add-Line "[5] 最近一次构建的编译警告"
$latestLog = $null
if (Test-Path -LiteralPath $LogDir) {
    $latestLog = Get-ChildItem -LiteralPath $LogDir -Filter "build_*.log" -File -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if ($null -eq $latestLog -or $latestLog.Length -eq 0) {
    Add-Line "  --  无可读构建日志（跳过）"
} else {
    Add-Line ("     日志：" + $latestLog.Name + "  (" + [math]::Round($latestLog.Length / 1MB, 2) + " MB)")
    $warnCount = 0
    $dupAssemblies = @()
    $dupPaths = @()
    $errors = @()

    $content = Get-Content -LiteralPath $latestLog.FullName -Encoding UTF8 -ErrorAction SilentlyContinue
    foreach ($ln in $content) {
        if ($ln -match "warning CS") { $warnCount++ }
        if ($ln -match "error CS") { $errors += $ln.Trim() }
        if ($ln -match "Duplicate assembly '([^']+)'") { $dupAssemblies += $matches[1] }
        if ($ln -match "2 files found with path '([^']+)'") { $dupPaths += $matches[1] }
    }

    Add-Line ("     warning CS：" + $warnCount)
    if ($errors.Count -gt 0) {
        Add-Line ("     error CS：" + $errors.Count)
        foreach ($e in ($errors | Select-Object -First 5)) { Add-Line ("       " + $e) }
    } else {
        Add-Line "     error CS：0"
    }
    if ($dupAssemblies.Count -gt 0) {
        foreach ($d in ($dupAssemblies | Sort-Object -Unique)) {
            Add-Line ("     !! 重复程序集：" + $d + "（版本冲突，运行时可能异常）")
        }
    }
    if ($dupPaths.Count -gt 0) {
        foreach ($d in ($dupPaths | Sort-Object -Unique)) {
            Add-Line ("     !! native 库重名：" + $d + "（需在 mainTemplate.gradle 里 pickFirst）")
        }
    }
}
Add-Line ""

# ---------- 汇总 ----------
Add-Line "=============================================="
$flags = $oversize.Count + $dups.Count + $noMeta.Count
if ($asmdefs.Count -eq 0) { $flags++ }
if ($flags -eq 0) {
    Add-Line "  结论：无腐烂信号"
} else {
    Add-Line ("  结论：发现 " + $flags + " 处需要处理的信号")
}
Add-Line "=============================================="

$text = $report -join [Environment]::NewLine
Write-Output $text

if (-not [string]::IsNullOrWhiteSpace($OutFile)) {
    $dir = Split-Path -Parent $OutFile
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    [IO.File]::WriteAllText($OutFile, $text + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
    Write-Output ""
    Write-Output ("已写入：" + $OutFile)
}
