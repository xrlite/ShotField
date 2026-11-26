# -*- coding: utf-8 -*-
"""Tier 3: Pairwise Cross-Feature Integration Tests (15 tests: T3.01 .. T3.15 covering all C(6,2) pairs)."""

from __future__ import annotations

import json
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from e2e_common import (
    DIR_03,
    DIR_05,
    DIR_06,
    DIR_07,
    GATES_MIRROR_DIR,
    PATROL_DIR,
    REPO_ROOT,
    check_cs_line_budgets,
    check_gate_mirror_parity,
    check_missing_metas,
    check_non_ascii_paths,
    check_utf8_file,
    e2e_case,
    inspect_apk,
    inspect_editor_build_settings,
    inspect_unity_scene_or_prefab,
    require_unity_or_skip,
    run_ps1,
    run_secret_scan,
    scan_text_for_secrets,
    validate_glb,
    validate_video,
)


def _r2_glbs():
    if DIR_06:
        d = DIR_06 / "Tripo生成" / "R2_Batch"
        if d.is_dir():
            return sorted(d.glob("*.glb"))
    d2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
    return sorted(d2.glob("*.glb")) if d2.is_dir() else []


def _sc02_path():
    matches = sorted((REPO_ROOT / "Assets" / "Scenes").glob("SC02_*.unity"))
    return matches[0] if matches else None


def _edited_mp4():
    cands = []
    for base in (DIR_05, DIR_03):
        if base and base.is_dir():
            for p in base.rglob("*.mp4"):
                if "final" not in p.stem.lower():
                    cands.append(p)
    cands.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return cands[0] if cands else None


class Tier3PairwiseTests(unittest.TestCase):
    @e2e_case("T3.01", 3, "A1xA2", "F1,F2,F3,F5", "M1")
    def test_t3_01_a1xa2_secret_free_apk_and_valid_manifest(self):
        apk = REPO_ROOT / "Library" / "EmuTest" / "ShotField.apk"
        info = inspect_apk(apk)
        self.assertTrue(info["ok"], f"APK failed manifest/secret checks: {info['errors']}")
        self.assertEqual(info.get("secret_hits"), [], f"Secrets inside APK: {info.get('secret_hits')}")

    @e2e_case("T3.02", 3, "A1xA3", "F1,F2,F8,F9", "M2")
    def test_t3_02_a1xa3_r2_assets_and_scripts_repo_compliance(self):
        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        self.assertTrue(assets_r2.is_dir(), f"Missing {assets_r2}")
        self.assertEqual(check_non_ascii_paths(assets_r2), [])
        self.assertEqual(check_missing_metas(assets_r2), [])
        self.assertEqual(check_cs_line_budgets(REPO_ROOT / "Assets" / "Scripts", max_lines=200), [])

    @e2e_case("T3.03", 3, "A1xA4", "F1,F2,F10,F11", "M3")
    def test_t3_03_a1xa4_sc02_and_story_scripts_compliance_and_secret_free(self):
        sc02 = _sc02_path()
        self.assertIsNotNone(sc02, "Missing SC02_*.unity")
        self.assertTrue(all(ord(c) < 128 for c in sc02.name))
        self.assertTrue(Path(str(sc02) + ".meta").is_file())
        self.assertEqual(scan_text_for_secrets(sc02.read_text(encoding="utf-8", errors="replace")), [])
        self.assertEqual(check_cs_line_budgets(REPO_ROOT / "Assets" / "Scripts" / "RuntimeUI", max_lines=200), [])

    @e2e_case("T3.04", 3, "A1xA5", "F1,F2,F12,F13", "M4")
    def test_t3_04_a1xa5_r4_logs_secret_free_utf8_and_07_empty(self):
        self.assertIsNotNone(DIR_05)
        nov = DIR_05 / "2026-11"
        self.assertTrue(nov.is_dir())
        mds = list(nov.glob("*.md"))
        self.assertTrue(mds, "Missing R4 markdown logs in 05_验证记录/2026-11/")
        for md in mds:
            self.assertEqual(check_utf8_file(md), [], f"UTF-8 issue in {md.name}")
            self.assertEqual(scan_text_for_secrets(md.read_text(encoding="utf-8")), [], f"Secret in {md.name}")
        self.assertIsNotNone(DIR_07)
        self.assertEqual(list(DIR_07.rglob("*")), [])

    @e2e_case("T3.05", 3, "A1xA6", "F1,F2,F14", "M5")
    def test_t3_05_a1xa6_gate_mirror_entropy_and_secret_scan_synergy(self):
        parity = check_gate_mirror_parity(GATES_MIRROR_DIR, PATROL_DIR)
        self.assertTrue(parity["ok"], parity["mismatches"])
        proc_sec = run_secret_scan(["tracked", "resources"], strict=True)
        self.assertEqual(proc_sec.returncode, 0, proc_sec.stdout)
        proc_ent = run_ps1(PATROL_DIR / "entropy_check.ps1", ["-ProjectPath", str(REPO_ROOT)])
        self.assertIn("无超预算文件", proc_ent.stdout)
        self.assertIn("全部脚本都有 .meta", proc_ent.stdout)

    @e2e_case("T3.06", 3, "A2xA3", "F4,F5,F8,F9", "M2")
    def test_t3_06_a2xa3_r2_glbs_valid_alongside_xr_rig(self):
        glbs = _r2_glbs()
        self.assertTrue(3 <= len(glbs) <= 5, f"Expected 3..5 GLBs, found {len(glbs)}")
        for g in glbs:
            v = validate_glb(g)
            self.assertTrue(v["ok"], f"{g.name}: {v['errors']}")
        sc01 = inspect_unity_scene_or_prefab(REPO_ROOT / "Assets" / "Scenes" / "SC01_Main.unity")
        self.assertIn("XR_Origin", sc01["raw_text"])

    @e2e_case("T3.07", 3, "A2xA4", "F4,F5,F10", "M3")
    def test_t3_07_a2xa4_shared_xr_rig_across_sc01_and_sc02(self):
        sc01 = inspect_unity_scene_or_prefab(REPO_ROOT / "Assets" / "Scenes" / "SC01_Main.unity")
        sc02_file = _sc02_path()
        self.assertIsNotNone(sc02_file, "Missing SC02_*.unity")
        sc02 = inspect_unity_scene_or_prefab(sc02_file)
        for label, s in (("SC01_Main", sc01), ("SC02", sc02)):
            self.assertEqual(s["ui_button_conflicts"], [], f"{label} UI conflicts: {s['ui_button_conflicts']}")
            self.assertIn("XRUIInputModule", s["raw_text"], f"{label} missing XRUIInputModule")
            self.assertIn("TrackedDeviceGraphicRaycaster", s["raw_text"], f"{label} missing TrackedDeviceGraphicRaycaster")
            self.assertIn("XR_Origin", s["raw_text"], f"{label} missing XR_Origin")

    @e2e_case("T3.08", 3, "A2xA5", "F6,F13", "M4")
    def test_t3_08_a2xa5_xr_fixes_documented_in_r4_logs(self):
        self.assertIsNotNone(DIR_05)
        nov = DIR_05 / "2026-11"
        retros = [p for p in nov.rglob("*.md") if any(k in p.name.lower() for k in ("retro", "复盘", "step"))]
        self.assertTrue(retros, "Missing R4 step retrospective docs documenting XR fixes")
        joined = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in retros)
        self.assertTrue(
            any(k in joined for k in ("Emulator", "模拟器", "HandTracking", "手势")),
            "R4 retrospective logs must document Emulator/XR UI troubleshooting",
        )

    @e2e_case("T3.09", 3, "A2xA6", "F4,F5,F14", "M5", requires_unity=True)
    def test_t3_09_a2xa6_xr_wiring_gate_and_utf_xr_contract_align(self):
        unity_exe = require_unity_or_skip(self)
        with tempfile.TemporaryDirectory() as tmp:
            xml_out = Path(tmp) / "utf_xr.xml"
            log_out = Path(tmp) / "utf_xr.log"
            proc = subprocess.run(
                [
                    str(unity_exe),
                    "-batchmode",
                    "-nographics",
                    "-projectPath",
                    str(REPO_ROOT),
                    "-runTests",
                    "-testPlatform",
                    "EditMode",
                    "-testFilter",
                    "ShotField.Tests.E2E.XrUiRigContractTests",
                    "-testResults",
                    str(xml_out),
                    "-logFile",
                    str(log_out),
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=240,
            )
            self.assertEqual(proc.returncode, 0)
            root = ET.parse(xml_out).getroot()
            self.assertEqual(int(root.attrib.get("failed", "1")), 0)

    @e2e_case("T3.10", 3, "A3xA4", "F9,F10", "M3")
    def test_t3_10_a3xa4_r2_glbs_mounted_in_sc02_and_catalog(self):
        glbs = _r2_glbs()
        self.assertTrue(3 <= len(glbs) <= 5, f"Expected 3..5 R2 GLBs, found {len(glbs)}")
        sc02_file = _sc02_path()
        self.assertIsNotNone(sc02_file, "Missing SC02_*.unity")
        sc02_txt = sc02_file.read_text(encoding="utf-8", errors="replace")
        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        self.assertTrue(assets_r2.is_dir(), f"Missing {assets_r2}")
        guids = []
        for g in glbs:
            meta = assets_r2 / f"{g.name}.meta"
            self.assertTrue(meta.is_file(), f"Missing .meta for {g.name}")
            m = re.search(r"^guid:\s*([0-9a-fA-F]{32})", meta.read_text(encoding="utf-8"), re.M)
            if m:
                guids.append(m.group(1).lower())
        referenced = sum(1 for guid in guids if guid in sc02_txt.lower())
        self.assertGreaterEqual(referenced, 1, "SC02 scene must reference at least 1 imported R2 GLB GUID")

    @e2e_case("T3.11", 3, "A3xA5", "F8,F9,F13", "M4")
    def test_t3_11_a3xa5_r2_asset_metrics_recorded_in_r4_quantitative_logs(self):
        self.assertIsNotNone(DIR_05)
        nov = DIR_05 / "2026-11"
        retros = [p for p in nov.rglob("*.md") if any(k in p.name.lower() for k in ("retro", "复盘", "step", "r4_quant"))]
        self.assertTrue(retros, "Missing R4 retrospective/quantitative log referencing R2 assets")
        joined = "\n".join(p.read_text(encoding="utf-8", errors="replace") for p in retros)
        self.assertTrue(
            any(k in joined for k in ("Tripo", "GLB", "2.5D")) and any(k in joined for k in ("面", "tris", "10000", "10k", "1万")),
            "R4 quantitative logs must reference R2 Tripo/GLB metrics",
        )

    @e2e_case("T3.12", 3, "A3xA6", "F8,F9,F14", "M5")
    def test_t3_12_a3xa6_r2_glbs_and_prop_library_consistent(self):
        glbs = _r2_glbs()
        self.assertTrue(3 <= len(glbs) <= 5)
        for g in glbs:
            self.assertTrue(validate_glb(g)["ok"])
        prop_lib = (REPO_ROOT / "Assets" / "Scripts" / "Core" / "PropLibrary.cs").read_text(encoding="utf-8")
        cat_txt = (REPO_ROOT / "Assets" / "StreamingAssets" / "CommunityCatalog" / "catalog_index.json").read_text(encoding="utf-8")
        self.assertTrue(all(g.stem in (prop_lib + cat_txt) for g in glbs))

    @e2e_case("T3.13", 3, "A4xA5", "F11,F12,F13", "M4")
    def test_t3_13_a4xa5_edited_video_showcases_dual_scale_and_story(self):
        mp4 = _edited_mp4()
        self.assertIsNotNone(mp4, "Missing edited R4 MP4")
        res = validate_video(mp4, min_duration=30.0, max_duration=90.0, min_shots=2)
        self.assertTrue(res["ok"], f"Video validation failed: {res['errors']}")
        sc02_file = _sc02_path()
        self.assertIsNotNone(sc02_file)
        scenes = inspect_editor_build_settings(REPO_ROOT / "ProjectSettings" / "EditorBuildSettings.asset")
        self.assertIn(f"Assets/Scenes/{sc02_file.name}", [s["path"] for s in scenes if s["enabled"] == 1])

    @e2e_case("T3.14", 3, "A4xA6", "F10,F11,F14", "M5", requires_unity=True)
    def test_t3_14_a4xa6_dual_scale_utf_contracts_pass(self):
        unity_exe = require_unity_or_skip(self)
        with tempfile.TemporaryDirectory() as tmp:
            xml_out = Path(tmp) / "utf_ds.xml"
            log_out = Path(tmp) / "utf_ds.log"
            proc = subprocess.run(
                [
                    str(unity_exe),
                    "-batchmode",
                    "-nographics",
                    "-projectPath",
                    str(REPO_ROOT),
                    "-runTests",
                    "-testPlatform",
                    "EditMode",
                    "-testFilter",
                    "ShotField.Tests.E2E.DualScaleStoryContractTests",
                    "-testResults",
                    str(xml_out),
                    "-logFile",
                    str(log_out),
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=240,
            )
            self.assertEqual(proc.returncode, 0)
            root = ET.parse(xml_out).getroot()
            self.assertEqual(int(root.attrib.get("failed", "1")), 0)

    @e2e_case("T3.15", 3, "A5xA6", "F12,F13,F14", "M5")
    def test_t3_15_a5xa6_evidence_logs_clean_under_strict_secret_and_utf8_gates(self):
        proc = run_secret_scan(["logs"], strict=True)
        self.assertEqual(proc.returncode, 0, proc.stdout)
        self.assertIsNotNone(DIR_05)
        changelog = DIR_05 / "CHANGELOG.md"
        self.assertTrue(changelog.is_file())
        self.assertEqual(check_utf8_file(changelog), [])
