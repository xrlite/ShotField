# -*- coding: utf-8 -*-
"""Shared opaque-box validators and helpers for ShotField 4-Tier E2E Suite."""

from __future__ import annotations

import hashlib
import json
import os
import re
import statistics
import struct
import subprocess
import unittest
import zipfile
import zlib
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import yaml

# ---- Workspace & Tool Discovery ----
THIS_DIR = Path(__file__).resolve().parent
REPO_ROOT = THIS_DIR.parents[2]  # F:\pico\04_Project\ShotField
PROJECT_PARENT = REPO_ROOT.parent  # F:\pico\04_Project
WORKSPACE_ROOT = PROJECT_PARENT.parent  # F:\pico


def find_numbered_dir(prefix: str) -> Optional[Path]:
    if not WORKSPACE_ROOT.is_dir():
        return None
    for p in sorted(WORKSPACE_ROOT.iterdir()):
        if p.is_dir() and p.name.startswith(prefix + "_"):
            return p
    return None


DIR_03 = find_numbered_dir("03")
DIR_05 = find_numbered_dir("05")
DIR_06 = find_numbered_dir("06")
DIR_07 = find_numbered_dir("07")

PATROL_DIR = PROJECT_PARENT / "巡检"
GATES_MIRROR_DIR = REPO_ROOT / "Tools" / "gates"

PYTHON_EXE = PROJECT_PARENT / "Tools" / "venv" / "Scripts" / "python.exe"
AAPT_EXE = Path(
    r"D:\unity\6000.6.5f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\aapt.exe"
)
EMU_ADB_EXE = (
    REPO_ROOT / "Library" / "EmuTest" / "6.2" / "emulator" / "system-images" / "platform-tools" / "adb.exe"
)
SDK_ADB_EXE = Path(
    r"D:\unity\6000.6.5f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
)
ADB_EXE = EMU_ADB_EXE if EMU_ADB_EXE.is_file() else SDK_ADB_EXE
UNITY_EXE = Path(os.environ.get("UNITY_EXE", r"D:\unity\6000.6.5f1\Editor\Unity.exe"))


def resolve_ffmpeg_exe() -> Path:
    try:
        import imageio_ffmpeg

        exe = Path(imageio_ffmpeg.get_ffmpeg_exe())
        if exe.is_file():
            return exe
    except Exception:
        pass
    fallback = (
        PROJECT_PARENT
        / "Tools"
        / "venv"
        / "Lib"
        / "site-packages"
        / "imageio_ffmpeg"
        / "binaries"
        / "ffmpeg-win-x86_64-v9.0.2.exe"
    )
    return fallback


FFMPEG_EXE = resolve_ffmpeg_exe()

GATE_MIRROR_FILES = (
    "entropy_check.ps1",
    "secret_scan.ps1",
    "unity_gate.ps1",
    "verifiers.map.json",
    "verify_all.ps1",
)

SECRET_REGEXES = [
    r"sk-[A-Za-z0-9_\-]{20,}",
    r"tsk_[A-Za-z0-9_\-]{20,}",
    r"ghp_[A-Za-z0-9_\-]{20,}",
    r"github_pat_[A-Za-z0-9_\-]{20,}",
    r"xoxb-[A-Za-z0-9_\-]{20,}",
    r"AIza[0-9A-Za-z_\-]{30,}",
    r"ark-[a-f0-9]{24,}",
    r"Bearer\s+[A-Za-z0-9_\-\.]{20,}",
    r"eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}",
    r"(?i:(?<![A-Za-z])(secret|token|apikey|api_key|authorization)[ \t]*[:=][ \t]*"
    r"(?:\"[^\"\s]{16,}\"|'[^'\s]{16,}'|(?=[A-Za-z0-9_-]*\d)[A-Za-z0-9_-]{16,31}|[A-Za-z0-9_-]{32,}))",
]
SECRET_RX = re.compile("|".join(SECRET_REGEXES))

MILESTONE_ORDER = {"M1": 1, "M2": 2, "M3": 3, "M4": 4, "M5": 5, "ALL": 99}


# ---- Test Metadata & Progressive Execution Helpers ----
def e2e_case(
    case_id: str,
    tier: int,
    area: str,
    features: str,
    milestone: str = "M1",
    requires_emulator: bool = False,
    requires_unity: bool = False,
):
    def decorator(func):
        func._e2e_meta = {
            "case_id": case_id,
            "tier": tier,
            "area": area,
            "features": features,
            "milestone": milestone,
            "requires_emulator": requires_emulator,
            "requires_unity": requires_unity,
        }
        return func

    return decorator


def env_flag(name: str, default: bool = False) -> bool:
    val = os.environ.get(name)
    if val is None:
        return default
    return val.strip().lower() in ("1", "true", "yes", "on")


def get_adb_devices(adb_exe: Path = ADB_EXE, timeout_s: float = 5.0) -> List[str]:
    if not adb_exe.is_file():
        return []
    try:
        proc = subprocess.run(
            [str(adb_exe), "devices"],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_s,
        )
    except Exception:
        return []
    devices: List[str] = []
    for line in proc.stdout.splitlines():
        line = line.strip()
        if not line or line.startswith("List of devices"):
            continue
        parts = line.split()
        if len(parts) >= 2 and parts[1] == "device":
            devices.append(parts[0])
    return devices


def require_emulator_or_skip(test_case: unittest.TestCase) -> str:
    include_emu = env_flag("SHOTFIELD_E2E_INCLUDE_EMU", False)
    require_emu = env_flag("SHOTFIELD_E2E_REQUIRE_EMU", False)
    devices = get_adb_devices() if (include_emu or require_emu) else []
    if require_emu and not devices:
        test_case.fail("Emulator required (-RequireEmulator) but 0 online ADB devices found")
    if not include_emu:
        test_case.skipTest("SKIP [emulator=disabled; pass -IncludeEmulator]")
    if not devices:
        test_case.skipTest("SKIP [emulator=no_online_device]")
    return devices[0]


def require_unity_or_skip(test_case: unittest.TestCase) -> Path:
    include_unity = env_flag("SHOTFIELD_E2E_INCLUDE_UNITY", False)
    if not include_unity:
        test_case.skipTest("SKIP [unity=disabled; pass -IncludeUnity]")
    if not UNITY_EXE.is_file():
        test_case.fail(f"Unity executable missing: {UNITY_EXE}")
    lockfile = REPO_ROOT / "Temp" / "UnityLockfile"
    if lockfile.exists():
        test_case.fail(f"UnityLockfile exists ({lockfile}); close Unity Editor before -IncludeUnity")
    return UNITY_EXE


# ---- GLB Opaque-Box Validator (Pure Python struct + json) ----
def validate_glb(
    path: Path,
    min_tris: int = 1000,
    max_tris: int = 30000,
    target_min_tris: int = 2000,
    target_max_tris: int = 20000,
) -> Dict[str, Any]:
    res: Dict[str, Any] = {
        "ok": False,
        "path": str(path),
        "errors": [],
        "warnings": [],
        "triangle_count": 0,
        "mesh_count": 0,
        "has_position": False,
        "has_normal": False,
        "has_texcoord0": False,
        "has_pbr": False,
        "has_base_color_tex": False,
        "has_mr_tex": False,
        "has_normal_tex": False,
        "has_bin": False,
    }
    p = Path(path)
    if not p.is_file():
        res["errors"].append("MISSING_FILE")
        return res
    data = p.read_bytes()
    if len(data) < 20:
        res["errors"].append(f"TRUNCATED_HEADER ({len(data)} bytes)")
        return res

    magic, version, total_len = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        res["errors"].append(f"BAD_MAGIC ({magic!r})")
        return res
    if version != 2:
        res["errors"].append(f"UNSUPPORTED_VERSION ({version})")
        return res
    if total_len != len(data):
        res["errors"].append(f"LENGTH_MISMATCH (header={total_len}, actual={len(data)})")
        return res

    offset = 12
    json_obj: Optional[Dict[str, Any]] = None
    bin_bytes = b""
    while offset + 8 <= len(data):
        chunk_len, chunk_type = struct.unpack_from("<I4s", data, offset)
        offset += 8
        if offset + chunk_len > len(data):
            res["errors"].append("TRUNCATED_CHUNK")
            return res
        payload = data[offset : offset + chunk_len]
        offset += chunk_len
        if chunk_type == b"JSON" and json_obj is None:
            try:
                json_obj = json.loads(payload.decode("utf-8"))
            except Exception as ex:
                res["errors"].append(f"INVALID_JSON ({ex})")
                return res
        elif chunk_type == b"BIN\x00":
            bin_bytes = payload

    if json_obj is None:
        res["errors"].append("MISSING_JSON_CHUNK")
        return res
    if len(bin_bytes) == 0:
        res["errors"].append("MISSING_OR_EMPTY_BIN_CHUNK")
    else:
        res["has_bin"] = True

    meshes = json_obj.get("meshes") or []
    accessors = json_obj.get("accessors") or []
    materials = json_obj.get("materials") or []
    res["mesh_count"] = len(meshes)
    if not meshes:
        res["errors"].append("NO_MESHES")

    total_tris = 0
    all_pos = bool(meshes)
    all_norm = bool(meshes)
    all_uv = bool(meshes)
    for m_idx, mesh in enumerate(meshes):
        prims = mesh.get("primitives") or []
        if not prims:
            res["errors"].append(f"MESH_{m_idx}_NO_PRIMITIVES")
            all_pos = all_norm = all_uv = False
            continue
        for p_idx, prim in enumerate(prims):
            attrs = prim.get("attributes") or {}
            if "POSITION" not in attrs:
                all_pos = False
                res["errors"].append(f"MISSING_POSITION (mesh={m_idx}, prim={p_idx})")
            if "NORMAL" not in attrs:
                all_norm = False
                res["errors"].append(f"MISSING_NORMAL (mesh={m_idx}, prim={p_idx})")
            if "TEXCOORD_0" not in attrs:
                all_uv = False
                res["errors"].append(f"MISSING_TEXCOORD_0 (mesh={m_idx}, prim={p_idx})")
            idx_acc = prim.get("indices")
            if idx_acc is not None and 0 <= idx_acc < len(accessors):
                total_tris += int(accessors[idx_acc].get("count", 0)) // 3
            elif "POSITION" in attrs and 0 <= attrs["POSITION"] < len(accessors):
                total_tris += int(accessors[attrs["POSITION"]].get("count", 0)) // 3

    res["has_position"] = all_pos
    res["has_normal"] = all_norm
    res["has_texcoord0"] = all_uv
    res["triangle_count"] = total_tris

    if total_tris < min_tris or total_tris > max_tris:
        res["errors"].append(f"TRIANGLE_BUDGET_OUT_OF_RANGE ({total_tris}, expected {min_tris}..{max_tris})")
    elif total_tris < target_min_tris or total_tris > target_max_tris:
        res["warnings"].append(f"TRIANGLE_OUTSIDE_TARGET_10K ({total_tris}, target {target_min_tris}..{target_max_tris})")

    has_pbr = False
    has_base_tex = False
    has_mr_tex = False
    has_norm_tex = False
    for mat in materials:
        pbr = mat.get("pbrMetallicRoughness")
        if isinstance(pbr, dict):
            has_pbr = True
            if "baseColorTexture" in pbr:
                has_base_tex = True
            if "metallicRoughnessTexture" in pbr:
                has_mr_tex = True
        if "normalTexture" in mat:
            has_norm_tex = True

    res["has_pbr"] = has_pbr
    res["has_base_color_tex"] = has_base_tex
    res["has_mr_tex"] = has_mr_tex
    res["has_normal_tex"] = has_norm_tex
    if not has_pbr:
        res["errors"].append("MISSING_PBR_METALLIC_ROUGHNESS")
    if not has_base_tex:
        res["errors"].append("MISSING_BASE_COLOR_TEXTURE")
    if not has_norm_tex:
        res["warnings"].append("MISSING_NORMAL_TEXTURE")

    res["ok"] = len(res["errors"]) == 0
    return res


def build_synthetic_glb(
    tri_count: int = 4000,
    include_bin: bool = True,
    include_uv: bool = True,
    include_normal: bool = True,
    include_pbr: bool = True,
    include_base_tex: bool = True,
    include_normal_tex: bool = True,
) -> bytes:
    attrs: Dict[str, int] = {"POSITION": 0}
    if include_normal:
        attrs["NORMAL"] = 1
    if include_uv:
        attrs["TEXCOORD_0"] = 2

    mat: Dict[str, Any] = {"name": "Mat0"}
    if include_pbr:
        pbr: Dict[str, Any] = {"metallicFactor": 0.1, "roughnessFactor": 0.7}
        if include_base_tex:
            pbr["baseColorTexture"] = {"index": 0}
            pbr["metallicRoughnessTexture"] = {"index": 0}
        mat["pbrMetallicRoughness"] = pbr
    if include_normal_tex:
        mat["normalTexture"] = {"index": 0}

    gltf: Dict[str, Any] = {
        "asset": {"version": "2.0"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0}],
        "meshes": [{"primitives": [{"attributes": attrs, "indices": 3, "material": 0}]}],
        "materials": [mat],
        "textures": [{"source": 0}],
        "images": [{"mimeType": "image/png", "bufferView": 0}],
        "buffers": [{"byteLength": 64}],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 64}],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
            {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3"},
            {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC2"},
            {"bufferView": 0, "componentType": 5125, "count": tri_count * 3, "type": "SCALAR"},
        ],
    }
    json_raw = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    while len(json_raw) % 4 != 0:
        json_raw += b" "

    chunks = struct.pack("<I4s", len(json_raw), b"JSON") + json_raw
    if include_bin:
        bin_raw = b"\x00" * 64
        chunks += struct.pack("<I4s", len(bin_raw), b"BIN\x00") + bin_raw

    total_len = 12 + len(chunks)
    return struct.pack("<4sII", b"glTF", 2, total_len) + chunks


# ---- 2.5D PNG Validator (Pure Python PNG + zlib unfilter) ----
def _paeth(a: int, b: int, c: int) -> int:
    p = a + b - c
    pa = abs(p - a)
    pb = abs(p - b)
    pc = abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    if pb <= pc:
        return b
    return c


def _decode_png_rgba(data: bytes) -> Tuple[int, int, bytearray]:
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("BAD_PNG_SIGNATURE")
    offset = 8
    width = height = bit_depth = color_type = interlace = -1
    idat_parts: List[bytes] = []
    palette: Optional[bytes] = None
    has_iend = False

    while offset + 8 <= len(data):
        length, ctype = struct.unpack_from(">I4s", data, offset)
        offset += 8
        if offset + length + 4 > len(data):
            raise ValueError("TRUNCATED_PNG_CHUNK")
        payload = data[offset : offset + length]
        offset += length + 4  # skip CRC
        if ctype == b"IHDR":
            width, height, bit_depth, color_type, _comp, _filt, interlace = struct.unpack(
                ">IIBBBBB", payload
            )
        elif ctype == b"PLTE":
            palette = payload
        elif ctype == b"IDAT":
            idat_parts.append(payload)
        elif ctype == b"IEND":
            has_iend = True
            break

    if width <= 0 or height <= 0 or not has_iend or not idat_parts:
        raise ValueError("INVALID_PNG_STRUCTURE")
    if interlace != 0:
        raise ValueError("INTERLACED_PNG_UNSUPPORTED")
    if bit_depth not in (8, 16):
        raise ValueError(f"UNSUPPORTED_BIT_DEPTH ({bit_depth})")

    channels_map = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}
    if color_type not in channels_map:
        raise ValueError(f"UNSUPPORTED_COLOR_TYPE ({color_type})")
    ch = channels_map[color_type]
    bpp = ch * (2 if bit_depth == 16 else 1)
    stride = width * bpp
    raw = zlib.decompress(b"".join(idat_parts))
    expected = height * (1 + stride)
    if len(raw) < expected:
        raise ValueError("TRUNCATED_IDAT_STREAM")

    recon = bytearray(height * stride)
    pos = 0
    for y in range(height):
        ftype = raw[pos]
        pos += 1
        row_start = y * stride
        prev_start = (y - 1) * stride
        if ftype == 0:
            recon[row_start : row_start + stride] = raw[pos : pos + stride]
        elif ftype == 1:
            for i in range(stride):
                left = recon[row_start + i - bpp] if i >= bpp else 0
                recon[row_start + i] = (raw[pos + i] + left) & 0xFF
        elif ftype == 2:
            for i in range(stride):
                up = recon[prev_start + i] if y > 0 else 0
                recon[row_start + i] = (raw[pos + i] + up) & 0xFF
        elif ftype == 3:
            for i in range(stride):
                left = recon[row_start + i - bpp] if i >= bpp else 0
                up = recon[prev_start + i] if y > 0 else 0
                recon[row_start + i] = (raw[pos + i] + ((left + up) >> 1)) & 0xFF
        elif ftype == 4:
            for i in range(stride):
                left = recon[row_start + i - bpp] if i >= bpp else 0
                up = recon[prev_start + i] if y > 0 else 0
                ul = recon[prev_start + i - bpp] if (y > 0 and i >= bpp) else 0
                recon[row_start + i] = (raw[pos + i] + _paeth(left, up, ul)) & 0xFF
        else:
            raise ValueError(f"UNKNOWN_PNG_FILTER ({ftype})")
        pos += stride

    rgba = bytearray(width * height * 4)
    step = 2 if bit_depth == 16 else 1
    for i in range(width * height):
        src = i * bpp
        dst = i * 4
        if color_type == 6:
            rgba[dst : dst + 4] = bytes((recon[src], recon[src + step], recon[src + 2 * step], recon[src + 3 * step]))
        elif color_type == 2:
            rgba[dst : dst + 4] = bytes((recon[src], recon[src + step], recon[src + 2 * step], 255))
        elif color_type == 0:
            v = recon[src]
            rgba[dst : dst + 4] = bytes((v, v, v, 255))
        elif color_type == 4:
            v = recon[src]
            rgba[dst : dst + 4] = bytes((v, v, v, recon[src + step]))
        elif color_type == 3:
            idx = recon[src] * 3
            if palette is None or idx + 3 > len(palette):
                raise ValueError("MISSING_PALETTE_ENTRY")
            rgba[dst : dst + 4] = bytes((palette[idx], palette[idx + 1], palette[idx + 2], 255))
    return width, height, rgba


def validate_25d_png(
    path: Path,
    min_width: int = 512,
    min_height: int = 512,
    white_min: int = 220,
    alpha_min: int = 240,
) -> Dict[str, Any]:
    res: Dict[str, Any] = {
        "ok": False,
        "path": str(path),
        "width": 0,
        "height": 0,
        "errors": [],
        "corner_samples": [],
    }
    p = Path(path)
    if not p.is_file():
        res["errors"].append("MISSING_FILE")
        return res
    try:
        w, h, rgba = _decode_png_rgba(p.read_bytes())
    except Exception as ex:
        res["errors"].append(str(ex))
        return res

    res["width"] = w
    res["height"] = h
    if w < min_width or h < min_height:
        res["errors"].append(f"DIMENSIONS_TOO_SMALL ({w}x{h} < {min_width}x{min_height})")

    margin_x = min(2, max(0, w - 1))
    margin_y = min(2, max(0, h - 1))
    coords = [
        (0, 0),
        (w - 1, 0),
        (0, h - 1),
        (w - 1, h - 1),
        (margin_x, margin_y),
        (w - 1 - margin_x, margin_y),
        (margin_x, h - 1 - margin_y),
        (w - 1 - margin_x, h - 1 - margin_y),
    ]
    non_white = 0
    for x, y in coords:
        idx = (y * w + x) * 4
        r, g, b, a = rgba[idx], rgba[idx + 1], rgba[idx + 2], rgba[idx + 3]
        res["corner_samples"].append((x, y, r, g, b, a))
        if r < white_min or g < white_min or b < white_min or a < alpha_min:
            non_white += 1

    if non_white > 0:
        res["errors"].append(f"NON_WHITE_OR_TRANSPARENT_CORNERS ({non_white}/{len(coords)})")
    res["ok"] = len(res["errors"]) == 0
    return res


def build_synthetic_png(width: int, height: int, bg_rgba: Tuple[int, int, int, int] = (255, 255, 255, 255)) -> bytes:
    def chunk(ctype: bytes, payload: bytes) -> bytes:
        crc = zlib.crc32(ctype + payload) & 0xFFFFFFFF
        return struct.pack(">I", len(payload)) + ctype + payload + struct.pack(">I", crc)

    row = b"\x00" + (bytes(bg_rgba) * width)
    raw = row * height
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, level=1))
        + chunk(b"IEND", b"")
    )


# ---- APK & AndroidManifest Validator ----
def parse_aapt_badging_and_xmltree(badging_text: str, xmltree_text: str) -> Dict[str, Any]:
    pkg_m = re.search(r"package:\s*name='([^']+)'", badging_text)
    launchables = re.findall(r"launchable-activity:\s*name='([^']+)'", badging_text)
    perms = re.findall(r"uses-permission:\s*name='([^']+)'", badging_text)
    native_codes = re.findall(r"'([^']+)'", "\n".join(re.findall(r"native-code:.*", badging_text)))

    meta_data: Dict[str, str] = {}
    lines = xmltree_text.splitlines()
    for i, line in enumerate(lines):
        if "E: meta-data" in line:
            name_val = None
            raw_val = None
            for sub in lines[i + 1 : i + 8]:
                if re.match(r"^\s*E:\s+", sub):
                    break
                nm = re.search(r'A:\s*android:name\([^)]+\)="([^"]+)"', sub)
                if nm:
                    name_val = nm.group(1)
                vm = re.search(r'A:\s*android:value\([^)]+\)=(.+)$', sub)
                if vm:
                    raw_val = vm.group(1).strip()
            if name_val:
                meta_data[name_val] = raw_val or ""

    return {
        "package_name": pkg_m.group(1) if pkg_m else "",
        "launchable_activities": launchables,
        "permissions": perms,
        "native_codes": native_codes,
        "meta_data": meta_data,
    }


def validate_apk_parsed_contract(info: Dict[str, Any], require_il2cpp: bool = True) -> List[str]:
    errors: List[str] = []
    if info.get("package_name") != "com.haoyunlai.shotfield":
        errors.append(f"BAD_PACKAGE ({info.get('package_name')!r})")
    launchables = info.get("launchable_activities") or []
    if len(launchables) != 1:
        errors.append(f"LAUNCHABLE_ACTIVITY_COUNT ({len(launchables)}: {launchables})")
    elif launchables[0] != "com.haoyunlai.shotfield.ShotFieldUnityActivity":
        errors.append(f"UNEXPECTED_LAUNCHER ({launchables[0]})")

    if "arm64-v8a" not in (info.get("native_codes") or []):
        errors.append("MISSING_ARM64_V8A")
    if require_il2cpp and not info.get("has_il2cpp_arm64"):
        errors.append("MISSING_LIBIL2CPP_SO")
    if "com.picovr.permission.HAND_TRACKING" not in (info.get("permissions") or []):
        errors.append("MISSING_HAND_TRACKING_PERMISSION")
    meta = info.get("meta_data") or {}
    if "handtracking" not in meta:
        errors.append("MISSING_HANDTRACKING_METADATA")
    if info.get("secret_hits"):
        errors.append(f"APK_SECRET_HITS ({len(info['secret_hits'])})")
    return errors


def inspect_apk(apk_path: Path, aapt_exe: Path = AAPT_EXE) -> Dict[str, Any]:
    p = Path(apk_path)
    res: Dict[str, Any] = {
        "ok": False,
        "path": str(p),
        "errors": [],
        "has_il2cpp_arm64": False,
        "secret_hits": [],
    }
    if not p.is_file():
        res["errors"].append("MISSING_APK")
        return res
    if not aapt_exe.is_file():
        res["errors"].append(f"MISSING_AAPT ({aapt_exe})")
        return res

    try:
        with zipfile.ZipFile(p, "r") as zf:
            names = zf.namelist()
            res["has_il2cpp_arm64"] = "lib/arm64-v8a/libil2cpp.so" in names
            for entry in names:
                low = entry.lower()
                if low.endswith("ai_credentials.json"):
                    res["secret_hits"].append((entry, "forbidden-credential-file"))
                    continue
                if (
                    (low.startswith("assets/") or low.startswith("res/raw/"))
                    and low.endswith((".json", ".xml", ".txt", ".cfg", ".ini", ".properties"))
                ):
                    raw = zf.read(entry)
                    if len(raw) <= 5_000_000:
                        txt = raw.decode("utf-8", errors="replace")
                        hits = scan_text_for_secrets(txt)
                        for h in hits:
                            res["secret_hits"].append((entry, h))
    except Exception as ex:
        res["errors"].append(f"ZIP_ERROR ({ex})")
        return res

    badg = subprocess.run(
        [str(aapt_exe), "dump", "badging", str(p)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if badg.returncode != 0:
        res["errors"].append(f"AAPT_BADGING_FAILED ({badg.stderr.strip()[:200]})")
        return res

    xmlt = subprocess.run(
        [str(aapt_exe), "dump", "xmltree", str(p), "AndroidManifest.xml"],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if xmlt.returncode != 0:
        res["errors"].append(f"AAPT_XMLTREE_FAILED ({xmlt.stderr.strip()[:200]})")
        return res

    parsed = parse_aapt_badging_and_xmltree(badg.stdout, xmlt.stdout)
    res.update(parsed)
    res["errors"].extend(validate_apk_parsed_contract(res, require_il2cpp=True))
    res["ok"] = len(res["errors"]) == 0
    return res


# ---- FFmpeg Video Validator ----
def parse_ffmpeg_probe(stderr_text: str) -> Dict[str, Any]:
    info: Dict[str, Any] = {
        "duration_s": None,
        "codec": None,
        "width": None,
        "height": None,
        "fps": None,
        "bitrate_kbps": None,
    }
    dm = re.search(r"Duration:\s*(\d+):(\d+):([\d.]+)", stderr_text)
    if dm:
        h, mi, s = int(dm.group(1)), int(dm.group(2)), float(dm.group(3))
        info["duration_s"] = h * 3600 + mi * 60 + s
    vm = re.search(r"Video:\s*(\w+).*?,\s*(\d+)x(\d+).*?,\s*(\d+(?:\.\d+)?)\s*fps", stderr_text)
    if vm:
        info["codec"] = vm.group(1).lower()
        info["width"] = int(vm.group(2))
        info["height"] = int(vm.group(3))
        info["fps"] = float(vm.group(4))
    bm = re.search(r"bitrate:\s*(\d+(?:\.\d+)?)\s*kb/s", stderr_text, re.IGNORECASE)
    if bm:
        info["bitrate_kbps"] = float(bm.group(1))
    return info


def _mean_abs_diff(a: bytes, b: bytes) -> float:
    # Fast pixel difference over 160x90x3 frame bytes
    return sum(abs(x - y) for x, y in zip(a, b)) / len(a)


def validate_video(
    mp4_path: Path,
    ffmpeg_exe: Path = FFMPEG_EXE,
    min_width: int = 1280,
    min_height: int = 720,
    min_fps: float = 24.0,
    min_duration: float = 30.0,
    max_duration: float = 90.0,
    min_shots: int = 2,
    reject_banned_names: bool = True,
) -> Dict[str, Any]:
    p = Path(mp4_path)
    res: Dict[str, Any] = {
        "ok": False,
        "path": str(p),
        "errors": [],
        "probe": {},
        "frame_count": 0,
        "median_full": 0.0,
        "loop_diff": 0.0,
        "last_mean": 0.0,
        "cut_spikes": 0,
    }
    if reject_banned_names and "final" in p.stem.lower():
        res["errors"].append(f"BANNED_FILENAME ({p.name})")
        return res
    if not p.is_file():
        res["errors"].append("MISSING_FILE")
        return res
    if not ffmpeg_exe.is_file():
        res["errors"].append(f"MISSING_FFMPEG ({ffmpeg_exe})")
        return res

    probe_proc = subprocess.run(
        [str(ffmpeg_exe), "-hide_banner", "-i", str(p)],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    info = parse_ffmpeg_probe(probe_proc.stderr)
    res["probe"] = info

    if info["codec"] != "h264":
        res["errors"].append(f"BAD_CODEC ({info['codec']})")
    if not info["width"] or not info["height"] or info["width"] < min_width or info["height"] < min_height:
        res["errors"].append(f"LOW_RESOLUTION ({info['width']}x{info['height']})")
    if not info["fps"] or info["fps"] < min_fps - 0.05:
        res["errors"].append(f"LOW_FPS ({info['fps']})")
    dur = info["duration_s"]
    if dur is None or dur < min_duration or dur > max_duration:
        res["errors"].append(f"DURATION_OUT_OF_RANGE ({dur}s, expected {min_duration}..{max_duration})")

    frame_w, frame_h = 160, 90
    frame_size = frame_w * frame_h * 3
    dec = subprocess.run(
        [
            str(ffmpeg_exe),
            "-hide_banner",
            "-i",
            str(p),
            "-vf",
            f"scale={frame_w}:{frame_h}",
            "-map",
            "0:v:0",
            "-f",
            "rawvideo",
            "-pix_fmt",
            "rgb24",
            "-",
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    if dec.returncode != 0 or len(dec.stdout) < frame_size * 2:
        res["errors"].append("DECODE_FAILED_OR_TOO_FEW_FRAMES")
        return res

    raw = dec.stdout
    count = len(raw) // frame_size
    res["frame_count"] = count
    frames = [raw[i * frame_size : (i + 1) * frame_size] for i in range(count)]

    diffs = [_mean_abs_diff(frames[i], frames[i + 1]) for i in range(count - 1)]
    median_full = statistics.median(diffs)
    loop_diff = _mean_abs_diff(frames[0], frames[-1])
    third = max(1, len(diffs) // 3)
    last_mean = statistics.mean(diffs[-third:])
    spikes = [d for d in diffs if d > max(8.0, 2.5 * median_full)]

    res["median_full"] = round(median_full, 4)
    res["loop_diff"] = round(loop_diff, 4)
    res["last_mean"] = round(last_mean, 4)
    res["cut_spikes"] = len(spikes)

    if median_full <= 1.5:
        res["errors"].append(f"STATIC_VIDEO (median_full={median_full:.2f} <= 1.5)")
    if loop_diff <= 2.0:
        res["errors"].append(f"LOOP_VIDEO (loop_diff={loop_diff:.2f} <= 2.0)")
    if last_mean <= 1.0:
        res["errors"].append(f"FROZEN_LAST_THIRD (last_mean={last_mean:.2f} <= 1.0)")
    if len(spikes) < min_shots:
        res["errors"].append(f"INSUFFICIENT_SHOT_CUTS ({len(spikes)} < {min_shots})")

    res["ok"] = len(res["errors"]) == 0
    return res


# ---- Unity YAML / Scene / Settings Scanners ----
_UNITY_DOC_HDR_RX = re.compile(r"^--- !u!(\d+) &(-?\d+)\s*$", re.MULTILINE)


def split_unity_yaml_docs(text: str) -> List[Tuple[int, str, str]]:
    matches = list(_UNITY_DOC_HDR_RX.finditer(text))
    docs: List[Tuple[int, str, str]] = []
    for i, m in enumerate(matches):
        cls_id = int(m.group(1))
        file_id = m.group(2)
        start = m.end()
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        body = text[start:end].strip()
        docs.append((cls_id, file_id, body))
    return docs


def inspect_project_settings(path: Path) -> Dict[str, Any]:
    txt = Path(path).read_text(encoding="utf-8", errors="replace")
    vr_splash = re.search(r"^\s*m_VirtualRealitySplashScreen:\s*(.+)$", txt, re.MULTILINE)
    and_splash = re.search(r"^\s*androidSplashScreen:\s*(.+)$", txt, re.MULTILINE)
    and_id = re.search(r"^\s*Android:\s*(\S+)", txt, re.MULTILINE)
    min_sdk = re.search(r"^\s*AndroidMinSdkVersion:\s*(\d+)", txt, re.MULTILINE)
    return {
        "vr_splash": vr_splash.group(1).strip() if vr_splash else "",
        "android_splash": and_splash.group(1).strip() if and_splash else "{fileID: 0}",
        "android_package": and_id.group(1).strip() if and_id else "",
        "android_min_sdk": int(min_sdk.group(1)) if min_sdk else 0,
    }


def inspect_openxr_settings(path: Path) -> Dict[str, int]:
    txt = Path(path).read_text(encoding="utf-8", errors="replace")
    features: Dict[str, int] = {}
    for cls_id, _fid, body in split_unity_yaml_docs(txt):
        if cls_id != 114:
            continue
        nm = re.search(r"^\s*m_Name:\s*(.+)$", body, re.MULTILINE)
        en = re.search(r"^\s*m_enabled:\s*(\d+)", body, re.MULTILINE)
        if nm and en:
            features[nm.group(1).strip()] = int(en.group(1))
    return features


def inspect_pxr_settings(path: Path) -> Dict[str, int]:
    txt = Path(path).read_text(encoding="utf-8", errors="replace")
    ht = re.search(r"^\s*(?:isHandTracking|handTracking):\s*(\d+)", txt, re.MULTILINE)
    ht_type = re.search(r"^\s*handTrackingSupportType:\s*(\d+)", txt, re.MULTILINE)
    return {
        "handTracking": int(ht.group(1)) if ht else 0,
        "handTrackingSupportType": int(ht_type.group(1)) if ht_type else 0,
    }


def inspect_editor_build_settings(path: Path) -> List[Dict[str, Any]]:
    txt = Path(path).read_text(encoding="utf-8", errors="replace")
    scenes: List[Dict[str, Any]] = []
    in_scenes = False
    cur: Optional[Dict[str, Any]] = None
    for line in txt.splitlines():
        if re.match(r"^\s*m_Scenes:\s*$", line):
            in_scenes = True
            continue
        if in_scenes and re.match(r"^\s*[a-zA-Z_][a-zA-Z0-9_]*:\s*", line) and not line.strip().startswith("-"):
            if not any(line.strip().startswith(k) for k in ("enabled:", "path:", "guid:")):
                in_scenes = False
                continue
        if not in_scenes:
            continue
        em = re.match(r"^\s*-\s*enabled:\s*(\d+)", line)
        if em:
            if cur:
                scenes.append(cur)
            cur = {"enabled": int(em.group(1)), "path": "", "guid": ""}
            continue
        if cur is not None:
            pm = re.match(r"^\s*path:\s*(.+)$", line)
            if pm:
                cur["path"] = pm.group(1).strip()
            gm = re.match(r"^\s*guid:\s*(\S+)", line)
            if gm:
                cur["guid"] = gm.group(1).strip()
    if cur:
        scenes.append(cur)
    return scenes


def collect_script_guid_map(repo_root: Path = REPO_ROOT) -> Dict[str, str]:
    guid_map: Dict[str, str] = {}
    scripts_dir = repo_root / "Assets" / "Scripts"
    if not scripts_dir.is_dir():
        return guid_map
    for meta in scripts_dir.rglob("*.cs.meta"):
        txt = meta.read_text(encoding="utf-8", errors="replace")
        gm = re.search(r"^guid:\s*([0-9a-fA-F]{32})", txt, re.MULTILINE)
        if gm:
            guid_map[gm.group(1).lower()] = meta.stem[:-3]  # strip .cs
    return guid_map


def inspect_unity_scene_or_prefab(path: Path, guid_map: Optional[Dict[str, str]] = None) -> Dict[str, Any]:
    txt = Path(path).read_text(encoding="utf-8", errors="replace")
    docs = split_unity_yaml_docs(txt)
    if guid_map is None:
        guid_map = collect_script_guid_map()

    game_objects: Dict[str, Dict[str, Any]] = {}
    components_by_go: Dict[str, List[Dict[str, Any]]] = {}
    monobehaviours: List[Dict[str, Any]] = []
    cameras: List[Dict[str, Any]] = []
    canvases: List[Dict[str, Any]] = []

    for cls_id, fid, body in docs:
        if cls_id == 1:  # GameObject
            nm = re.search(r"^\s*m_Name:\s*(.+)$", body, re.MULTILINE)
            go_name = nm.group(1).strip() if nm else ""
            comp_ids = re.findall(r"-\s*component:\s*\{fileID:\s*(-?\d+)\}", body)
            game_objects[fid] = {"name": go_name, "components": comp_ids}
        elif cls_id == 20:  # Camera
            go_m = re.search(r"^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body, re.MULTILINE)
            cf_m = re.search(r"^\s*m_ClearFlags:\s*(\d+)", body, re.MULTILINE)
            go_id = go_m.group(1) if go_m else ""
            cam = {"file_id": fid, "go_id": go_id, "clear_flags": int(cf_m.group(1)) if cf_m else -1}
            cameras.append(cam)
            components_by_go.setdefault(go_id, []).append({"type": "Camera", **cam})
        elif cls_id == 65:  # BoxCollider
            go_m = re.search(r"^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body, re.MULTILINE)
            go_id = go_m.group(1) if go_m else ""
            components_by_go.setdefault(go_id, []).append({"type": "BoxCollider", "file_id": fid})
        elif cls_id == 223:  # Canvas
            go_m = re.search(r"^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body, re.MULTILINE)
            rm_m = re.search(r"^\s*m_RenderMode:\s*(\d+)", body, re.MULTILINE)
            go_id = go_m.group(1) if go_m else ""
            c = {"file_id": fid, "go_id": go_id, "render_mode": int(rm_m.group(1)) if rm_m else -1}
            canvases.append(c)
            components_by_go.setdefault(go_id, []).append({"type": "Canvas", **c})
        elif cls_id == 114:  # MonoBehaviour
            go_m = re.search(r"^\s*m_GameObject:\s*\{fileID:\s*(-?\d+)\}", body, re.MULTILINE)
            go_id = go_m.group(1) if go_m else ""
            scr_m = re.search(r"^\s*m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]+)", body, re.MULTILINE)
            guid = scr_m.group(1).lower() if scr_m else ""
            cls_m = re.search(r"^\s*m_EditorClassIdentifier:\s*(.+)$", body, re.MULTILINE)
            eci = cls_m.group(1).strip() if cls_m else ""
            script_name = guid_map.get(guid, "")
            if not script_name and "::" in eci:
                script_name = eci.split("::")[-1].split(".")[-1]
            hand_m = re.search(r"^\s*m_Handedness:\s*(\d+)", body, re.MULTILINE)
            mb = {
                "file_id": fid,
                "go_id": go_id,
                "guid": guid,
                "eci": eci,
                "script_name": script_name,
                "handedness": int(hand_m.group(1)) if hand_m else None,
                "body": body,
            }
            monobehaviours.append(mb)
            components_by_go.setdefault(go_id, []).append({"type": script_name or eci or guid, **mb})

    # Detect Button + XRSimpleInteractable + BoxCollider conflicts on same GO
    conflicts: List[str] = []
    for go_id, comps in components_by_go.items():
        types = " ".join(str(c.get("type", "")) + " " + str(c.get("eci", "")) for c in comps)
        has_button = "UnityEngine.UI.Button" in types or "Button" in [c.get("type") for c in comps]
        has_simple = "XRSimpleInteractable" in types
        has_box = any(c.get("type") == "BoxCollider" for c in comps)
        if has_button and (has_simple or has_box):
            go_name = game_objects.get(go_id, {}).get("name", go_id)
            conflicts.append(go_name)

    return {
        "raw_text": txt,
        "game_objects": game_objects,
        "components_by_go": components_by_go,
        "monobehaviours": monobehaviours,
        "cameras": cameras,
        "canvases": canvases,
        "ui_button_conflicts": conflicts,
    }


# ---- Repo, Security, UTF-8 & Gate Helpers ----
def scan_text_for_secrets(text: str) -> List[str]:
    hits: List[str] = []
    for m in SECRET_RX.finditer(text):
        v = m.group(0)
        hits.append((v[:8] + "***") if len(v) > 8 else "***")
    return hits


def check_cs_line_budgets(root_dir: Path, max_lines: int = 200) -> List[Tuple[str, int]]:
    oversize: List[Tuple[str, int]] = []
    if not Path(root_dir).is_dir():
        return oversize
    for cs in Path(root_dir).rglob("*.cs"):
        lines = cs.read_text(encoding="utf-8", errors="replace").splitlines()
        if len(lines) > max_lines:
            oversize.append((str(cs), len(lines)))
    return oversize


def check_non_ascii_paths(root_dir: Path) -> List[str]:
    bad: List[str] = []
    base = Path(root_dir)
    if not base.is_dir():
        return bad
    for p in base.rglob("*"):
        rel = p.relative_to(base).as_posix()
        if not all(ord(ch) < 128 for ch in rel):
            bad.append(rel)
    return bad


def check_missing_metas(assets_dir: Path) -> List[str]:
    missing: List[str] = []
    base = Path(assets_dir)
    if not base.is_dir():
        return missing
    for p in base.rglob("*"):
        name = p.name
        if name.endswith(".meta") or name.startswith(".") or name.endswith("~"):
            continue
        parts = p.relative_to(base).parts
        # Unity ignores hidden dirs ending in ~ and manages files inside *.androidlib bundles via the folder .meta
        if any(part.endswith("~") or part.startswith(".") for part in parts):
            continue
        if any(parent.endswith(".androidlib") for parent in parts[:-1]):
            continue
        meta = Path(str(p) + ".meta")
        if not meta.exists():
            missing.append(p.relative_to(base).as_posix())
    return missing


def check_utf8_file(path: Path, is_ps1: bool = False) -> List[str]:
    issues: List[str] = []
    raw = Path(path).read_bytes()
    if b"\x00" in raw:
        issues.append("NUL_BYTE")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as ex:
        issues.append(f"UTF8_DECODE_ERROR ({ex})")
        return issues
    if "\ufffd" in text:
        issues.append("U_FFFD_REPLACEMENT_CHAR")
    if is_ps1:
        has_non_ascii = any(ord(ch) >= 128 for ch in text)
        has_bom = raw.startswith(b"\xef\xbb\xbf")
        if has_non_ascii and not has_bom:
            issues.append("PS1_NON_ASCII_WITHOUT_UTF8_BOM")
    return issues


def check_gate_mirror_parity(gates_dir: Path = GATES_MIRROR_DIR, source_dir: Path = PATROL_DIR) -> Dict[str, Any]:
    mismatches: List[str] = []
    checked: List[str] = []
    for fname in GATE_MIRROR_FILES:
        g_path = Path(gates_dir) / fname
        s_path = Path(source_dir) / fname
        if not g_path.is_file() or not s_path.is_file():
            mismatches.append(f"MISSING:{fname}")
            continue
        g_bytes = g_path.read_bytes()
        s_bytes = s_path.read_bytes()
        g_sha = hashlib.sha256(g_bytes).hexdigest()
        s_sha = hashlib.sha256(s_bytes).hexdigest()
        checked.append(fname)
        if g_sha != s_sha or len(g_bytes) != len(s_bytes):
            mismatches.append(f"DRIFT:{fname} (mirror={len(g_bytes)}B/{g_sha[:8]}, src={len(s_bytes)}B/{s_sha[:8]})")
    return {"ok": len(mismatches) == 0, "checked": checked, "mismatches": mismatches}


def run_ps1(script_path: Path, args: Optional[List[str]] = None, timeout_s: float = 120.0) -> subprocess.CompletedProcess:
    cmd = [
        "powershell",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(script_path),
    ] + (args or [])
    return subprocess.run(
        cmd,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout_s,
    )


def run_secret_scan(targets: List[str], strict: bool = True, timeout_s: float = 120.0) -> subprocess.CompletedProcess:
    script = PATROL_DIR / "secret_scan.ps1"
    if not script.is_file():
        script = GATES_MIRROR_DIR / "secret_scan.ps1"
    arr_lit = ",".join(f"'{t}'" for t in targets)
    strict_flag = " -Strict" if strict else ""
    ps_cmd = f"& '{script}' -Targets {arr_lit}{strict_flag}; exit $LASTEXITCODE"
    return subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps_cmd],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout_s,
    )
