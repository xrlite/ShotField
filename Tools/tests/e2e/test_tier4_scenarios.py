# -*- coding: utf-8 -*-
"""Tier 4: Real-World Acceptance Scenarios (7 scenarios: S1 .. S7)."""

from __future__ import annotations

import json
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from e2e_common import (
    ADB_EXE,
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
    inspect_openxr_settings,
    inspect_project_settings,
    inspect_pxr_settings,
    inspect_unity_scene_or_prefab,
    require_emulator_or_skip,
    require_unity_or_skip,
    run_ps1,
    run_secret_scan,
    validate_25d_png,
    validate_glb,
    validate_video,
)


class Tier4ScenarioTests(unittest.TestCase):
    @e2e_case("S1", 4, "A2", "F3,F4,F5,F6", "M1", requires_emulator=True)
    def test_s1_emulator_apk_boot_and_dual_mode_xr_ui_click(self):
        ps = inspect_project_settings(REPO_ROOT / "ProjectSettings" / "ProjectSettings.asset")
        self.assertEqual(ps["vr_splash"], "{fileID: 0}")
        self.assertEqual(ps["android_splash"], "{fileID: 0}")

        oxr = inspect_openxr_settings(REPO_ROOT / "Assets" / "XR" / "Settings" / "OpenXRPackageSettings.asset")
        self.assertEqual(oxr.get("HandInteractionProfile Android"), 1)
        pico_cfg = REPO_ROOT / "Assets" / "Resources" / "PICOProjectSetting.asset"
        if not pico_cfg.is_file():
            pico_cfg = REPO_ROOT / "Assets" / "Resources" / "PXR_ProjectSetting.asset"
        pxr = inspect_pxr_settings(pico_cfg)
        self.assertEqual(pxr.get("handTracking"), 1, f"PICO handTracking disabled in {pico_cfg.name}: {pxr}")

        apk_info = inspect_apk(REPO_ROOT / "Library" / "EmuTest" / "ShotField.apk")
        self.assertTrue(apk_info["ok"], f"APK invalid: {apk_info['errors']}")

        serial = require_emulator_or_skip(self)
        subprocess.run([str(ADB_EXE), "-s", serial, "logcat", "-c"], check=False, timeout=10)
        subprocess.run(
            [str(ADB_EXE), "-s", serial, "shell", "am", "start", "-W", "-n", "com.haoyunlai.shotfield/com.haoyunlai.shotfield.ShotFieldUnityActivity"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            timeout=30,
            check=True,
        )
        log = subprocess.run(
            [str(ADB_EXE), "-s", serial, "logcat", "-d", "-s", "Unity:I", "AndroidRuntime:E"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=15,
        ).stdout
        self.assertNotIn("FATAL EXCEPTION", log)
        self.assertRegex(log, r"(?i)(Click|Pinch|Button|ShotField)")

    @e2e_case("S2", 4, "A3", "F7,F8,F9", "M2")
    def test_s2_ai_breakdown_25d_png_tripo_glb_and_library_pipeline(self):
        self.assertIsNotNone(DIR_06)
        r2_dir = DIR_06 / "Tripo生成" / "R2_Batch"
        self.assertTrue(r2_dir.is_dir(), f"Missing {r2_dir}")
        glbs = sorted(r2_dir.glob("*.glb"))
        self.assertTrue(3 <= len(glbs) <= 5, f"Expected 3..5 GLBs, found {len(glbs)}")
        for g in glbs:
            v = validate_glb(g, min_tris=1000, max_tris=30000)
            self.assertTrue(v["ok"], f"GLB {g.name} failed: {v['errors']}")

        pngs = sorted(r2_dir.glob("*.png"))
        if not pngs and (DIR_06 / "生成图" / "R2_Batch").is_dir():
            pngs = sorted((DIR_06 / "生成图" / "R2_Batch").glob("*.png"))
        self.assertTrue(3 <= len(pngs) <= 5, f"Expected 3..5 2.5D PNGs, found {len(pngs)}")
        for p in pngs:
            pv = validate_25d_png(p)
            self.assertTrue(pv["ok"], f"PNG {p.name} failed: {pv['errors']}")

        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        self.assertTrue(assets_r2.is_dir(), f"Missing {assets_r2}")
        for g in glbs:
            self.assertTrue((assets_r2 / g.name).is_file())
            self.assertTrue((assets_r2 / f"{g.name}.meta").is_file())

    @e2e_case("S3", 4, "A4", "F10,F11", "M3", requires_emulator=True)
    def test_s3_lightweight_scene_3_step_story_and_dual_scale_walkthrough(self):
        matches = sorted((REPO_ROOT / "Assets" / "Scenes").glob("SC02_*.unity"))
        self.assertTrue(matches, "Missing SC02_*.unity")
        sc02 = matches[0]
        build_scenes = inspect_editor_build_settings(REPO_ROOT / "ProjectSettings" / "EditorBuildSettings.asset")
        self.assertIn(f"Assets/Scenes/{sc02.name}", [s["path"] for s in build_scenes if s["enabled"] == 1])
        parsed = inspect_unity_scene_or_prefab(sc02)
        self.assertIn("XR_Origin", parsed["raw_text"])
        self.assertIn("XRUIInputModule", parsed["raw_text"])
        self.assertIn("TrackedDeviceGraphicRaycaster", parsed["raw_text"])
        self.assertEqual(parsed["ui_button_conflicts"], [])

        serial = require_emulator_or_skip(self)
        log = subprocess.run(
            [str(ADB_EXE), "-s", serial, "logcat", "-d", "-s", "Unity:I"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=15,
        ).stdout
        self.assertRegex(log, r"(TabletopMini|Immersive1to1)")
        self.assertRegex(log, r"(Step|Story)")

    @e2e_case("S4", 4, "A5", "F12,F13", "M4")
    def test_s4_edited_30_to_90s_video_and_quantitative_retrospectives(self):
        cands = []
        for base in (DIR_05, DIR_03):
            if base and base.is_dir():
                for p in base.rglob("*.mp4"):
                    if "final" not in p.stem.lower():
                        cands.append(p)
        cands.sort(key=lambda p: p.stat().st_mtime, reverse=True)
        self.assertTrue(cands, "Missing edited R4 MP4 in 05_验证记录/ or 03_制作/")
        v = validate_video(cands[0], min_duration=30.0, max_duration=90.0, min_shots=2)
        self.assertTrue(v["ok"], f"Edited video failed: {v['errors']}")

        self.assertIsNotNone(DIR_05)
        nov = DIR_05 / "2026-11"
        retros = [p for p in nov.rglob("*.md") if any(k in p.name.lower() for k in ("retro", "复盘", "step"))]
        self.assertGreaterEqual(len(retros), 4)
        self.assertTrue((DIR_05 / "CHANGELOG.md").is_file())

    @e2e_case("S5", 4, "A1", "F1,F2", "M1")
    def test_s5_r5_key_audit_secret_isolation_and_repo_compliance(self):
        self.assertIsNotNone(DIR_05)
        self.assertIsNotNone(DIR_07)
        self.assertEqual(list(DIR_07.rglob("*")), [], "07_提交包 must remain empty")
        self.assertEqual(check_cs_line_budgets(REPO_ROOT / "Assets", max_lines=200), [])
        self.assertEqual(check_non_ascii_paths(REPO_ROOT / "Assets"), [])
        self.assertEqual(check_missing_metas(REPO_ROOT / "Assets"), [])
        proc = run_secret_scan(["tracked", "resources"], strict=True)
        self.assertEqual(proc.returncode, 0, proc.stdout)

    @e2e_case("S6", 4, "A2xA3xA4", "F4,F5,F9,F10", "M3")
    def test_s6_cross_scene_xr_rig_and_r2_asset_integration(self):
        prefabs = list((REPO_ROOT / "Assets" / "Prefabs" / "XR").glob("*.prefab"))
        self.assertTrue(prefabs, "Missing shared XR Rig prefab under Assets/Prefabs/XR/")
        sc01 = inspect_unity_scene_or_prefab(REPO_ROOT / "Assets" / "Scenes" / "SC01_Main.unity")
        sc02_list = sorted((REPO_ROOT / "Assets" / "Scenes").glob("SC02_*.unity"))
        self.assertTrue(sc02_list, "Missing SC02_*.unity")
        sc02 = inspect_unity_scene_or_prefab(sc02_list[0])
        self.assertEqual(sc01["ui_button_conflicts"], [])
        self.assertEqual(sc02["ui_button_conflicts"], [])

        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        self.assertTrue(assets_r2.is_dir(), f"Missing {assets_r2}")
        glbs = sorted(assets_r2.glob("*.glb"))
        self.assertTrue(3 <= len(glbs) <= 5)
        for g in glbs:
            self.assertTrue(validate_glb(g)["ok"])

    @e2e_case("S7", 4, "A6", "F14", "M5", requires_unity=True)
    def test_s7_full_m5_release_gate_sweep(self):
        parity = check_gate_mirror_parity(GATES_MIRROR_DIR, PATROL_DIR)
        self.assertTrue(parity["ok"], parity["mismatches"])
        proc_sec = run_secret_scan(["tracked", "resources", "apks", "logs"], strict=True)
        self.assertEqual(proc_sec.returncode, 0, proc_sec.stdout)
        proc_ent = run_ps1(PATROL_DIR / "entropy_check.ps1", ["-ProjectPath", str(REPO_ROOT)])
        self.assertIn("无超预算文件", proc_ent.stdout)
        self.assertIn("全部脚本都有 .meta", proc_ent.stdout)

        unity_exe = require_unity_or_skip(self)
        with tempfile.TemporaryDirectory() as tmp:
            xml_out = Path(tmp) / "utf_s7.xml"
            log_out = Path(tmp) / "utf_s7.log"
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
                    "-assemblyNames",
                    "ShotField.Tests.E2E",
                    "-testResults",
                    str(xml_out),
                    "-logFile",
                    str(log_out),
                ],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=300,
            )
            self.assertEqual(proc.returncode, 0)
            root = ET.parse(xml_out).getroot()
            self.assertEqual(int(root.attrib.get("failed", "1")), 0)
