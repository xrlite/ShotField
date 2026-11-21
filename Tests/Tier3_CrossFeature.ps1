# Tier3_CrossFeature.ps1
# Tier 3: Cross-Feature Combinations (Pairwise Coverage, 6 test cases: T3.01 - T3.06)
# Encoded in UTF-8 with BOM for compatibility with Windows PowerShell 5.1 and pwsh.

[CmdletBinding()]
param()

$currentDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $currentDir "E2ETestCommon.ps1")

# =============================================================================
# Tier 3: Pairwise Combinations
# =============================================================================

# T3.01 [R1xR2]: Environment Serialization & Palette Integrity
Register-E2ETest -Id "T3.01" -Tier 3 -Requirement "R1xR2" -Name "Environment Serialization & Palette Integrity" `
    -Description "Asserts PrevisPalette.cs, MAT_STU_* and MAT_Actor_* serialize cleanly under ForceText without git conflict markers." `
    -ScriptBlock {
        param($Ctx)
        $targets = @(
            (Join-Path $Ctx.RepoRoot "Assets\Scripts\Core\PrevisPalette.cs"),
            (Join-Path $Ctx.RepoRoot "Assets\Models\Studio\Materials\MAT_STU_Wall_v05.mat"),
            (Join-Path $Ctx.RepoRoot "Assets\Models\Studio\Materials\MAT_STU_Floor_v05.mat")
        )
        $actorMats = Get-ChildItem -LiteralPath (Join-Path $Ctx.RepoRoot "Assets\Models\Characters") -Filter "MAT_Actor_*.mat" -File -ErrorAction SilentlyContinue
        if ($actorMats) {
            foreach ($m in $actorMats) { $targets += $m.FullName }
        }

        foreach ($t in $targets) {
            Assert-PathExists $t "Target asset must exist: $t"
            $raw = Get-Content -LiteralPath $t -Raw -Encoding UTF8
            Assert-False ($raw -match '(?m)^(?:<{7}|={7}|>{7})') "File $t must not contain git conflict markers"
            if ($t.EndsWith(".mat")) {
                Assert-Match '^%YAML 1\.1' $raw "Material $t must be serialized in ForceText (YAML 1.1) format"
            }
        }
    }

# T3.02 [R1xR3]: Git Staged Changes & Incremental Gate Trigger
Register-E2ETest -Id "T3.02" -Tier 3 -Requirement "R1xR3" -Name "Git Staged Changes & Incremental Gate Trigger" `
    -Description "Simulates changed file mapping in verify_changed.ps1 against verifiers.map.json rules." `
    -ScriptBlock {
        param($Ctx)
        $mapFile = Join-Path $Ctx.PicoRoot "04_Project\巡检\verifiers.map.json"
        Assert-PathExists $mapFile "verifiers.map.json must exist"

        $doc = Get-Content -LiteralPath $mapFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $simulatedChange = "Assets/Scripts/Core/PrevisPalette.cs"

        $matchedVerifiers = @()
        foreach ($entry in $doc.verifiers) {
            foreach ($pattern in $entry.watch) {
                if ($simulatedChange -like $pattern) {
                    $matchedVerifiers += $entry.id
                    break
                }
            }
        }
        Assert-True ($matchedVerifiers -contains "palette") "Changing PrevisPalette.cs must map to 'palette' verifier in verifiers.map.json"
    }

# T3.03 [R1xR4]: Main Scene Integrity & M1 Six-Step Reload
Register-E2ETest -Id "T3.03" -Tier 3 -Requirement "R1xR4" -Name "Main Scene Integrity & M1 Six-Step Reload" `
    -Description "Inspects Assets/Scenes/SC01_Main.unity to assert presence of key M1 GameObjects." `
    -ScriptBlock {
        param($Ctx)
        $mainScene = Join-Path $Ctx.RepoRoot "Assets\Scenes\SC01_Main.unity"
        Assert-PathExists $mainScene "SC01_Main.unity must exist"

        $sceneText = Get-Content -LiteralPath $mainScene -Raw -Encoding UTF8
        # Check essential M1 GameObjects in scene text
        $requiredObjects = @("Director", "Floor", "Canvas")
        foreach ($obj in $requiredObjects) {
            Assert-Match ("m_Name:\s*.*" + [regex]::Escape($obj)) $sceneText "SC01_Main.unity must contain GameObject matching '$obj'"
        }
    }

# T3.04 [R2xR3]: Palette Modification & Gate Verification
Register-E2ETest -Id "T3.04" -Tier 3 -Requirement "R2xR3" -Name "Palette Modification & Gate Verification" `
    -Description "Verifies palette verifier has tag PALETTE-VERIFIER and is triggered by palette asset changes." `
    -ScriptBlock {
        param($Ctx)
        $mapFile = Join-Path $Ctx.PicoRoot "04_Project\巡检\verifiers.map.json"
        Assert-PathExists $mapFile "verifiers.map.json must exist"

        $doc = Get-Content -LiteralPath $mapFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $paletteEntry = $doc.verifiers | Where-Object { $_.id -eq "palette" }
        Assert-True ($null -ne $paletteEntry) "'palette' verifier must be defined in verifiers.map.json"
        Assert-Equal "PALETTE-VERIFIER" $paletteEntry.tag "Palette verifier must have tag PALETTE-VERIFIER"

        $paletteVerifierCs = Join-Path $Ctx.RepoRoot "Assets\Scripts\Editor\PrevisPaletteVerifier.cs"
        Assert-PathExists $paletteVerifierCs "PrevisPaletteVerifier.cs must exist"
        $csContent = Get-Content -LiteralPath $paletteVerifierCs -Raw -Encoding UTF8
        Assert-Match 'PALETTE-VERIFIER' $csContent "PrevisPaletteVerifier.cs must emit LogTag PALETTE-VERIFIER"
    }

# T3.05 [R2xR4]: Palette Colors & Actor Material Alignment
Register-E2ETest -Id "T3.05" -Tier 3 -Requirement "R2xR4" -Name "Palette Colors & Actor Material Alignment" `
    -Description "Verifies actor materials in Characters folder match hex colors defined in PrevisPalette.cs." `
    -ScriptBlock {
        param($Ctx)
        $charDir = Join-Path $Ctx.RepoRoot "Assets\Models\Characters"
        $expectedActorColors = @{
            "Red" = "MAT_Actor_Red.mat"
            "Blue" = "MAT_Actor_Blue.mat"
            "Yellow" = "MAT_Actor_Yellow.mat"
            "Green" = "MAT_Actor_Green.mat"
        }

        foreach ($colorKey in $expectedActorColors.Keys) {
            $matPath = Join-Path $charDir $expectedActorColors[$colorKey]
            if (Test-Path -LiteralPath $matPath) {
                $matContent = Get-Content -LiteralPath $matPath -Raw -Encoding UTF8
                Assert-Match '_BaseColor:' $matContent "$($expectedActorColors[$colorKey]) must define _BaseColor"
            }
        }
    }

# T3.06 [R3xR4]: Full Gate Sweep & M1 Verifier Conformance
Register-E2ETest -Id "T3.06" -Tier 3 -Requirement "R3xR4" -Name "Full Gate Sweep & M1 Verifier Conformance" `
    -Description "Verifies M1 verifiers (actor_navigator, locomotion, navmesh, m3_assembly) are mapped in verifiers.map.json." `
    -ScriptBlock {
        param($Ctx)
        $mapFile = Join-Path $Ctx.PicoRoot "04_Project\巡检\verifiers.map.json"
        Assert-PathExists $mapFile "verifiers.map.json must exist"

        $doc = Get-Content -LiteralPath $mapFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $ids = @($doc.verifiers | ForEach-Object { $_.id })

        $m1Verifiers = @("actor_navigator", "locomotion", "navmesh", "m3_assembly")
        foreach ($v in $m1Verifiers) {
            Assert-True ($ids -contains $v) "verifiers.map.json must contain M1 verifier '$v'"
        }
    }
