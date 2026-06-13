#!/usr/bin/env python3
"""
Step 3: Convert the cleaned CSV (VisualStudio_TC_standard.csv) into individual .txt
test-case files under TestCasesData/.

Implements the Step 3 prompt rules:
  * Naming: {Priority}_[BVT_]{TCID}_{Automation_Name}.txt
      - TCID = "TC" + 3-digit zero-padded ID.
      - Sanitize: replace  * / : ? " < > |  with _.
      - "BVT_" inserted when the case is classified BVT.
  * File content template: Test Case ID / Title / Feature / Priority / Automation Name,
    Test Data (bare keys), Expected Result, numbered Test Steps.
  * Test Data: deduplicated config keys used by the steps, but per the overwrite
    logic ONLY fields that differ from the ServerAndUserDataASK.txt defaults.
  * Continuous numbering: step numbers never reset within a file.
  * No noise: strip section dividers, server tags and [brackets] from step lines.
  * Formatting: {braces} only for variables inside step text; bare keys in Test Data.
"""
import csv
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLEANED_CSV = os.path.join(ROOT, "VisualStudio_TC_standard.csv")
SOURCE_XLSX = os.path.join(ROOT, "VisualStudio_TC.xlsx")
CONFIG_JSON = os.path.join(ROOT, "config_global.json")
OUT_DIR = os.path.join(ROOT, "TestCasesData")


def maybe_convert_xlsx():
    """If an Excel workbook exists, regenerate the CSV from it first so authors can
    drop in VisualStudio_TC.xlsx and run a single command.

    Safety: only convert when the workbook is NEWER than the CSV (or the CSV is missing),
    so editing the CSV directly is never silently clobbered by a stale workbook."""
    if not os.path.exists(SOURCE_XLSX):
        return
    if os.path.exists(CLEANED_CSV) and \
            os.path.getmtime(SOURCE_XLSX) <= os.path.getmtime(CLEANED_CSV):
        print(f"Skipping {os.path.basename(SOURCE_XLSX)} — CSV is newer (edited directly). "
              f"Delete the CSV to force a fresh conversion from Excel.")
        return
    try:
        from xlsx_to_csv import convert  # local module in this tools/ folder
    except ImportError:
        import sys
        sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
        from xlsx_to_csv import convert
    print(f"Detected newer {os.path.basename(SOURCE_XLSX)} — converting to CSV first.")
    convert(SOURCE_XLSX, CLEANED_CSV)


SANITIZE_RX = re.compile(r'[*/:?"<>|]')
NOISE_RX = re.compile(r"[\[\]{}]|--+|==+")          # brackets, dividers
SERVER_TAG_RX = re.compile(r"\b(Server\d+|User\d+)\s*[:>-]?\s*", re.IGNORECASE)


def sanitize(name: str) -> str:
    return SANITIZE_RX.sub("_", name).strip()


def strip_noise(text: str) -> str:
    text = NOISE_RX.sub("", text)
    text = SERVER_TAG_RX.sub("", text)
    return re.sub(r"\s+", " ", text).strip()


def is_bvt(tc) -> bool:
    blob = f"{tc['automation']} {tc['title']} {tc['feature']}".lower()
    return "bvt" in blob


def inject_var(action: str, dataset: str) -> str:
    """Inject {VarName} into step text where a config value is used."""
    if not dataset:
        return action
    low = action.lower()
    base = action.rstrip(".")
    if "password" in low:
        return f"{base} {{Password}}."
    if "email" in low or "address" in low:
        return f"{base} {{Username}}."
    if "recipient" in low or " to " in f" {low} ":
        return f"{base} {{Recipient}}."
    return action


def load_config():
    with open(CONFIG_JSON, encoding="utf-8") as f:
        return json.load(f)


def default_user(cfg):
    return (cfg.get("Users", {}) or {}).get("Default", {})


def load_cases():
    cases = []
    current = None
    with open(CLEANED_CSV, encoding="utf-8-sig", newline="") as f:
        reader = csv.reader(f)
        rows = list(reader)
    for row in rows[1:]:
        if not any(c.strip() for c in row):
            continue
        row = (row + [""] * 17)[:17]
        tcid_raw, automation, status, feature, title, priority = row[0:6]
        user_config = row[7]
        step_no, action, dataset, step_expected = row[8], row[9], row[10], row[11]
        module_ref, module_count = row[15], row[16]

        if step_no.strip() == "":  # header row
            current = {
                "id": tcid_raw.strip(),
                "automation": automation.strip(),
                "feature": feature.strip(),
                "title": title.strip(),
                "priority": priority.strip(),
                "user_config": user_config.strip() or "Default",
                "expected": step_expected.strip(),
                "steps": [],
            }
            cases.append(current)
        else:
            # Step row. If column A (TC_ID) is blank, keep appending to the current case;
            # only switch cases when a non-blank TC_ID is given.
            tid = tcid_raw.strip()
            if tid and (current is None or current["id"] != tid):
                current = next((c for c in cases if c["id"] == tid), None)
            if current is not None:
                current["steps"].append({
                    "action": action.strip(),
                    "dataset": dataset.strip(),
                    "expected": step_expected.strip(),
                    "module": module_ref.strip(),
                })
    return cases


def build_test_data(tc, cfg):
    """Only list config fields that differ from the defaults (overwrite logic)."""
    defaults = default_user(cfg)
    chosen = (cfg.get("Users", {}) or {}).get(tc["user_config"], {})
    diffs = {}
    for key, val in chosen.items():
        if defaults.get(key) != val:
            diffs[key] = val
    return diffs


def render(tc, cfg) -> str:
    lines = []
    tcid = "TC" + tc["id"].zfill(3)
    lines.append(f"Test Case ID: {tcid}")
    lines.append(f"Title: {tc['title']}")
    lines.append(f"Feature: {tc['feature']}")
    lines.append(f"Priority: {tc['priority']}")
    lines.append(f"Automation Name: {tc['automation']}")
    lines.append(f"Classification: {'BVT' if is_bvt(tc) else 'Functional'}")
    lines.append("")

    lines.append("Test Data:")
    diffs = build_test_data(tc, cfg)
    if diffs:
        for key, val in diffs.items():
            lines.append(f"  {key}: {val}")
    else:
        lines.append("  (uses default configuration)")
    lines.append("")

    lines.append("Expected Result:")
    if tc["expected"]:
        lines.append(f"  - {tc['expected']}")
    lines.append("")

    lines.append("Test Steps:")
    n = 0
    for s in tc["steps"]:
        n += 1  # continuous numbering, never resets
        action = inject_var(strip_noise(s["action"]), s["dataset"])
        lines.append(f"  {n}. {action}")
        if s["expected"]:
            lines.append(f"     Expected: {s['expected']}")
    lines.append("")
    return "\n".join(lines)


def filename(tc) -> str:
    tcid = "TC" + tc["id"].zfill(3)
    parts = [tc["priority"]]
    if is_bvt(tc):
        parts.append("BVT")
    parts.append(tcid)
    parts.append(sanitize(tc["automation"]))
    return "_".join(p for p in parts if p) + ".txt"


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    maybe_convert_xlsx()
    cfg = load_config()
    cases = load_cases()
    written = []
    for tc in cases:
        path = os.path.join(OUT_DIR, filename(tc))
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(render(tc, cfg))
        written.append(os.path.basename(path))
    print(f"Wrote {len(written)} test-case file(s) to {OUT_DIR}:")
    for w in written:
        print(f"  {w}")


if __name__ == "__main__":
    main()
