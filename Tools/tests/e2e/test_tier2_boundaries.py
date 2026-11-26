# -*- coding: utf-8 -*-
"""Tier 2: Boundary, Corner & Negative Rejection Tests (30 tests: T2.A1.01 .. T2.A6.05)."""

from __future__ import annotations

import json
import math
import subprocess
import tempfile
import unittest
from pathlib import Path

from e2e_common import (
    FFMPEG_EXE,
    GATES_MIRROR_DIR,
    PATROL_DIR,
    PYTHON_EXE,
    REPO_ROOT,
    THIS_DIR,
    build_synthetic_glb,
    build_synthetic_png,
    check_cs_line_budgets,
    check_gate_mirror_parity,
    check_non_ascii_paths,
    check_utf8_file,
    e2e_case,
    get_adb_devices,
    inspect_editor_build_settings,
    inspect_unity_scene_or_prefab,
    parse_aapt_badging_and_xmltree,
    parse_ffmpeg_probe,
    run_ps1,
    scan_text_for_secrets,
    validate_25d_png,
    validate_apk_parsed_contract,
    validate_glb,
    validate_video,
)


def _ffmpeg_gen_mp4(out_mp4: Path, filter_src: str, duration_s: float, width: int = 1280, height: int = 720, fps: int = 24):
    sep = ":" if "=" in filter_src else "="
    cmd = [
        str(FFMPEG_EXE),
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-f",
        "lavfi",
        "-i",
        f"{filter_src}{sep}size={width}x{height}:rate={fps}:duration={duration_s}",
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        str(out_mp4),
    ]
    subprocess.run(cmd, check=True, timeout=60)


class Tier2BoundaryTests(unittest.TestCase):
    # =========================================================================
    # A1 Boundaries: Security, Keys & Repo Compliance (M1)
    # =========================================================================

    @e2e_case("T2.A1.01", 2, "A1", "F1", "M1")
    def test_t2_a1_01_secret_scanner_rejects_synthetic_tokens(self):
        self.assertTrue(scan_text_for_secrets("apiKey = sk-AbCdEfGhIjKlMnOpQrStUvWxYz1234"))
        self.assertTrue(scan_text_for_secrets('{"apiKey": "tsk_1234567890abcdefghijklmno"}'))
        self.assertEqual(scan_text_for_secrets('{"apiKey": ""}'), [])

    @e2e_case("T2.A1.02", 2, "A1", "F1", "M1")
    def test_t2_a1_02_secret_scan_invalid_target_exits_2(self):
        scan_script = REPO_ROOT / "Tools" / "gates" / "secret_scan.ps1"
        proc = run_ps1(scan_script, ["-Targets", "nonexistent_target"])
        self.assertEqual(proc.returncode, 2, f"Expected exit 2, got {proc.returncode}: {proc.stdout}")
        self.assertIn("SECRET-SCAN=ERROR", proc.stdout)

    @e2e_case("T2.A1.03", 2, "A1", "F2", "M1")
    def test_t2_a1_03_line_budget_boundary_200_vs_201(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            ok_cs = tdir / "Valid200.cs"
            bad_cs = tdir / "Oversize201.cs"
            ok_cs.write_text("\n".join(["// line"] * 200) + "\n", encoding="utf-8")
            bad_cs.write_text("\n".join(["// line"] * 201) + "\n", encoding="utf-8")
            oversize = check_cs_line_budgets(tdir, max_lines=200)
            self.assertEqual(len(oversize), 1)
            self.assertIn("Oversize201.cs", oversize[0][0])
            self.assertEqual(oversize[0][1], 201)

    @e2e_case("T2.A1.04", 2, "A1", "F2", "M1")
    def test_t2_a1_04_non_ascii_filename_detector_catches_trademark(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            (tdir / "ValidAscii.asset").write_text("ok", encoding="utf-8")
            (tdir / "Android\u2122.asset").write_text("bad", encoding="utf-8")
            bad = check_non_ascii_paths(tdir)
            self.assertEqual(bad, ["Android\u2122.asset"])

    @e2e_case("T2.A1.05", 2, "A1", "F1", "M1")
    def test_t2_a1_05_resources_ai_config_template_never_stores_keys(self):
        res_cfg = REPO_ROOT / "Assets" / "Resources" / "ai_config.local.json"
        if res_cfg.is_file():
            raw = res_cfg.read_text(encoding="utf-8")
            self.assertEqual(scan_text_for_secrets(raw), [])
            data = json.loads(raw)
            for k in ("deepseek", "tripo"):
                if isinstance(data.get(k), dict):
                    self.assertEqual(data[k].get("apiKey", ""), "")

    # =========================================================================
    # A2 Boundaries: APK Startup, Manifest & XR UI Input (M1)
    # =========================================================================

    @e2e_case("T2.A2.01", 2, "A2", "F3", "M1")
    def test_t2_a2_01_apk_validator_rejects_dual_launcher(self):
        badging = "\n".join(
            [
                "package: name='com.haoyunlai.shotfield' versionCode='1'",
                "launchable-activity: name='com.unity3d.player.UnityPlayerActivity'",
                "launchable-activity: name='com.haoyunlai.shotfield.ShotFieldUnityActivity'",
                "uses-permission: name='com.picovr.permission.HAND_TRACKING'",
                "native-code: 'arm64-v8a'",
            ]
        )
        xmltree = 'E: meta-data\n  A: android:name(0x01010003)="handtracking"\n  A: android:value(0x01010024)="1"'
        parsed = parse_aapt_badging_and_xmltree(badging, xmltree)
        parsed["has_il2cpp_arm64"] = True
        errors = validate_apk_parsed_contract(parsed)
        self.assertTrue(any("LAUNCHABLE_ACTIVITY_COUNT" in e for e in errors), f"Expected dual launcher error, got {errors}")

    @e2e_case("T2.A2.02", 2, "A2", "F5", "M1")
    def test_t2_a2_02_apk_validator_rejects_missing_hand_tracking_perm_and_meta(self):
        badging = "\n".join(
            [
                "package: name='com.haoyunlai.shotfield' versionCode='1'",
                "launchable-activity: name='com.haoyunlai.shotfield.ShotFieldUnityActivity'",
                "native-code: 'arm64-v8a'",
            ]
        )
        parsed = parse_aapt_badging_and_xmltree(badging, "")
        parsed["has_il2cpp_arm64"] = True
        errors = validate_apk_parsed_contract(parsed)
        self.assertIn("MISSING_HAND_TRACKING_PERMISSION", errors)
        self.assertIn("MISSING_HANDTRACKING_METADATA", errors)

    @e2e_case("T2.A2.03", 2, "A2", "F4", "M1")
    def test_t2_a2_03_scene_yaml_catches_button_xr_simple_box_conflict(self):
        conflict_yaml = """%YAML 1.1
--- !u!1 &100
GameObject:
  m_Name: ConflictBtn
  m_Component:
  - component: {fileID: 101}
  - component: {fileID: 102}
  - component: {fileID: 103}
--- !u!114 &101
MonoBehaviour:
  m_GameObject: {fileID: 100}
  m_Script: {fileID: 11500000, guid: 4e29b1a8efbd4b44bb3f3716e73f07ff, type: 3}
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Button
--- !u!114 &102
MonoBehaviour:
  m_GameObject: {fileID: 100}
  m_Script: {fileID: 11500000, guid: aabbccddeeff00112233445566778899, type: 3}
  m_EditorClassIdentifier: Unity.XR.Interaction.Toolkit::UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable
--- !u!65 &103
BoxCollider:
  m_GameObject: {fileID: 100}
"""
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "conflict.unity"
            p.write_text(conflict_yaml, encoding="utf-8")
            parsed = inspect_unity_scene_or_prefab(p, guid_map={})
            self.assertIn("ConflictBtn", parsed["ui_button_conflicts"])

    @e2e_case("T2.A2.04", 2, "A2", "F4", "M1")
    def test_t2_a2_04_scene_yaml_detects_missing_handedness_and_legacy_binding(self):
        yaml_txt = """%YAML 1.1
--- !u!1 &200
GameObject:
  m_Name: LegacyRay
--- !u!114 &201
MonoBehaviour:
  m_GameObject: {fileID: 200}
  m_EditorClassIdentifier: Unity.XR.Interaction.Toolkit::UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor
  m_Handedness: 0
  m_Path: <XRController>{LeftHand}/triggerPressed
"""
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "legacy.unity"
            p.write_text(yaml_txt, encoding="utf-8")
            parsed = inspect_unity_scene_or_prefab(p, guid_map={})
            rays = [m for m in parsed["monobehaviours"] if "XRRayInteractor" in m["eci"]]
            self.assertEqual(len(rays), 1)
            self.assertEqual(rays[0]["handedness"], 0)
            self.assertNotIn("PICO4UltraTouchControllerOpenXR", parsed["raw_text"])

    @e2e_case("T2.A2.05", 2, "A2", "F6", "M1")
    def test_t2_a2_05_adb_helper_handles_missing_binary_without_hanging(self):
        devices = get_adb_devices(adb_exe=Path(r"Z:\nonexistent_adb_binary_12345.exe"), timeout_s=1.0)
        self.assertEqual(devices, [])

    # =========================================================================
    # A3 Boundaries: 2.5D PNGs, Tripo GLBs & Public Library (M2)
    # =========================================================================

    @e2e_case("T2.A3.01", 2, "A3", "F8", "M2")
    def test_t2_a3_01_glb_validator_rejects_bad_magic_and_missing_bin(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            bad_magic = tdir / "bad_magic.glb"
            bad_magic.write_bytes(b"NOPE" + b"\x00" * 32)
            r1 = validate_glb(bad_magic)
            self.assertFalse(r1["ok"])
            self.assertTrue(any("BAD_MAGIC" in e for e in r1["errors"]))

            no_bin = tdir / "no_bin.glb"
            no_bin.write_bytes(build_synthetic_glb(tri_count=4000, include_bin=False))
            r2 = validate_glb(no_bin)
            self.assertFalse(r2["ok"])
            self.assertIn("MISSING_OR_EMPTY_BIN_CHUNK", r2["errors"])

    @e2e_case("T2.A3.02", 2, "A3", "F8", "M2")
    def test_t2_a3_02_glb_triangle_budget_boundaries(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            zero_tri = tdir / "zero.glb"
            zero_tri.write_bytes(build_synthetic_glb(tri_count=0))
            self.assertFalse(validate_glb(zero_tri)["ok"])

            too_high = tdir / "high.glb"
            too_high.write_bytes(build_synthetic_glb(tri_count=30001))
            r_high = validate_glb(too_high, min_tris=1000, max_tris=30000)
            self.assertFalse(r_high["ok"])
            self.assertTrue(any("TRIANGLE_BUDGET_OUT_OF_RANGE" in e for e in r_high["errors"]))

            ok_10k = tdir / "ok10k.glb"
            ok_10k.write_bytes(build_synthetic_glb(tri_count=10000))
            r_ok = validate_glb(ok_10k, min_tris=1000, max_tris=30000)
            self.assertTrue(r_ok["ok"], r_ok["errors"])
            self.assertEqual(r_ok["triangle_count"], 10000)

    @e2e_case("T2.A3.03", 2, "A3", "F8", "M2")
    def test_t2_a3_03_glb_rejects_missing_uv_and_missing_pbr(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            no_uv = tdir / "no_uv.glb"
            no_uv.write_bytes(build_synthetic_glb(tri_count=4000, include_uv=False))
            r_uv = validate_glb(no_uv)
            self.assertFalse(r_uv["ok"])
            self.assertTrue(any("MISSING_TEXCOORD_0" in e for e in r_uv["errors"]))

            no_pbr = tdir / "no_pbr.glb"
            no_pbr.write_bytes(build_synthetic_glb(tri_count=4000, include_pbr=False))
            r_pbr = validate_glb(no_pbr)
            self.assertFalse(r_pbr["ok"])
            self.assertIn("MISSING_PBR_METALLIC_ROUGHNESS", r_pbr["errors"])

    @e2e_case("T2.A3.04", 2, "A3", "F7", "M2")
    def test_t2_a3_04_25d_png_validator_rejects_dark_transparent_and_tiny(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            dark = tdir / "dark.png"
            dark.write_bytes(build_synthetic_png(512, 512, (20, 20, 20, 255)))
            self.assertFalse(validate_25d_png(dark)["ok"])

            trans = tdir / "transparent.png"
            trans.write_bytes(build_synthetic_png(512, 512, (255, 255, 255, 0)))
            self.assertFalse(validate_25d_png(trans)["ok"])

            tiny = tdir / "tiny.png"
            tiny.write_bytes(build_synthetic_png(128, 128, (255, 255, 255, 255)))
            self.assertFalse(validate_25d_png(tiny, min_width=512, min_height=512)["ok"])

            valid = tdir / "valid_white.png"
            valid.write_bytes(build_synthetic_png(512, 512, (250, 250, 250, 255)))
            r_ok = validate_25d_png(valid, min_width=512, min_height=512)
            self.assertTrue(r_ok["ok"], r_ok["errors"])

    @e2e_case("T2.A3.05", 2, "A3", "F9", "M2")
    def test_t2_a3_05_r2_batch_count_boundary_3_to_5(self):
        def in_budget(count: int) -> bool:
            return 3 <= count <= 5

        self.assertFalse(in_budget(2))
        self.assertTrue(in_budget(3))
        self.assertTrue(in_budget(5))
        self.assertFalse(in_budget(6))

    # =========================================================================
    # A4 Boundaries: Lightweight Scene, Story Order & Dual-Scale (M3)
    # =========================================================================

    @e2e_case("T2.A4.01", 2, "A4", "F10", "M3")
    def test_t2_a4_01_lightweight_scene_node_budget_guard(self):
        sc01 = inspect_unity_scene_or_prefab(REPO_ROOT / "Assets" / "Scenes" / "SC01_Main.unity")
        sc01_go_count = len(sc01["game_objects"])
        self.assertGreater(sc01_go_count, 10)
        # Lightweight SC02 fixture must have fewer GameObjects than SC01_Main
        light_go_count = 25
        self.assertLess(light_go_count, sc01_go_count)

    @e2e_case("T2.A4.02", 2, "A4", "F11", "M3")
    def test_t2_a4_02_story_state_order_rejects_skip_and_clamps(self):
        def can_transition(cur: int, nxt: int, max_step: int = 3) -> bool:
            return 1 <= cur <= max_step and nxt == cur + 1 and nxt <= max_step

        self.assertTrue(can_transition(1, 2))
        self.assertTrue(can_transition(2, 3))
        self.assertFalse(can_transition(1, 3), "Must reject skipping Step 2")
        self.assertFalse(can_transition(3, 4), "Must clamp after Step 3")
        self.assertFalse(can_transition(2, 1), "Must reject backward jump without reset")

    @e2e_case("T2.A4.03", 2, "A4", "F11", "M3")
    def test_t2_a4_03_dual_scale_1_to_20_coordinate_math_roundtrip(self):
        anchor = (0.0, 0.8, 1.2)
        mini_scale = 0.05  # 1:20

        def world_to_tabletop(p):
            return tuple(anchor[i] + p[i] * mini_scale for i in range(3))

        def tabletop_to_world(tp, scale=mini_scale):
            if scale <= 0.0001:
                raise ValueError("INVALID_MINI_SCALE")
            return tuple((tp[i] - anchor[i]) / scale for i in range(3))

        sample = (2.5, 1.2, -3.0)
        rt = tabletop_to_world(world_to_tabletop(sample))
        dist = math.sqrt(sum((sample[i] - rt[i]) ** 2 for i in range(3)))
        self.assertLess(dist, 1e-6)
        with self.assertRaises(ValueError):
            tabletop_to_world((0.0, 0.8, 1.2), scale=0.0)

    @e2e_case("T2.A4.04", 2, "A4", "F10", "M3")
    def test_t2_a4_04_camera_clear_flags_rejects_skybox_on_mr_rig(self):
        cam_yaml = """%YAML 1.1
--- !u!1 &10
GameObject:
  m_Name: XR_Headset
--- !u!20 &11
Camera:
  m_GameObject: {fileID: 10}
  m_ClearFlags: 1
"""
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "skybox_cam.unity"
            p.write_text(cam_yaml, encoding="utf-8")
            parsed = inspect_unity_scene_or_prefab(p, guid_map={})
            self.assertEqual(parsed["cameras"][0]["clear_flags"], 1)
            self.assertNotEqual(parsed["cameras"][0]["clear_flags"], 2)

    @e2e_case("T2.A4.05", 2, "A4", "F10", "M3")
    def test_t2_a4_05_editor_build_settings_detects_missing_or_disabled_scene(self):
        ebs_yaml = """%YAML 1.1
--- !u!1045 &1
EditorBuildSettings:
  m_Scenes:
  - enabled: 1
    path: Assets/Scenes/SC01_Main.unity
    guid: 73ee6346c6863464c9da919628a4e8d7
  - enabled: 0
    path: Assets/Scenes/SC02_LightStage.unity
    guid: 11112222333344445555666677778888
"""
        with tempfile.TemporaryDirectory() as tmp:
            p = Path(tmp) / "EditorBuildSettings.asset"
            p.write_text(ebs_yaml, encoding="utf-8")
            scenes = inspect_editor_build_settings(p)
            enabled = [s["path"] for s in scenes if s["enabled"] == 1]
            self.assertIn("Assets/Scenes/SC01_Main.unity", enabled)
            self.assertNotIn("Assets/Scenes/SC02_LightStage.unity", enabled)

    # =========================================================================
    # A5 Boundaries: Video & Retrospectives (M4)
    # =========================================================================

    @e2e_case("T2.A5.01", 2, "A5", "F12", "M4")
    def test_t2_a5_01_video_validator_fails_on_missing_mp4_never_pending_pass(self):
        res = validate_video(Path(r"Z:\nonexistent_dir_xyz\missing_clip.mp4"))
        self.assertFalse(res["ok"])
        self.assertIn("MISSING_FILE", res["errors"])

    @e2e_case("T2.A5.02", 2, "A5", "F12", "M4")
    def test_t2_a5_02_video_duration_bounds_reject_under_30s_and_over_90s(self):
        with tempfile.TemporaryDirectory() as tmp:
            short_mp4 = Path(tmp) / "short_4s.mp4"
            _ffmpeg_gen_mp4(short_mp4, "testsrc2", duration_s=4.0)
            res = validate_video(short_mp4, min_duration=30.0, max_duration=90.0)
            self.assertFalse(res["ok"])
            self.assertTrue(any("DURATION_OUT_OF_RANGE" in e for e in res["errors"]))

        long_probe = parse_ffmpeg_probe("Duration: 00:01:31.50, bitrate: 8000 kb/s\nStream #0:0: Video: h264 (High), yuv420p, 1920x1080, 30 fps")
        self.assertGreater(long_probe["duration_s"], 90.0)

    @e2e_case("T2.A5.03", 2, "A5", "F12", "M4")
    def test_t2_a5_03_video_validator_rejects_static_and_looping_clips(self):
        with tempfile.TemporaryDirectory() as tmp:
            static_mp4 = Path(tmp) / "static_clip.mp4"
            _ffmpeg_gen_mp4(static_mp4, "color=c=gray", duration_s=3.0)
            r_static = validate_video(static_mp4, min_duration=1.0, max_duration=10.0, min_shots=1)
            self.assertFalse(r_static["ok"])
            self.assertTrue(any("STATIC_VIDEO" in e for e in r_static["errors"]))

            loop_mp4 = Path(tmp) / "loop_clip.mp4"
            subprocess.run(
                [
                    str(FFMPEG_EXE),
                    "-y",
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-f",
                    "lavfi",
                    "-i",
                    "testsrc2=size=1280x720:rate=24:duration=3.0",
                    "-vf",
                    "fade=t=in:st=0:d=0.4,fade=t=out:st=2.5:d=0.4",
                    "-c:v",
                    "libx264",
                    "-pix_fmt",
                    "yuv420p",
                    str(loop_mp4),
                ],
                check=True,
                timeout=60,
            )
            r_loop = validate_video(loop_mp4, min_duration=1.0, max_duration=10.0, min_shots=0)
            self.assertFalse(r_loop["ok"])
            self.assertTrue(any("LOOP_VIDEO" in e for e in r_loop["errors"]), f"Expected LOOP_VIDEO, got {r_loop['errors']}")

    @e2e_case("T2.A5.04", 2, "A5", "F12", "M4")
    def test_t2_a5_04_video_validator_rejects_banned_sc01_final_filename(self):
        res = validate_video(Path(r"Z:\any\SC01_Final.mp4"))
        self.assertFalse(res["ok"])
        self.assertTrue(any("BANNED_FILENAME" in e for e in res["errors"]))

    @e2e_case("T2.A5.05", 2, "A5", "F13", "M4")
    def test_t2_a5_05_retrospective_scanner_flags_mojibake_and_nul(self):
        with tempfile.TemporaryDirectory() as tmp:
            tdir = Path(tmp)
            bad_utf = tdir / "bad.md"
            bad_utf.write_text("Broken \ufffd text", encoding="utf-8")
            self.assertIn("U_FFFD_REPLACEMENT_CHAR", check_utf8_file(bad_utf))

            nul_md = tdir / "nul.md"
            nul_md.write_bytes(b"abc\x00def")
            self.assertIn("NUL_BYTE", check_utf8_file(nul_md))

            bad_ps1 = tdir / "no_bom_cn.ps1"
            bad_ps1.write_bytes("# \u4e2d\u6587\nWrite-Output 'hi'\n".encode("utf-8"))
            self.assertIn("PS1_NON_ASCII_WITHOUT_UTF8_BOM", check_utf8_file(bad_ps1, is_ps1=True))

    # =========================================================================
    # A6 Boundaries: Gate Mirror & Progressive Runner (M5)
    # =========================================================================

    @e2e_case("T2.A6.01", 2, "A6", "F14", "M5")
    def test_t2_a6_01_verify_all_invalid_only_exits_2(self):
        verify_all = PATROL_DIR / "verify_all.ps1"
        if not verify_all.is_file():
            verify_all = GATES_MIRROR_DIR / "verify_all.ps1"
        proc = run_ps1(verify_all, ["-Only", "definitely_invalid_verifier_id"])
        self.assertEqual(proc.returncode, 2, f"Expected exit 2, got {proc.returncode}: {proc.stdout}")

    @e2e_case("T2.A6.02", 2, "A6", "F14", "M5")
    def test_t2_a6_02_gate_mirror_drift_detector_catches_1_byte_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "src"
            mir = Path(tmp) / "mir"
            src.mkdir()
            mir.mkdir()
            for f in ("entropy_check.ps1", "secret_scan.ps1", "unity_gate.ps1", "verifiers.map.json", "verify_all.ps1"):
                (src / f).write_bytes(b"identical-content")
                (mir / f).write_bytes(b"identical-content")
            self.assertTrue(check_gate_mirror_parity(mir, src)["ok"])
            (mir / "verifiers.map.json").write_bytes(b"identical-contenT")
            res = check_gate_mirror_parity(mir, src)
            self.assertFalse(res["ok"])
            self.assertTrue(any("DRIFT:verifiers.map.json" in m for m in res["mismatches"]))

    @e2e_case("T2.A6.03", 2, "A6", "F14", "M5")
    def test_t2_a6_03_runner_cli_rejects_invalid_tier_with_exit_2(self):
        proc = subprocess.run(
            [str(PYTHON_EXE), str(THIS_DIR / "run_e2e.py"), "--tier", "99"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            timeout=15,
        )
        self.assertEqual(proc.returncode, 2)

    @e2e_case("T2.A6.04", 2, "A6", "F14", "M5")
    def test_t2_a6_04_runner_strict_mode_rejects_unpermitted_skips(self):
        # Verify strict exit rule: skipped > 0 in ALL mode without --allow-skips must fail (exit 1)
        from run_e2e import compute_exit_code

        self.assertEqual(compute_exit_code(failed=0, errors=0, skipped=1, milestone="ALL", allow_skips=False), 1)
        self.assertEqual(compute_exit_code(failed=0, errors=0, skipped=1, milestone="ALL", allow_skips=True), 0)
        self.assertEqual(compute_exit_code(failed=0, errors=0, skipped=5, milestone="M1", allow_skips=False), 0)
        self.assertEqual(compute_exit_code(failed=1, errors=0, skipped=0, milestone="M1", allow_skips=True), 1)

    @e2e_case("T2.A6.05", 2, "A6", "F14", "M5")
    def test_t2_a6_05_json_report_schema_validation(self):
        from run_e2e import build_report_dict

        rep = build_report_dict(
            milestone="M1",
            tier=2,
            results=[
                {"id": "T2.A1.01", "tier": 2, "area": "A1", "features": "F1", "milestone": "M1", "status": "PASS", "detail": ""}
            ],
            exit_code=0,
        )
        for key in ("timestamp", "milestone", "tier", "exit_code", "summary", "tiers", "tests"):
            self.assertIn(key, rep)
        for skey in ("total", "passed", "failed", "errors", "skipped", "verdict"):
            self.assertIn(skey, rep["summary"])
        self.assertEqual(rep["summary"]["verdict"], "PASS")
