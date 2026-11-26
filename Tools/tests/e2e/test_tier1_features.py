# -*- coding: utf-8 -*-
"""Tier 1: Feature Coverage Tests (30 tests: T1.A1.01 .. T1.A6.05)."""

from __future__ import annotations

import json
import re
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import List

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
    scan_text_for_secrets,
    validate_25d_png,
    validate_glb,
    validate_video,
)


def _find_r2_25d_pngs() -> List[Path]:
    candidates: List[Path] = []
    if DIR_06:
        for sub in (DIR_06 / "Tripo生成" / "R2_Batch", DIR_06 / "生成图" / "R2_Batch", DIR_06 / "R2_Batch"):
            if sub.is_dir():
                candidates.extend(sorted(sub.glob("*.png")))
    if not candidates:
        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        if assets_r2.is_dir():
            candidates.extend(sorted(assets_r2.glob("*.png")))
    return candidates


def _find_r2_glbs() -> List[Path]:
    glbs: List[Path] = []
    if DIR_06:
        r2_dir = DIR_06 / "Tripo生成" / "R2_Batch"
        if r2_dir.is_dir():
            glbs = sorted(r2_dir.glob("*.glb"))
    if not glbs:
        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        if assets_r2.is_dir():
            glbs = sorted(assets_r2.glob("*.glb"))
    return glbs


def _find_sc02_scene() -> Path | None:
    scenes_dir = REPO_ROOT / "Assets" / "Scenes"
    if not scenes_dir.is_dir():
        return None
    matches = sorted(scenes_dir.glob("SC02_*.unity"))
    return matches[0] if matches else None


def _find_edited_r4_mp4() -> Path | None:
    candidates: List[Path] = []
    for base in (DIR_05, DIR_03):
        if base and base.is_dir():
            for p in base.rglob("*.mp4"):
                if "final" in p.stem.lower():
                    continue
                candidates.append(p)
    if not candidates:
        return None
    candidates.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return candidates[0]


class Tier1FeatureTests(unittest.TestCase):
    # =========================================================================
    # A1: Security, Keys & Repo Compliance (F1, F2) — M1
    # =========================================================================

    @e2e_case("T1.A1.01", 1, "A1", "F1", "M1")
    def test_a1_01_r5_key_inventory_doc_exists(self):
        self.assertIsNotNone(DIR_05, "05_* evidence directory must exist")
        nov_dir = DIR_05 / "2026-11"
        self.assertTrue(nov_dir.is_dir(), f"Missing {nov_dir}")
        key_docs = []
        for md in nov_dir.rglob("*.md"):
            name_low = md.name.lower()
            if any(k in name_low for k in ("key", "secret", "r5", "密钥", "凭据")):
                key_docs.append(md)
        self.assertTrue(key_docs, "Missing R5 key inventory document in 05_验证记录/2026-11/")
        combined = "\n".join(d.read_text(encoding="utf-8", errors="replace") for d in key_docs)
        self.assertTrue(
            any(k in combined for k in ("TRIPO", "SHOTFIELD_TRIPO_KEY", "SHOTFIELD_DEEPSEEK_KEY")),
            "R5 key inventory doc must list TRIPO/DEEPSEEK keys",
        )
        self.assertIn("persistentDataPath", combined)

    @e2e_case("T1.A1.02", 1, "A1", "F1", "M1")
    def test_a1_02_secret_scan_and_resources_clean(self):
        proc = run_secret_scan(["tracked", "resources"], strict=True)
        self.assertEqual(proc.returncode, 0, f"secret_scan failed:\n{proc.stdout}\n{proc.stderr}")
        self.assertIn("SECRET-SCAN=PASS", proc.stdout)

        res_cfg = REPO_ROOT / "Assets" / "Resources" / "ai_config.local.json"
        if res_cfg.is_file():
            data = json.loads(res_cfg.read_text(encoding="utf-8"))
            for sec in ("deepseek", "tripo"):
                if isinstance(data.get(sec), dict):
                    self.assertEqual(data[sec].get("apiKey", ""), "", f"{sec}.apiKey must be blank in Resources")

        streaming = REPO_ROOT / "Assets" / "StreamingAssets"
        if streaming.is_dir():
            for f in streaming.rglob("*"):
                if not f.is_file():
                    continue
                self.assertNotEqual(f.name.lower(), "ai_credentials.json")
                if f.suffix.lower() in (".json", ".txt", ".cfg", ".xml", ".md"):
                    hits = scan_text_for_secrets(f.read_text(encoding="utf-8", errors="replace"))
                    self.assertEqual(hits, [], f"Secret hit in StreamingAssets/{f.name}: {hits}")

    @e2e_case("T1.A1.03", 1, "A1", "F2", "M1")
    def test_a1_03_cs_line_budget_le_200(self):
        oversize = check_cs_line_budgets(REPO_ROOT / "Assets", max_lines=200)
        self.assertEqual(oversize, [], f"C# files exceed 200 lines: {oversize}")

        entropy_script = REPO_ROOT / "Tools" / "gates" / "entropy_check.ps1"
        self.assertTrue(entropy_script.is_file(), f"Missing {entropy_script}")
        proc = run_ps1(entropy_script, ["-ProjectPath", str(REPO_ROOT)])
        self.assertEqual(proc.returncode, 0, proc.stderr)
        self.assertIn("无超预算文件", proc.stdout, f"entropy_check reported oversize:\n{proc.stdout}")

    @e2e_case("T1.A1.04", 1, "A1", "F2", "M1")
    def test_a1_04_assets_ascii_filenames_and_metas(self):
        assets_dir = REPO_ROOT / "Assets"
        non_ascii = check_non_ascii_paths(assets_dir)
        self.assertEqual(non_ascii, [], f"Non-ASCII paths under Assets/: {non_ascii}")

        missing_metas = check_missing_metas(assets_dir)
        self.assertEqual(missing_metas, [], f"Missing .meta files under Assets/: {missing_metas[:20]}")

    @e2e_case("T1.A1.05", 1, "A1", "F2", "M1")
    def test_a1_05_submission_dir_07_untouched(self):
        self.assertIsNotNone(DIR_07, "07_* submission directory must exist")
        items = list(DIR_07.rglob("*"))
        self.assertEqual(items, [], f"07_提交包 must stay empty, found: {[p.name for p in items]}")

    # =========================================================================
    # A2: APK Startup, Manifest & XR Controller/Hand UI (F3, F4, F5, F6) — M1
    # =========================================================================

    @e2e_case("T1.A2.01", 1, "A2", "F3", "M1")
    def test_a2_01_splash_and_transition_overlay_clean(self):
        ps = inspect_project_settings(REPO_ROOT / "ProjectSettings" / "ProjectSettings.asset")
        self.assertEqual(ps["vr_splash"], "{fileID: 0}", f"Bad m_VirtualRealitySplashScreen: {ps['vr_splash']}")
        self.assertEqual(ps["android_splash"], "{fileID: 0}", f"Bad androidSplashScreen: {ps['android_splash']}")
        self.assertEqual(ps["android_package"], "com.haoyunlai.shotfield")

        fade_cs = (REPO_ROOT / "Assets" / "Scripts" / "RuntimeUI" / "Transitions" / "StudioFadeTransition.cs").read_text(encoding="utf-8")
        arch_cs = (REPO_ROOT / "Assets" / "Scripts" / "RuntimeUI" / "Transitions" / "ArchTunnelOverlay.cs").read_text(encoding="utf-8")
        self.assertIn("_fadeCanvas.enabled", fade_cs)
        self.assertIn("_canvas.enabled", arch_cs)

    @e2e_case("T1.A2.02", 1, "A2", "F3", "M1")
    def test_a2_02_apk_single_launcher_and_arm64_il2cpp(self):
        apk = REPO_ROOT / "Library" / "EmuTest" / "ShotField.apk"
        info = inspect_apk(apk)
        self.assertEqual(info.get("package_name"), "com.haoyunlai.shotfield")
        launchables = info.get("launchable_activities") or []
        self.assertEqual(len(launchables), 1, f"Expected single launchable-activity, got: {launchables}")
        self.assertEqual(launchables[0], "com.haoyunlai.shotfield.ShotFieldUnityActivity")
        self.assertIn("arm64-v8a", info.get("native_codes") or [])
        self.assertTrue(info.get("has_il2cpp_arm64"), "APK missing lib/arm64-v8a/libil2cpp.so")

    @e2e_case("T1.A2.03", 1, "A2", "F4", "M1")
    def test_a2_03_shared_xr_rig_and_sc01_controller_ui_bindings(self):
        prefabs = list((REPO_ROOT / "Assets" / "Prefabs" / "XR").glob("*.prefab"))
        self.assertTrue(prefabs, "Missing shared XR Rig prefab under Assets/Prefabs/XR/")

        sc01 = inspect_unity_scene_or_prefab(REPO_ROOT / "Assets" / "Scenes" / "SC01_Main.unity")
        self.assertEqual(sc01["ui_button_conflicts"], [], f"Button/XRSimpleInteractable conflicts: {sc01['ui_button_conflicts']}")
        raw = sc01["raw_text"]
        self.assertIn("XRUIInputModule", raw)
        self.assertIn("TrackedDeviceGraphicRaycaster", raw)
        self.assertTrue(
            "PICO4UltraTouchControllerOpenXR" in raw or "ShotFieldXRIActions" in raw or "HandInteraction" in raw,
            "SC01_Main must bind PICO OpenXR / shared InputActionAsset instead of bare legacy <XRController>",
        )
        ray_items = [m for m in sc01["monobehaviours"] if "XRRayInteractor" in (m.get("eci") or "")]
        self.assertTrue(ray_items, "SC01_Main missing XRRayInteractor components")
        for r in ray_items:
            self.assertIsNotNone(r.get("handedness"))
            self.assertNotEqual(r.get("handedness"), 0, "XRRayInteractor m_Handedness must be Left(1) or Right(2), not None(0)")

    @e2e_case("T1.A2.04", 1, "A2", "F5", "M1")
    def test_a2_04_hand_tracking_sdk_openxr_and_apk_manifest(self):
        manifest = json.loads((REPO_ROOT / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        deps = manifest.get("dependencies") or {}
        self.assertIn("com.unity.xr.hands", deps, "Packages/manifest.json missing com.unity.xr.hands")

        oxr = inspect_openxr_settings(REPO_ROOT / "Assets" / "XR" / "Settings" / "OpenXRPackageSettings.asset")
        self.assertEqual(oxr.get("HandInteractionProfile Android"), 1, f"OpenXR HandInteractionProfile disabled: {oxr}")

        pico_cfg = REPO_ROOT / "Assets" / "Resources" / "PICOProjectSetting.asset"
        if not pico_cfg.is_file():
            pico_cfg = REPO_ROOT / "Assets" / "Resources" / "PXR_ProjectSetting.asset"
        pxr = inspect_pxr_settings(pico_cfg)
        self.assertEqual(pxr.get("handTracking"), 1, f"PICO handTracking disabled in {pico_cfg.name}: {pxr}")

        apk_info = inspect_apk(REPO_ROOT / "Library" / "EmuTest" / "ShotField.apk")
        self.assertIn("com.picovr.permission.HAND_TRACKING", apk_info.get("permissions") or [])
        self.assertIn("handtracking", apk_info.get("meta_data") or {})

    @e2e_case("T1.A2.05", 1, "A2", "F6", "M1", requires_emulator=True)
    def test_a2_05_emulator_launch_and_dual_input_ui_logs(self):
        serial = require_emulator_or_skip(self)
        apk = REPO_ROOT / "Library" / "EmuTest" / "ShotField.apk"
        self.assertTrue(apk.is_file(), f"Missing {apk}")
        subprocess.run([str(ADB_EXE), "-s", serial, "logcat", "-c"], check=False, timeout=10)
        subprocess.run(
            [str(ADB_EXE), "-s", serial, "shell", "am", "start", "-W", "-n", "com.haoyunlai.shotfield/com.haoyunlai.shotfield.ShotFieldUnityActivity"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
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
        self.assertRegex(log, r"(?i)(ShotField|XR|UI|Click|Pinch|Button)")

    # =========================================================================
    # A3: AI Breakdown, 2.5D PNGs, Tripo GLBs & Public Library (F7, F8, F9) — M2
    # =========================================================================

    @e2e_case("T1.A3.01", 1, "A3", "F7", "M2")
    def test_a3_01_scene_breakdown_element_table_exists(self):
        found: List[Path] = []
        for base in (DIR_05, DIR_06):
            if base and base.is_dir():
                for p in base.rglob("*"):
                    if p.is_file() and p.suffix.lower() in (".json", ".md") and any(
                        k in p.name.lower() for k in ("r2_breakdown", "element_table", "元素表", "r2_elements")
                    ):
                        found.append(p)
        self.assertTrue(found, "Missing R2 scene breakdown element table (*r2_breakdown* / *元素表*)")
        content = found[0].read_text(encoding="utf-8", errors="replace")
        self.assertGreater(len(content.strip()), 100, "R2 breakdown element table is too short")

    @e2e_case("T1.A3.02", 1, "A3", "F7", "M2")
    def test_a3_02_white_bg_25d_pngs_valid(self):
        pngs = _find_r2_25d_pngs()
        self.assertTrue(3 <= len(pngs) <= 5, f"Expected 3..5 R2 2.5D PNGs, found {len(pngs)}: {[p.name for p in pngs]}")
        for png in pngs:
            res = validate_25d_png(png, min_width=512, min_height=512, white_min=220)
            self.assertTrue(res["ok"], f"2.5D PNG validation failed for {png.name}: {res['errors']}")

    @e2e_case("T1.A3.03", 1, "A3", "F8", "M2")
    def test_a3_03_tripo_glbs_valid_pbr_and_uv(self):
        glbs = _find_r2_glbs()
        self.assertTrue(3 <= len(glbs) <= 5, f"Expected 3..5 R2 GLBs, found {len(glbs)}: {[g.name for g in glbs]}")
        for glb in glbs:
            res = validate_glb(glb)
            self.assertTrue(res["ok"], f"GLB failed for {glb.name}: {res['errors']}")
            self.assertTrue(res["has_pbr"] and res["has_base_color_tex"], f"Missing PBR/baseColorTexture in {glb.name}")
            self.assertTrue(res["has_texcoord0"], f"Missing TEXCOORD_0 in {glb.name}")

    @e2e_case("T1.A3.04", 1, "A3", "F8", "M2")
    def test_a3_04_tripo_glbs_within_10k_triangle_budget(self):
        glbs = _find_r2_glbs()
        self.assertTrue(3 <= len(glbs) <= 5, f"Expected 3..5 R2 GLBs, found {len(glbs)}")
        for glb in glbs:
            res = validate_glb(glb, min_tris=1000, max_tris=30000)
            self.assertTrue(
                1000 <= res["triangle_count"] <= 30000,
                f"{glb.name} triangles={res['triangle_count']} outside ~10k range (1000..30000)",
            )

    @e2e_case("T1.A3.05", 1, "A3", "F9", "M2")
    def test_a3_05_r2_assets_archived_and_registered_in_public_library(self):
        self.assertIsNotNone(DIR_06)
        archive_dir = DIR_06 / "Tripo生成" / "R2_Batch"
        self.assertTrue(archive_dir.is_dir(), f"Missing archive dir {archive_dir}")
        archived_glbs = sorted(archive_dir.glob("*.glb"))
        self.assertTrue(3 <= len(archived_glbs) <= 5, f"Archive GLB count={len(archived_glbs)}")

        assets_r2 = REPO_ROOT / "Assets" / "Models" / "Shared" / "R2_Batch"
        self.assertTrue(assets_r2.is_dir(), f"Missing project library dir {assets_r2}")
        for g in archived_glbs:
            imported = assets_r2 / g.name
            self.assertTrue(imported.is_file(), f"Missing imported GLB in Assets: {imported}")
            self.assertTrue(Path(str(imported) + ".meta").is_file(), f"Missing .meta for {imported}")
            self.assertTrue(all(ord(c) < 128 for c in g.name), f"Non-ASCII GLB name: {g.name}")

        prop_lib = (REPO_ROOT / "Assets" / "Scripts" / "Core" / "PropLibrary.cs").read_text(encoding="utf-8")
        cat_txt = (REPO_ROOT / "Assets" / "StreamingAssets" / "CommunityCatalog" / "catalog_index.json").read_text(encoding="utf-8")
        combined = prop_lib + "\n" + cat_txt
        matched = sum(1 for g in archived_glbs if g.stem in combined)
        self.assertGreaterEqual(matched, len(archived_glbs), "Not all R2 GLBs are registered in PropLibrary or CommunityCatalog")

    # =========================================================================
    # A4: Lightweight Scene, 3-Step Story & Dual-Scale Mode (F10, F11) — M3
    # =========================================================================

    @e2e_case("T1.A4.01", 1, "A4", "F10", "M3")
    def test_a4_01_lightweight_scene_in_build_settings_and_uses_xr_rig(self):
        sc02 = _find_sc02_scene()
        self.assertIsNotNone(sc02, "Missing Assets/Scenes/SC02_*.unity")
        build_scenes = inspect_editor_build_settings(REPO_ROOT / "ProjectSettings" / "EditorBuildSettings.asset")
        rel_sc02 = f"Assets/Scenes/{sc02.name}"
        enabled_paths = [s["path"] for s in build_scenes if s["enabled"] == 1]
        self.assertIn(rel_sc02, enabled_paths, f"{rel_sc02} not enabled in EditorBuildSettings.asset")
        parsed = inspect_unity_scene_or_prefab(sc02)
        self.assertIn("XR_Origin", parsed["raw_text"])

    @e2e_case("T1.A4.02", 1, "A4", "F10", "M3")
    def test_a4_02_sc02_has_xrui_raycaster_and_r2_assets(self):
        sc02 = _find_sc02_scene()
        self.assertIsNotNone(sc02, "Missing SC02_*.unity")
        parsed = inspect_unity_scene_or_prefab(sc02)
        self.assertEqual(parsed["ui_button_conflicts"], [], f"Conflicts in SC02: {parsed['ui_button_conflicts']}")
        self.assertIn("XRUIInputModule", parsed["raw_text"])
        self.assertIn("TrackedDeviceGraphicRaycaster", parsed["raw_text"])
        glbs = _find_r2_glbs()
        self.assertTrue(glbs, "No R2 GLBs found to verify SC02 references")

    @e2e_case("T1.A4.03", 1, "A4", "F11", "M3")
    def test_a4_03_three_step_story_controller_wired(self):
        sc02 = _find_sc02_scene()
        self.assertIsNotNone(sc02, "Missing SC02_*.unity")
        story_scripts = list((REPO_ROOT / "Assets" / "Scripts" / "RuntimeUI").rglob("*Story*.cs"))
        self.assertTrue(story_scripts, "Missing 3-step story controller script (*Story*.cs)")
        for s in story_scripts:
            lines = len(s.read_text(encoding="utf-8").splitlines())
            self.assertLessEqual(lines, 200, f"{s.name} exceeds 200 lines ({lines})")

    @e2e_case("T1.A4.04", 1, "A4", "F11", "M3")
    def test_a4_04_dual_scale_and_cinematography_wired_in_sc02(self):
        sc02 = _find_sc02_scene()
        self.assertIsNotNone(sc02, "Missing SC02_*.unity")
        raw = sc02.read_text(encoding="utf-8", errors="replace")
        guid_map = {
            m.stem[:-3]: re.search(r"^guid:\s*([0-9a-fA-F]{32})", m.read_text(encoding="utf-8"), re.M).group(1).lower()
            for m in (REPO_ROOT / "Assets" / "Scripts").rglob("*.cs.meta")
            if m.stem[:-3] in ("TabletopDirector", "CameraMotionRecorder", "DirectorLocomotion")
        }
        self.assertIn("TabletopDirector", guid_map)
        self.assertIn(guid_map["TabletopDirector"], raw.lower(), "SC02 missing TabletopDirector")

    @e2e_case("T1.A4.05", 1, "A4", "F11", "M3", requires_emulator=True)
    def test_a4_05_sc02_live_story_and_dual_scale_execution(self):
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

    # =========================================================================
    # A5: 30-90s Video & Quantitative Retrospectives (F12, F13) — M4
    # =========================================================================

    @e2e_case("T1.A5.01", 1, "A5", "F12", "M4")
    def test_a5_01_edited_video_exists_and_non_banned_name(self):
        mp4 = _find_edited_r4_mp4()
        self.assertIsNotNone(mp4, "Missing edited R4 MP4 in 05_验证记录/ or 03_制作/ (never PENDING-APK)")
        self.assertNotIn("final", mp4.stem.lower(), f"Banned filename: {mp4.name}")
        self.assertGreater(mp4.stat().st_size, 1_000_000, f"Video too small: {mp4.stat().st_size} bytes")

    @e2e_case("T1.A5.02", 1, "A5", "F12", "M4")
    def test_a5_02_video_codec_resolution_fps_and_30_to_90s_duration(self):
        mp4 = _find_edited_r4_mp4()
        self.assertIsNotNone(mp4, "Missing edited R4 MP4")
        res = validate_video(mp4, min_width=1280, min_height=720, min_fps=24.0, min_duration=30.0, max_duration=90.0)
        probe = res.get("probe") or {}
        self.assertEqual(probe.get("codec"), "h264", f"Bad codec: {probe}")
        self.assertGreaterEqual(probe.get("width") or 0, 1280)
        self.assertGreaterEqual(probe.get("height") or 0, 720)
        self.assertGreaterEqual(probe.get("fps") or 0.0, 24.0)
        dur = probe.get("duration_s") or 0.0
        self.assertTrue(30.0 <= dur <= 90.0, f"Duration {dur}s outside 30..90s")

    @e2e_case("T1.A5.03", 1, "A5", "F12", "M4")
    def test_a5_03_video_motion_non_loop_and_shot_cuts(self):
        mp4 = _find_edited_r4_mp4()
        self.assertIsNotNone(mp4, "Missing edited R4 MP4")
        res = validate_video(mp4, min_shots=2)
        self.assertTrue(res["ok"], f"Video motion/cut validation failed: {res['errors']}")
        self.assertGreater(res["median_full"], 1.5)
        self.assertGreater(res["loop_diff"], 2.0)
        self.assertGreater(res["last_mean"], 1.0)
        self.assertGreaterEqual(res["cut_spikes"], 2)

    @e2e_case("T1.A5.04", 1, "A5", "F13", "M4")
    def test_a5_04_step_retrospectives_and_fix_logs_present(self):
        self.assertIsNotNone(DIR_05)
        nov_dir = DIR_05 / "2026-11"
        retros = [p for p in nov_dir.rglob("*.md") if any(k in p.name.lower() for k in ("retro", "复盘", "step"))]
        self.assertGreaterEqual(len(retros), 4, f"Expected >= 4 step retrospective docs, found {len(retros)}")
        for r in retros:
            issues = check_utf8_file(r)
            self.assertEqual(issues, [], f"UTF-8 issues in {r.name}: {issues}")

    @e2e_case("T1.A5.05", 1, "A5", "F13", "M4")
    def test_a5_05_quantitative_metrics_and_changelog_present(self):
        self.assertIsNotNone(DIR_05)
        nov_dir = DIR_05 / "2026-11"
        quant_docs = [p for p in nov_dir.rglob("*.md") if any(k in p.name.lower() for k in ("quant", "metric", "量化"))]
        self.assertTrue(quant_docs, "Missing quantitative metrics doc in 05_验证记录/2026-11/")
        changelog = DIR_05 / "CHANGELOG.md"
        self.assertTrue(changelog.is_file(), f"Missing {changelog}")
        txt = changelog.read_text(encoding="utf-8")
        self.assertRegex(txt, r"2026-11-\d{2}")

    # =========================================================================
    # A6: Gate Mirror, Text Integrity & Engine E2E Suite (F14) — M5
    # =========================================================================

    @e2e_case("T1.A6.01", 1, "A6", "F14", "M5")
    def test_a6_01_gates_mirror_sha256_parity(self):
        parity = check_gate_mirror_parity(GATES_MIRROR_DIR, PATROL_DIR)
        self.assertTrue(parity["ok"], f"Tools/gates mirror drift vs 04_Project/巡检: {parity['mismatches']}")

    @e2e_case("T1.A6.02", 1, "A6", "F14", "M5", requires_unity=True)
    def test_a6_02_verify_all_product_gates_pass(self):
        require_unity_or_skip(self)
        verify_all = PATROL_DIR / "verify_all.ps1"
        self.assertTrue(verify_all.is_file())
        proc = run_ps1(
            verify_all,
            ["-Only", "e2e_pipeline,xri_wiring,tabletop_scale,workflow_assembly"],
            timeout_s=300.0,
        )
        self.assertEqual(proc.returncode, 0, f"verify_all failed:\n{proc.stdout}\n{proc.stderr}")

    @e2e_case("T1.A6.03", 1, "A6", "F14", "M5")
    def test_a6_03_secret_scan_strict_all_targets(self):
        proc = run_secret_scan(["tracked", "resources", "apks", "logs"], strict=True, timeout_s=180.0)
        self.assertEqual(proc.returncode, 0, f"Strict secret_scan failed:\n{proc.stdout}\n{proc.stderr}")
        self.assertIn("SECRET-SCAN=PASS", proc.stdout)

    @e2e_case("T1.A6.04", 1, "A6", "F14", "M5")
    def test_a6_04_first_party_text_utf8_integrity(self):
        bad = []
        for root in (REPO_ROOT / "Assets" / "Scripts", REPO_ROOT / "Tools", REPO_ROOT / "Assets" / "Tests"):
            if not root.is_dir():
                continue
            for p in root.rglob("*"):
                if not p.is_file():
                    continue
                ext = p.suffix.lower()
                if ext in (".cs", ".py", ".ps1", ".json", ".md", ".asmdef"):
                    issues = check_utf8_file(p, is_ps1=(ext == ".ps1"))
                    if issues:
                        bad.append((str(p), issues))
        self.assertEqual(bad, [], f"Text integrity failures: {bad[:15]}")

    @e2e_case("T1.A6.05", 1, "A6", "F14", "M5", requires_unity=True)
    def test_a6_05_unity_utf_editmode_suite_passes(self):
        unity_exe = require_unity_or_skip(self)
        with tempfile.TemporaryDirectory() as tmp:
            xml_out = Path(tmp) / "utf_results.xml"
            log_out = Path(tmp) / "unity_test.log"
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
                text=True,
                timeout=300,
            )
            self.assertEqual(proc.returncode, 0, f"Unity UTF exited {proc.returncode}")
            self.assertTrue(xml_out.is_file(), "Missing NUnit XML test results")
            tree = ET.parse(xml_out)
            root = tree.getroot()
            failed = int(root.attrib.get("failed", "1"))
            passed = int(root.attrib.get("passed", "0"))
            self.assertEqual(failed, 0, f"Unity UTF failed={failed}")
            self.assertGreater(passed, 0, "No UTF tests executed")
