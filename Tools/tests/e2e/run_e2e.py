# -*- coding: utf-8 -*-
"""Opaque-box 4-Tier E2E Test Runner for ShotField."""

from __future__ import annotations

import argparse
import datetime
import json
import os
import re
import sys
import unittest
from pathlib import Path
from typing import Any, Dict, List, Optional

THIS_DIR = Path(__file__).resolve().parent
if str(THIS_DIR) not in sys.path:
    sys.path.insert(0, str(THIS_DIR))

from e2e_common import DIR_05, MILESTONE_ORDER, REPO_ROOT  # noqa: E402

TEST_MODULES = (
    "test_tier1_features",
    "test_tier2_boundaries",
    "test_tier3_pairwise",
    "test_tier4_scenarios",
)


def compute_exit_code(
    failed: int,
    errors: int,
    skipped: int,
    milestone: str = "ALL",
    allow_skips: bool = False,
) -> int:
    if failed > 0 or errors > 0:
        return 1
    if skipped > 0 and not allow_skips and milestone.upper() == "ALL":
        return 1
    return 0


def build_report_dict(
    milestone: str,
    tier: int,
    results: List[Dict[str, Any]],
    exit_code: int,
) -> Dict[str, Any]:
    passed = sum(1 for r in results if r["status"] == "PASS")
    failed = sum(1 for r in results if r["status"] == "FAIL")
    errors = sum(1 for r in results if r["status"] == "ERROR")
    skipped = sum(1 for r in results if r["status"] == "SKIP")
    total = len(results)
    verdict = "PASS" if exit_code == 0 else "FAIL"

    tiers: Dict[str, Dict[str, int]] = {}
    for t in (1, 2, 3, 4):
        sub = [r for r in results if r["tier"] == t]
        tiers[f"tier{t}"] = {
            "total": len(sub),
            "passed": sum(1 for r in sub if r["status"] == "PASS"),
            "failed": sum(1 for r in sub if r["status"] == "FAIL"),
            "errors": sum(1 for r in sub if r["status"] == "ERROR"),
            "skipped": sum(1 for r in sub if r["status"] == "SKIP"),
        }

    return {
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "repo_root": str(REPO_ROOT),
        "milestone": milestone,
        "tier": tier,
        "exit_code": exit_code,
        "summary": {
            "total": total,
            "passed": passed,
            "failed": failed,
            "errors": errors,
            "skipped": skipped,
            "verdict": verdict,
        },
        "tiers": tiers,
        "tests": results,
    }


def _iter_tests(suite: unittest.TestSuite):
    for item in suite:
        if isinstance(item, unittest.TestSuite):
            yield from _iter_tests(item)
        else:
            yield item


def _get_case_meta(test_case: unittest.TestCase) -> Dict[str, Any]:
    method = getattr(test_case, test_case._testMethodName, None)
    meta = getattr(method, "_e2e_meta", None) if method is not None else None
    if isinstance(meta, dict):
        return meta
    return {
        "case_id": test_case._testMethodName,
        "tier": 0,
        "area": "?",
        "features": "?",
        "milestone": "M5",
        "requires_emulator": False,
        "requires_unity": False,
    }


class _CollectingResult(unittest.TestResult):
    def __init__(self) -> None:
        super().__init__()
        self.records: List[Dict[str, Any]] = []

    def addSuccess(self, test: unittest.TestCase) -> None:
        super().addSuccess(test)
        meta = _get_case_meta(test)
        self.records.append({**meta, "id": meta["case_id"], "status": "PASS", "detail": ""})

    def addFailure(self, test: unittest.TestCase, err) -> None:
        super().addFailure(test, err)
        meta = _get_case_meta(test)
        msg = self._exc_info_to_string(err, test).strip().splitlines()[-1]
        self.records.append({**meta, "id": meta["case_id"], "status": "FAIL", "detail": msg})

    def addError(self, test: unittest.TestCase, err) -> None:
        super().addError(test, err)
        meta = _get_case_meta(test)
        msg = self._exc_info_to_string(err, test).strip().splitlines()[-1]
        self.records.append({**meta, "id": meta["case_id"], "status": "ERROR", "detail": msg})

    def addSkip(self, test: unittest.TestCase, reason: str) -> None:
        super().addSkip(test, reason)
        meta = _get_case_meta(test)
        self.records.append({**meta, "id": meta["case_id"], "status": "SKIP", "detail": reason})


def _default_report_path() -> Path:
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    if DIR_05 is not None:
        return DIR_05 / "2026-11" / "gate_logs" / f"e2e_report_{stamp}.json"
    return REPO_ROOT / "Logs" / f"e2e_report_{stamp}.json"


def _parse_args(argv: Optional[List[str]] = None) -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="ShotField 4-Tier Opaque-Box E2E Runner")
    ap.add_argument("--tier", type=int, default=0, help="0=All, 1..4=Specific Tier")
    ap.add_argument("--milestone", default="ALL", help="M1|M2|M3|M4|M5|ALL")
    ap.add_argument("--filter", default="", help="Substring/regex filter on case ID, area, features, or name")
    ap.add_argument("--include-emulator", action="store_true", help="Enable live ADB emulator tests")
    ap.add_argument("--require-emulator", action="store_true", help="Fail if emulator device is unavailable")
    ap.add_argument("--include-unity", action="store_true", help="Enable Unity batchmode UTF EditMode tests")
    ap.add_argument("--allow-skips", action="store_true", help="Permit skipped tests even in -Milestone ALL")
    ap.add_argument("--json-report", default="", help="Custom JSON output report path")
    args = ap.parse_args(argv)

    if args.tier not in (0, 1, 2, 3, 4):
        ap.error(f"Invalid --tier {args.tier}; expected 0..4")
    args.milestone = args.milestone.upper()
    if args.milestone not in MILESTONE_ORDER:
        ap.error(f"Invalid --milestone {args.milestone}; expected M1|M2|M3|M4|M5|ALL")
    return args


def _safe_print(line: str) -> None:
    try:
        print(line)
    except UnicodeEncodeError:
        sys.stdout.buffer.write((line + "\n").encode("utf-8", errors="replace"))


def main(argv: Optional[List[str]] = None) -> int:
    args = _parse_args(argv)

    os.environ["SHOTFIELD_E2E_INCLUDE_EMU"] = "1" if (args.include_emulator or args.require_emulator) else "0"
    os.environ["SHOTFIELD_E2E_REQUIRE_EMU"] = "1" if args.require_emulator else "0"
    os.environ["SHOTFIELD_E2E_INCLUDE_UNITY"] = "1" if args.include_unity else "0"
    os.environ["SHOTFIELD_E2E_MILESTONE"] = args.milestone

    loader = unittest.TestLoader()
    discovered = unittest.TestSuite()
    for mod in TEST_MODULES:
        discovered.addTests(loader.loadTestsFromName(mod))

    target_ms_rank = MILESTONE_ORDER[args.milestone]
    rx_filter = re.compile(args.filter, re.IGNORECASE) if args.filter else None

    active_suite = unittest.TestSuite()
    milestone_skips: List[Dict[str, Any]] = []

    for tc in _iter_tests(discovered):
        meta = _get_case_meta(tc)
        if args.tier != 0 and meta["tier"] != args.tier:
            continue
        if rx_filter is not None:
            haystack = f"{meta['case_id']} {meta['area']} {meta['features']} {tc._testMethodName}"
            if not rx_filter.search(haystack):
                continue
        if args.milestone != "ALL" and MILESTONE_ORDER.get(meta["milestone"], 99) > target_ms_rank:
            milestone_skips.append(
                {
                    **meta,
                    "id": meta["case_id"],
                    "status": "SKIP",
                    "detail": f"SKIP [milestone={meta['milestone']}]",
                }
            )
            continue
        active_suite.addTest(tc)

    collector = _CollectingResult()
    active_suite.run(collector)
    all_records = collector.records + milestone_skips

    if not all_records:
        _safe_print("[OVERALL] ERROR: 0 tests matched selection")
        return 2

    all_records.sort(key=lambda r: (r["tier"], r["id"]))
    for r in all_records:
        detail = f" | {r['detail']}" if r["detail"] else ""
        _safe_print(f"[{r['status']}] {r['id']} (T{r['tier']} {r['area']} {r['features']} {r['milestone']}){detail}")

    passed = sum(1 for r in all_records if r["status"] == "PASS")
    failed = sum(1 for r in all_records if r["status"] == "FAIL")
    errors = sum(1 for r in all_records if r["status"] == "ERROR")
    skipped = sum(1 for r in all_records if r["status"] == "SKIP")
    exit_code = compute_exit_code(failed, errors, skipped, milestone=args.milestone, allow_skips=args.allow_skips)

    _safe_print("")
    for t in (1, 2, 3, 4):
        sub = [r for r in all_records if r["tier"] == t]
        if not sub:
            continue
        tp = sum(1 for r in sub if r["status"] == "PASS")
        tf = sum(1 for r in sub if r["status"] == "FAIL")
        te = sum(1 for r in sub if r["status"] == "ERROR")
        ts = sum(1 for r in sub if r["status"] == "SKIP")
        verdict_t = "PASS" if (tf == 0 and te == 0 and (ts == 0 or args.allow_skips or args.milestone != "ALL")) else "FAIL"
        _safe_print(f"[TIER {t}] {verdict_t} total={len(sub)} passed={tp} failed={tf} errors={te} skipped={ts}")

    overall_verdict = "PASS" if exit_code == 0 else "FAIL"
    _safe_print(
        f"[OVERALL] {overall_verdict} milestone={args.milestone} tier={args.tier} "
        f"total={len(all_records)} passed={passed} failed={failed} errors={errors} skipped={skipped}"
    )

    report_path = Path(args.json_report) if args.json_report else _default_report_path()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_obj = build_report_dict(args.milestone, args.tier, all_records, exit_code)
    report_path.write_text(json.dumps(report_obj, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    _safe_print(f"[REPORT] {report_path}")
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
