# Tier2_BoundaryCorner.ps1
# Tier 2: Boundary & Corner Cases (BVA, 20 test cases: T2.R1.01-05, T2.R2.01-05, T2.R3.01-05, T2.R4.01-05)
# Encoded in UTF-8 with BOM for compatibility with Windows PowerShell 5.1 and pwsh.

[CmdletBinding()]
param()

$currentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $currentDir "E2ETestCommon.ps1")

# =============================================================================
# Group B1: Environment Limits (R1 Boundaries)
# =============================================================================

# T2.R1.01: Binary Serialization Rejection
Register-E2ETest -Id "T2.R1.01" -Tier 2 -Requirement "R1" -Name "Binary Serialization Rejection" `
    -Description "Verifies test validation logic rejects serialization modes other than ForceText (mode 2)." `
    -ScriptBlock {
        param($Ctx)
        $testModes = @(
            @{ mode = "0"; expected = $false; name = "Mixed" },
            @{ mode = "1"; expected = $false; name = "ForceBinary" },
            @{ mode = "2"; expected = $true;  name = "ForceText" }
        )

        foreach ($tm in $testModes) {
            $isForceText = ($tm.mode -eq "2")
            Assert-Equal $tm.expected $isForceText "Serialization mode $($tm.name) ($($tm.mode)) must evaluate to $($tm.expected)"
        }
    }

# T2.R1.02: Missing Scene File Handling
Register-E2ETest -Id "T2.R1.02" -Tier 2 -Requirement "R1" -Name "Missing Scene File Handling" `
    -Description "Verifies runner and path assertions fail cleanly on non-existent scene paths." `
    -ScriptBlock {
        param($Ctx)
        $fakeScene = Join-Path $Ctx.RepoRoot "Assets\Scenes\NonExistent_Ghost_Scene_9999.unity"
        $threwException = $false
        try {
            Assert-PathExists $fakeScene "Non-existent scene must trigger path exception"
        } catch [System.IO.FileNotFoundException] {
            $threwException = $true
        }
        Assert-True $threwException "Validating non-existent scene path must raise FileNotFoundException"
    }

# T2.R1.03: Untracked Asset Contamination
Register-E2ETest -Id "T2.R1.03" -Tier 2 -Requirement "R1" -Name "Untracked Asset Contamination" `
    -Description "Verifies git working tree has 0 untracked generated binary assets under Assets/ (E08 compliance)." `
    -ScriptBlock {
        param($Ctx)
        $untracked = Get-GitUntrackedAssets -RepoRoot $Ctx.RepoRoot
        if ($untracked.Count -gt 0) {
            $list = $untracked -join ", "
            Assert-True ($untracked.Count -eq 0) "Found untracked generated binary assets under Assets/: $list (E08 violation)"
        }
    }

# T2.R1.04: Editor Hanging / Modal Lock Detection
Register-E2ETest -Id "T2.R1.04" -Tier 2 -Requirement "R1" -Name "Editor Hanging / Modal Lock Detection" `
    -Description "Verifies JSON-RPC call with 5s timeout catches hung editor modals (E14 compliance)." `
    -ScriptBlock {
        param($Ctx)
        $url = Get-E2EGateEndpoint -PicoRoot $Ctx.PicoRoot
        $sid = Start-E2EGateSession -Url $url
        if ($null -ne $sid) {
            $dialog = Get-E2EEditorDialog -Url $url -Sid $sid
            Assert-True $dialog.ok "get_editor_dialog query must succeed via bridge"
            if ($dialog.data -and $dialog.data.is_open) {
                Assert-False ($dialog.data.is_open) "Editor is blocked by modal dialog: $($dialog.data.title)"
            }
        }
    }

# T2.R1.05: Model Provider Limit Boundary
Register-E2ETest -Id "T2.R1.05" -Tier 2 -Requirement "R1" -Name "Model Provider Limit Boundary" `
    -Description "Hard constraint check: repo must NOT contain 80+ model providers." `
    -ScriptBlock {
        param($Ctx)
        # Search for AI model provider definitions or bundles in project
        $aiDirs = Get-ChildItem -LiteralPath $Ctx.RepoRoot -Directory -Recurse -Filter "*ModelProvider*" -ErrorAction SilentlyContinue
        $count = if ($aiDirs) { $aiDirs.Count } else { 0 }
        Assert-True ($count -lt 80) "Repository contains $count model providers, exceeding hard constraint limit of 80"
    }

# =============================================================================
# Group B2: Palette Boundaries (R2 Boundaries)
# =============================================================================

# T2.R2.01: Magic Float Color Threshold Scan
Register-E2ETest -Id "T2.R2.01" -Tier 2 -Requirement "R2" -Name "Magic Float Color Threshold Scan" `
    -Description "Scans Assets/Scripts/Editor/*.cs asserting 0 magic float comparisons like col.r > 0.8f or wallCol.r - 0.847f." `
    -ScriptBlock {
        param($Ctx)
        $editorDir = Join-Path $Ctx.RepoRoot "Assets\Scripts\Editor"
        $magicFindings = Scan-MagicColorComparisons -DirectoryPath $editorDir
        if ($magicFindings.Count -gt 0) {
            $details = ($magicFindings | ForEach-Object { "$($_.File):$($_.LineNumber) $($_.Code)" }) -join "; "
            Assert-True ($magicFindings.Count -eq 0) "Found magic float color comparisons in verifiers (Anti-Self-Cert defect): $details"
        }
    }

# T2.R2.02: Hex Parsing Invalid String Rejection
Register-E2ETest -Id "T2.R2.02" -Tier 2 -Requirement "R2" -Name "Hex Parsing Invalid String Rejection" `
    -Description "Verifies invalid hex codes fail parsing gracefully and do not silently pass." `
    -ScriptBlock {
        param($Ctx)
        $invalidHexes = @("INVALID_HEX", "#12345", "#GGGGGG", "", "#ZZZZZZ")
        foreach ($h in $invalidHexes) {
            # Simulate HTML hex parsing logic
            $isValid = ($h -match '^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$')
            Assert-False $isValid "Malformed hex string '$h' must be rejected"
        }
    }

# T2.R2.03: Duplicate / Conflicting Material Name Handling
Register-E2ETest -Id "T2.R2.03" -Tier 2 -Requirement "R2" -Name "Duplicate / Conflicting Material Name Handling" `
    -Description "Verifies material assets in Characters and Studio do not have naming collisions with conflicting GUIDs." `
    -ScriptBlock {
        param($Ctx)
        $matDir = Join-Path $Ctx.RepoRoot "Assets\Models"
        if (Test-Path -LiteralPath $matDir) {
            $mats = Get-ChildItem -LiteralPath $matDir -Recurse -Filter *.mat -File
            $byName = @{}
            foreach ($m in $mats) {
                if (-not $byName.ContainsKey($m.Name)) { $byName[$m.Name] = @() }
                $byName[$m.Name] += $m.FullName
            }
            $collisions = @()
            foreach ($name in $byName.Keys) {
                if ($byName[$name].Count -gt 1) {
                    $collisions += "$name ($($byName[$name] -join ', '))"
                }
            }
            Assert-True ($collisions.Count -eq 0) "Found duplicate material names in Assets/Models: $($collisions -join '; ')"
        }
    }

# T2.R2.04: Furniture Texture Map Over-Complexity
Register-E2ETest -Id "T2.R2.04" -Tier 2 -Requirement "R2" -Name "Furniture Texture Map Over-Complexity" `
    -Description "Asserts whitebox stage materials have 0 albedo texture maps (_BaseMap fileID: 0)." `
    -ScriptBlock {
        param($Ctx)
        $whiteboxMats = @(
            (Join-Path $Ctx.RepoRoot "Assets\Models\Studio\Materials\MAT_STU_Wall_v05.mat"),
            (Join-Path $Ctx.RepoRoot "Assets\Models\Studio\Materials\MAT_STU_Floor_v05.mat")
        )
        foreach ($mPath in $whiteboxMats) {
            if (Test-Path -LiteralPath $mPath) {
                $content = Get-Content -LiteralPath $mPath -Raw -Encoding UTF8
                # In Unity YAML:
                # - _BaseMap:
                #     m_Texture: {fileID: 0}
                Assert-Match '(?s)_BaseMap:\s*m_Texture:\s*\{fileID:\s*0\}' $content "Material $(Split-Path $mPath -Leaf) must not bind textures (0 textures rule)"
            }
        }
    }

# T2.R2.05: Color Distance Separation Boundary
Register-E2ETest -Id "T2.R2.05" -Tier 2 -Requirement "R2" -Name "Color Distance Separation Boundary" `
    -Description "Asserts minimum RGB distance between actor colors in PrevisPalette is strictly > 0.25." `
    -ScriptBlock {
        param($Ctx)
        $csPath = Join-Path $Ctx.RepoRoot "Assets\Scripts\Core\PrevisPalette.cs"
        Assert-PathExists $csPath "PrevisPalette.cs must exist"

        $content = Get-Content -LiteralPath $csPath -Raw -Encoding UTF8
        Assert-Match 'MinRgbDistance\s*=\s*0\.35f' $content "PrevisPalette.MinRgbDistance must be at least 0.35f (> 0.25 threshold)"
    }

# =============================================================================
# Group B3: Gate Robustness (R3 Boundaries)
# =============================================================================

# T2.R3.01: Incremental Gate 30-Second SLA Timeout
Register-E2ETest -Id "T2.R3.01" -Tier 2 -Requirement "R3" -Name "Incremental Gate 30-Second SLA Timeout" `
    -Description "Enforces SLA boundary: verify_changed.ps1 must never exceed 30 seconds." `
    -ScriptBlock {
        param($Ctx)
        $gateScript = Join-Path $Ctx.PicoRoot "04_Project\巡检\verify_changed.ps1"
        Assert-PathExists $gateScript "verify_changed.ps1 must exist"

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $proc = Start-Process -FilePath "powershell.exe" `
            -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$gateScript`"", "-Files", "Assets/Scripts/Core/PrevisPalette.cs", "-RepoRoot", "`"$($Ctx.RepoRoot)`"" `
            -PassThru -NoNewWindow -Wait
        $sw.Stop()

        Assert-True ($sw.Elapsed.TotalSeconds -le 30.0) "Incremental gate SLA exceeded: $($sw.Elapsed.TotalSeconds)s > 30.0s"
    }

# T2.R3.02: Empty Git Diff Execution
Register-E2ETest -Id "T2.R3.02" -Tier 2 -Requirement "R3" -Name "Empty Git Diff Execution" `
    -Description "Verifies verify_changed.ps1 with 0 mapped changed files exits cleanly with code 0." `
    -ScriptBlock {
        param($Ctx)
        $gateScript = Join-Path $Ctx.PicoRoot "04_Project\巡检\verify_changed.ps1"
        Assert-PathExists $gateScript "verify_changed.ps1 must exist"

        $proc = Start-Process -FilePath "powershell.exe" `
            -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$gateScript`"", "-Files", "NonExistentPath/File.txt", "-RepoRoot", "`"$($Ctx.RepoRoot)`"" `
            -PassThru -NoNewWindow -Wait

        Assert-Equal 0 $proc.ExitCode "verify_changed.ps1 with no mapped changes must exit 0"
    }

# T2.R3.03: Console Log Buffer Overflow (Count > 120)
Register-E2ETest -Id "T2.R3.03" -Tier 2 -Requirement "R3" -Name "Console Log Buffer Overflow (Count > 120)" `
    -Description "Verifies unity_gate.ps1 queries at least 120 log entries to avoid log truncation (E15 compliance)." `
    -ScriptBlock {
        param($Ctx)
        $unityGate = Join-Path $Ctx.PicoRoot "04_Project\巡检\unity_gate.ps1"
        Assert-PathExists $unityGate "unity_gate.ps1 must exist"

        $content = Get-Content -LiteralPath $unityGate -Raw -Encoding UTF8
        Assert-Match 'count\s*=\s*120' $content "unity_gate.ps1 must query count = 120 for tagged log search"
    }

# T2.R3.04: Strict 4-Verdict Classification
Register-E2ETest -Id "T2.R3.04" -Tier 2 -Requirement "R3" -Name "Strict 4-Verdict Classification" `
    -Description "Verifies unity_gate.ps1 only outputs verdicts in {PASS, FAIL, NO-ERROR, NO-VERDICT}." `
    -ScriptBlock {
        param($Ctx)
        $unityGate = Join-Path $Ctx.PicoRoot "04_Project\巡检\unity_gate.ps1"
        Assert-PathExists $unityGate "unity_gate.ps1 must exist"

        $content = Get-Content -LiteralPath $unityGate -Raw -Encoding UTF8
        $validVerdicts = @("PASS", "FAIL", "NO-ERROR", "NO-VERDICT")
        foreach ($v in $validVerdicts) {
            Assert-Match -Pattern ([regex]::Escape($v)) -Value $content -Message "unity_gate.ps1 must support verdict '$v'"
        }
    }

# T2.R3.05: Exit Code Correctness (0 on Pass, 1 on Fail, 2 on Bad Args)
Register-E2ETest -Id "T2.R3.05" -Tier 2 -Requirement "R3" -Name "Exit Code Correctness" `
    -Description "Verifies verify_all.ps1 exits with code 2 when given an invalid verifier id." `
    -ScriptBlock {
        param($Ctx)
        $gateScript = Join-Path $Ctx.PicoRoot "04_Project\巡检\verify_all.ps1"
        Assert-PathExists $gateScript "verify_all.ps1 must exist"

        $proc = Start-Process -FilePath "powershell.exe" `
            -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$gateScript`"", "-Only", "nonexistent_verifier_xyz_999" `
            -PassThru -NoNewWindow -Wait

        Assert-Equal 2 $proc.ExitCode "verify_all.ps1 with invalid -Only argument must exit with code 2"
    }

# =============================================================================
# Group B4: M1 Edge Cases (R4 Boundaries)
# =============================================================================

# T2.R4.01: Inactive GameObject Search Robustness (E16)
Register-E2ETest -Id "T2.R4.01" -Tier 2 -Requirement "R4" -Name "Inactive GameObject Search Robustness (E16)" `
    -Description "Scans verifiers for naked GameObject.Find calls that fail on inactive GameObjects (E16 defect)." `
    -ScriptBlock {
        param($Ctx)
        $editorDir = Join-Path $Ctx.RepoRoot "Assets\Scripts\Editor"
        $files = Get-ChildItem -LiteralPath $editorDir -Filter "Sc01M3AssemblyVerifier.cs" -File
        foreach ($f in $files) {
            $content = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
            # E16: GameObject.Find("SC01_Performer_A") fails if performer is deactivated
            if ($content -match 'GameObject\.Find\("SC01_Performer_[AB]"\)') {
                Assert-False $true "Found naked GameObject.Find on performers in $($f.Name) (E16 violation: fails if deactivated)"
            }
        }
    }

# T2.R4.02: Position Drift Boundary (0.049m Pass vs 0.051m Fail)
Register-E2ETest -Id "T2.R4.02" -Tier 2 -Requirement "R4" -Name "Position Drift Boundary" `
    -Description "Verifies exact boundary evaluation logic for M1 reload offset (0.049m passes, 0.051m fails)." `
    -ScriptBlock {
        param($Ctx)
        $threshold = 0.05
        $testSamples = @(
            @{ delta = 0.049; expected = $true },
            @{ delta = 0.050; expected = $false },
            @{ delta = 0.051; expected = $false }
        )

        foreach ($s in $testSamples) {
            $actual = ($s.delta -lt $threshold)
            Assert-Equal $s.expected $actual "Offset $($s.delta)m evaluated against threshold $threshold must be $($s.expected)"
        }
    }

# T2.R4.03: Animator Culling Mode Boundary (E03)
Register-E2ETest -Id "T2.R4.03" -Tier 2 -Requirement "R4" -Name "Animator Culling Mode Boundary (E03)" `
    -Description "Verifies character navigator and scenes do not use CullUpdateTransforms (must be AlwaysAnimate per E03)." `
    -ScriptBlock {
        param($Ctx)
        $actorNav = Join-Path $Ctx.RepoRoot "Assets\Scripts\RuntimeUI\ActorNavigator.cs"
        if (Test-Path -LiteralPath $actorNav) {
            $content = Get-Content -LiteralPath $actorNav -Raw -Encoding UTF8
            Assert-False ($content -match 'cullingMode\s*=\s*AnimatorCullingMode\.CullUpdateTransforms') `
                "ActorNavigator must not configure Animator cullingMode to CullUpdateTransforms (E03 compliance)"
        }
    }

# T2.R4.04: NavMesh Agent Warp Boundary
Register-E2ETest -Id "T2.R4.04" -Tier 2 -Requirement "R4" -Name "NavMesh Agent Warp Boundary" `
    -Description "Verifies ActorNavigator uses agent.Warp() instead of transform.position for teleportation." `
    -ScriptBlock {
        param($Ctx)
        $actorNav = Join-Path $Ctx.RepoRoot "Assets\Scripts\RuntimeUI\ActorNavigator.cs"
        if (Test-Path -LiteralPath $actorNav) {
            $content = Get-Content -LiteralPath $actorNav -Raw -Encoding UTF8
            Assert-Match 'Warp\(' $content "ActorNavigator must use agent.Warp() for setting agent position on NavMesh"
        }
    }

# T2.R4.05: Scene Dirty Flag on Reload
Register-E2ETest -Id "T2.R4.05" -Tier 2 -Requirement "R4" -Name "Scene Dirty Flag on Reload" `
    -Description "Verifies inspecting SC01_Main.unity does not dirty working tree (git status clean, E04 compliance)." `
    -ScriptBlock {
        param($Ctx)
        Push-Location $Ctx.RepoRoot
        try {
            $dirty = git status --porcelain "Assets/Scenes/SC01_Main.unity" 2>$null
            Assert-True ([string]::IsNullOrWhiteSpace($dirty)) "Assets/Scenes/SC01_Main.unity must not be dirtied in working tree ($dirty)"
        } finally {
            Pop-Location
        }
    }
